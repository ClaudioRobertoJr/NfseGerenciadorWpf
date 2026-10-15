using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;
namespace NfseGerenciador.Tests.Atualizacao;
public class AtualizadorScriptTests
{
    private static string Script()=>Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../Scripts/Atualizar.ps1"));
    private static async Task<int> Run(string source,string dest)
    {
        var si=new ProcessStartInfo("powershell.exe"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var a in new[]{"-NoProfile","-NonInteractive","-ExecutionPolicy","Bypass","-File",Script(),"-Origem",source,"-Destino",dest})si.ArgumentList.Add(a);
        using var p=Process.Start(si)!;var stdout=p.StandardOutput.ReadToEndAsync();var stderr=p.StandardError.ReadToEndAsync();await p.WaitForExitAsync();await Task.WhenAll(stdout,stderr);if(p.ExitCode!=0)Console.WriteLine(await stderr);return p.ExitCode;
    }
    private static (string root,string source,string dest) Setup()
    {
        var root=Path.Combine(Path.GetTempPath(),"nfse-updater-"+Guid.NewGuid());var source=Path.Combine(root,"source");var dest=Path.Combine(root,"installed");Directory.CreateDirectory(source);Directory.CreateDirectory(dest);
        File.WriteAllText(Path.Combine(dest,"NfseGerenciador.exe"),"OLD");File.WriteAllText(Path.Combine(dest,"appsettings.json"),"PRESERVE-ME");
        File.WriteAllText(Path.Combine(source,"NfseGerenciador.exe"),"NEW");File.WriteAllText(Path.Combine(source,"appsettings.json"),"NEVER-COPY");
        Manifest(source,"NfseGerenciador.exe");return(root,source,dest);
    }
    private static void Manifest(string source,string path)
    {
        var hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(source,"NfseGerenciador.exe"))));
        File.WriteAllText(Path.Combine(source,"manifest.json"),JsonSerializer.Serialize(new { files=new[] { new { path,sha256=hash } } }));
    }
    [Fact] public async Task Replaces_executable_and_preserves_configuration()
    {var x=Setup();try{Assert.Equal(0,await Run(x.source,x.dest));Assert.Equal("NEW",File.ReadAllText(Path.Combine(x.dest,"NfseGerenciador.exe")));Assert.Equal("PRESERVE-ME",File.ReadAllText(Path.Combine(x.dest,"appsettings.json")));Assert.Contains(Directory.GetFiles(x.root,"NfseGerenciador.exe",SearchOption.AllDirectories),p=>p.Contains("backup-")&&File.ReadAllText(p)=="OLD");}finally{Directory.Delete(x.root,true);}}
    [Fact] public async Task Refuses_same_directory_without_modification()
    {var x=Setup();try{Assert.NotEqual(0,await Run(x.dest,x.dest));Assert.Equal("OLD",File.ReadAllText(Path.Combine(x.dest,"NfseGerenciador.exe")));}finally{Directory.Delete(x.root,true);}}
    [Fact] public async Task Rejects_tampered_package_before_touching_installation()
    {var x=Setup();try{File.WriteAllText(Path.Combine(x.source,"NfseGerenciador.exe"),"TAMPERED");Assert.NotEqual(0,await Run(x.source,x.dest));Assert.Equal("OLD",File.ReadAllText(Path.Combine(x.dest,"NfseGerenciador.exe")));}finally{Directory.Delete(x.root,true);}}
    [Fact] public async Task Rejects_path_traversal()
    {var x=Setup();try{var hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(x.source,"NfseGerenciador.exe"))));File.WriteAllText(Path.Combine(x.source,"manifest.json"),JsonSerializer.Serialize(new{files=new[]{new{path="NfseGerenciador.exe",sha256=hash},new{path="../outside.exe",sha256=hash}}}));Assert.NotEqual(0,await Run(x.source,x.dest));Assert.False(File.Exists(Path.Combine(x.root,"outside.exe")));Assert.Equal("OLD",File.ReadAllText(Path.Combine(x.dest,"NfseGerenciador.exe")));}finally{Directory.Delete(x.root,true);}}
    [Fact] public async Task Restores_first_file_if_later_copy_fails()
    {
        var x=Setup();
        try
        {
            File.WriteAllText(Path.Combine(x.dest,"z.dll"),"ORIGINAL");File.WriteAllText(Path.Combine(x.source,"z.dll"),"NEW");
            var hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(x.source,"NfseGerenciador.exe"))));
            File.WriteAllText(Path.Combine(x.source,"manifest.json"),JsonSerializer.Serialize(new{files=new[]{new{path="NfseGerenciador.exe",sha256=hash},new{path="z.dll",sha256=hash}}}));
            using(var locked=new FileStream(Path.Combine(x.dest,"z.dll"),FileMode.Open,FileAccess.Read,FileShare.Read))
                Assert.NotEqual(0,await Run(x.source,x.dest));
            Assert.Equal("OLD",File.ReadAllText(Path.Combine(x.dest,"NfseGerenciador.exe")));Assert.Equal("ORIGINAL",File.ReadAllText(Path.Combine(x.dest,"z.dll")));Assert.Equal("PRESERVE-ME",File.ReadAllText(Path.Combine(x.dest,"appsettings.json")));
        }finally{Directory.Delete(x.root,true);}
    }
}
