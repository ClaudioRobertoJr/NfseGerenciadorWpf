using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NfseGerenciador.Models;
using NfseGerenciador.Services;
using NfseGerenciador.Services.ImportacaoBaixas;
using Xunit;

namespace NfseGerenciador.Tests.ImportacaoBaixas;

public class NacionalImportacaoFluxoTests
{
    private sealed record Lote(long Esperado, List<AdnDocument> Documentos);
    private sealed class WorkerSimulado : IDisposable
    {
        private readonly string file=Path.Combine(Path.GetTempPath(),"NfseAdnSintetico-"+Guid.NewGuid().ToString("N")+".json");
        public WorkerSimulado(params Lote[] lotes) => File.WriteAllText(file,JsonSerializer.Serialize(lotes));
        public AdnProcessClient Criar()
        {
            string config=new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent!.Name;
            var exe=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../AdnTests/bin",config,"net8.0/AdnTests.exe"));
            var start=new ProcessStartInfo(exe);start.ArgumentList.Add("--fake-worker");start.ArgumentList.Add("import");start.ArgumentList.Add(file);
            return new AdnProcessClient(start);
        }
        public void Dispose()=>File.Delete(file);
    }
    private sealed class Progresso : IProgress<string> { public void Report(string value){} }
    [Fact] public async Task Auxiliar_sintetico_exercita_selecao_lote_e_fim_por_IPC()
    {
        using var worker=new WorkerSimulado(new(0,[Documento(1)]),new(1,[]));
        using var client=worker.Criar();await client.SelectAsync(CancellationToken.None);
        var lote=await client.ReadAsync(0,PadraoNacionalTests.Cnpj,CancellationToken.None);
        Assert.Equal(PadraoNacionalTests.Xml(),NacionalXml.Decode(Assert.Single(lote).ArquivoXml));
        Assert.Empty(await client.ReadAsync(1,PadraoNacionalTests.Cnpj,CancellationToken.None));
    }
    private static AdnDocument Documento(long nsu,string? xml=null,string? chave=null) => new()
    {NSU=nsu,TipoDocumento="NFSE",ChaveAcesso=chave??PadraoNacionalTests.Chave,ArquivoXml=PadraoNacionalTests.Compactar(xml??PadraoNacionalTests.Xml())};

    [SqlServerFact] public async Task Importacao_real_enriquece_nova_e_reentrega_nao_altera_baixa_documento_ou_avaliacao()
    {
        await using var f=new SqlServerFixture();await f.InitializeAsync();
        using(var worker=new WorkerSimulado(new(0,[Documento(1)]),new(1,[])))
        {
            string resumo=await new NacionalImportService(f,worker.Criar).ImportAsync(PadraoNacionalTests.Cnpj,new Progresso(),CancellationToken.None);
            Assert.Contains("1 conforme(s)",resumo);
        }
        string estado,original;
        await using(var db=f.CreateDbContext())
        {
            var n=await db.Nfses.Include(n=>n.Parcelas).Include(n=>n.AvaliacaoNacional)!.ThenInclude(a=>a!.Documento).SingleAsync();
            Assert.Equal(StatusPadraoNacional.Conforme,n.AvaliacaoNacional!.Status);
            original=n.AvaliacaoNacional.Documento!.ArquivoXmlGzipBase64;
            Assert.Equal(PadraoNacionalTests.Xml(),NacionalXml.Decode(original));
            n.EstaPago=true;n.PagamentoId=2;n.DataPagamentoEfetivo=new(2026,11,17);n.Excluida=true;
            n.Parcelas[0].ParcelaPaga=true;n.Parcelas[0].DataPagamentoParcela=n.DataPagamentoEfetivo;
            await db.SaveChangesAsync();estado=ConciliadorBaixas.Estado(n);
        }
        using(var worker=new WorkerSimulado(new(1,[Documento(2,PadraoNacionalTests.Xml().Replace("040301","040302"))]),new(2,[])))
        {
            string resumo=await new NacionalImportService(f,worker.Criar).ImportAsync(PadraoNacionalTests.Cnpj,new Progresso(),CancellationToken.None);
            Assert.Contains("0 conforme(s), 0 divergente(s)",resumo);
        }
        await using var check=f.CreateDbContext();
        var atual=await check.Nfses.Include(n=>n.Parcelas).Include(n=>n.AvaliacaoNacional)!.ThenInclude(a=>a!.Documento).SingleAsync();
        Assert.Equal(estado,ConciliadorBaixas.Estado(atual));Assert.Equal(original,atual.AvaliacaoNacional!.Documento!.ArquivoXmlGzipBase64);
        Assert.Equal(StatusPadraoNacional.Conforme,atual.AvaliacaoNacional.Status);Assert.Equal(2,(await check.Set<NacionalSync>().SingleAsync()).UltimoNsu);
    }

    [SqlServerFact] public async Task Falha_no_segundo_documento_desfaz_lote_real_e_preserva_NSU_anterior()
    {
        await using var f=new SqlServerFixture();await f.InitializeAsync();
        await using(var db=f.CreateDbContext()){db.Add(new NacionalSync{Cnpj=PadraoNacionalTests.Cnpj,UltimoNsu=10});await db.SaveChangesAsync();}
        using var worker=new WorkerSimulado(new Lote(10,[Documento(11),Documento(12,"<quebrado>",new string('8',50))]));
        await Assert.ThrowsAnyAsync<Exception>(()=>new NacionalImportService(f,worker.Criar).ImportAsync(PadraoNacionalTests.Cnpj,new Progresso(),CancellationToken.None));
        await using var check=f.CreateDbContext();
        Assert.Equal(10,(await check.Set<NacionalSync>().SingleAsync()).UltimoNsu);Assert.Empty(await check.Nfses.ToListAsync());
        Assert.Empty(await check.Set<AvaliacaoNacional>().ToListAsync());Assert.Empty(await check.Set<DocumentoNacional>().ToListAsync());
    }

    [SqlServerFact] public async Task Campos_nao_avaliaveis_nao_bloqueiam_importacao_nem_avanco_NSU()
    {
        await using var f=new SqlServerFixture();await f.InitializeAsync();
        using var worker=new WorkerSimulado(new(0,[Documento(1,PadraoNacionalTests.Xml(new string('9',40)))]),new(1,[]));
        string resumo=await new NacionalImportService(f,worker.Criar).ImportAsync(PadraoNacionalTests.Cnpj,new Progresso(),CancellationToken.None);
        Assert.Contains("1 não avaliada(s)",resumo);
        await using var db=f.CreateDbContext();Assert.Equal(StatusPadraoNacional.NaoAvaliada,(await db.Set<AvaliacaoNacional>().SingleAsync()).Status);
        Assert.Equal(1,(await db.Set<NacionalSync>().SingleAsync()).UltimoNsu);
    }
}
