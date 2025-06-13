using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

// Adicione o namespace aqui
namespace NfseGerenciador.Models
{
    public class Pagamento
    {
        [Key]
        public int Id { get; set; }
        public string TipoRecebimento { get; set; } // Cartao, Pix, Dinheiro, Cheque, PlanoSaude, Convenio
        public bool EhAVista { get; set; } // True para à vista, false para parcelado
        public int? NumeroParcelas { get; set; } // Número de parcelas (nulo se à vista)
    }
}