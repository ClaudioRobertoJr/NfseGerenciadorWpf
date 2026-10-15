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

