using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NfseGerenciador.Models
{
    public class EmpresaClassificada
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string Cnpj { get; set; } // Indexar este campo para buscas rápidas
        [Required]
        public string RazaoSocial { get; set; }
        [Required]
        public string TipoClassificacao { get; set; } // "Plano de Saúde", "Convênio"
    }
}
