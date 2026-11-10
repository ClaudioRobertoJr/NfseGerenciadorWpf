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
    public bool PodeSelecionar => Item.PodeBaixar;
    public bool PodeRevisar => !Item.JaBaixada && Item.Status!=StatusConciliacao.Apta;
    private bool selected=item.Status==StatusConciliacao.Apta && item.PodeBaixar;
    public bool Selecionada { get=>selected; set { var v=value && PodeSelecionar;if(v==selected)return;selected=v;PropertyChanged?.Invoke(this,new(nameof(Selecionada))); } }
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
    public int QuantidadeConferirSelecionada=>Itens.Count(i=>i.Selecionada && i.Item.Status==StatusConciliacao.Revisao);
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
            foreach(var item in result.Itens.Where(i=>!i.JaBaixada)){var line=new LinhaBaixa(item);line.PropertyChanged+=Changed;Itens.Add(line);}
            Mensagem=$"{Itens.Count} lançamentos pendentes. Marque Baixar em Aptas ou Conferir; a confirmação grava todas as selecionadas com a data do relatório.";
        }
        catch { Mensagem="Não foi possível gerar a prévia. Confira o arquivo e a conexão.";throw; }
        finally {EstaOcupado=false;NotificarResumo();}
    }
    public async Task ConfirmarAsync()
    {
        if(!PodeConfirmar)throw new InvalidOperationException("Gere uma prévia e selecione lançamentos disponíveis para baixa.");
        var current=preview!;
        var ids=Itens.Where(i=>i.Selecionada).Select(i=>i.Item.Origem.Id).ToArray();
        EstaOcupado=true;Mensagem="Revalidando e gravando o lote…";
        try
        {
            Resultado=await service.ConfirmarAsync(current,ids);
            foreach(var line in Itens.Where(i=>ids.Contains(i.Item.Origem.Id)).ToArray()){line.PropertyChanged-=Changed;Itens.Remove(line);}
            preview=current with{Itens=current.Itens.Where(i=>!ids.Contains(i.Origem.Id)).ToArray()};
            Mensagem=$"Lote {Resultado.LoteId} concluído: {Resultado.Quantidade} notas, {Resultado.Valor:C2}.";
            Notify(nameof(Resultado));
        }
        catch {preview=null;Mensagem="A operação não foi concluída. Gere uma nova prévia antes de tentar novamente.";throw;}
        finally {foreach(var line in Itens)line.Selecionada=false;EstaOcupado=false;NotificarResumo();}
    }
    public async Task RevisarAsync(LinhaBaixa linha,string numero,string justificativa,Func<ItemPrevia,bool> confirmar)
    {
        if(busy || preview is null || !Itens.Contains(linha) || !linha.PodeRevisar)throw new InvalidOperationException("Gere a prévia e selecione uma linha para conferir.");
        int version=generation;
        EstaOcupado=true;
        try
        {
            var item=await service.RevisarAsync(preview,linha.Item.Origem.Id,numero,justificativa);
            if(version!=generation)return;
            if(!confirmar(item))return;
            if(version!=generation)return;
            preview=preview with{Itens=preview.Itens.Select(i=>i.Origem.Id==item.Origem.Id ? item : i).ToArray()};
            var replacement=new LinhaBaixa(item);replacement.PropertyChanged+=Changed;
            linha.PropertyChanged-=Changed;Itens[Itens.IndexOf(linha)]=replacement;
            Mensagem="Nota conferida e movida para Aptas. A baixa só será gravada ao confirmar as selecionadas.";
        }
        finally {EstaOcupado=false;NotificarResumo();}
    }
    public void SelecionarAptas(bool value){if(busy)return;foreach(var i in Itens)if(i.Apta || !value)i.Selecionada=value;}
    public void SelecionarConferir(){if(busy)return;foreach(var i in Revisao)i.Selecionada=true;}
    private void Changed(object? sender,PropertyChangedEventArgs args)
    {Notify(nameof(QuantidadeSelecionada));Notify(nameof(QuantidadeConferirSelecionada));Notify(nameof(ValorSelecionado));Notify(nameof(PodeConfirmar));}
    private void Invalidar()
    {
        generation++;preview=null;Resultado=null;
        foreach(var i in Itens)i.PropertyChanged-=Changed;
        Itens.Clear();NotificarResumo();Notify(nameof(Resultado));Notify(nameof(PodePreparar));
    }
    private void NotificarResumo()
    {
        foreach(var name in new[]{nameof(Aptas),nameof(Revisao),nameof(Ignoradas),nameof(TituloAptas),nameof(TituloRevisao),nameof(TituloIgnoradas),nameof(QuantidadeSelecionada),nameof(QuantidadeConferirSelecionada),nameof(ValorSelecionado),nameof(PodeConfirmar)})Notify(name);
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName]string? name=null)=>PropertyChanged?.Invoke(this,new(name));
}

