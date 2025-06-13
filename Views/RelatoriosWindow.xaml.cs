// NfseGerenciador/Views/RelatoriosWindow.xaml.cs
using NfseGerenciador.ViewModels;
using System.Windows;

namespace NfseGerenciador.Views
{
    public partial class RelatoriosWindow : Window
    {
        // **CORREÇÃO AQUI**: O parâmetro agora é do tipo RelatoriosViewModel
        public RelatoriosWindow(RelatoriosViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}