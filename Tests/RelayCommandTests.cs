using Xunit;
namespace NfseGerenciador.Tests;
public class RelayCommandTests
{
    [Fact] public void Synchronous_UI_action_runs_on_the_calling_thread()
    {
        int caller=Environment.CurrentManagedThreadId,executed=-1;
        var command=new RelayCommand(()=>{executed=Environment.CurrentManagedThreadId;});
        command.Execute(null!);
        Assert.Equal(caller,executed);
    }
}
