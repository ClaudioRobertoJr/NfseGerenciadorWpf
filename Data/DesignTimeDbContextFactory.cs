using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

// Migration generation never reads the connection of an installed application.
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("NFSE_DESIGN_CONNECTION") ?? @"Server=(localdb)\NfseCodexTests;Database=NfseDesignOnly;Integrated Security=True;TrustServerCertificate=True";
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["ConnectionStrings:DefaultConnection"] = connection }).Build();
        return new ApplicationDbContext(config);
    }
}
