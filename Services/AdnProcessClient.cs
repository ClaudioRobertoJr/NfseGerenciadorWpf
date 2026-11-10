using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace NfseGerenciador.Services;

public record AdnWorkerRequest(long Nsu, string Cnpj, string Thumbprint);
public record AdnWorkerResponse(List<AdnDocument>? Documents, string? Error, string? Detail = null);

// Anonymous redirected pipes keep XML in memory and restrict communication to this child.
public sealed class AdnProcessClient : IDisposable
{
    private readonly Process process;
    private readonly string thumbprint;
    private bool disposed;

    public AdnProcessClient(string thumbprint, ProcessStartInfo? startInfo = null)
    {
        this.thumbprint = thumbprint;
        startInfo ??= new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "NfseAdnWorker.exe"));
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardInput = true;
        startInfo.RedirectStandardOutput = true;
        process = Process.Start(startInfo) ?? throw new InvalidOperationException("Não foi possível iniciar a conexão isolada com o cartão.");
    }

    public async Task<List<AdnDocument>> ReadAsync(long nsu, string cnpj, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(150));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        try
        {
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new AdnWorkerRequest(nsu, cnpj, thumbprint)).AsMemory(), linked.Token);
            await process.StandardInput.FlushAsync(linked.Token);
            var line = await process.StandardOutput.ReadLineAsync(linked.Token);
            if (line == null) throw new IOException("O processo de conexão com o cartão encerrou sem responder.");
            var response = JsonSerializer.Deserialize<AdnWorkerResponse>(line) ?? throw new InvalidDataException("Resposta vazia do processo de conexão.");
            if (response.Error != null) throw new InvalidOperationException(response.Error,
                response.Detail == null ? null : new Exception(response.Detail));
            return response.Documents ?? throw new InvalidDataException("Resposta sem documentos do processo de conexão.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            Dispose();
            throw new TimeoutException("A conexão isolada com o cartão não respondeu em 150 segundos.");
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { /* Child already exited during cleanup. */ }
        finally { process.Dispose(); }
    }
}
