using System.ComponentModel.DataAnnotations;
namespace NfseGerenciador.Models.ImportacaoBaixas;
public sealed class LoteBaixa
{
    public int Id { get; set; }
    [MaxLength(64)] public string HashArquivo { get; set; } = "";
    [MaxLength(260)] public string NomeArquivo { get; set; } = "";
    [MaxLength(14)] public string PrestadorCnpj { get; set; } = "";
    public DateTime ImportadoEmUtc { get; set; }
    public int Quantidade { get; set; }
    public decimal Valor { get; set; }
}

