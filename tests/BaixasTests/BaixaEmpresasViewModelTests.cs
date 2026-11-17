using NfseGerenciador.Models;
using NfseGerenciador.Services;
using NfseGerenciador.ViewModels;
using Xunit;
namespace NfseGerenciador.Tests.ImportacaoBaixas;
public class BaixaEmpresasViewModelTests
{
    private static GerenciadorNfseViewModel Create(Action query) => new(null!,new(new LogService()),new LogService(),f=>{query();return Task.FromResult(new PaginaNotas([],0,1));},Array.Empty<Pagamento>());
    [Fact] public async Task Declining_confirmation_does_not_execute_or_refresh()
    {
        int queries=0,calls=0;var vm=Create(()=>queries++);int before=queries;
        await vm.BaixarEmpresasAsync(()=>false,()=>{calls++;return Task.FromResult(new ResultadoBaixaEmpresas(0,0,new Dictionary<string,int>()));},_=>throw new Exception("Sem resumo quando cancelada"));
        Assert.Equal(0,calls);Assert.Equal(before,queries);Assert.False(vm.IsBusy);
    }
    [Fact] public async Task Busy_action_blocks_second_click_and_refreshes_once_after_completion()
    {
        int queries=0,calls=0;var vm=Create(()=>queries++);var pending=new TaskCompletionSource<ResultadoBaixaEmpresas>();string summary="";int before=queries;
        var running=vm.BaixarEmpresasAsync(()=>true,()=>{calls++;return pending.Task;},s=>summary=s);
        Assert.True(vm.IsBusy);Assert.False(vm.BaixarEmpresasClassificadasCommand.CanExecute(null));
        await vm.BaixarEmpresasAsync(()=>true,()=>{calls++;return pending.Task;},_=>{});Assert.Equal(1,calls);
        pending.SetResult(new(2,200m,new Dictionary<string,int>{{"Histórico preservado",1}}));await running;
        Assert.False(vm.IsBusy);Assert.Equal(before+1,queries);Assert.Contains("2",summary);Assert.Contains("Histórico preservado",summary);Assert.True(vm.BaixarEmpresasClassificadasCommand.CanExecute(null));
    }
    [Fact] public async Task Failure_restores_command_without_success_summary()
    {
        var vm=Create(()=>{});string summary="";
        await vm.BaixarEmpresasAsync(()=>true,()=>Task.FromException<ResultadoBaixaEmpresas>(new InvalidOperationException("Simulated failure")),s=>summary=s);
        Assert.False(vm.IsBusy);Assert.Contains("Não foi possível",summary);Assert.True(vm.BaixarEmpresasClassificadasCommand.CanExecute(null));
    }
}
