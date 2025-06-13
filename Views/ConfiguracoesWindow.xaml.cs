// NfseGerenciador/Views/ConfiguracoesWindow.xaml.cs
using NfseGerenciador.ViewModels;
using System.Windows;

namespace NfseGerenciador.Views
{
    public partial class ConfiguracoesWindow : Window
    {
        public ConfiguracoesWindow(ConfiguracoesViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}