// NfseGerenciador/ViewModels/BackupRestoreViewModel.cs
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using NfseGerenciador.Services;
using System;
using System.ComponentModel;
using System.Data;
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
                    string sqlCommand = $"BACKUP DATABASE {QuotedDatabaseName} TO DISK = @path WITH NOFORMAT, NOINIT, NOREWIND, NOUNLOAD, STATS = 10";

                    await ExecuteSqlCommand(sqlCommand, "Backup", filePath);

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
                        await RestoreSelectedBackupAsync(filePath);

                        StatusMessage = "Restauração concluída com sucesso. Reinicie a aplicação.";
                        MessageBox.Show(StatusMessage, "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        StatusMessage = "Falha na restauração.";
                        _logService.LogError(ex, $"Falha ao restaurar o banco de dados '{_databaseName}'.");
                        MessageBox.Show("Ocorreu um erro durante a restauração. Verifique se não há outras janelas do sistema abertas e consulte o log de erros.", "Erro de Restauração", MessageBoxButton.OK, MessageBoxImage.Error);

                    }
                    finally
                    {
                        IsBusy = false;
                    }
                }
            }
        }

        private string QuotedDatabaseName => "[" + _databaseName.Replace("]", "]]") + "]";

        internal async Task RestoreSelectedBackupAsync(string filePath)
        {
            var builder = new SqlConnectionStringBuilder(_connectionString) { InitialCatalog = "master", Pooling = false };
            using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            using var headerCommand = new SqlCommand("RESTORE HEADERONLY FROM DISK = @path;", connection) { CommandTimeout = 120 };
            headerCommand.Parameters.Add("@path", SqlDbType.NVarChar, 4000).Value = filePath;
            var headers = new DataTable();
            using (var reader = await headerCommand.ExecuteReaderAsync()) headers.Load(reader);
            int position = BackupSetSelector.SelectLatestFull(headers, _databaseName);

            // Validate the selected set before disconnecting users or replacing any data.
            using var verify = new SqlCommand($"RESTORE VERIFYONLY FROM DISK = @path WITH FILE = {position};", connection) { CommandTimeout = 120 };
            verify.Parameters.Add("@path", SqlDbType.NVarChar, 4000).Value = filePath;
            await verify.ExecuteNonQueryAsync();

            using var restore = new SqlCommand($@"
                ALTER DATABASE {QuotedDatabaseName} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                RESTORE DATABASE {QuotedDatabaseName} FROM DISK = @path WITH FILE = {position}, REPLACE;
                ALTER DATABASE {QuotedDatabaseName} SET MULTI_USER;", connection) { CommandTimeout = 120 };
            restore.Parameters.Add("@path", SqlDbType.NVarChar, 4000).Value = filePath;
            try
            {
                await restore.ExecuteNonQueryAsync();
            }
            catch
            {
                try
                {
                    await ExecuteSqlCommand($"ALTER DATABASE {QuotedDatabaseName} SET MULTI_USER WITH ROLLBACK IMMEDIATE;", "ResetUserMode");
                }
                catch (Exception resetEx)
                {
                    _logService.LogError(resetEx, "Falha ao redefinir o banco de dados para MULTI_USER após erro de restauração.");
                }
                throw;
            }
        }

        private async Task ExecuteSqlCommand(string commandText, string operation, string filePath = null)
        {
            // Usa uma connection string que aponta para o banco 'master' para poder manipular o banco de dados do aplicativo
            var masterDbBuilder = new SqlConnectionStringBuilder(_connectionString) { InitialCatalog = "master" };

            using (var connection = new SqlConnection(masterDbBuilder.ConnectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand(commandText, connection))
                {
                    if (filePath != null) command.Parameters.Add("@path", SqlDbType.NVarChar, 4000).Value = filePath;
                    command.CommandTimeout = 120; // Aumenta o timeout para operações longas
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
