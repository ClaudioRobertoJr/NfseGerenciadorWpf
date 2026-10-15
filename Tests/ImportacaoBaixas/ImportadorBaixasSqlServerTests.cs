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

