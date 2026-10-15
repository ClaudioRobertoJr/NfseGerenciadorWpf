using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using NfseGerenciador.Services.ImportacaoBaixas;
using NfseGerenciador.Models.ImportacaoBaixas;
namespace NfseGerenciador.ViewModels;

public sealed class LinhaBaixa(ItemPrevia item) : INotifyPropertyChanged
{
    public ItemPrevia Item { get; } = item;
    public bool Apta => Item.Status==StatusConciliacao.Apta;
    private bool selected=item.Status==StatusConciliacao.Apta;
    public bool Selecionada { get=>selected; set { var v=value && Apta;if(v==selected)return;selected=v;PropertyChanged?.Invoke(this,new(nameof(Selecionada))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}
public sealed class ImportacaoBaixasViewModel(IImportadorBaixas service) : INotifyPropertyChanged
{
    private string path="",provider="",message="Selecione o relatório e confira o prestador para gerar a prévia.";
    private bool busy;
    private int generation;
    private PreviaBaixas? preview;
    public string CaminhoArquivo {get=>path;set{if(path==value)return;path=value;Invalidar();Notify();}}
    public string PrestadorCnpj {get=>provider;set{if(provider==value)return;provider=value;Invalidar();Notify();}}
    public ObservableCollection<LinhaBaixa> Itens {get;}=[];
    public IEnumerable<LinhaBaixa> Aptas=>Itens.Where(i=>i.Item.Status==StatusConciliacao.Apta);
    public IEnumerable<LinhaBaixa> Revisao=>Itens.Where(i=>i.Item.Status==StatusConciliacao.Revisao);
    public IEnumerable<LinhaBaixa> Ignoradas=>Itens.Where(i=>i.Item.Status==StatusConciliacao.Ignorada);
    public string TituloAptas=>$"Aptas ({Aptas.Count()})";
    public string TituloRevisao=>$"Conferir ({Revisao.Count()})";
    public string TituloIgnoradas=>$"Ignoradas ({Ignoradas.Count()})";
    public bool EstaOcupado {get=>busy;private set{busy=value;Notify();Notify(nameof(PodeEditar));Notify(nameof(PodePreparar));Notify(nameof(PodeConfirmar));}}
    public bool PodeEditar=>!busy;
    public bool PodePreparar=>!busy && !string.IsNullOrWhiteSpace(path) && !string.IsNullOrWhiteSpace(provider);
    public bool PodeConfirmar=>!busy && preview is not null && QuantidadeSelecionada>0;
    public int QuantidadeSelecionada=>Itens.Count(i=>i.Selecionada);
    public decimal ValorSelecionado=>Itens.Where(i=>i.Selecionada).Sum(i=>i.Item.Origem.Credito ?? 0);
    public string Mensagem {get=>message;private set{message=value;Notify();}}
    public ResultadoImportacao? Resultado {get;private set;}
    public async Task PrepararAsync()
    {
        if(!PodePreparar)throw new InvalidOperationException("Selecione o relatório e o prestador.");
        Invalidar();
        int version=generation;
        EstaOcupado=true;Mensagem="Lendo o relatório e conferindo as notas…";
        try
        {
            var result=await service.PrepararAsync(path,provider);
            if(version!=generation)return;
            preview=result;
            foreach(var item in result.Itens){var line=new LinhaBaixa(item);line.PropertyChanged+=Changed;Itens.Add(line);}
            Mensagem=$"{Itens.Count} lançamentos analisados. Apenas itens aptos podem ser baixados. Data de pagamento: data do relatório.";
        }
        catch { Mensagem="Não foi possível gerar a prévia. Confira o arquivo e a conexão.";throw; }
        finally {EstaOcupado=false;NotificarResumo();}
    }
    public async Task ConfirmarAsync()
    {
        if(!PodeConfirmar)throw new InvalidOperationException("Gere uma prévia e selecione itens aptos.");
        var current=preview!;
        var ids=Itens.Where(i=>i.Selecionada).Select(i=>i.Item.Origem.Id).ToArray();
        EstaOcupado=true;Mensagem="Revalidando e gravando o lote…";
        try
        {
            Resultado=await service.ConfirmarAsync(current,ids);
            Mensagem=$"Lote {Resultado.LoteId} concluído: {Resultado.Quantidade} notas, {Resultado.Valor:C2}.";
            Notify(nameof(Resultado));
        }
        catch {Mensagem="A operação não foi concluída. Gere uma nova prévia antes de tentar novamente.";throw;}
        finally {preview=null;foreach(var line in Itens)line.Selecionada=false;EstaOcupado=false;NotificarResumo();}
    }
    public void SelecionarAptas(bool value){if(busy)return;foreach(var i in Itens)i.Selecionada=value;}
    private void Changed(object? sender,PropertyChangedEventArgs args)=>NotificarResumo();
    private void Invalidar()
    {
        generation++;preview=null;Resultado=null;
        foreach(var i in Itens)i.PropertyChanged-=Changed;
        Itens.Clear();NotificarResumo();Notify(nameof(Resultado));Notify(nameof(PodePreparar));
    }
    private void NotificarResumo()
    {
        foreach(var name in new[]{nameof(Aptas),nameof(Revisao),nameof(Ignoradas),nameof(TituloAptas),nameof(TituloRevisao),nameof(TituloIgnoradas),nameof(QuantidadeSelecionada),nameof(ValorSelecionado),nameof(PodeConfirmar)})Notify(name);
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName]string? name=null)=>PropertyChanged?.Invoke(this,new(name));
}

