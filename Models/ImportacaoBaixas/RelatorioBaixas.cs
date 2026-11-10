namespace NfseGerenciador.Models.ImportacaoBaixas;

public sealed record LancamentoRelatorio
{
    public int Id { get; init; }
    public string Planilha { get; init; } = "";
    public int Linha { get; init; }
    public string Centro { get; init; } = "";
    public string Conta { get; init; } = "";
    public string Empresa { get; init; } = "";
    public DateTime? Data { get; init; }
    public string Lote { get; init; } = "";
    public string Contrapartida { get; init; } = "";
    public string Historico { get; init; } = "";
    public decimal? Debito { get; init; }
    public decimal? Credito { get; init; }
    public string? Erro { get; init; }
}
public sealed record RelatorioBaixas(string HashArquivo, IReadOnlyList<LancamentoRelatorio> Lancamentos, IReadOnlyList<string> Diagnosticos);

