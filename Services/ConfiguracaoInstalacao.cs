using System.IO;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
namespace NfseGerenciador.Services;
public static class ConfiguracaoInstalacao
{
    public static IConfigurationRoot Carregar(string diretorioInstalacao)
    {
        string path=Path.Combine(Path.GetFullPath(diretorioInstalacao),"appsettings.json");
        if(!File.Exists(path))throw new FileNotFoundException("Não foi encontrado appsettings.json na pasta do programa. Preserve e copie a configuração da instalação anterior; não use uma conexão de exemplo.",path);
        var config=new ConfigurationBuilder().SetBasePath(Path.GetDirectoryName(path)!).AddJsonFile("appsettings.json",false,false).Build();
        try
        {
            var value=config.GetConnectionString("DefaultConnection");
            if(string.IsNullOrWhiteSpace(value))throw new InvalidDataException("A conexão DefaultConnection não está configurada.");
            var cs=new SqlConnectionStringBuilder(value);
            if(string.IsNullOrWhiteSpace(cs.DataSource)||string.IsNullOrWhiteSpace(cs.InitialCatalog))
                throw new InvalidDataException("Informe o servidor e o nome do banco existente.");
            return config;
        }
        catch { ((IDisposable)config).Dispose();throw; }
    }
}

