// NfseGerenciador/Views/GerenciadorNfseWindow.xaml.cs
using NfseGerenciador.ViewModels;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace NfseGerenciador.Views
{
    /// <summary>
    /// Interaction logic for GerenciadorNfseWindow.xaml.
    /// A View (janela) para gerenciar as Notas Fiscais.
    /// </summary>
    public partial class GerenciadorNfseWindow : Window
    {
        // O ViewModel é injetado automaticamente pelo container de DI configurado no App.xaml.cs
        public GerenciadorNfseWindow(GerenciadorNfseViewModel viewModel)
        {
            InitializeComponent();

            // Define o ViewModel como o contexto de dados da janela.
            // A partir daqui, todos os Bindings no XAML funcionarão com as propriedades e comandos do viewModel.
            DataContext = viewModel;
        }
        private async void ImportarBaixas(object sender, RoutedEventArgs e)
        {
            var window = App.ServiceProvider.GetRequiredService<ImportacaoBaixasWindow>();
            window.Owner = this;
            window.ShowDialog();
            if (window.HouveBaixas && DataContext is GerenciadorNfseViewModel vm)
                await vm.AtualizarAsync();
        }
    }
}
