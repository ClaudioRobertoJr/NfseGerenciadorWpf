using NfseGerenciador.Services;
using Xunit;
namespace NfseGerenciador.Tests.Atualizacao;
public class SqlBackupCaminhoTests
{
    [Theory]
    [InlineData(@"C:\Backups\clinica.bak")]
    [InlineData(@"\\servidor\backups\clinica.bak")]
    public void Repeated_backup_selection_uses_distinct_files_in_same_server_folder(string selected)
    {
        var first=SqlBackupService.CaminhoExclusivo(selected);
        var second=SqlBackupService.CaminhoExclusivo(selected);
        Assert.NotEqual(selected,first);
        Assert.NotEqual(first,second);
        Assert.Equal(System.IO.Path.GetDirectoryName(selected),System.IO.Path.GetDirectoryName(first));
        Assert.StartsWith("clinica_",System.IO.Path.GetFileName(first));
        Assert.EndsWith(".bak",first);
    }
}
