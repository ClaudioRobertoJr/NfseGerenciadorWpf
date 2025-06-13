// NfseGerenciador/Views/GerenciadorNfseWindow.xaml.cs
using NfseGerenciador.ViewModels;
using System.Windows;

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
    }
}