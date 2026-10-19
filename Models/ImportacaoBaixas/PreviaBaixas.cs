namespace NfseGerenciador.Models.ImportacaoBaixas;
public enum StatusConciliacao { Apta, Revisao, Ignorada }
public sealed record ConferenciaManual(int NotaId, string Justificativa);
public sealed record ItemPrevia
{
    public required LancamentoRelatorio Origem { get; init; }
    public int? NfseId { get; init; }
    public string Numero { get; init; } = "";
    public string Tomador { get; init; } = "";
    public decimal? ValorNota { get; init; }
    public StatusConciliacao Status { get; init; }
    public string Motivo { get; init; } = "";
    public string TipoPagamento { get; init; } = "";
    public int? PagamentoId { get; init; }
    public LocalPagamento Local { get; init; }
    public string EstadoOriginal { get; init; } = "";
    public string Impressao { get; init; } = "";
    public ConferenciaManual? Conferencia { get; init; }
}
public sealed record PreviaBaixas(RelatorioBaixas Relatorio, string PrestadorCnpj, IReadOnlyList<ItemPrevia> Itens)
{
    public string NomeArquivo { get; init; } = "";
}
public sealed record ResultadoImportacao(int LoteId, int Quantidade, decimal Valor);

