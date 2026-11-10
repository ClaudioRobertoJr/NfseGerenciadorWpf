using Microsoft.EntityFrameworkCore;
using NfseGerenciador.Models;
using NfseGerenciador.Services;
using System.ComponentModel;
using System.Windows;

namespace NfseGerenciador.Views;

public partial class NacionalImportWindow : Window
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;
    private CancellationTokenSource? _cancel;
    private Task<string>? _activeImport;
    private bool _windowFailed;
    public NacionalImportWindow(IDbContextFactory<ApplicationDbContext> factory)
    {
        InitializeComponent();
        _factory = factory;
        using var db = factory.CreateDbContext();
        CnpjInput.Text = db.Set<NacionalSync>().Select(x => x.Cnpj).FirstOrDefault() ?? "";
    }
    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        var cnpj = NacionalXml.Digits(CnpjInput.Text);
        if (cnpj.Length != 14) { StatusText.Text = "Informe o CNPJ completo com 14 dígitos."; return; }
        try
        {
            _cancel = new CancellationTokenSource();
            StartButton.IsEnabled = false; CnpjInput.IsEnabled = false; CancelButton.IsEnabled = true;
            var progress = new Progress<string>(text => { if (!_windowFailed) StatusText.Text = text; });
            var token = _cancel.Token;
            _activeImport = Task.Run(() => new NacionalImportService(_factory).ImportAsync(cnpj, progress, token));
            var result = await _activeImport;
            if (!_windowFailed) StatusText.Text = result;
            ImportDiagnostics.Write("Importação concluída");
        }
        catch (OperationCanceledException ex)
        {
            ImportDiagnostics.Write("Importação cancelada", ex);
            if (!_windowFailed) StatusText.Text = "Importação cancelada. Os lotes confirmados foram preservados; a próxima tentativa continuará dali.";
        }
        catch (Exception ex)
        {
            ImportDiagnostics.Write("Falha na seleção ou execução da importação", ex);
            if (!_windowFailed) StatusText.Text = "Importação interrompida: " + ex.Message + " Os lotes já confirmados foram preservados. Diagnóstico: " + ImportDiagnostics.FilePath;
        }
        finally
        {
            _cancel?.Dispose(); _cancel = null;
            _activeImport = null;
            if (!_windowFailed) { StartButton.IsEnabled = true; CnpjInput.IsEnabled = true; CancelButton.IsEnabled = false; }
        }
    }
    public async Task StopAfterWindowFailureAsync()
    {
        _windowFailed = true;
        ImportDiagnostics.Write("Solicitando cancelamento após falha da janela");
        _cancel?.Cancel();
        var active = _activeImport;
        try { Close(); }
        catch (Exception ex) { ImportDiagnostics.Write("Falha ao fechar janela de importação", ex); }
        if (active != null)
        {
            try { await active; }
            catch (OperationCanceledException) { }
            catch (Exception ex) { ImportDiagnostics.Write("Operação interrompida após falha da janela", ex); }
        }
        ImportDiagnostics.Write("Operação encerrada após falha da janela");
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) { _cancel?.Cancel(); CancelButton.IsEnabled = false; }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_cancel != null && !_windowFailed) { _cancel.Cancel(); e.Cancel = true; StatusText.Text = "Interrompendo; aguarde para fechar."; }
    }
}
