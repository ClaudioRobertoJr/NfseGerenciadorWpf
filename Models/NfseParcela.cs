using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NfseGerenciador.Models
{
    public class NfseParcela
    {
        [Key]
        public int Id { get; set; }

        public int NfseId { get; set; } // Chave estrangeira para a NFS-e
        [ForeignKey("NfseId")]
        public Nfse Nfse { get; set; }

        public int NumeroParcela { get; set; } // Ex: 1, 2, 3

        [Column(TypeName = "decimal(18,2)")]
        public decimal ValorParcela { get; set; }

        public DateTime DataVencimentoParcela { get; set; }

        public bool ParcelaPaga { get; set; } = false; // Para controle individual de pagamento da parcela (opcional avançado)
        public DateTime? DataPagamentoParcela { get; set; } // Data que esta parcela específica foi paga (opcional avançado)
    }
}