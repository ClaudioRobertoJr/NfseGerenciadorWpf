using Microsoft.EntityFrameworkCore;
using NfseGerenciador.Models;
using NfseGerenciador.Services;
using System.ComponentModel;
using System.Security.Cryptography.X509Certificates;
using System.Windows;

namespace NfseGerenciador.Views;

public partial class NacionalImportWindow : Window
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;
    private CancellationTokenSource? _cancel;
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
            using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);
            var valid = new X509Certificate2Collection(store.Certificates.Cast<X509Certificate2>()
                .Where(x => x.HasPrivateKey && x.NotBefore <= DateTime.Now && x.NotAfter > DateTime.Now).ToArray());
            if (valid.Count == 0) { StatusText.Text = "Nenhum certificado disponível. Conecte o cartão e confira o driver da leitora."; return; }
            var selection = X509Certificate2UI.SelectFromCollection(valid, "Certificado para NFS-e", "Escolha o e-CPF com procuração ou o certificado da instituição.", X509SelectionFlag.SingleSelection);
            if (selection.Count == 0) return;
            using var cert = selection[0];
            _cancel = new CancellationTokenSource();
            StartButton.IsEnabled = false; CnpjInput.IsEnabled = false; CancelButton.IsEnabled = true;
            var progress = new Progress<string>(text => StatusText.Text = text);
            var token = _cancel.Token;
            StatusText.Text = await Task.Run(() => new NacionalImportService(_factory).ImportAsync(cnpj, cert, progress, token));
        }
        catch (OperationCanceledException)
        { StatusText.Text = "Importação interrompida ou tempo de consulta esgotado. Os lotes confirmados foram preservados; a próxima tentativa continuará dali."; }
        catch (Exception ex)
        { StatusText.Text = "Importação interrompida: " + ex.Message + " Os lotes já confirmados foram preservados."; }
        finally
        {
            _cancel?.Dispose(); _cancel = null;
            StartButton.IsEnabled = true; CnpjInput.IsEnabled = true; CancelButton.IsEnabled = false;
        }
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) { _cancel?.Cancel(); CancelButton.IsEnabled = false; }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_cancel != null) { _cancel.Cancel(); e.Cancel = true; StatusText.Text = "Interrompendo; aguarde para fechar."; }
    }
}
