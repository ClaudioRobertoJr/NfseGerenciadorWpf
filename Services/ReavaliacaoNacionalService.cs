using System.IO;
using Microsoft.EntityFrameworkCore;
using NfseGerenciador.Models;

namespace NfseGerenciador.Services;

public sealed record ResultadoReavaliacaoNacional(int Reavaliadas, int SemDocumento, int RegraMaisRecente, bool Cancelada);

public sealed class ReavaliacaoNacionalService(IDbContextFactory<ApplicationDbContext> factory)
{
    // Session lock spans batches so two computers cannot run this operation simultaneously.
    public async Task<ResultadoReavaliacaoNacional> ReavaliarAsync(IProgress<string>? progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await using var guard = factory.CreateDbContext();
        await guard.Database.OpenConnectionAsync(ct);
        await guard.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sp_getapplock @Resource='NFSePadraoNacional:72547623000190', @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=0; IF @r < 0 THROW 51000, 'Já existe uma reavaliação nacional em andamento.', 1;", ct);
        int processadas = 0, semDocumento = 0, futuras = 0;
        try
        {
            var notas = guard.Nfses.AsNoTracking().Where(n => n.ChaveAcessoNacional != null && n.ChaveAcessoNacional != ""
                && n.PrestadorCnpj.Replace(".", "").Replace("/", "").Replace("-", "").Replace(" ", "") == AvaliacaoNacional.CnpjAlvo);
            int limite = await notas.Select(n => (int?)n.Id).MaxAsync(ct) ?? 0;
            semDocumento = await notas.CountAsync(n => n.Id <= limite && (n.AvaliacaoNacional == null || n.AvaliacaoNacional.Documento == null), ct);
            futuras = await notas.CountAsync(n => n.Id <= limite && n.AvaliacaoNacional != null && n.AvaliacaoNacional.VersaoRegra > AvaliadorPadraoNacional.VersaoRegra, ct);
            int ultimo = 0;
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                await using var db = factory.CreateDbContext();
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                var lote = await db.Set<AvaliacaoNacional>().Include(a => a.Documento)
                    .Where(a => a.NfseId > ultimo && a.NfseId <= limite && a.Documento != null && a.VersaoRegra <= AvaliadorPadraoNacional.VersaoRegra
                        && a.Nota.ChaveAcessoNacional != null && a.Nota.ChaveAcessoNacional != ""
                        && a.Nota.PrestadorCnpj.Replace(".", "").Replace("/", "").Replace("-", "").Replace(" ", "") == AvaliacaoNacional.CnpjAlvo)
                    .OrderBy(a => a.NfseId).Take(50)
                    .Select(a => new { Avaliacao = a, Chave = a.Nota.ChaveAcessoNacional!, Prestador = a.Nota.PrestadorCnpj }).ToListAsync(ct);
                if (lote.Count == 0) break;
                foreach (var item in lote)
                {
                    ct.ThrowIfCancellationRequested();
                    var anterior = item.Avaliacao;
                    var nova = AvaliarDocumento(anterior.Documento!, item.Chave, item.Prestador);
                    anterior.Status = nova.Status; anterior.Resumo = nova.Resumo; anterior.CamposJson = nova.CamposJson;
                    anterior.VersaoLeiaute = nova.VersaoLeiaute; anterior.VersaoRegra = nova.VersaoRegra; anterior.AvaliadaEmUtc = nova.AvaliadaEmUtc;
                }
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                ultimo = lote[^1].Avaliacao.NfseId;
                processadas += lote.Count;
                progress?.Report($"{processadas} avaliação(ões) gravada(s). {semDocumento} nota(s) sem documento disponível.");
            }
            return new(processadas, semDocumento, futuras, false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new(processadas, semDocumento, futuras, true);
        }
        finally
        {
            await guard.Database.ExecuteSqlRawAsync("EXEC sp_releaseapplock @Resource='NFSePadraoNacional:72547623000190', @LockOwner='Session';", CancellationToken.None);
        }
    }

    internal static AvaliacaoNacional AvaliarDocumento(DocumentoNacional documento, string chave, string prestador)
    {
        if (!string.Equals(AvaliadorPadraoNacional.CriarDocumento(documento.ArquivoXmlGzipBase64).Sha256, documento.Sha256, StringComparison.OrdinalIgnoreCase))
            return AvaliadorPadraoNacional.Indisponivel("Documento armazenado com integridade inconsistente; conteúdo original preservado.");
        try { return AvaliadorPadraoNacional.Avaliar(NacionalXml.Decode(documento.ArquivoXmlGzipBase64), chave, prestador); }
        catch (Exception ex) when (ex is FormatException or InvalidDataException or IOException)
        { return AvaliadorPadraoNacional.Indisponivel("Não foi possível ler o documento armazenado; conteúdo original preservado."); }
    }
}
