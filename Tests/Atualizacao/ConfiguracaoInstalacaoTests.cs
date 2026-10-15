using System.IO;
using Microsoft.Extensions.Configuration;
using NfseGerenciador.Services;
using Xunit;
namespace NfseGerenciador.Tests.Atualizacao;
public class ConfiguracaoInstalacaoTests
{
    [Fact] public void Loads_only_installation_file_and_does_not_modify_it()
    {
        string dir=Path.Combine(Path.GetTempPath(),"nfse-config-"+Guid.NewGuid());Directory.CreateDirectory(dir);
        const string json="""{"ConnectionStrings":{"DefaultConnection":"Server=TESTE;Database=EXISTENTE;Integrated Security=true"}}""";
        try{File.WriteAllText(Path.Combine(dir,"appsettings.json"),json);using var config=(IDisposable)ConfiguracaoInstalacao.Carregar(dir);Assert.Equal("Server=TESTE;Database=EXISTENTE;Integrated Security=true",((IConfiguration)config).GetConnectionString("DefaultConnection"));Assert.Equal(json,File.ReadAllText(Path.Combine(dir,"appsettings.json")));}finally{Directory.Delete(dir,true);}
    }
    [Fact] public void Missing_configuration_never_falls_back_to_developer_database()
    {Assert.Throws<FileNotFoundException>(()=>ConfiguracaoInstalacao.Carregar(Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString())));}
    [Fact] public void Empty_connection_is_rejected()
    {
        string dir=Path.Combine(Path.GetTempPath(),"nfse-config-"+Guid.NewGuid());Directory.CreateDirectory(dir);
        try{File.WriteAllText(Path.Combine(dir,"appsettings.json"),"{}");Assert.Throws<InvalidDataException>(()=>ConfiguracaoInstalacao.Carregar(dir));}finally{Directory.Delete(dir,true);}
    }
}

