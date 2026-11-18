using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using NfseGerenciador.Models;
using NfseGerenciador.Services;
using NfseGerenciador.Services.ImportacaoBaixas;
using Xunit;

namespace NfseGerenciador.Tests.ImportacaoBaixas;

public class PadraoNacionalSqlTests
{
    [Fact] public void Modelo_tem_relacoes_um_para_um_sem_mudancas_pendentes()
    {
        using var db = new ApplicationDbContext(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string,string?> { ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\MSSQLLocalDB;Database=NfseDesign;Integrated Security=true" }).Build());
        Assert.NotNull(db.Model.FindEntityType(typeof(AvaliacaoNacional)));
        Assert.NotNull(db.Model.FindEntityType(typeof(DocumentoNacional)));
        Assert.False(db.Database.HasPendingModelChanges());
        string sql = db.GetService<IMigrator>().GenerateScript("20261109143826_NacionalAdn");
        Assert.Contains("CREATE TABLE [AvaliacoesNacionais]", sql);
        Assert.DoesNotContain("DROP TABLE", sql);
        Assert.Null(db.Model.FindEntityType(typeof(AvaliacaoNacional))!.FindProperty(nameof(AvaliacaoNacional.VersaoLeiaute))!.GetMaxLength());
        Assert.True(db.Model.FindEntityType(typeof(AvaliacaoNacional))!.FindProperty(nameof(AvaliacaoNacional.VersaoRegra))!.IsConcurrencyToken);
    }

    [Fact] public void Consulta_SQL_filtra_avaliacao_sem_carregar_documento_original()
    {
        using var db=new ApplicationDbContext(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string,string?> { ["ConnectionStrings:DefaultConnection"]="Server=(localdb)\\MSSQLLocalDB;Database=NfseDesign;Integrated Security=true" }).Build());
        foreach(var status in Enum.GetValues<StatusPadraoNacional>())
        {
            var sql=ConsultaNotasService.FiltrarPadrao(db.Nfses.AsNoTracking().Include(n=>n.AvaliacaoNacional),status)
                .OrderBy(n=>n.Id).Skip(50).Take(50).ToQueryString();
            Assert.Contains("WHERE",sql);Assert.Contains("OFFSET",sql);Assert.DoesNotContain("DocumentosNacionais",sql);
            Assert.DoesNotContain("ArquivoXmlGzipBase64",sql);
        }
    }

    [SqlServerFact] public async Task Migracao_aditiva_preserva_notas_anteriores_e_pagamentos()
    {
        await using var f=new SqlServerFixture();await f.InitializeAsync();
        await using var db=f.CreateDbContext();
        await db.GetService<IMigrator>().MigrateAsync("20261109143826_NacionalAdn");
        var n=NovaNota();n.AvaliacaoNacional=null;db.Add(n);db.Add(new NacionalSync{Cnpj=PadraoNacionalTests.Cnpj,UltimoNsu=77});await db.SaveChangesAsync();
        var estado=ConciliadorBaixas.Estado(n);int pagamentos=await db.Pagamentos.CountAsync();
        await db.Database.MigrateAsync();db.ChangeTracker.Clear();
        var depois=await db.Nfses.Include(x=>x.Parcelas).Include(x=>x.AvaliacaoNacional).SingleAsync();
        Assert.Equal(estado,ConciliadorBaixas.Estado(depois));Assert.Null(depois.AvaliacaoNacional);
        Assert.Equal(77,(await db.Set<NacionalSync>().SingleAsync()).UltimoNsu);Assert.Equal(pagamentos,await db.Pagamentos.CountAsync());
    }

    [SqlServerFact] public async Task Filtro_considera_banco_inteiro_antes_da_pagina_e_nao_afeta_pagamento()
    {
        await using var f=new SqlServerFixture();await f.InitializeAsync();
        await using(var db=f.CreateDbContext())
        {
            for(int i=1;i<=55;i++){var n=NovaNota(i.ToString().PadLeft(50,'0'));if(i<=52)n.AvaliacaoNacional=null;else n.AvaliacaoNacional!.Status=StatusPadraoNacional.Divergente;db.Add(n);}
            await db.SaveChangesAsync();
        }
        var filtro=new FiltroConsultaNotas("Pagas",null,null,null,null,1,2,StatusPadraoNacional.Divergente);
        var service=new ConsultaNotasService(f);var p1=await service.CarregarAsync(filtro);var p2=await service.CarregarAsync(filtro with{Pagina=2});
        Assert.Equal(3,p1.Total);Assert.Equal(2,p1.Notas.Count);Assert.Single(p2.Notas);Assert.All(p1.Notas,n=>Assert.True(n.EstaPago));
        Assert.Empty(p1.Notas.Select(n=>n.Id).Intersect(p2.Notas.Select(n=>n.Id)));
        Assert.All(p1.Notas,n=>Assert.Null(n.AvaliacaoNacional!.Documento));
        Assert.Equal(52,(await service.CarregarAsync(filtro with{Padrao=StatusPadraoNacional.NaoAvaliada})).Total);
    }

    private sealed class ProgressoSincrono(Action<string> action) : IProgress<string> { public void Report(string value)=>action(value); }
    [SqlServerFact] public async Task Parar_apos_lote_preserva_50_resultados_e_permite_retomar()
    {
        await using var f=new SqlServerFixture();await f.InitializeAsync();
        await using(var db=f.CreateDbContext())
        {
            for(int i=1;i<=51;i++){var n=NovaNota(i.ToString().PadLeft(50,'0'));n.AvaliacaoNacional!.Status=StatusPadraoNacional.Divergente;db.Add(n);}
            await db.SaveChangesAsync();
        }
        using var cts=new CancellationTokenSource();var service=new ReavaliacaoNacionalService(f);
        var parcial=await service.ReavaliarAsync(new ProgressoSincrono(_=>cts.Cancel()),cts.Token);
        Assert.True(parcial.Cancelada);Assert.Equal(50,parcial.Reavaliadas);
        await using(var check=f.CreateDbContext())Assert.Equal(50,await check.Set<AvaliacaoNacional>().CountAsync(a=>a.Status==StatusPadraoNacional.Conforme));
        var final=await service.ReavaliarAsync(null,CancellationToken.None);Assert.False(final.Cancelada);Assert.Equal(51,final.Reavaliadas);
    }

    internal static Nfse NovaNota(string chave = PadraoNacionalTests.Chave)
    {
        var n = NacionalXml.Parse(PadraoNacionalTests.Xml().Replace(PadraoNacionalTests.Chave,chave), chave);
        n.EstaPago = true; n.PagamentoId = 2; n.DataPagamentoEfetivo = new(2026,11,17);
        n.Parcelas.Add(new(){NumeroParcela=1,ValorParcela=100,ParcelaPaga=true,DataPagamentoParcela=n.DataPagamentoEfetivo,DataVencimentoParcela=new(2026,11,17)});
        var xml = PadraoNacionalTests.Xml().Replace(PadraoNacionalTests.Chave,chave);
        n.AvaliacaoNacional = AvaliadorPadraoNacional.Avaliar(xml,chave,n.PrestadorCnpj);
        n.AvaliacaoNacional.Documento = AvaliadorPadraoNacional.CriarDocumento(PadraoNacionalTests.Compactar(xml));
        return n;
    }

    [SqlServerFact] public async Task Reavaliar_preserva_financeiro_documento_NSU_e_versao_futura()
    {
        await using var f = new SqlServerFixture(); await f.InitializeAsync();
        string estado, original; int id;
        await using (var db=f.CreateDbContext())
        {
            var n=NovaNota(); n.Cancelada=true; n.Excluida=true;
            estado=ConciliadorBaixas.Estado(n); original=n.AvaliacaoNacional!.Documento!.ArquivoXmlGzipBase64;
            n.AvaliacaoNacional.Status=StatusPadraoNacional.Divergente;
            db.Add(n);
            var futura=NovaNota(new string('9',50)); futura.AvaliacaoNacional!.VersaoRegra=99; futura.AvaliacaoNacional.Resumo="Regra futura"; db.Add(futura);
            var antiga=NovaNota(new string('8',50)); antiga.AvaliacaoNacional=null; db.Add(antiga);
            db.Add(new NacionalSync{Cnpj=PadraoNacionalTests.Cnpj,UltimoNsu=87654});
            await db.SaveChangesAsync(); id=n.Id;
            estado=ConciliadorBaixas.Estado(n);
        }
        var service=new ReavaliacaoNacionalService(f);
        var result=await service.ReavaliarAsync(null,CancellationToken.None);
        Assert.Equal(1,result.Reavaliadas); Assert.Equal(1,result.SemDocumento); Assert.Equal(1,result.RegraMaisRecente);
        await service.ReavaliarAsync(null,CancellationToken.None);
        await using var check=f.CreateDbContext();
        var atual=await check.Nfses.Include(n=>n.Parcelas).Include(n=>n.AvaliacaoNacional)!.ThenInclude(a=>a!.Documento).SingleAsync(n=>n.Id==id);
        Assert.Equal(estado,ConciliadorBaixas.Estado(atual)); Assert.True(atual.Cancelada); Assert.True(atual.Excluida);
        Assert.Equal(StatusPadraoNacional.Conforme,atual.AvaliacaoNacional!.Status);
        Assert.Equal(original,atual.AvaliacaoNacional.Documento!.ArquivoXmlGzipBase64);
        Assert.Equal(87654,(await check.Set<NacionalSync>().SingleAsync()).UltimoNsu);
        Assert.Equal("Regra futura",(await check.Set<AvaliacaoNacional>().SingleAsync(a=>a.VersaoRegra==99)).Resumo);
    }
    [SqlServerFact] public async Task Rollback_nao_avanca_NSU_nem_persiste_documento_e_chave_unica_continua_ativa()
    {
        await using var f=new SqlServerFixture(); await f.InitializeAsync();
        await using(var db=f.CreateDbContext())
        {
            await using(var tx=await db.Database.BeginTransactionAsync())
            {
                db.Add(NovaNota()); db.Add(new NacionalSync{Cnpj=PadraoNacionalTests.Cnpj,UltimoNsu=123});
                await db.SaveChangesAsync(); await tx.RollbackAsync();
            }
            db.ChangeTracker.Clear();
            Assert.Empty(await db.Nfses.ToListAsync()); Assert.Empty(await db.Set<DocumentoNacional>().ToListAsync()); Assert.Empty(await db.Set<NacionalSync>().ToListAsync());
            db.Add(NovaNota()); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            db.Add(NovaNota()); await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());
        }
    }
    [SqlServerFact] public async Task Cancelamento_antes_de_iniciar_nao_altera_avaliacao()
    {
        await using var f=new SqlServerFixture(); await f.InitializeAsync();
        await using(var db=f.CreateDbContext()){db.Add(NovaNota());await db.SaveChangesAsync();}
        using var cts=new CancellationTokenSource();cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new ReavaliacaoNacionalService(f).ReavaliarAsync(null,cts.Token));
    }
}
