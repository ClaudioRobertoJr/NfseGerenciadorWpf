using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using NfseGerenciador.Services;

internal static class Program
{
    [STAThread]
    private static int Main() => Task.Run(RunAsync).GetAwaiter().GetResult();

    private static async Task<int> RunAsync()
    {
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        X509Certificate2? certificate = null;
        HttpClient? http = null;
        try
        {
            string? line;
            while ((line = await Console.In.ReadLineAsync()) != null)
            {
                var request = JsonSerializer.Deserialize<AdnWorkerRequest>(line) ?? throw new InvalidDataException("Consulta ausente.");
                if (http == null)
                {
                    store.Open(OpenFlags.ReadOnly);
                    var matches = store.Certificates.Find(X509FindType.FindByThumbprint, request.Thumbprint, validOnly: false);
                    certificate = matches.Cast<X509Certificate2>().FirstOrDefault(x => x.HasPrivateKey)
                        ?? throw new InvalidOperationException("O certificado selecionado não está disponível no processo de conexão.");
                    var handler = new HttpClientHandler { ClientCertificateOptions = ClientCertificateOption.Manual, AllowAutoRedirect = false };
                    handler.ClientCertificates.Add(certificate);
                    http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(120) };
                }
                if (!string.Equals(certificate!.Thumbprint, request.Thumbprint, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Certificado alterado durante a conexão.");
                var documents = await new AdnClient(http).ReadAsync(request.Nsu, request.Cnpj, CancellationToken.None);
                await Console.Out.WriteLineAsync(JsonSerializer.Serialize(new AdnWorkerResponse(documents, null)));
                await Console.Out.FlushAsync();
            }
            return 0;
        }
        catch (Exception ex)
        {
            var error = ex is OperationCanceledException && ex.InnerException is TimeoutException
                ? "A consulta HTTPS excedeu o limite de 120 segundos."
                : "Falha na conexão isolada com o cartão/API: " + ex.GetBaseException().Message;
            await Console.Out.WriteLineAsync(JsonSerializer.Serialize(new AdnWorkerResponse(null, error, ex.ToString())));
            await Console.Out.FlushAsync();
            return 1;
        }
        finally { http?.Dispose(); certificate?.Dispose(); }
    }
}
