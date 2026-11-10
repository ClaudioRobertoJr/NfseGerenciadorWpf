using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace NfseGerenciador.Services;

public record AdnWorkerRequest(long Nsu, string Cnpj, string Command = "Read");
public record AdnWorkerResponse(List<AdnDocument>? Documents, string? Error, string? Detail = null, bool Selected = false, bool Cancelled = false);

// Anonymous redirected pipes keep XML in memory and restrict communication to this child.
public sealed class AdnProcessClient : IDisposable
{
    private readonly Process process;
    private bool disposed;

    public AdnProcessClient(ProcessStartInfo? startInfo = null)
    {
        startInfo ??= new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "NfseAdnWorker.exe"));
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardInput = true;
        startInfo.RedirectStandardOutput = true;
        process = Process.Start(startInfo) ?? throw new InvalidOperationException("Não foi possível iniciar a conexão isolada com o cartão.");
    }

    public async Task SelectAsync(CancellationToken ct)
    {
        var response = await ExchangeAsync(new AdnWorkerRequest(0, "", "Select"), TimeSpan.FromMinutes(5), ct);
        if (response.Cancelled) throw new OperationCanceledException("Seleção de certificado cancelada.");
        if (!response.Selected) throw new InvalidDataException("O auxiliar não confirmou a seleção do certificado.");
    }

    public async Task<List<AdnDocument>> ReadAsync(long nsu, string cnpj, CancellationToken ct)
    {
        var response = await ExchangeAsync(new AdnWorkerRequest(nsu, cnpj), TimeSpan.FromSeconds(150), ct);
        return response.Documents ?? throw new InvalidDataException("Resposta sem documentos do processo de conexão.");
    }

    private async Task<AdnWorkerResponse> ExchangeAsync(AdnWorkerRequest request, TimeSpan limit, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        using var timeout = new CancellationTokenSource(limit);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        try
        {
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request).AsMemory(), linked.Token);
            await process.StandardInput.FlushAsync(linked.Token);
            var line = await process.StandardOutput.ReadLineAsync(linked.Token);
            if (line == null) throw new IOException("O processo de conexão com o cartão encerrou sem responder.");
            var response = JsonSerializer.Deserialize<AdnWorkerResponse>(line) ?? throw new InvalidDataException("Resposta vazia do processo de conexão.");
            if (response.Error != null) throw new InvalidOperationException(response.Error,
                response.Detail == null ? null : new Exception(response.Detail));
            return response;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            Dispose();
            throw new TimeoutException($"O auxiliar não respondeu durante {request.Command} em {limit.TotalSeconds:0} segundos.");
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
