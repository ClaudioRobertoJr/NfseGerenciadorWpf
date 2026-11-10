using System.IO;

namespace NfseGerenciador.Services;

public static class ImportDiagnostics
{
    private static readonly object Gate = new();
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NfseGerenciador", "Logs");
    public static string FilePath => Path.Combine(Folder, "importacao-nacional.log");
    public static void Write(string stage, Exception? error = null)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Folder);
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length > 1024 * 1024)
                    File.Move(FilePath, FilePath + ".previous", true);
                // Never record certificate subjects, private keys, PIN, request bodies or XML.
                File.AppendAllText(FilePath, $"{DateTimeOffset.Now:O} | {stage} | thread={Environment.CurrentManagedThreadId} | {Thread.CurrentThread.GetApartmentState()}\n"
                    + (error == null ? "" : $"{error}\n"));
            }
        }
        catch { /* Logging must not introduce another failure. */ }
    }
}
