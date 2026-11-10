using NfseGerenciador.Models;
using NfseGerenciador.Services;
using NfseGerenciador.ViewModels;
using Xunit;
namespace NfseGerenciador.Tests.ImportacaoBaixas;
public class PaginacaoTests
{
    private static GerenciadorNfseViewModel Criar(Func<FiltroConsultaNotas,Task<PaginaNotas>> query) => new(null!,new(new LogService()),new LogService(),query,Array.Empty<Pagamento>());
    private static PaginaNotas Pagina(int pagina,int total=160) => new([new Nfse{Id=pagina,Numero=pagina.ToString()}],total,pagina);
    [Fact] public async Task Navega_ate_quarta_pagina_e_volta_com_lista_correspondente()
    {
        var vm=Criar(f=>Task.FromResult(Pagina(f.Pagina)));
        for(int p=2;p<=4;p++){vm.ProximaPaginaCommand.Execute(null);await vm.CarregamentoAtual;Assert.Equal(p,vm.CurrentPage);Assert.Equal(p,Assert.Single(vm.Nfses).Id);}
        Assert.False(vm.ProximaPaginaCommand.CanExecute(null));
        vm.PaginaAnteriorCommand.Execute(null);await vm.CarregamentoAtual;Assert.Equal(3,vm.CurrentPage);
    }
    [Fact] public async Task Resposta_antiga_nao_sobrescreve_filtro_novo()
    {
        var antigo=new TaskCompletionSource<PaginaNotas>();var recente=new TaskCompletionSource<PaginaNotas>();int calls=0;
        var vm=Criar(f=>++calls==1?antigo.Task:recente.Task);var primeira=vm.CarregamentoAtual;
        vm.FiltroTexto="novo";var segunda=vm.CarregamentoAtual;
        recente.SetResult(new([new(){Id=22}],1,1));await segunda;
        antigo.SetResult(new([new(){Id=11}],160,1));await primeira;
        Assert.Equal(22,Assert.Single(vm.Nfses).Id);Assert.Equal(1,vm.TotalItems);Assert.False(vm.IsCarregandoLista);
    }
    [Fact] public async Task Falha_na_proxima_pagina_preserva_indicador_e_lista_e_permite_tentar_novamente()
    {
        bool falhar=true;var vm=Criar(f=>f.Pagina==2&&falhar?Task.FromException<PaginaNotas>(new Exception("Falha SQL simulada")):Task.FromResult(Pagina(f.Pagina)));
        vm.ProximaPaginaCommand.Execute(null);await vm.CarregamentoAtual;
        Assert.Equal(1,vm.CurrentPage);Assert.Equal(1,Assert.Single(vm.Nfses).Id);Assert.NotEmpty(vm.ErroConsulta);Assert.True(vm.ProximaPaginaCommand.CanExecute(null));
        falhar=false;vm.ProximaPaginaCommand.Execute(null);await vm.CarregamentoAtual;Assert.Equal(2,vm.CurrentPage);Assert.Empty(vm.ErroConsulta);
    }
    [Fact] public async Task Limpar_filtros_dispara_uma_consulta_e_retorna_primeira_pagina()
    {
        var filtros=new List<FiltroConsultaNotas>();var vm=Criar(f=>{filtros.Add(f);return Task.FromResult(Pagina(f.Pagina));});
        vm.FiltroTexto="abc";vm.FiltroStatus="Pagas";vm.FiltroDataInicial=new(2026,11,1);vm.ProximaPaginaCommand.Execute(null);await vm.CarregamentoAtual;
        int antes=filtros.Count;vm.LimparFiltrosCommand.Execute(null);await vm.CarregamentoAtual;
        Assert.Equal(antes+1,filtros.Count);Assert.Equal(1,vm.CurrentPage);Assert.Null(filtros.Last().Inicio);Assert.Equal("Todas",filtros.Last().Status);Assert.Equal("",filtros.Last().Texto);
    }
    [Fact] public async Task Pagina_so_muda_quando_resultado_chega_e_nao_aceita_cliques_simultaneos()
    {
        var pending=new TaskCompletionSource<PaginaNotas>();var vm=Criar(f=>f.Pagina==1?Task.FromResult(Pagina(1)):pending.Task);
        vm.ProximaPaginaCommand.Execute(null);Assert.Equal(1,vm.CurrentPage);Assert.False(vm.ProximaPaginaCommand.CanExecute(null));
        pending.SetResult(Pagina(2));await vm.CarregamentoAtual;Assert.Equal(2,vm.CurrentPage);
    }
    [Fact] public void Lista_vazia_mostra_pagina_um_de_um_e_navegacao_desabilitada()
    {
        var vm=Criar(_=>Task.FromResult(new PaginaNotas([],0,1)));
        Assert.Equal(1,vm.TotalPages);Assert.False(vm.ProximaPaginaCommand.CanExecute(null));Assert.False(vm.PaginaAnteriorCommand.CanExecute(null));
    }
    [SqlServerFact] public async Task SQL_pagina_sem_repeticao_e_combina_status_datas_texto_documento_local()
    {
        await using var f=new SqlServerFixture();await f.InitializeAsync();
        await using(var db=f.CreateDbContext())
        {
            for(int i=1;i<=160;i++){var n=ConciliadorBaixasTests.Nota();n.Id=0;n.Numero=i.ToString();n.DataEmissao=new(2026,11,1);n.TomadorRazaoSocial="Tomador "+i;n.TomadorCpfCnpj="123.456.789-01";n.EstaPago=i%2==0;n.DataPagamentoEfetivo=n.EstaPago?new(2026,11,5):(DateTime?)null;n.Local=i<=80?LocalPagamento.Particular:LocalPagamento.Laboratorio;db.Nfses.Add(n);}await db.SaveChangesAsync();
        }
        var service=new ConsultaNotasService(f);var filtro=new FiltroConsultaNotas("Todas",null,null,null,null,1,50);var ids=new List<int>();
        for(int p=1;p<=4;p++){var r=await service.CarregarAsync(filtro with{Pagina=p});Assert.Equal(p,r.Pagina);Assert.Equal(160,r.Total);ids.AddRange(r.Notas.Select(n=>n.Id));}
        Assert.Equal(160,ids.Distinct().Count());
        Assert.Equal("81",Assert.Single((await service.CarregarAsync(filtro with{Texto="81"})).Notas).Numero);
        var vm=new GerenciadorNfseViewModel(f,new(new LogService()),new LogService(),service.CarregarAsync,Array.Empty<Pagamento>());
        await vm.CarregamentoAtual;int notificacoes=0;
        vm.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(vm.NfseSelecionada))notificacoes++;};
        vm.NfseSelecionada=vm.Nfses[0];await vm.CarregamentoDetalhes;
        Assert.True(vm.IsDetalhesVisible);Assert.True(notificacoes>=2);
        var filtrado=await service.CarregarAsync(filtro with{Status="Pagas",Texto="123.456.789-01",Local="Particular",Inicio=new(2026,11,5),Fim=new(2026,11,5)});
        Assert.Equal(40,filtrado.Total);Assert.All(filtrado.Notas,n=>Assert.True(n.EstaPago));
        Assert.Equal(0,(await service.CarregarAsync(filtro with{Texto="inexistente"})).Total);
        Assert.Equal(4,(await service.CarregarAsync(filtro with{Pagina=99})).Pagina);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.CarregarAsync(filtro with{Inicio=new(2026,11,9),Fim=new(2026,11,1)}));
    }
}
