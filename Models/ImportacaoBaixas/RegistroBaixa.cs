using System.ComponentModel.DataAnnotations;
namespace NfseGerenciador.Models.ImportacaoBaixas;
public sealed class RegistroBaixa
{
    public int Id { get; set; }
    public int LoteBaixaId { get; set; }
    public LoteBaixa Lote { get; set; } = null!;
    public int NfseId { get; set; }
    public Nfse Nota { get; set; } = null!;
    [MaxLength(64)] public string Impressao { get; set; } = "";
    public string Planilha { get; set; } = "";
    public int Linha { get; set; }
    public string Historico { get; set; } = "";
    public string Centro { get; set; } = "";
    public string Contrapartida { get; set; } = "";
    public DateTime DataRelatorio { get; set; }
    public decimal Valor { get; set; }
    public string EstadoAnterior { get; set; } = "";
    public string EstadoPosterior { get; set; } = "";
}

