using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NfseGerenciador.Models.ImportacaoBaixas;
using NfseGerenciador.Services.ImportacaoBaixas;
using NfseGerenciador.Models;
namespace NfseGerenciador.Services;

public sealed record ResultadoBaixaEmpresas(int Quantidade, decimal Valor, IReadOnlyDictionary<string,int> Ignoradas);
internal sealed record CandidatoBaixaEmpresa(Nfse Nota, string? Classificacao, int? PagamentoId, string? Motivo);

public sealed class BaixaEmpresasClassificadasService(IDbContextFactory<ApplicationDbContext> factory)
{
    private const string Regra = "EMPRESAS_CLASSIFICADAS_V1";
    internal static IReadOnlyList<CandidatoBaixaEmpresa> Planejar(IReadOnlyList<Nfse> notas, IReadOnlyList<EmpresaClassificada> empresas, IReadOnlyList<Pagamento> pagamentos)
    {
        var cadastros=empresas.GroupBy(e=>ConciliadorBaixas.Digitos(e.Cnpj)).ToDictionary(g=>g.Key,g=>g.ToList());
        var result=new List<CandidatoBaixaEmpresa>();
        foreach(var note in notas)
        {
            string cnpj=ConciliadorBaixas.Digitos(note.TomadorCpfCnpj);
            CandidatoBaixaEmpresa Skip(string reason) => new(note,null,null,reason);
            if(cnpj.Length!=14 || !cadastros.TryGetValue(cnpj,out var matches)) {result.Add(Skip("Empresa não cadastrada"));continue;}
            if(matches.Count!=1) {result.Add(Skip("CNPJ com cadastro ambíguo"));continue;}
            string? tipo=LeitorRelatorioBaixas.Normalizar(matches[0].TipoClassificacao) switch
            { "PLANODESAUDE"=>"Plano de Saúde", "CONVENIO"=>"Convênio", _=>null };
            if(tipo is null) {result.Add(Skip("Classificação não suportada"));continue;}
            if(note.EstaPago || note.Cancelada || note.Excluida) {result.Add(Skip("Nota paga, cancelada ou excluída"));continue;}
            if(note.ValorLiquidoNfse<=0 || ConciliadorBaixas.Digitos(note.PrestadorCnpj).Length!=14)
            {result.Add(Skip("Valor ou prestador inválido"));continue;}
            if(!SemHistorico(note)) {result.Add(Skip("Histórico financeiro preservado"));continue;}
            var payment=pagamentos.Where(p=>LeitorRelatorioBaixas.Normalizar(p.TipoRecebimento)==LeitorRelatorioBaixas.Normalizar(tipo)
                && p.EhAVista && p.NumeroParcelas is null or 1).OrderBy(p=>p.Id).FirstOrDefault();
            if(payment is null) {result.Add(Skip("Pagamento à vista não cadastrado: "+tipo));continue;}
            result.Add(new(note,tipo,payment.Id,null));
        }
        return result;
    }
    private static bool SemHistorico(Nfse n) => n.DataPagamentoEfetivo is null && n.PagamentoId is null && n.Pagamento is null &&
        (n.Parcelas.Count==0 || n.Parcelas.Count==1 && n.DataEmissao<=DateTime.MaxValue.AddDays(-30) &&
         n.Parcelas[0].NumeroParcela==1 && !n.Parcelas[0].ParcelaPaga && n.Parcelas[0].DataPagamentoParcela is null &&
         n.Parcelas[0].ValorParcela==n.ValorLiquidoNfse && n.Parcelas[0].DataVencimentoParcela==n.DataEmissao.AddDays(30));
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static string Impressao(Nfse n) => Hash($"{Regra}|{ConciliadorBaixas.Digitos(n.PrestadorCnpj)}|{n.Id}");
    private static string Estado(Nfse n,string? classificacao=null) => JsonSerializer.Serialize(new {
        Regra,Classificacao=classificacao??n.ClassificacaoEspecial,n.Id,n.Numero,n.PrestadorCnpj,n.TomadorCpfCnpj,
        n.EstaPago,n.PagamentoId,n.DataPagamentoEfetivo,n.DataPrevistaRecebimento,n.Local,n.ValorLiquidoNfse,
        Parcelas=n.Parcelas.Select(p=>new{p.Id,p.NumeroParcela,p.ValorParcela,p.DataVencimentoParcela,p.ParcelaPaga,p.DataPagamentoParcela}) });
    public async Task<ResultadoBaixaEmpresas> BaixarAsync(CancellationToken ct=default)
    {
        await using var db=await factory.CreateDbContextAsync(ct);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var companies=await db.EmpresasClassificadas.AsNoTracking().ToListAsync(ct);
        var known=companies.Select(c=>ConciliadorBaixas.Digitos(c.Cnpj)).Where(c=>c.Length==14).ToHashSet();
        var notes=await db.Nfses.Where(n=>!n.EstaPago && !n.Cancelada && !n.Excluida)
            .Include(n=>n.Parcelas).Include(n=>n.Pagamento).ToListAsync(ct);
        var candidates=Planejar(notes.Where(n=>known.Contains(ConciliadorBaixas.Digitos(n.TomadorCpfCnpj))).ToList(),companies,
            await db.Pagamentos.AsNoTracking().ToListAsync(ct));
        var used=(await db.RegistrosBaixas.Where(r=>r.Contrapartida==Regra).Select(r=>r.Impressao).ToListAsync(ct)).ToHashSet();
        var skipped=new Dictionary<string,int>();
        var lots=new Dictionary<string,LoteBaixa>();
        int count=0;decimal total=0;
        foreach(var candidate in candidates)
        {
            ct.ThrowIfCancellationRequested();
            var note=candidate.Nota;string print=Impressao(note);
            var reason=candidate.Motivo??(used.Contains(print)?"Baixa já registrada no histórico":null);
            if(reason is not null) {skipped[reason]=skipped.GetValueOrDefault(reason)+1;continue;}
            string provider=ConciliadorBaixas.Digitos(note.PrestadorCnpj);
            if(!lots.TryGetValue(provider,out var lot))
            {
                lot=new LoteBaixa { HashArquivo=Hash(Regra+Guid.NewGuid().ToString("N")),NomeArquivo="Baixa por empresas classificadas",
                    PrestadorCnpj=provider,ImportadoEmUtc=DateTime.UtcNow };
                lots.Add(provider,lot);db.LotesBaixas.Add(lot);
            }
            string before=Estado(note);var date=note.DataEmissao.Date;
            note.EstaPago=true;note.PagamentoId=candidate.PagamentoId;note.DataPagamentoEfetivo=date;
            note.DataPrevistaRecebimento=date;note.ClassificacaoEspecial=candidate.Classificacao;
            if(note.Parcelas.Count==0)note.Parcelas.Add(new NfseParcela {NumeroParcela=1,ValorParcela=note.ValorLiquidoNfse});
            var parcel=note.Parcelas.Single();parcel.ParcelaPaga=true;parcel.DataPagamentoParcela=date;parcel.DataVencimentoParcela=date;
            lot.Quantidade++;lot.Valor+=note.ValorLiquidoNfse;
            db.RegistrosBaixas.Add(new RegistroBaixa { Lote=lot,Nota=note,Impressao=print,Planilha="Empresas classificadas",Linha=lot.Quantidade,
                Historico=$"Baixa confirmada pelo usuário pelo cadastro CNPJ {ConciliadorBaixas.Digitos(note.TomadorCpfCnpj)}: {candidate.Classificacao}; NF {note.Numero}",
                Centro=note.Local.ToString(),Contrapartida=Regra,DataRelatorio=date,Valor=note.ValorLiquidoNfse,
                EstadoAnterior=before,EstadoPosterior=Estado(note,candidate.Classificacao) });
            count++;total+=note.ValorLiquidoNfse;
        }
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        return new(count,total,skipped);
    }
}
