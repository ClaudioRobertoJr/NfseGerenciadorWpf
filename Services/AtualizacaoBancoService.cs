using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
namespace NfseGerenciador.Services;
public sealed class AtualizacaoBancoService(IDbContextFactory<ApplicationDbContext> factory,IConfiguration config)
{
    public async Task<string[]> PendentesAsync(CancellationToken ct=default)
    {
        await using var db=await factory.CreateDbContextAsync(ct);
        if(!await db.Database.CanConnectAsync(ct))throw new InvalidOperationException("Não foi possível conectar ao banco existente. Confira o servidor, nome do banco e permissões no appsettings.json. Nenhum banco foi criado.");
        if(!(await db.Database.GetAppliedMigrationsAsync(ct)).Any())
            throw new InvalidOperationException("O banco configurado não possui o histórico de migrações esperado. A atualização foi interrompida para preservar os dados.");
        return (await db.Database.GetPendingMigrationsAsync(ct)).ToArray();
    }
    public async Task<string?> AtualizarAsync(CancellationToken ct=default)
    {
        if((await PendentesAsync(ct)).Length==0)return null;
        string backup=await new SqlBackupService(config).CriarVerificadoAsync(ct:ct);
        await using var db=await factory.CreateDbContextAsync(ct);
        await db.Database.MigrateAsync(ct);
        return backup;
    }
}

