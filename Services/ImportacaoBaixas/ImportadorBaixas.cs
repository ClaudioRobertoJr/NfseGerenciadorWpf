using System.Data;
using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NfseGerenciador.Models;
using NfseGerenciador.Models.ImportacaoBaixas;
namespace NfseGerenciador.Services.ImportacaoBaixas;

public sealed class ImportadorBaixas(IDbContextFactory<ApplicationDbContext> factory) : IImportadorBaixas
{
    public async Task<PreviaBaixas> PrepararAsync(string caminho, string prestadorCnpj, CancellationToken ct=default)
    {
        var report=await Task.Run(() => { using var stream=File.OpenRead(caminho);return new LeitorRelatorioBaixas().Ler(stream); },ct);
        await using var db=await factory.CreateDbContextAsync(ct);
        var notes=await NotasDoPrestador(db,prestadorCnpj).AsNoTracking().Include(n=>n.Parcelas).Include(n=>n.Pagamento).ToListAsync(ct);
        var preview=new ConciliadorBaixas().Conciliar(report,notes,await db.Pagamentos.AsNoTracking().ToListAsync(ct),prestadorCnpj);
        var prints=preview.Itens.Select(i=>i.Impressao).ToArray();
        var used=(await db.RegistrosBaixas.AsNoTracking().Where(x=>prints.Contains(x.Impressao)).Select(x=>x.Impressao).ToListAsync(ct)).ToHashSet();
        return preview with { NomeArquivo=Path.GetFileName(caminho), Itens=preview.Itens.Where(i=>!i.JaBaixada && !used.Contains(i.Impressao)).ToList().AsReadOnly() };
    }
    public async Task<ItemPrevia> RevisarAsync(PreviaBaixas previa,int linhaId,string numero,string justificativa,CancellationToken ct=default)
    {
        var original=previa.Itens.Single(i=>i.Origem.Id==linhaId);
        if(original.Status==StatusConciliacao.Apta || original.JaBaixada)throw new InvalidOperationException("Selecione uma linha de Conferir ou Ignoradas.");
        if(string.IsNullOrWhiteSpace(justificativa))throw new InvalidOperationException("Informe a justificativa da conferência.");
        await using var db=await factory.CreateDbContextAsync(ct);
        var notes=await NotasDoPrestador(db,previa.PrestadorCnpj).AsNoTracking().Include(n=>n.Parcelas).Include(n=>n.Pagamento).ToListAsync(ct);
        var candidates=notes.Where(n=>n.Numero.Trim().TrimStart('0')==numero.Trim().TrimStart('0')).ToList();
        if(candidates.Count!=1)throw new InvalidOperationException("A nota informada não foi encontrada ou tem número duplicado para este prestador. Confira o número ou importe seu XML primeiro.");
        var mapping=new Dictionary<int,ConferenciaManual>{{linhaId,new(candidates[0].Id,justificativa.Trim())}};
        var item=new ConciliadorBaixas().Conciliar(previa.Relatorio,notes,await db.Pagamentos.AsNoTracking().ToListAsync(ct),previa.PrestadorCnpj,mapping).Itens.Single(i=>i.Origem.Id==linhaId);
        if(item.Status!=StatusConciliacao.Apta)throw new InvalidOperationException(item.Motivo);
        if(previa.Itens.Any(i=>i.Origem.Id!=linhaId && i.PodeBaixar && i.NfseId==item.NfseId))throw new InvalidOperationException("Outra linha já está vinculada a esta nota. Não é possível baixá-la duas vezes.");
        if(await db.RegistrosBaixas.AnyAsync(x=>x.Impressao==item.Impressao,ct))throw new InvalidOperationException("Este lançamento já foi importado.");
        return item;
    }
    public async Task<ResultadoImportacao> ConfirmarAsync(PreviaBaixas previa, IReadOnlyCollection<int> linhasSelecionadas, CancellationToken ct=default)
    {
        var ids=linhasSelecionadas.ToHashSet();
        var selected=previa.Itens.Where(i=>ids.Contains(i.Origem.Id)).ToList();
        if(ids.Count==0 || ids.Count!=linhasSelecionadas.Count || selected.Count!=ids.Count || selected.Any(i=>!i.PodeBaixar))
            throw new InvalidOperationException("Selecione lançamentos disponíveis para baixa, sem repetição.");
        if(selected.Select(i=>i.NfseId).Distinct().Count()!=selected.Count)throw new InvalidOperationException("Há mais de uma linha para a mesma nota.");
        await using var db=await factory.CreateDbContextAsync(ct);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var notes=await NotasDoPrestador(db,previa.PrestadorCnpj).Include(n=>n.Parcelas).Include(n=>n.Pagamento).ToListAsync(ct);
        var mapping=selected.Where(i=>i.Conferencia is not null).ToDictionary(i=>i.Origem.Id,i=>i.Conferencia!);
        var fresh=new ConciliadorBaixas().Conciliar(previa.Relatorio,notes,await db.Pagamentos.ToListAsync(ct),previa.PrestadorCnpj,mapping);
        var prints=selected.Select(i=>i.Impressao).ToArray();
        if(await db.RegistrosBaixas.AnyAsync(x=>prints.Contains(x.Impressao),ct))
            throw new InvalidOperationException("Um lançamento já foi importado. Atualize a prévia; nenhuma baixa deste lote foi gravada.");
        foreach(var old in selected)
        {
            var current=fresh.Itens.SingleOrDefault(i=>i.Origem.Id==old.Origem.Id);
            if(current is null || !current.PodeBaixar || current.NfseId!=old.NfseId ||
                current.EstadoOriginal!=old.EstadoOriginal || current.Impressao!=old.Impressao ||
                current.PagamentoId!=old.PagamentoId || current.Local!=old.Local)
                throw new InvalidOperationException("A nota ou o cadastro mudou desde a prévia. Gere outra prévia; nenhuma baixa foi gravada.");
        }
        var lot=new LoteBaixa { HashArquivo=previa.Relatorio.HashArquivo,NomeArquivo=previa.NomeArquivo,
            PrestadorCnpj=previa.PrestadorCnpj,ImportadoEmUtc=DateTime.UtcNow,Quantidade=selected.Count,Valor=selected.Sum(i=>i.Origem.Credito!.Value) };
        db.LotesBaixas.Add(lot);
        foreach(var item in selected)
        {
            var note=notes.Single(n=>n.Id==item.NfseId);
            string before=Auditoria(note);
            var date=item.Origem.Data!.Value.Date;
            note.EstaPago=true;note.Local=item.Local;note.PagamentoId=item.PagamentoId;note.DataPagamentoEfetivo=date;
            if(note.Parcelas.Count==0) note.Parcelas.Add(new NfseParcela { NumeroParcela=1,ValorParcela=note.ValorLiquidoNfse,DataVencimentoParcela=date });
            var parcel=note.Parcelas.Single();
            parcel.ParcelaPaga=true;parcel.DataPagamentoParcela=date;
            note.DataPrevistaRecebimento=parcel.DataVencimentoParcela;
            db.RegistrosBaixas.Add(new RegistroBaixa { Lote=lot,Nota=note,Impressao=item.Impressao,
                Planilha=item.Origem.Planilha,Linha=item.Origem.Linha,Historico=item.Origem.Historico,Centro=item.Origem.Centro,
                Contrapartida=item.Origem.Contrapartida,DataRelatorio=date,Valor=item.Origem.Credito.Value,
                EstadoAnterior=before,EstadoPosterior=Auditoria(note,item.Conferencia ?? (item.Status==StatusConciliacao.Revisao ? new ConferenciaManual(note.Id,"Selecionada pelo usuário na aba Conferir: "+item.Motivo) : null)) });
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(lot.Id,lot.Quantidade,lot.Valor);
    }
    private static IQueryable<Nfse> NotasDoPrestador(ApplicationDbContext db,string cnpj)
    {
        string digits=ConciliadorBaixas.Digitos(cnpj);
        return db.Nfses.Where(n=>n.PrestadorCnpj.Replace(".","").Replace("/","").Replace("-","").Replace(" ","")==digits);
    }
    private static string Auditoria(Nfse n,ConferenciaManual? conferencia=null) => JsonSerializer.Serialize(new {
        ConferenciaManual=conferencia,
        n.Id,n.Numero,n.PrestadorCnpj,n.EstaPago,n.Local,n.PagamentoId,n.DataPagamentoEfetivo,n.DataPrevistaRecebimento,
        Parcelas=n.Parcelas.Select(p=>new{p.Id,p.NumeroParcela,p.ValorParcela,p.DataVencimentoParcela,p.ParcelaPaga,p.DataPagamentoParcela}) });
}

