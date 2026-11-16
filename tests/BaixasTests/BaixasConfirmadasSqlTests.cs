using Microsoft.EntityFrameworkCore;
using NfseGerenciador.Models;
using NfseGerenciador.Models.ImportacaoBaixas;
using NfseGerenciador.Services.ImportacaoBaixas;
using Xunit;
namespace NfseGerenciador.Tests.ImportacaoBaixas;

public class BaixasConfirmadasSqlTests
{
    private static async Task<PreviaBaixas> Setup(SqlServerFixture f,string code="CONFIRMADO_CRED_6_V1")
    {
        await f.InitializeAsync();await using var db=f.CreateDbContext();var n=ConciliadorBaixasTests.Nota();n.Id=0;
        n.Parcelas.Add(new(){NumeroParcela=1,ValorParcela=100,DataVencimentoParcela=n.DataEmissao.AddDays(30)});
        db.Nfses.Add(n);await db.SaveChangesAsync();
        return new ConciliadorBaixas().Conciliar(new("TEST",[BaixasConfirmadasTests.Row(code)],[]),[n],await db.Pagamentos.ToListAsync(),ConciliadorBaixasTests.Cnpj);
    }
    [SqlServerFact] public async Task Six_paid_installments_preserve_sum_dates_type_local_and_audit()
    {
        await using var f=new SqlServerFixture();var p=await Setup(f);var svc=new ImportadorBaixas(f);await svc.ConfirmarAsync(p,[1]);
        await using var db=f.CreateDbContext();var n=await db.Nfses.Include(n=>n.Parcelas).SingleAsync();var parcels=n.Parcelas.OrderBy(x=>x.NumeroParcela).ToArray();
        Assert.True(n.EstaPago);Assert.Equal(3,n.PagamentoId);Assert.Equal(LocalPagamento.Laboratorio,n.Local);Assert.Equal(new DateTime(2026,1,31),n.DataPagamentoEfetivo);
        Assert.Equal(6,parcels.Length);Assert.Equal(100m,parcels.Sum(x=>x.ValorParcela));Assert.Equal(16.67m,parcels[0].ValorParcela);Assert.Equal(16.65m,parcels[5].ValorParcela);
        for(int i=0;i<6;i++){Assert.True(parcels[i].ParcelaPaga);Assert.Equal(new DateTime(2026,1,31),parcels[i].DataPagamentoParcela);Assert.Equal(new DateTime(2026,1,31).AddMonths(i),parcels[i].DataVencimentoParcela);}
        Assert.Equal(new DateTime(2026,6,30),n.DataPrevistaRecebimento);Assert.Equal(7,await db.Pagamentos.CountAsync());
        var audit=await db.RegistrosBaixas.SingleAsync();Assert.Equal("CONFIRMADO_CRED_6_V1",audit.Contrapartida);Assert.Contains("NumeroParcela",audit.EstadoPosterior);Assert.Contains("Confirmada pelo usuário",System.Text.Json.JsonDocument.Parse(audit.EstadoPosterior).RootElement.GetProperty("ConferenciaManual").GetProperty("Justificativa").GetString());
        await Assert.ThrowsAsync<InvalidOperationException>(()=>svc.ConfirmarAsync(p,[1]));Assert.Equal(1,await db.LotesBaixas.CountAsync());
    }
    [SqlServerFact] public async Task Debit_uses_existing_debit_and_one_paid_parcel()
    {
        await using var f=new SqlServerFixture();var p=await Setup(f,"CONFIRMADO_DEB_1_V1");await new ImportadorBaixas(f).ConfirmarAsync(p,[1]);
        await using var db=f.CreateDbContext();var n=await db.Nfses.Include(n=>n.Parcelas).SingleAsync();Assert.Equal(4,n.PagamentoId);Assert.True(Assert.Single(n.Parcelas).ParcelaPaga);
    }
    [SqlServerFact] public async Task Newly_paid_parcel_aborts_without_deleting_history()
    {
        await using var f=new SqlServerFixture();var p=await Setup(f);
        await using(var db=f.CreateDbContext()){var parcel=await db.NfseParcelas.SingleAsync();parcel.ParcelaPaga=true;parcel.DataPagamentoParcela=new(2026,1,1);await db.SaveChangesAsync();}
        await Assert.ThrowsAsync<InvalidOperationException>(()=>new ImportadorBaixas(f).ConfirmarAsync(p,[1]));
        await using var check=f.CreateDbContext();Assert.True((await check.NfseParcelas.SingleAsync()).ParcelaPaga);Assert.Empty(await check.LotesBaixas.ToListAsync());
    }
    [SqlServerFact] public async Task Changed_contract_after_preview_aborts_without_payment()
    {
        await using var f=new SqlServerFixture();var p=await Setup(f);p=p with{Relatorio=p.Relatorio with{Lancamentos=[BaixasConfirmadasTests.Row("CONFIRMADO_CRED_3_V1")]}};
        await Assert.ThrowsAsync<InvalidOperationException>(()=>new ImportadorBaixas(f).ConfirmarAsync(p,[1]));
        await using var check=f.CreateDbContext();Assert.False((await check.Nfses.SingleAsync()).EstaPago);Assert.Single(await check.NfseParcelas.ToListAsync());Assert.Empty(await check.RegistrosBaixas.ToListAsync());
    }
}

