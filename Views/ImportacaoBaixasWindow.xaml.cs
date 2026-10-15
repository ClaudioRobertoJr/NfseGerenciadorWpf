using System.ComponentModel;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using NfseGerenciador.Services;
using NfseGerenciador.ViewModels;
namespace NfseGerenciador.Views;
public partial class ImportacaoBaixasWindow : Window
{
    private readonly ImportacaoBaixasViewModel vm;
    private readonly IDbContextFactory<ApplicationDbContext> factory;
    private readonly LogService log;
    public bool HouveBaixas {get;private set;}
    public ImportacaoBaixasWindow(ImportacaoBaixasViewModel viewModel,IDbContextFactory<ApplicationDbContext> contextFactory,LogService logger)
    {InitializeComponent();vm=viewModel;factory=contextFactory;log=logger;DataContext=vm;}
    private sealed record Prestador(string Cnpj,string Nome){public string Rotulo=>Nome+" • "+Cnpj;}
    private async void AoCarregar(object sender,RoutedEventArgs e)
    {
        try {
            await using var db=await factory.CreateDbContextAsync();
            var values=await db.Nfses.AsNoTracking().Where(n=>!n.Excluida).Select(n=>new{n.PrestadorCnpj,n.PrestadorRazaoSocial}).Distinct().ToListAsync();
            var providers=values.GroupBy(n=>n.PrestadorCnpj).Select(g=>new Prestador(g.Key,g.First().PrestadorRazaoSocial)).OrderBy(p=>p.Nome).ToList();
            Prestadores.ItemsSource=providers;
            if(providers.Count==1)vm.PrestadorCnpj=providers[0].Cnpj;
            if(providers.Count==0)MessageBox.Show(this,"Importe as notas XML antes de gerar as baixas.","Sem notas",MessageBoxButton.OK,MessageBoxImage.Information);
        } catch(Exception ex){Erro(ex);}
    }
    private void EscolherArquivo(object sender,RoutedEventArgs e)
    { var dialog=new OpenFileDialog{Filter="Relatório Excel (*.xlsx)|*.xlsx",Title="Selecionar relatório de recebimentos"};if(dialog.ShowDialog(this)==true)vm.CaminhoArquivo=dialog.FileName; }
    private async void Preparar(object sender,RoutedEventArgs e){try{await vm.PrepararAsync();}catch(Exception ex){Erro(ex);}}
    private async void Confirmar(object sender,RoutedEventArgs e)
    {
        if(!vm.PodeConfirmar)return;
        if(MessageBox.Show(this,$"Confirmar a baixa de {vm.QuantidadeSelecionada} notas, no total de {vm.ValorSelecionado:C2}?\n\nCada nota usará a data de seu lançamento no relatório.","Confirmar lote",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
        try{await vm.ConfirmarAsync();HouveBaixas=true;}catch(Exception ex){Erro(ex);}
    }
    private void SelecionarTodas(object sender,RoutedEventArgs e)=>vm.SelecionarAptas(true);
    private void LimparSelecao(object sender,RoutedEventArgs e)=>vm.SelecionarAptas(false);
    private void AoFechar(object? sender,CancelEventArgs e){if(vm.EstaOcupado){e.Cancel=true;MessageBox.Show(this,"Aguarde a operação terminar antes de fechar.","Operação em andamento");}}
    private void Erro(Exception ex){log.LogError(ex,"Importação de baixas");MessageBox.Show(this,ex is InvalidOperationException or System.IO.InvalidDataException ? ex.Message : "Não foi possível concluir a operação. Confira o acesso ao arquivo e ao banco. Detalhes no log.","Importar baixas",MessageBoxButton.OK,MessageBoxImage.Warning);}
}

