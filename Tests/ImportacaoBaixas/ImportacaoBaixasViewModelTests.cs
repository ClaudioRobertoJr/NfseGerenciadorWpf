using NfseGerenciador.Models.ImportacaoBaixas;
using NfseGerenciador.Services.ImportacaoBaixas;
using NfseGerenciador.ViewModels;
using Xunit;
namespace NfseGerenciador.Tests.ImportacaoBaixas;
public class ImportacaoBaixasViewModelTests
{
    private sealed class Service : IImportadorBaixas
    {
        public TaskCompletionSource<PreviaBaixas>? Pending;
        public TaskCompletionSource<ResultadoImportacao>? Saving;
        public Task<PreviaBaixas> PrepararAsync(string c,string p,CancellationToken ct=default) => Pending?.Task ?? Task.FromResult(Preview());
        public Task<ResultadoImportacao> ConfirmarAsync(PreviaBaixas p,IReadOnlyCollection<int> ids,CancellationToken ct=default) => Saving?.Task ?? Task.FromResult(new ResultadoImportacao(1,ids.Count,100));
        public Task<ItemPrevia> RevisarAsync(PreviaBaixas p,int id,string numero,string motivo,CancellationToken ct=default) => Task.FromResult(p.Itens.Single(i=>i.Origem.Id==id) with{Status=StatusConciliacao.Apta,NfseId=99,Conferencia=new(99,motivo)});
    }
    private static PreviaBaixas Preview() => new(new("H",[],[]),"12345678000190",[
        new(){Origem=ConciliadorBaixasTests.Linha(),Status=StatusConciliacao.Apta},
        new(){Origem=ConciliadorBaixasTests.Linha() with {Id=2,Credito=200},Status=StatusConciliacao.Revisao}]);
    private static ImportacaoBaixasViewModel VM(Service s) => new(s){CaminhoArquivo="arquivo.xlsx",PrestadorCnpj="12345678000190"};
    [Fact] public async Task Only_eligible_rows_are_selected_and_counted()
    {
        var vm=VM(new());await vm.PrepararAsync();
        Assert.Equal(1,vm.QuantidadeSelecionada);Assert.Equal(100m,vm.ValorSelecionado);
        vm.Itens[1].Selecionada=true;Assert.False(vm.Itens[1].Selecionada);
        vm.Itens[0].Selecionada=false;Assert.Equal(0,vm.QuantidadeSelecionada);Assert.False(vm.PodeConfirmar);
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task Manual_review_moves_row_only_after_explicit_confirmation(bool accepted)
    {
        var vm=VM(new());await vm.PrepararAsync();vm.Itens[0].Selecionada=false;
        await vm.RevisarAsync(vm.Itens[1],"8001","Recibo conferido",item=>accepted);
        Assert.False(vm.Itens[0].Selecionada);
        Assert.Equal(accepted ? 2 : 1,vm.Aptas.Count());Assert.Equal(accepted ? 0 : 1,vm.Revisao.Count());
        Assert.Equal(accepted ? 1 : 0,vm.QuantidadeSelecionada);Assert.Equal(accepted ? 200m : 0,vm.ValorSelecionado);
    }
    [Fact] public async Task Selection_does_not_replace_grid_collections_during_a_click()
    {
        var vm=VM(new());await vm.PrepararAsync();var notified=new List<string?>();vm.PropertyChanged+=(_,e)=>notified.Add(e.PropertyName);
        vm.Itens[0].Selecionada=false;
        Assert.DoesNotContain(nameof(vm.Aptas),notified);Assert.DoesNotContain(nameof(vm.Revisao),notified);Assert.Contains(nameof(vm.QuantidadeSelecionada),notified);
    }
    [Fact] public async Task Changing_file_or_provider_invalidates_preview()
    {
        var vm=VM(new());await vm.PrepararAsync();vm.CaminhoArquivo="outro.xlsx";Assert.False(vm.PodeConfirmar);Assert.Empty(vm.Itens);
        await vm.PrepararAsync();vm.PrestadorCnpj="99999999000199";Assert.False(vm.PodeConfirmar);Assert.Empty(vm.Itens);
    }
    [Fact] public async Task Busy_blocks_confirmation_and_stale_preview_is_discarded()
    {
        var s=new Service{Pending=new()};var vm=VM(s);var work=vm.PrepararAsync();
        Assert.True(vm.EstaOcupado);Assert.False(vm.PodeConfirmar);vm.CaminhoArquivo="novo.xlsx";
        s.Pending.SetResult(Preview());await work;Assert.Empty(vm.Itens);Assert.False(vm.EstaOcupado);
    }
    [Fact] public async Task Cannot_submit_twice_while_saving_and_result_invalidates_preview()
    {
        var s=new Service{Saving=new()};var vm=VM(s);await vm.PrepararAsync();var work=vm.ConfirmarAsync();
        Assert.False(vm.PodeConfirmar);await Assert.ThrowsAsync<InvalidOperationException>(()=>vm.ConfirmarAsync());
        s.Saving.SetResult(new(10,1,100));await work;Assert.False(vm.PodeConfirmar);Assert.Equal(10,vm.Resultado!.LoteId);
    }
}

