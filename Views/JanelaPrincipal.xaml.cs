// NfseGerenciador/Views/JanelaPrincipal.xaml.cs
using Microsoft.Extensions.DependencyInjection;
using NfseGerenciador.Views;
using System.Windows;

namespace NfseGerenciador
{
    public partial class JanelaPrincipal : Window
    {
        public JanelaPrincipal()
        {
            InitializeComponent();
        }

        private void BtnGerenciarNotas_Click(object sender, RoutedEventArgs e)
        {
            // Usamos o ServiceProvider estático da App para criar a janela
            var notasWindow = App.ServiceProvider.GetService<GerenciadorNfseWindow>();
            notasWindow.Owner = this;
            notasWindow.ShowDialog();
        }

        private void BtnGerenciarEmpresas_Click(object sender, RoutedEventArgs e)
        {
            var empresasWindow = App.ServiceProvider.GetService<EmpresasClassificadasWindow>();
            empresasWindow.Owner = this;
            empresasWindow.ShowDialog();
        }

        private void BtnRelatorios_Click(object sender, RoutedEventArgs e)
        {
            var relatoriosWin = App.ServiceProvider.GetService<RelatoriosWindow>();
            relatoriosWin.Owner = this;
            relatoriosWin.ShowDialog();
        }

        private void BtnDashboard_Click(object sender, RoutedEventArgs e)
        {
            var dashboardWin = App.ServiceProvider.GetService<DashboardWindow>();
            dashboardWin.Owner = this;
            dashboardWin.ShowDialog();
        }
        private void BtnBackup_Click(object sender, RoutedEventArgs e)
        {
            var backupWindow = App.ServiceProvider.GetService<BackupRestoreWindow>();
            backupWindow.Owner = this;
            backupWindow.ShowDialog();
        }
        private void BtnConfiguracoes_Click(object sender, RoutedEventArgs e)
        {
            var configWindow = App.ServiceProvider.GetService<ConfiguracoesWindow>();
            configWindow.Owner = this;
            configWindow.ShowDialog();
        }
        private void BtnSair_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}