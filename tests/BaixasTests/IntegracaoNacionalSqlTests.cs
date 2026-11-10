using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NfseGerenciador.Models;
using NfseGerenciador.Services.ImportacaoBaixas;
using Xunit;
namespace NfseGerenciador.Tests.ImportacaoBaixas;
public class IntegracaoNacionalSqlTests
{
    [SqlServerFact] public async Task Atualizacao_113_preserva_notas_parcelas_pagamentos_e_NSU()
    {
        await using var f = new SqlServerFixture(); await f.InitializeAsync();
        await using var db = f.CreateDbContext();
        // Reproduce the published 1.1.3 schema in this disposable test database only.
        await db.Database.ExecuteSqlRawAsync("DROP TABLE RegistrosBaixas; DROP TABLE LotesBaixas; DELETE FROM __EFMigrationsHistory WHERE MigrationId='20261015141547_HistoricoImportacaoBaixas';");
        var n = ConciliadorBaixasTests.Nota(); n.Id=0; n.EstaPago=true; n.PagamentoId=2; n.DataPagamentoEfetivo=new(2026,11,9);
        n.Parcelas.Add(new(){NumeroParcela=1,ValorParcela=100,ParcelaPaga=true,DataPagamentoParcela=new(2026,11,9),DataVencimentoParcela=new(2026,11,9)});
        db.Nfses.Add(n); db.Set<NacionalSync>().Add(new(){Cnpj=n.PrestadorCnpj,UltimoNsu=87654}); await db.SaveChangesAsync();
        var state=ConciliadorBaixas.Estado(n); var count=await db.Pagamentos.CountAsync();
        await db.Database.MigrateAsync(); db.ChangeTracker.Clear();
        Assert.Equal(state,ConciliadorBaixas.Estado(await db.Nfses.Include(x=>x.Parcelas).SingleAsync()));
        Assert.Equal(87654,(await db.Set<NacionalSync>().SingleAsync()).UltimoNsu);
        Assert.Equal(count,await db.Pagamentos.CountAsync()); Assert.Empty(await db.RegistrosBaixas.ToListAsync());
        Assert.False(db.Database.HasPendingModelChanges());
    }
    [SqlServerFact] public async Task Migracao_repetida_preserva_historico_e_baixas()
    {
        await using var f=new SqlServerFixture();await f.InitializeAsync();
        await using var db=f.CreateDbContext();var n=ConciliadorBaixasTests.Nota();n.Id=0;db.Nfses.Add(n);await db.SaveChangesAsync();
        var p=new ConciliadorBaixas().Conciliar(new("H",[ConciliadorBaixasTests.Linha()],[]),[n],await db.Pagamentos.ToListAsync(),n.PrestadorCnpj);
        await new ImportadorBaixas(f).ConfirmarAsync(p,[1]);
        await db.Database.MigrateAsync();db.ChangeTracker.Clear();
        Assert.True((await db.Nfses.SingleAsync()).EstaPago);Assert.Single(await db.RegistrosBaixas.ToListAsync());Assert.Single(await db.LotesBaixas.ToListAsync());
    }
    [SqlServerFact] public async Task Cancelamento_entre_previa_e_confirmacao_impede_todo_lote()
    {
        await using var f=new SqlServerFixture();await f.InitializeAsync();
        await using var db=f.CreateDbContext();var n=ConciliadorBaixasTests.Nota();n.Id=0;db.Nfses.Add(n);await db.SaveChangesAsync();
        var p=new ConciliadorBaixas().Conciliar(new("H",[ConciliadorBaixasTests.Linha()],[]),[n],await db.Pagamentos.ToListAsync(),n.PrestadorCnpj);
        n.Cancelada=true;await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>new ImportadorBaixas(f).ConfirmarAsync(p,[1]));
        db.ChangeTracker.Clear();Assert.False((await db.Nfses.SingleAsync()).EstaPago);Assert.Empty(await db.RegistrosBaixas.ToListAsync());Assert.Empty(await db.LotesBaixas.ToListAsync());
    }
}
