using Xunit;

namespace NfseGerenciador.Tests.ImportacaoBaixas;

public class RelayCommandTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Acao_sincrona_executa_na_thread_STA_chamadora(bool comParametro)
    {
        var done = new TaskCompletionSource<(Thread Caller, Thread Executor, ApartmentState Apartment, object? Parameter)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var parameter = new object();
        var thread = new Thread(() =>
        {
            try
            {
                var caller = Thread.CurrentThread;
                void RecordExecution(object? received) => done.TrySetResult((
                    caller, Thread.CurrentThread, Thread.CurrentThread.GetApartmentState(), received));
                var command = comParametro
                    ? new RelayCommand((Action<object>)(received => RecordExecution(received)))
                    : new RelayCommand((Action)(() => RecordExecution(null)));

                command.Execute(parameter);
            }
            catch (Exception ex)
            {
                done.TrySetException(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        var execution = await done.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Same(execution.Caller, execution.Executor);
        Assert.Equal(ApartmentState.STA, execution.Apartment);
        Assert.Same(comParametro ? parameter : null, execution.Parameter);
    }
}
