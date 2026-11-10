using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using NfseGerenciador.Services;

internal static class Program
{
    private static string? logPath;

    [STAThread]
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        if (args.Contains("--info"))
        {
            Console.WriteLine($"TesteConexaoA3; runtime={Environment.Version}; x64={Environment.Is64BitProcess}; timeout=120s; consulta unica; sem banco");
            return 0;
        }
        var exitCode = 1;
        try
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NfseGerenciador", "Logs");
            Directory.CreateDirectory(directory);
            logPath = Path.Combine(directory, $"teste-conexao-a3-{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}.log");
            Log($"Teste isolado; runtime={Environment.Version}; x64={Environment.Is64BitProcess}; Windows={Environment.OSVersion}");
            Console.WriteLine("Feche o gerenciador antes deste teste. Sera feita uma unica consulta de leitura na API de producao.");
            Console.WriteLine("Nenhum dado sera importado no banco. O log nao grava XML, PIN nem dados do certificado.");
            Console.Write("CNPJ da instituicao representada (14 digitos): ");
            var cnpj = Console.ReadLine()?.Trim() ?? "";
            if (cnpj.Length != 14 || cnpj.Any(c => c < '0' || c > '9'))
                throw new ArgumentException("Informe 14 digitos, sem pontuacao, para executar o teste.");

            using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);
            var valid = new X509Certificate2Collection(store.Certificates.Cast<X509Certificate2>()
                .Where(x => x.HasPrivateKey && x.NotBefore <= DateTime.Now && x.NotAfter > DateTime.Now).ToArray());
            if (valid.Count == 0) throw new InvalidOperationException("Nenhum certificado valido com chave privada disponivel no Windows.");
            Log("Abrindo seletor de certificado");
            var selected = X509Certificate2UI.SelectFromCollection(valid, "Teste de conexao A3", "Escolha o mesmo certificado utilizado no gerenciador.", X509SelectionFlag.SingleSelection);
            if (selected.Count == 0) { Log("Selecao cancelada; nenhuma consulta enviada."); return 2; }
            using var certificate = selected[0];
            Log("Certificado selecionado; iniciando teste sem janela WPF e sem banco.");
            Task.Run(async () =>
            {
                using var handler = new HttpClientHandler { ClientCertificateOptions = ClientCertificateOption.Manual, AllowAutoRedirect = false };
                handler.ClientCertificates.Add(certificate);
                using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(120) };
                var watch = Stopwatch.StartNew();
                Log("Iniciando HTTPS; timeout de 120 segundos. Digite o PIN somente na janela do cartao.");
                try
                {
                    var documents = await new AdnClient(http).ReadAsync(0, cnpj, CancellationToken.None);
                    Log($"SUCESSO: API de producao validada em {watch.Elapsed.TotalSeconds:F1}s; documentos no lote={documents.Count}. Nenhum XML salvo e nenhum NSU avancado no gerenciador.");
                }
                catch (Exception ex)
                {
                    Log($"FALHA apos {watch.Elapsed.TotalSeconds:F1}s; timeout identificado={ex is OperationCanceledException && ex.InnerException is TimeoutException}");
                    throw;
                }
            }).GetAwaiter().GetResult();
            exitCode = 0;
        }
        catch (Exception ex) { Log(ex.ToString()); }
        finally
        {
            Console.WriteLine($"\nLog do teste: {logPath ?? "nao foi possivel criar"}");
            Console.WriteLine("Pressione Enter para fechar.");
            Console.ReadLine();
        }
        return exitCode;
    }

    private static void Log(string message)
    {
        var line = $"{DateTimeOffset.Now:O} | thread={Environment.CurrentManagedThreadId} | {Thread.CurrentThread.GetApartmentState()} | {message}";
        Console.WriteLine(line);
        if (logPath != null)
        {
            try { File.AppendAllText(logPath, line + Environment.NewLine, Encoding.UTF8); }
            catch (Exception ex) { Console.WriteLine("Nao foi possivel gravar o log: " + ex.Message); }
        }
    }
}
