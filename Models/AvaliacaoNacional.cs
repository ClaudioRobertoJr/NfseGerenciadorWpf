using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace NfseGerenciador.Models;

public enum StatusPadraoNacional { NaoAvaliada, Conforme, Divergente, NaoSeAplica }
public enum ResultadoCampoNacional { Conforme, Divergente, Indisponivel }
public sealed record CampoAvaliacaoNacional(string Campo, string? Encontrado, string Esperado, ResultadoCampoNacional Resultado, string Motivo)
{
    public string ValorExibido => Encontrado ?? "Indisponível";
    public string ResultadoExibido => Resultado == ResultadoCampoNacional.Indisponivel ? "Não avaliado" : Resultado.ToString();
}

public sealed class AvaliacaoNacional
{
    public const string CnpjAlvo = "72547623000190";
    public int NfseId { get; set; }
    public Nfse Nota { get; set; } = null!;
    public StatusPadraoNacional Status { get; set; }
    public int VersaoRegra { get; set; }
    public string? VersaoLeiaute { get; set; }
    public DateTime AvaliadaEmUtc { get; set; }
    public string Resumo { get; set; } = "";
    public string CamposJson { get; set; } = "[]";
    public DocumentoNacional? Documento { get; set; }
    [NotMapped] public IReadOnlyList<CampoAvaliacaoNacional> Campos
    {
        get
        {
            try { return JsonSerializer.Deserialize<List<CampoAvaliacaoNacional>>(CamposJson) ?? []; }
            catch (JsonException) { return [new("Avaliação armazenada", null, "Detalhes válidos", ResultadoCampoNacional.Indisponivel, "Detalhes ilegíveis; reavalie o documento.")]; }
        }
        set => CamposJson = JsonSerializer.Serialize(value);
    }
    public static bool PrestadorAlvo(string? cnpj) => cnpj?.Replace(".", "").Replace("/", "").Replace("-", "").Replace(" ", "") == CnpjAlvo;
    public static string NomeStatus(StatusPadraoNacional status) => status switch
    {
        StatusPadraoNacional.Conforme => "Conforme",
        StatusPadraoNacional.Divergente => "Divergente",
        StatusPadraoNacional.NaoSeAplica => "Não se aplica",
        _ => "Não avaliada"
    };
}

// Separate table: listing evaluations never loads the original document.
public sealed class DocumentoNacional
{
    public int NfseId { get; set; }
    public AvaliacaoNacional Avaliacao { get; set; } = null!;
    public string ArquivoXmlGzipBase64 { get; set; } = "";
    public string Sha256 { get; set; } = "";
}
