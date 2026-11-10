using System.ComponentModel;

namespace NfseGerenciador.Services;

public static class ImportWindowGuard
{
    // ShowDialog runs a nested dispatcher: its errors are outside the async import's try/catch.
    public static async Task RunAsync(Action showDialog, Func<Task> stop, Action<Exception> report)
    {
        try { showDialog(); }
        catch (Win32Exception error)
        {
            var stopping = stop();
            report(error);
            await stopping;
        }
    }
}
