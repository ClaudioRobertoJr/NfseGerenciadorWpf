namespace NfseGerenciador.Services;

public static class ReportCsv
{
    public static string Quote(string? value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
}
