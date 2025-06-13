using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

// Adicione o namespace aqui
namespace NfseGerenciador.Models
{
    public class Nfse
    {
        [Key]
        public int Id { get; set; }
        public string Numero { get; set; }
        public string CodigoVerificacao { get; set; }
        public DateTime DataEmissao { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ValorLiquidoNfse { get; set; }

        public string PrestadorCnpj { get; set; }
        public string PrestadorRazaoSocial { get; set; }

        public string TomadorCpfCnpj { get; set; }
        public string TomadorRazaoSocial { get; set; }

        public bool EstaPago { get; set; } = false;
        public LocalPagamento Local { get; set; }
        public int? PagamentoId { get; set; }
        public Pagamento? Pagamento { get; set; }

        public string? ClassificacaoEspecial { get; set; }
        public DateTime? DataPrevistaRecebimento { get; set; }
        public DateTime? DataPagamentoEfetivo { get; set; }
        public List<NfseParcela> Parcelas { get; set; } = new List<NfseParcela>();
        public Nfse() // Construtor para inicializar a coleção
        {
            Parcelas = new List<NfseParcela>();
        }
        [DefaultValue(false)]
        public bool Excluida { get; set; }

    }
}