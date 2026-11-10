using System.Data;

namespace NfseGerenciador.Services;

public static class BackupSetSelector
{
    public static int SelectLatestFull(DataTable headers, string databaseName)
    {
        var candidate = headers.AsEnumerable()
            .Where(row => Convert.ToInt32(row["BackupType"]) == 1
                && string.Equals(Convert.ToString(row["DatabaseName"]), databaseName, StringComparison.Ordinal)
                && row["BackupFinishDate"] is DateTime
                && row["Position"] != DBNull.Value
                && Convert.ToInt32(row["Position"]) > 0
                && (!headers.Columns.Contains("IsDamaged") || row["IsDamaged"] is false))
            .OrderByDescending(row => (DateTime)row["BackupFinishDate"])
            .ThenByDescending(row => Convert.ToInt32(row["Position"]))
            .FirstOrDefault();
        if (candidate == null)
            throw new InvalidOperationException("O arquivo não contém um backup completo e válido do banco selecionado.");
        return Convert.ToInt32(candidate["Position"]);
    }
}
