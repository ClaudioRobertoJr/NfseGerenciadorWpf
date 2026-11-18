using System.ComponentModel;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using NfseGerenciador.Services;

namespace NfseGerenciador.Views;

public partial class ReavaliacaoNacionalWindow : Window
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;
    private CancellationTokenSource? _cts;
    private bool _fecharAoTerminar;
    public ReavaliacaoNacionalWindow(IDbContextFactory<ApplicationDbContext> factory)
    { InitializeComponent(); _factory = factory; }

    private async void Iniciar_Click(object sender, RoutedEventArgs e)
    {
        if (_cts != null) return;
        _cts = new();
        IniciarButton.IsEnabled = false; PararButton.IsEnabled = true; Progresso.Visibility = Visibility.Visible;
        ResultadoText.Text = "Preparando reavaliação...";
        var progress = new Progress<string>(s => ResultadoText.Text = s);
        var token = _cts.Token;
        try
        {
            var r = await Task.Run(() => new ReavaliacaoNacionalService(_factory).ReavaliarAsync(progress, token));
            ResultadoText.Text = $"{(r.Cancelada ? "Interrompida" : "Concluída")}: {r.Reavaliadas} avaliação(ões) gravada(s); {r.SemDocumento} nota(s) sem documento; {r.RegraMaisRecente} com regra mais recente preservada(s).";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        { ResultadoText.Text = "Reavaliação interrompida. Os lotes já concluídos foram preservados."; }
        catch (Exception ex)
        {
            new LogService().LogError(ex, "Reavaliar padrão nacional");
            ResultadoText.Text = "Não foi possível concluir a reavaliação. Lotes já concluídos foram preservados; consulte o log. Verifique se há outra reavaliação em andamento.";
        }
        finally
        {
            _cts.Dispose(); _cts = null; IniciarButton.IsEnabled = true; PararButton.IsEnabled = false; Progresso.Visibility = Visibility.Collapsed;
            if (_fecharAoTerminar) Close();
        }
    }
    private void Parar_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_cts == null) return;
        e.Cancel = true; _fecharAoTerminar = true; _cts.Cancel();
    }
}
