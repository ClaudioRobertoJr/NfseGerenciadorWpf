using System.IO;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NfseGerenciador.Services;
using NfseGerenciador.ViewModels;
using Xunit;
namespace NfseGerenciador.Tests.ImportacaoBaixas;
public class BackupSqlTests
{
    [SqlServerFact] public async Task Restaura_ultimo_conjunto_completo_em_banco_descartavel()
    {
        await using var fixture=new SqlServerFixture();await fixture.InitializeAsync();
        var cs=new SqlConnectionStringBuilder(fixture.ConnectionString);var name=cs.InitialCatalog;
        Assert.StartsWith("NfseTests_",name);
        var path=Path.Combine(Path.GetTempPath(),name+"'backup.bak");
        try
        {
            await using(var db=fixture.CreateDbContext()){var n=ConciliadorBaixasTests.Nota();n.Id=0;db.Nfses.Add(n);await db.SaveChangesAsync();}
            cs.InitialCatalog="master";cs.Pooling=false;
            using(var connection=new SqlConnection(cs.ConnectionString))
            {
                await connection.OpenAsync();
                for(int i=1;i<=2;i++)
                {
                    using(var update=new SqlCommand($"UPDATE [{name}].dbo.Nfses SET TomadorRazaoSocial=@value;",connection)){update.Parameters.AddWithValue("@value","Backup "+i);await update.ExecuteNonQueryAsync();}
                    using(var backup=new SqlCommand($"BACKUP DATABASE [{name}] TO DISK=@path WITH NOINIT;",connection)){backup.Parameters.AddWithValue("@path",path);await backup.ExecuteNonQueryAsync();}
                }
                using var change=new SqlCommand($"UPDATE [{name}].dbo.Nfses SET TomadorRazaoSocial='Depois';",connection);await change.ExecuteNonQueryAsync();
            }
            using(var pooled=new SqlConnection(fixture.ConnectionString))SqlConnection.ClearPool(pooled);
            var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["ConnectionStrings:DefaultConnection"]=fixture.ConnectionString}).Build();
            await new BackupRestoreViewModel(config,new LogService()).RestoreSelectedBackupAsync(path);
            await using var check=fixture.CreateDbContext();Assert.Equal("Backup 2",(await check.Nfses.SingleAsync()).TomadorRazaoSocial);
        }
        finally {if(File.Exists(path))File.Delete(path);}
    }
}
