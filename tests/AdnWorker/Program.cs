using System.IO;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using NfseGerenciador.Services;

internal static class Program
{
    // Match the successful probe: certificate selection on the helper's STA main
    // thread, then HTTPS on the thread pool. No WPF Application or Dispatcher.
    [STAThread]
    private static int Main()
    {
        try
        {
            var line = Console.ReadLine();
            if (line == null) return 0;
            var request = JsonSerializer.Deserialize<AdnWorkerRequest>(line);
            if (request?.Command != "Select") throw new InvalidDataException("Selecione o certificado antes de consultar.");
            using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);
            var valid = new X509Certificate2Collection(store.Certificates.Cast<X509Certificate2>()
                .Where(x => x.HasPrivateKey && x.NotBefore <= DateTime.Now && x.NotAfter > DateTime.Now).ToArray());
            if (valid.Count == 0) throw new InvalidOperationException("Nenhum certificado válido disponível no Windows.");
            var selected = X509Certificate2UI.SelectFromCollection(valid, "Certificado para NFS-e",
                "Escolha o e-CPF com procuração ou o certificado da instituição.", X509SelectionFlag.SingleSelection);
            if (selected.Count == 0)
            {
                Reply(new(null, null, Cancelled: true));
                return 0;
            }
            using var certificate = selected[0];
            Reply(new(null, null, Selected: true));
            return Task.Run(() => RunAsync(certificate)).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            var error = ex is OperationCanceledException && ex.InnerException is TimeoutException
                ? "A consulta HTTPS excedeu o limite de 120 segundos."
                : "Falha no auxiliar de conexão: " + ex.GetBaseException().Message;
            Reply(new(null, error, ex.ToString()));
            return 1;
        }
    }

    private static async Task<int> RunAsync(X509Certificate2 certificate)
    {
        using var handler = new HttpClientHandler { ClientCertificateOptions = ClientCertificateOption.Manual, AllowAutoRedirect = false };
        handler.ClientCertificates.Add(certificate);
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(120) };
        var client = new AdnClient(http);
        string? line;
        while ((line = await Console.In.ReadLineAsync()) != null)
        {
            var request = JsonSerializer.Deserialize<AdnWorkerRequest>(line) ?? throw new InvalidDataException("Consulta ausente.");
            if (request.Command != "Read") throw new InvalidDataException("Comando inesperado após seleção.");
            var documents = await client.ReadAsync(request.Nsu, request.Cnpj, CancellationToken.None);
            Reply(new(documents, null));
        }
        return 0;
    }

    private static void Reply(AdnWorkerResponse response)
    {
        Console.WriteLine(JsonSerializer.Serialize(response));
        Console.Out.Flush();
    }
}
