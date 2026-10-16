using System.IO;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
namespace NfseGerenciador.Services;
public sealed class SqlBackupService(IConfiguration config)
{
    public static string CaminhoExclusivo(string arquivo) => Path.Combine(
        Path.GetDirectoryName(arquivo) ?? "",
        $"{Path.GetFileNameWithoutExtension(arquivo)}_{Guid.NewGuid():N}.bak");
    public async Task<string> CriarVerificadoAsync(string? arquivo=null,CancellationToken ct=default)
    {
        var builder=new SqlConnectionStringBuilder(config.GetConnectionString("DefaultConnection"));
        string db=builder.InitialCatalog;
        if(string.IsNullOrWhiteSpace(db))throw new InvalidOperationException("Nome do banco ausente.");
        builder.InitialCatalog="master";
        await using var connection=new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(ct);
        if(string.IsNullOrWhiteSpace(arquivo))
        {
            string? folder=config["AppSettings:DefaultBackupPath"];
            if(string.IsNullOrWhiteSpace(folder))
            {
                using var cmd=new SqlCommand("SELECT CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultBackupPath'))",connection);
                folder=(await cmd.ExecuteScalarAsync(ct)) as string;
            }
            if(string.IsNullOrWhiteSpace(folder))throw new InvalidOperationException("Configure AppSettings:DefaultBackupPath com uma pasta acessível pelo serviço SQL Server.");
            string safe=string.Concat(db.Select(c=>char.IsLetterOrDigit(c)||c=='_'?c:'_'));
            // SQL Server resolves this path on its own machine; do not create a client-side directory.
            arquivo=folder.TrimEnd('\\','/')+@"\"+$"NFSe_{safe}_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.bak";
        }
        else arquivo=CaminhoExclusivo(arquivo);
        string quoted="["+db.Replace("]","]]")+"]";
        using(var cmd=new SqlCommand($"BACKUP DATABASE {quoted} TO DISK=@path WITH COPY_ONLY, CHECKSUM, NOINIT;",connection))
        {
            cmd.Parameters.AddWithValue("@path",arquivo);cmd.CommandTimeout=600;
            await cmd.ExecuteNonQueryAsync(ct);
        }
        using(var verify=new SqlCommand("RESTORE VERIFYONLY FROM DISK=@path WITH CHECKSUM;",connection))
        {
            verify.Parameters.AddWithValue("@path",arquivo);verify.CommandTimeout=600;
            await verify.ExecuteNonQueryAsync(ct);
        }
        return arquivo;
    }
}

