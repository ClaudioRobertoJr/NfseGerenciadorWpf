using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
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
        if(MessageBox.Show(this,$"Confirmar a baixa de {vm.QuantidadeSelecionada} notas, no total de {vm.ValorSelecionado:C2}?\n\n{vm.QuantidadeConferirSelecionada} selecionadas na aba Conferir: você está aceitando as divergências de nome/data dessas linhas.\n\nCada nota usará a data de seu lançamento no relatório.","Confirmar lote",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
        try{await vm.ConfirmarAsync();HouveBaixas=true;}catch(Exception ex){Erro(ex);}
    }
    private void SelecionarTodas(object sender,RoutedEventArgs e)=>vm.SelecionarAptas(true);
    private void SelecionarConferir(object sender,RoutedEventArgs e)=>vm.SelecionarConferir();
    private async void RevisarLinha(object sender,RoutedEventArgs e)
    {
        if(sender is not FrameworkElement {DataContext: LinhaBaixa linha} || !linha.PodeRevisar)return;
        var number=new TextBox {Text=linha.Item.Numero,Margin=new Thickness(0,4,0,12)};
        var reason=new TextBox {AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=70,Margin=new Thickness(0,4,0,12)};
        var panel=new StackPanel {Margin=new Thickness(20)};
        panel.Children.Add(new TextBlock {Text=$"Linha {linha.Item.Origem.Linha} • Crédito {linha.Item.Origem.Credito:C2} • Data {linha.Item.Origem.Data:dd/MM/yyyy}",FontWeight=FontWeights.SemiBold});
        panel.Children.Add(new TextBlock {Text=linha.Item.Motivo+"\n"+linha.Item.Origem.Historico,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,14)});
        panel.Children.Add(new TextBlock {Text="Número da nota correta, já importada neste prestador"});panel.Children.Add(number);
        panel.Children.Add(new TextBlock {Text="Justificativa da conferência (obrigatória)"});panel.Children.Add(reason);
        panel.Children.Add(new TextBlock {Text="A próxima etapa mostra o tomador e o valor da nota antes de liberar. Nenhuma baixa será gravada agora.",TextWrapping=TextWrapping.Wrap});
        var dialog=new Window {Title="Conferir lançamento",Owner=this,Width=590,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize,Content=panel};
        var next=new Button {Content="Localizar e conferir",IsDefault=true,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,14,0,0)};
        next.Click+=(_,_)=>{if(!string.IsNullOrWhiteSpace(number.Text)&&!string.IsNullOrWhiteSpace(reason.Text))dialog.DialogResult=true;else MessageBox.Show(dialog,"Informe o número da nota e a justificativa.");};panel.Children.Add(next);
        if(dialog.ShowDialog()!=true)return;
        try
        {
            await vm.RevisarAsync(linha,number.Text,reason.Text,item=>MessageBox.Show(this,
                $"Liberar esta associação para baixa?\n\nNota: {item.Numero}\nTomador: {item.Tomador}\nValor da nota: {item.ValorNota:C2}\nCrédito: {item.Origem.Credito:C2}\nData do recebimento: {item.Origem.Data:dd/MM/yyyy}\nPagamento: {item.TipoPagamento}\nLocal: {item.Local}\n\nHistórico original: {item.Origem.Historico}\n\nJustificativa: {item.Conferencia?.Justificativa}\n\nVocê está confirmando manualmente a identidade da nota e a data do relatório, mesmo se divergirem do cadastro.",
                "Confirmar conferência manual",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)==MessageBoxResult.Yes);
        }catch(Exception ex){Erro(ex);}
    }
    private void LimparSelecao(object sender,RoutedEventArgs e)=>vm.SelecionarAptas(false);
    private void AoFechar(object? sender,CancelEventArgs e){if(vm.EstaOcupado){e.Cancel=true;MessageBox.Show(this,"Aguarde a operação terminar antes de fechar.","Operação em andamento");}}
    private void Erro(Exception ex){log.LogError(ex,"Importação de baixas");MessageBox.Show(this,ex is InvalidOperationException or System.IO.InvalidDataException ? ex.Message : "Não foi possível concluir a operação. Confira o acesso ao arquivo e ao banco. Detalhes no log.","Importar baixas",MessageBoxButton.OK,MessageBoxImage.Warning);}
}

