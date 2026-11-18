using System.Windows;
using NfseGerenciador.Models;
using NfseGerenciador.Services;

namespace NfseGerenciador.Views;

public partial class AvaliacaoNacionalWindow : Window
{
    public AvaliacaoNacionalWindow(Nfse nota)
    {
        InitializeComponent();
        TituloText.Text = $"Nota {nota.Numero} • {nota.PadraoNacionalTexto}";
        var avaliacao = nota.StatusPadrao == StatusPadraoNacional.NaoSeAplica ? null : nota.AvaliacaoNacional;
        ResumoText.Text = nota.StatusPadrao == StatusPadraoNacional.NaoSeAplica
            ? "O padrão só se aplica às notas nacionais do prestador configurado."
            : avaliacao?.Resumo ?? "Não avaliada — documento não disponível. A importação anterior não armazenava os campos necessários.";
        CamposGrid.ItemsSource = avaliacao?.Campos ?? (nota.StatusPadrao == StatusPadraoNacional.NaoSeAplica ? []
            : AvaliadorPadraoNacional.Indisponivel("Documento não disponível.").Campos);
        VersaoText.Text = avaliacao == null ? "" : $"Regra v{avaliacao.VersaoRegra} • Leiaute {avaliacao.VersaoLeiaute ?? "desconhecido"} • Avaliada em {DateTime.SpecifyKind(avaliacao.AvaliadaEmUtc, DateTimeKind.Utc).ToLocalTime():dd/MM/yyyy HH:mm:ss}";
    }
}
