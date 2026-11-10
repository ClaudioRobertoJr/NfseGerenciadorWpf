// NfseGerenciador/Views/GerenciadorNfseWindow.xaml.cs
using NfseGerenciador.ViewModels;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using NfseGerenciador.Services;

namespace NfseGerenciador.Views
{
    /// <summary>
    /// Interaction logic for GerenciadorNfseWindow.xaml.
    /// A View (janela) para gerenciar as Notas Fiscais.
    /// </summary>
    public partial class GerenciadorNfseWindow : Window
    {
        private bool _nationalWindowOpen;
        private async void ImportarNacional_Click(object sender, RoutedEventArgs e)
        {
            if (_nationalWindowOpen) return;
            _nationalWindowOpen = true;
            try
            {
                ImportDiagnostics.Write($"Abrindo importação; versão={typeof(App).Assembly.GetName().Version}; runtime={Environment.Version}; x64={Environment.Is64BitProcess}");
                var factory = App.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
                var window = new NacionalImportWindow(factory) { Owner = this };
                await ImportWindowGuard.RunAsync(() => { window.ShowDialog(); }, window.StopAfterWindowFailureAsync,
                    ex =>
                    {
                        ImportDiagnostics.Write("Falha Win32 no loop da janela de importação", ex);
                        MessageBox.Show(this,
                            "O Windows apresentou uma falha na janela durante a importação. A operação está sendo interrompida; o gerenciador permanecerá aberto.\n\n"
                            + "Os lotes já confirmados foram preservados. Não é necessário reinstalar o driver sem analisar o diagnóstico.\n\n"
                            + "Envie o arquivo: " + ImportDiagnostics.FilePath,
                            "Falha na janela de importação", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                ((GerenciadorNfseViewModel)DataContext).AtualizarListaCommand.Execute(null);
            }
            catch (Exception ex)
            {
                ImportDiagnostics.Write("Falha ao abrir ou encerrar importação", ex);
                MessageBox.Show(this, "Não foi possível concluir a abertura/encerramento da importação.\n\nDiagnóstico: " + ImportDiagnostics.FilePath,
                    "Erro na importação", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { _nationalWindowOpen = false; }
        }
        // O ViewModel é injetado automaticamente pelo container de DI configurado no App.xaml.cs
        public GerenciadorNfseWindow(GerenciadorNfseViewModel viewModel)
        {
            InitializeComponent();

            // Define o ViewModel como o contexto de dados da janela.
            // A partir daqui, todos os Bindings no XAML funcionarão com as propriedades e comandos do viewModel.
            DataContext = viewModel;
        }
    }
}
