// NfseGerenciador/ViewModels/BackupRestoreViewModel.cs
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using NfseGerenciador.Services;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace NfseGerenciador.ViewModels
{
    public class BackupRestoreViewModel : INotifyPropertyChanged
    {
        private readonly IConfiguration _configuration;
        private readonly LogService _logService;
        private string _connectionString;
        private string _databaseName;
        private bool _isBusy;
        private string _statusMessage;

        public ICommand BackupCommand { get; }
        public ICommand RestoreCommand { get; }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(nameof(IsBusy)); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(nameof(StatusMessage)); }
        }

        public BackupRestoreViewModel(IConfiguration configuration, LogService logService)
        {
            _configuration = configuration;
            _logService = logService;

            try
            {
                _connectionString = _configuration.GetConnectionString("DefaultConnection");
                var builder = new SqlConnectionStringBuilder(_connectionString);
                _databaseName = builder.InitialCatalog; // Extrai o nome do banco de dados da connection string
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "Erro ao ler a string de conexão para backup/restauração.");
                MessageBox.Show("Não foi possível ler as configurações do banco de dados. Verifique o appsettings.json e o log de erros.", "Erro de Configuração", MessageBoxButton.OK, MessageBoxImage.Error);
            }


            BackupCommand = new RelayCommand(BackupDatabaseAsync);
            RestoreCommand = new RelayCommand(RestoreDatabaseAsync);
        }

        private async Task BackupDatabaseAsync()
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Arquivo de Backup do SQL Server (*.bak)|*.bak",
                FileName = $"Backup_{_databaseName}_{DateTime.Now:yyyyMMdd_HHmmss}.bak",
                Title = "Salvar Backup do Banco de Dados"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                IsBusy = true;
                StatusMessage = "Iniciando backup... Isso pode levar alguns minutos.";
                try
                {
                    string filePath = saveFileDialog.FileName;
                    string sqlCommand = $"BACKUP DATABASE [{_databaseName}] TO DISK = '{filePath}' WITH NOFORMAT, NOINIT, NAME = N'{_databaseName}-Full Database Backup', SKIP, NOREWIND, NOUNLOAD, STATS = 10";

                    await ExecuteSqlCommand(sqlCommand, "Backup");

                    StatusMessage = $"Backup concluído com sucesso em: {filePath}";
                    MessageBox.Show(StatusMessage, "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    StatusMessage = "Falha no backup.";
                    _logService.LogError(ex, $"Falha ao executar o backup do banco de dados '{_databaseName}'.");
                    MessageBox.Show("Ocorreu um erro durante o backup. Verifique o log de erros para mais detalhes.", "Erro de Backup", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        private async Task RestoreDatabaseAsync()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Arquivo de Backup (*.bak)|*.bak",
                Title = "Selecionar Arquivo de Backup para Restaurar"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var result = MessageBox.Show(
                    "AVISO: A restauração substituirá TODOS os dados atuais do banco de dados pelos dados do arquivo de backup. Esta ação não pode ser desfeita. Deseja continuar?",
                    "Confirmar Restauração",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    IsBusy = true;
                    StatusMessage = "Iniciando restauração... Feche todas as outras telas do sistema.";
                    try
                    {
                        string filePath = openFileDialog.FileName;
                        // Força o fechamento de outras conexões e restaura o banco de dados
                        string sqlCommand = $@"
                            ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                            RESTORE DATABASE [{_databaseName}] FROM DISK = '{filePath}' WITH REPLACE;
                            ALTER DATABASE [{_databaseName}] SET MULTI_USER;";

                        await ExecuteSqlCommand(sqlCommand, "Restore");

                        StatusMessage = "Restauração concluída com sucesso. Reinicie a aplicação.";
                        MessageBox.Show(StatusMessage, "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        StatusMessage = "Falha na restauração.";
                        _logService.LogError(ex, $"Falha ao restaurar o banco de dados '{_databaseName}'.");
                        MessageBox.Show("Ocorreu um erro durante a restauração. Verifique se não há outras janelas do sistema abertas e consulte o log de erros.", "Erro de Restauração", MessageBoxButton.OK, MessageBoxImage.Error);

                        // Tenta colocar o banco de dados de volta ao modo multiusuário em caso de falha
                        try
                        {
                            await ExecuteSqlCommand($"ALTER DATABASE [{_databaseName}] SET MULTI_USER;", "ResetUserMode");
                        }
                        catch (Exception resetEx)
                        {
                            _logService.LogError(resetEx, $"Falha ao redefinir o banco de dados para MULTI_USER após erro de restauração.");
                        }
                    }
                    finally
                    {
                        IsBusy = false;
                    }
                }
            }
        }

        private async Task ExecuteSqlCommand(string commandText, string operation)
        {
            // Usa uma connection string que aponta para o banco 'master' para poder manipular o banco de dados do aplicativo
            var masterDbBuilder = new SqlConnectionStringBuilder(_connectionString) { InitialCatalog = "master" };

            using (var connection = new SqlConnection(masterDbBuilder.ConnectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand(commandText, connection))
                {
                    command.CommandTimeout = 120; // Aumenta o timeout para operações longas
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}