using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using NfseGerenciador.Services;
using NfseGerenciador.Tests.ImportacaoBaixas;
using Xunit;
namespace NfseGerenciador.Tests.Atualizacao;
public class AtualizacaoBancoSqlServerTests
{
    [SqlServerFact] public async Task Migrates_old_database_after_backup_preserving_existing_rows()
    {
        await using var f=new SqlServerFixture();
        await using(var db=f.CreateDbContext())
        {
            await db.GetService<IMigrator>().MigrateAsync("20250610143214_NfseGerenciador");
            var note=ConciliadorBaixasTests.Nota();note.Id=0;db.Nfses.Add(note);await db.SaveChangesAsync();
        }
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["ConnectionStrings:DefaultConnection"]=f.ConnectionString}).Build();
        var svc=new AtualizacaoBancoService(f,config);
        Assert.Single(await svc.PendentesAsync());Assert.NotNull(await svc.AtualizarAsync());
        await using var check=f.CreateDbContext();Assert.Equal(100m,(await check.Nfses.SingleAsync()).ValorLiquidoNfse);Assert.Equal(7,await check.Pagamentos.CountAsync());Assert.Empty(await svc.PendentesAsync());
        Assert.Null(await svc.AtualizarAsync());
    }
    [SqlServerFact] public async Task Failed_backup_does_not_apply_pending_migration()
    {
        await using var f=new SqlServerFixture();await using(var db=f.CreateDbContext())await db.GetService<IMigrator>().MigrateAsync("20250610143214_NfseGerenciador");
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["ConnectionStrings:DefaultConnection"]=f.ConnectionString,["AppSettings:DefaultBackupPath"]=@"Z:\impossible-nfse-test-path\not-present"}).Build();
        await Assert.ThrowsAnyAsync<Exception>(()=>new AtualizacaoBancoService(f,config).AtualizarAsync());
        await using var check=f.CreateDbContext();Assert.Single(await check.Database.GetAppliedMigrationsAsync());
    }
}

