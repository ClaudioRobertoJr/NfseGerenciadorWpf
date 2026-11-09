using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace NfseGerenciador.Services;

public sealed class AdnClient(HttpClient http)
{
    public async Task<List<AdnDocument>> ReadAsync(long nsu, string cnpj, CancellationToken ct)
    {
        using var response = await http.GetAsync($"https://adn.nfse.gov.br/contribuintes/DFe/{nsu}?cnpjConsulta={Uri.EscapeDataString(cnpj)}&lote=true", ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        AdnResponse result;
        try { result = JsonSerializer.Deserialize<AdnResponse>(body) ?? throw new JsonException(); }
        catch (JsonException) { throw new InvalidDataException($"Resposta inesperada da API (HTTP {(int)response.StatusCode}). Verifique certificado, conexão e disponibilidade do serviço."); }
        result.Erros ??= [];
        result.LoteDFe ??= [];
        if (result.TipoAmbiente != "PRODUCAO") throw new InvalidDataException("A API não confirmou ambiente de produção.");
        // Only the documented end-of-feed response terminates synchronization.
        if (response.StatusCode == HttpStatusCode.NotFound && result.StatusProcessamento == "NENHUM_DOCUMENTO_LOCALIZADO"
            && result.Erros.Count > 0 && result.Erros.All(x => x.Codigo == "E2220")) return [];
        if (!response.IsSuccessStatusCode || result.Erros.Count != 0)
            throw new InvalidOperationException($"API HTTP {(int)response.StatusCode}: " + string.Join("; ", result.Erros.Select(x => x.Codigo + " — " + x.Descricao)));
        if (result.StatusProcessamento != "DOCUMENTOS_LOCALIZADOS" || result.LoteDFe.Count == 0)
            throw new InvalidDataException("Resposta sem lote válido; o progresso não foi avançado.");
        var docs = result.LoteDFe.OrderBy(x => x.NSU).ToList();
        if (docs.Any(x => x.NSU <= nsu) || docs.Select(x => x.NSU).Distinct().Count() != docs.Count)
            throw new InvalidDataException("Sequência de NSU inválida; importação interrompida.");
        return docs;
    }
}

public class AdnResponse
{
    public string TipoAmbiente { get; set; } = "";
    public string StatusProcessamento { get; set; } = "";
    public List<AdnDocument> LoteDFe { get; set; } = [];
    public List<AdnError> Erros { get; set; } = [];
}
public class AdnError
{
    public string Codigo { get; set; } = "";
    public string Descricao { get; set; } = "";
}
public class AdnDocument
{
    public long NSU { get; set; }
    public string ChaveAcesso { get; set; } = "";
    public string TipoDocumento { get; set; } = "";
    public string? TipoEvento { get; set; }
    public string ArquivoXml { get; set; } = "";
}
