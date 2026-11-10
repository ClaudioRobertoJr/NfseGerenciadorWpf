using System.IO;
using Microsoft.EntityFrameworkCore;
using NfseGerenciador.Models.ImportacaoBaixas;
using NfseGerenciador.Services.ImportacaoBaixas;
using Xunit;
namespace NfseGerenciador.Tests.ImportacaoBaixas;
public class ImportadorBaixasSqlServerTests
{
    private static async Task<PreviaBaixas> Setup(SqlServerFixture f,bool parcel=true)
    {
        await f.InitializeAsync(); await using var db=f.CreateDbContext();
        var n=ConciliadorBaixasTests.Nota();n.Id=0;
        if(parcel)n.Parcelas.Add(new(){NumeroParcela=1,ValorParcela=100,DataVencimentoParcela=new(2026,6,1)});
        db.Nfses.Add(n);await db.SaveChangesAsync();
        return new ConciliadorBaixas().Conciliar(new("HASH",[ConciliadorBaixasTests.Linha()],[]),[n],await db.Pagamentos.ToListAsync(),ConciliadorBaixasTests.Cnpj);
    }
    private static PreviaBaixas NeedsReview(PreviaBaixas p)
    {
        var row=p.Itens[0].Origem with{Historico="NOTA 9999 - NOME DIVERGENTE",Data=new DateTime(2025,1,1)};
        return p with{Relatorio=p.Relatorio with{Lancamentos=[row]},Itens=[p.Itens[0] with{Origem=row,Status=StatusConciliacao.Revisao}]};
    }
    [SqlServerFact] public async Task Manual_review_keeps_original_receipt_and_audits_explicit_association()
    {
        await using var f=new SqlServerFixture();var p=NeedsReview(await Setup(f));var svc=new ImportadorBaixas(f);
        var approved=await svc.RevisarAsync(p,1,"8000","Recibo conferido");p=p with{Itens=[approved]};
        await svc.ConfirmarAsync(p,[1]);await using var db=f.CreateDbContext();var audit=await db.RegistrosBaixas.SingleAsync();
        Assert.Equal("NOTA 9999 - NOME DIVERGENTE",audit.Historico);Assert.Contains("Recibo conferido",audit.EstadoPosterior);
        Assert.Equal(new DateTime(2025,1,1),(await db.Nfses.SingleAsync()).DataPagamentoEfetivo);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>svc.ConfirmarAsync(p,[1]));
    }
    [SqlServerFact] public async Task Manual_review_still_rejects_note_changed_after_approval()
    {
        await using var f=new SqlServerFixture();var p=NeedsReview(await Setup(f));var svc=new ImportadorBaixas(f);
        p=p with{Itens=[await svc.RevisarAsync(p,1,"8000","Conferido")]};
        await using(var db=f.CreateDbContext()){(await db.Nfses.SingleAsync()).ValorLiquidoNfse=90;await db.SaveChangesAsync();}
        await Assert.ThrowsAsync<InvalidOperationException>(()=>svc.ConfirmarAsync(p,[1]));
        await using var check=f.CreateDbContext();Assert.Empty(await check.RegistrosBaixas.ToListAsync());Assert.False((await check.Nfses.SingleAsync()).EstaPago);
    }
    [SqlServerFact] public async Task Settles_multiple_review_warnings_directly_without_individual_approval()
    {
        await using var f=new SqlServerFixture();await Setup(f);
        await using(var db=f.CreateDbContext()){var n=ConciliadorBaixasTests.Nota();n.Id=0;n.Numero="8001";db.Nfses.Add(n);await db.SaveChangesAsync();}
        var row=ConciliadorBaixasTests.Linha() with{Data=new DateTime(2025,1,1)};
        await using var read=f.CreateDbContext();
        var p=new ConciliadorBaixas().Conciliar(new("H",[row,row with{Id=2,Historico="NOTA 8001 - OUTRO NOME"}],[]),await read.Nfses.Include(n=>n.Parcelas).ToListAsync(),await read.Pagamentos.ToListAsync(),ConciliadorBaixasTests.Cnpj);
        Assert.All(p.Itens,i=>{Assert.Equal(StatusConciliacao.Revisao,i.Status);Assert.True(i.PodeBaixar);Assert.Null(i.Conferencia);});
        var svc=new ImportadorBaixas(f);var result=await svc.ConfirmarAsync(p,[1,2]);Assert.Equal(2,result.Quantidade);
        await using var check=f.CreateDbContext();Assert.Equal(2,await check.Nfses.CountAsync(n=>n.EstaPago));
        Assert.All(await check.RegistrosBaixas.ToListAsync(),r=>Assert.Contains("Selecionada pelo usu",r.EstadoPosterior));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>svc.ConfirmarAsync(p,[1,2]));
    }
    [SqlServerFact] public async Task Cash_duplicates_are_reused_without_deleting_or_relinking_existing_payments()
    {
        await using var f=new SqlServerFixture();await Setup(f);int duplicateId;
        await using(var db=f.CreateDbContext())
        {
            var duplicate=new NfseGerenciador.Models.Pagamento{TipoRecebimento="DINHEIRO",EhAVista=true,NumeroParcelas=1};db.Pagamentos.Add(duplicate);await db.SaveChangesAsync();duplicateId=duplicate.Id;
            var historical=ConciliadorBaixasTests.Nota();historical.Id=0;historical.Numero="7777";historical.EstaPago=true;historical.PagamentoId=duplicateId;db.Nfses.Add(historical);await db.SaveChangesAsync();
        }
        await using var read=f.CreateDbContext();
        var p=new ConciliadorBaixas().Conciliar(new("H",[ConciliadorBaixasTests.Linha() with{Contrapartida="132"}],[]),await read.Nfses.Include(n=>n.Parcelas).ToListAsync(),await read.Pagamentos.ToListAsync(),ConciliadorBaixasTests.Cnpj);
        Assert.Equal(1,p.Itens[0].PagamentoId);await new ImportadorBaixas(f).ConfirmarAsync(p,[1]);
        await using var check=f.CreateDbContext();Assert.Equal(1,(await check.Nfses.SingleAsync(n=>n.Numero=="8000")).PagamentoId);
        Assert.Equal(duplicateId,(await check.Nfses.SingleAsync(n=>n.Numero=="7777")).PagamentoId);Assert.Equal(8,await check.Pagamentos.CountAsync());
    }
    [SqlServerFact] public async Task Commits_note_parcel_and_history_together()
    {
        await using var f=new SqlServerFixture();var preview=await Setup(f);
        await using var before=f.CreateDbContext();var id=(await before.NfseParcelas.SingleAsync()).Id;
        var result=await new ImportadorBaixas(f).ConfirmarAsync(preview,[1]);
        await using var db=f.CreateDbContext();var n=await db.Nfses.Include(n=>n.Parcelas).SingleAsync();
        Assert.Equal(1,result.Quantidade);Assert.Equal(100m,result.Valor);Assert.True(n.EstaPago);
        Assert.Equal(new DateTime(2026,10,15),n.DataPagamentoEfetivo);
        Assert.Equal(id,Assert.Single(n.Parcelas).Id);Assert.True(n.Parcelas[0].ParcelaPaga);
        Assert.Equal(new DateTime(2026,10,15),n.Parcelas[0].DataPagamentoParcela);
        Assert.Equal(1,await db.RegistrosBaixas.CountAsync());Assert.Equal(1,await db.LotesBaixas.CountAsync());
        Assert.Contains("EstaPago", (await db.RegistrosBaixas.SingleAsync()).EstadoAnterior);
    }
    [SqlServerFact] public async Task Reimport_and_changed_position_cannot_pay_twice()
    {
        await using var f=new SqlServerFixture();var p=await Setup(f);var svc=new ImportadorBaixas(f);await svc.ConfirmarAsync(p,[1]);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>svc.ConfirmarAsync(p,[1]));
        await using(var db=f.CreateDbContext()){var n=await db.Nfses.Include(n=>n.Parcelas).SingleAsync();n.EstaPago=false;n.DataPagamentoEfetivo=null;foreach(var x in n.Parcelas){x.ParcelaPaga=false;x.DataPagamentoParcela=null;}await db.SaveChangesAsync();}
        await using var fresh=f.CreateDbContext();var notes=await fresh.Nfses.Include(n=>n.Parcelas).Include(n=>n.Pagamento).ToListAsync();
        var q=new ConciliadorBaixas().Conciliar(new("OTHERHASH",[ConciliadorBaixasTests.Linha() with{Id=2,Linha=50,Planilha="Outra"}],[]),notes,await fresh.Pagamentos.ToListAsync(),ConciliadorBaixasTests.Cnpj);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>svc.ConfirmarAsync(q,[2]));
        Assert.Equal(1,await fresh.RegistrosBaixas.CountAsync());
    }
    [SqlServerFact] public async Task Changed_note_aborts_without_history()
    {
        await using var f=new SqlServerFixture();var p=await Setup(f);
        await using(var db=f.CreateDbContext()){var n=await db.Nfses.SingleAsync();n.ValorLiquidoNfse=90;await db.SaveChangesAsync();}
        await Assert.ThrowsAsync<InvalidOperationException>(()=>new ImportadorBaixas(f).ConfirmarAsync(p,[1]));
        await using var check=f.CreateDbContext();Assert.False((await check.Nfses.SingleAsync()).EstaPago);Assert.Equal(0,await check.RegistrosBaixas.CountAsync());
    }
    [SqlServerFact] public async Task Removed_payment_aborts_without_history()
    {
        await using var f=new SqlServerFixture();var p=await Setup(f);
        await using(var db=f.CreateDbContext()){db.Pagamentos.Remove(await db.Pagamentos.SingleAsync(x=>x.TipoRecebimento=="Pix"));await db.SaveChangesAsync();}
        await Assert.ThrowsAsync<InvalidOperationException>(()=>new ImportadorBaixas(f).ConfirmarAsync(p,[1]));
        await using var check=f.CreateDbContext();Assert.Equal(0,await check.LotesBaixas.CountAsync());
    }
    [SqlServerFact] public async Task Concurrent_imports_only_settle_once()
    {
        await using var f=new SqlServerFixture();var p=await Setup(f);
        async Task<bool> Run(){try{await new ImportadorBaixas(f).ConfirmarAsync(p,[1]);return true;}catch(Exception){return false;}}
        var results=await Task.WhenAll(Run(),Run());
        Assert.Equal(1,results.Count(x=>x));await using var db=f.CreateDbContext();Assert.Equal(1,await db.RegistrosBaixas.CountAsync());
    }
    [SqlServerFact] public async Task Database_failure_rolls_back_note_and_lot()
    {
        await using var f=new SqlServerFixture();var p=await Setup(f);
        await using(var db=f.CreateDbContext())await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectTestReceipt ON RegistrosBaixas AFTER INSERT AS THROW 51000, 'Induced test failure', 1;");
        await Assert.ThrowsAnyAsync<Exception>(()=>new ImportadorBaixas(f).ConfirmarAsync(p,[1]));
        await using var check=f.CreateDbContext();Assert.False((await check.Nfses.SingleAsync()).EstaPago);
        Assert.Equal(0,await check.LotesBaixas.CountAsync());Assert.Equal(0,await check.RegistrosBaixas.CountAsync());
    }
    [SqlServerFact] public async Task Creates_single_paid_parcel_when_none_exists()
    {
        await using var f=new SqlServerFixture();var p=await Setup(f,false);await new ImportadorBaixas(f).ConfirmarAsync(p,[1]);
        await using var db=f.CreateDbContext();var parcel=await db.NfseParcelas.SingleAsync();Assert.True(parcel.ParcelaPaga);Assert.Equal(100m,parcel.ValorParcela);
    }
}

