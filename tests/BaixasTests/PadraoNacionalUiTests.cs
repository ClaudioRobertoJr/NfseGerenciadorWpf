using System.Windows;
using NfseGerenciador.Models;
using NfseGerenciador.Services;
using NfseGerenciador.ViewModels;
using NfseGerenciador.Views;
using Xunit;

namespace NfseGerenciador.Tests.ImportacaoBaixas;

public class PadraoNacionalUiTests
{
    [Fact] public void Nota_antiga_nacional_nao_avaliada_e_municipal_nao_aplicavel()
    {
        var n=PadraoNacionalSqlTests.NovaNota();n.AvaliacaoNacional=null;
        Assert.Equal("Não avaliada",n.PadraoNacionalTexto);
        n.ChaveAcessoNacional=null;Assert.Equal("Não se aplica",n.PadraoNacionalTexto);
        n.ChaveAcessoNacional=PadraoNacionalTests.Chave;n.PrestadorCnpj="99999999000199";
        Assert.Equal("Não se aplica",n.PadraoNacionalTexto);
    }
    [Fact] public async Task Filtro_padrao_independe_de_pagamento_reseta_pagina_e_limpa_junto()
    {
        var filtros=new List<FiltroConsultaNotas>();
        var vm=new GerenciadorNfseViewModel(null!,new(new LogService()),new LogService(),f=>
        { filtros.Add(f);return Task.FromResult(new PaginaNotas([],150,f.Pagina)); },[]);
        vm.FiltroStatus="Pagas";vm.ProximaPaginaCommand.Execute(null);await vm.CarregamentoAtual;
        Assert.Equal(2,vm.CurrentPage);
        vm.FiltroPadraoNacional="Divergente";await vm.CarregamentoAtual;
        Assert.Equal(1,vm.CurrentPage);Assert.Equal("Pagas",filtros[^1].Status);Assert.Equal(StatusPadraoNacional.Divergente,filtros[^1].Padrao);
        int count=filtros.Count;vm.LimparFiltrosCommand.Execute(null);await vm.CarregamentoAtual;
        Assert.Null(filtros[^1].Padrao);Assert.Equal(count+1,filtros.Count);
    }
    [Fact] public async Task Janelas_abrem_para_nota_paga_cancelada_e_antiga_sem_consultar_banco()
    {
        var done=new TaskCompletionSource();
        var thread=new Thread(()=>
        {
            try
            {
                var nota=PadraoNacionalSqlTests.NovaNota();nota.Cancelada=true;
                var view=new AvaliacaoNacionalWindow(nota);
                view.Measure(new Size(900,550));view.Arrange(new Rect(0,0,900,550));view.UpdateLayout();
                Assert.Equal(5,view.CamposGrid.Items.Count);view.Close();
                nota.AvaliacaoNacional=null;
                var antiga=new AvaliacaoNacionalWindow(nota);
                Assert.Contains("documento",antiga.ResumoText.Text,StringComparison.OrdinalIgnoreCase);antiga.Close();
                var reavaliar=new ReavaliacaoNacionalWindow(null!);reavaliar.Close();
                done.SetResult();
            }
            catch(Exception ex){done.SetException(ex);}
        });
        thread.SetApartmentState(ApartmentState.STA);thread.IsBackground=true;thread.Start();
        await done.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }
}
