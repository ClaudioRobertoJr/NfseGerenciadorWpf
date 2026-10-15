// NfseGerenciador/ViewModels/ConfiguracoesViewModel.cs
using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using NfseGerenciador.Services;
using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace NfseGerenciador.ViewModels
{
    public class ConfiguracoesViewModel : INotifyPropertyChanged
    {
        private readonly LogService _logService;
        private string _connectionString;
        private string _backupPath;
        private bool _isBusy;
        private string _statusMessage;

        public ICommand SalvarCommand { get; }
        public ICommand TestarConexaoCommand { get; }
        public ICommand ProcurarPastaBackupCommand { get; }

        public string ConnectionString
        {
            get => _connectionString;
            set { _connectionString = value; OnPropertyChanged(nameof(ConnectionString)); }
        }

        public string BackupPath
        {
            get => _backupPath;
            set { _backupPath = value; OnPropertyChanged(nameof(BackupPath)); }
        }

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

        public ConfiguracoesViewModel(LogService logService)
        {
            _logService = logService;

            SalvarCommand = new RelayCommand(SalvarConfiguracoesAsync);
            TestarConexaoCommand = new RelayCommand(TestarConexaoAsync);
            ProcurarPastaBackupCommand = new RelayCommand(ProcurarPastaBackup);

            CarregarConfiguracoes();
        }

        private void CarregarConfiguracoes()
        {
            try
            {
                string configFile = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                if (File.Exists(configFile))
                {
                    string json = File.ReadAllText(configFile);
                    var jsonNode = JsonNode.Parse(json);
                    ConnectionString = jsonNode["ConnectionStrings"]?["DefaultConnection"]?.ToString() ?? "";
                    BackupPath = jsonNode["AppSettings"]?["DefaultBackupPath"]?.ToString() ?? "";
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "Erro ao carregar configurações do appsettings.json.");
                StatusMessage = "Erro ao carregar configurações.";
            }
        }

        private async Task SalvarConfiguracoesAsync()
        {
            IsBusy = true;
            StatusMessage = "Salvando configurações...";
            try
            {
                string configFile = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                var jsonNode = JsonNode.Parse(File.ReadAllText(configFile));

                // Garante que os nós existam
                if (jsonNode["ConnectionStrings"] == null)
                    jsonNode["ConnectionStrings"] = new JsonObject();
                if (jsonNode["AppSettings"] == null)
                    jsonNode["AppSettings"] = new JsonObject();

                jsonNode["ConnectionStrings"]["DefaultConnection"] = ConnectionString;
                jsonNode["AppSettings"]["DefaultBackupPath"] = BackupPath;

                var options = new JsonSerializerOptions { WriteIndented = true };
                await File.WriteAllTextAsync(configFile, jsonNode.ToJsonString(options));

                StatusMessage = "Configurações salvas. Reinicie o programa para aplicar a conexão.";
                MessageBox.Show(StatusMessage, "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = "Falha ao salvar configurações.";
                _logService.LogError(ex, "Erro ao salvar appsettings.json.");
                MessageBox.Show("Não foi possível salvar as configurações. Verifique o log de erros.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task TestarConexaoAsync()
        {
            if (string.IsNullOrWhiteSpace(ConnectionString))
            {
                MessageBox.Show("A string de conexão não pode estar vazia.", "Teste de Conexão", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            StatusMessage = "Testando conexão...";
            try
            {
                using (var connection = new SqlConnection(ConnectionString))
                {
                    await connection.OpenAsync();
                }
                StatusMessage = "Conexão bem-sucedida!";
                MessageBox.Show(StatusMessage, "Teste de Conexão", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = "Falha na conexão.";
                _logService.LogError(ex, "Teste de conexão com o banco de dados falhou.");
                MessageBox.Show($"Falha na conexão: {ex.Message}", "Teste de Conexão", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ProcurarPastaBackup()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Selecione a pasta padrão para backups"
            };

            if (dialog.ShowDialog() == true)
            {
                BackupPath = dialog.FolderName;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
