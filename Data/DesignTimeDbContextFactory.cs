using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace NfseGerenciador.Data;

// Used only by EF tooling; never connects to the user's database during build.
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args) => new(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\MSSQLLocalDB;Database=NfseDesign;Trusted_Connection=True;TrustServerCertificate=True" })
        .Build());
}
