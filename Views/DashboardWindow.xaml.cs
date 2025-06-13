// NfseGerenciador/Views/DashboardWindow.xaml.cs
using NfseGerenciador.ViewModels;
using System.Windows;

namespace NfseGerenciador.Views
{
    public partial class DashboardWindow : Window
    {
        public DashboardWindow(DashboardViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}