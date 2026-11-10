using Microsoft.EntityFrameworkCore;
using NfseGerenciador.Models;

namespace NfseGerenciador.Services;

public sealed class NacionalImportService(IDbContextFactory<ApplicationDbContext> factory)
{
    public async Task<string> ImportAsync(string cnpj, IProgress<string> progress, CancellationToken ct)
    {
        ImportDiagnostics.Write("Iniciando processo separado para HTTPS e cartão");
        using var client = new AdnProcessClient();
        progress.Report("Selecione o certificado na janela do auxiliar de conexão.");
        ImportDiagnostics.Write("Solicitando seleção de certificado no processo auxiliar");
        await client.SelectAsync(ct);
        ImportDiagnostics.Write("Seleção confirmada pelo auxiliar; iniciando sincronização");
        int imported = 0, read = 0, cancelled = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            using var db = factory.CreateDbContext();
            ImportDiagnostics.Write("Abrindo transação e controle de sincronização no banco");
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var resource = "NFSeADN:" + cnpj;
            // Database lock protects the cursor and duplicate check across computers.
            await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r = sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=0; IF @r < 0 THROW 51000, 'Já existe uma sincronização deste CNPJ em andamento.', 1;", ct);
            var state = await db.Set<NacionalSync>().FindAsync(new object[] { cnpj }, ct);
            if (state == null) { state = new NacionalSync { Cnpj = cnpj }; db.Add(state); }
            progress.Report($"Consultando após NSU {state.UltimoNsu}. Importadas nesta sessão: {imported}. O cartão pode solicitar o PIN.");
            ImportDiagnostics.Write("Iniciando chamada HTTPS autenticada; aguardando TLS/cartão/API");
            var documents = await client.ReadAsync(state.UltimoNsu, cnpj, ct);
            ImportDiagnostics.Write("Resposta da API recebida e validada");
            if (documents.Count == 0)
            {
                await tx.RollbackAsync(ct);
                return $"Sincronização concluída. {read} documentos examinados; {imported} notas emitidas desde 01/11/2026 importadas; {cancelled} cancelamentos aplicados. Último NSU: {state.UltimoNsu}.";
            }
            int batchImported = 0, batchCancelled = 0;
            foreach (var doc in documents)
            {
                ct.ThrowIfCancellationRequested();
                if (doc.ChaveAcesso.Length != 50 || !doc.ChaveAcesso.All(char.IsAsciiDigit))
                    throw new InvalidOperationException($"Chave inválida no NSU {doc.NSU}.");
                if (doc.TipoDocumento == "NFSE")
                {
                    var note = NacionalXml.Parse(NacionalXml.Decode(doc.ArquivoXml), doc.ChaveAcesso);
                    if (NacionalXml.ShouldImport(note, cnpj))
                    {
                        // Never update an existing note's payments, installments or logical deletion.
                        bool exists = db.Nfses.Local.Any(x => x.ChaveAcessoNacional == doc.ChaveAcesso)
                            || await db.Nfses.AnyAsync(x => x.ChaveAcessoNacional == doc.ChaveAcesso, ct);
                        if (!exists)
                        {
                            note.Cancelada = db.Set<NacionalEvento>().Local.Any(x => x.Chave == doc.ChaveAcesso && x.Cancela)
                                || await db.Set<NacionalEvento>().AnyAsync(x => x.Cnpj == cnpj && x.Chave == doc.ChaveAcesso && x.Cancela, ct);
                            var company = await db.EmpresasClassificadas.FirstOrDefaultAsync(x => x.Cnpj == note.TomadorCpfCnpj, ct);
                            note.ClassificacaoEspecial = company?.TipoClassificacao;
                            note.DataPrevistaRecebimento = note.DataEmissao.AddDays(30);
                            note.Parcelas.Add(new NfseParcela { NumeroParcela = 1, ValorParcela = note.ValorLiquidoNfse, DataVencimentoParcela = note.DataPrevistaRecebimento.Value });
                            db.Nfses.Add(note);
                            batchImported++;
                        }
                    }
                }
                else if (doc.TipoDocumento == "EVENTO")
                {
                    if (string.IsNullOrWhiteSpace(doc.TipoEvento)) throw new InvalidOperationException($"Evento sem tipo no NSU {doc.NSU}.");
                    var cancels = NacionalXml.IsCancellation(doc.TipoEvento);
                    db.Add(new NacionalEvento { Cnpj = cnpj, Nsu = doc.NSU, Chave = doc.ChaveAcesso, Tipo = doc.TipoEvento, Cancela = cancels });
                    if (cancels)
                    {
                        var note = db.Nfses.Local.FirstOrDefault(x => x.ChaveAcessoNacional == doc.ChaveAcesso)
                            ?? await db.Nfses.FirstOrDefaultAsync(x => x.ChaveAcessoNacional == doc.ChaveAcesso, ct);
                        if (note != null && !note.Cancelada) { note.Cancelada = true; batchCancelled++; }
                    }
                }
                else throw new InvalidOperationException($"Tipo de documento não suportado no NSU {doc.NSU}: {doc.TipoDocumento}.");
                state.UltimoNsu = doc.NSU;
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            ImportDiagnostics.Write("Lote confirmado no banco");
            imported += batchImported; cancelled += batchCancelled; read += documents.Count;
            progress.Report($"{read} documentos examinados; {imported} notas importadas. Progresso salvo no NSU {state.UltimoNsu}.");
            await Task.Delay(1000, ct);
        }
    }
}
