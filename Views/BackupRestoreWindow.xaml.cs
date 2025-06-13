// NfseGerenciador/Views/BackupRestoreWindow.xaml.cs
using NfseGerenciador.ViewModels;
using System.Windows;

namespace NfseGerenciador.Views
{
    public partial class BackupRestoreWindow : Window
    {
        public BackupRestoreWindow(BackupRestoreViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}