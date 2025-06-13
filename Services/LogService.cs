using System;
using System.IO;

namespace NfseGerenciador.Services
{
    public class LogService
    {
        private readonly string _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log_erros.txt");

        public void LogError(Exception ex, string context = "Erro geral")
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(_logFilePath, true))
                {
                    writer.WriteLine("--------------------------------------------------");
                    writer.WriteLine($"Data/Hora: {DateTime.Now}");
                    writer.WriteLine($"Contexto: {context}");
                    writer.WriteLine($"Erro: {ex.Message}");
                    writer.WriteLine($"StackTrace: {ex.StackTrace}");
                    writer.WriteLine("--------------------------------------------------\n");
                }
            }
            catch (Exception)
            {
                // Ignora erros ao tentar logar para não criar um loop infinito
            }
        }
    }
}