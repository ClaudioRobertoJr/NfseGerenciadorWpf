using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace NfseGerenciador.Tests.ImportacaoBaixas;
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NFSE_TEST_CONNECTION"))) Skip="Requer NFSE_TEST_CONNECTION para SQL Server isolado; não usa appsettings."; }
}
public sealed class SqlServerFixture : IAsyncDisposable, IDbContextFactory<ApplicationDbContext>
{
    private readonly IConfiguration config;
    public string ConnectionString { get; }
    public SqlServerFixture()
    {
        var value=Environment.GetEnvironmentVariable("NFSE_TEST_CONNECTION") ?? throw new InvalidOperationException("NFSE_TEST_CONNECTION ausente.");
        var cs=new SqlConnectionStringBuilder(value);
        // This harness only creates/deletes a freshly generated database, never the database named by the caller.
        cs.InitialCatalog="NfseTests_"+Guid.NewGuid().ToString("N");cs.AttachDBFilename="";
        ConnectionString=cs.ConnectionString;
        config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["ConnectionStrings:DefaultConnection"]=ConnectionString }).Build();
    }
    public ApplicationDbContext CreateDbContext() => new(config);
    public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken=default) => Task.FromResult(CreateDbContext());
    public async Task InitializeAsync() { await using var db=CreateDbContext();await db.Database.MigrateAsync(); }
    public async ValueTask DisposeAsync()
    {
        if(!new SqlConnectionStringBuilder(ConnectionString).InitialCatalog.StartsWith("NfseTests_",StringComparison.Ordinal))throw new InvalidOperationException("Banco fora do escopo de teste.");
        await using var db=CreateDbContext();await db.Database.EnsureDeletedAsync();
    }
}

