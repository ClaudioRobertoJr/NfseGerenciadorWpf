using NfseGerenciador.Models.ImportacaoBaixas;
using NfseGerenciador.Services.ImportacaoBaixas;
using Xunit;
namespace NfseGerenciador.Tests.ImportacaoBaixas;
public class NacionalBaixasTests
{
    [Fact] public void Escala_decimal_do_SQL_nao_altera_estado_financeiro()
    {
        var nota=ConciliadorBaixasTests.Nota();
        nota.Parcelas.Add(new(){NumeroParcela=1,ValorParcela=100m});
        var antes=ConciliadorBaixas.Estado(nota);
        nota.ValorLiquidoNfse=100.00m;nota.Parcelas[0].ValorParcela=100.00m;
        Assert.Equal(antes,ConciliadorBaixas.Estado(nota));
        nota.ValorLiquidoNfse=100.01m;
        Assert.NotEqual(antes,ConciliadorBaixas.Estado(nota));
    }
    [Fact] public void Cancelada_nao_pode_ser_baixada()
    {
        var nota = ConciliadorBaixasTests.Nota(); nota.Cancelada = true;
        Assert.False(ConciliadorBaixasTests.Match(nota: nota).PodeBaixar);
    }
    [Fact] public void Cancelamento_altera_estado_da_previa()
    {
        var nota = ConciliadorBaixasTests.Nota(); var antes = ConciliadorBaixas.Estado(nota);
        nota.Cancelada = true;
        Assert.NotEqual(antes, ConciliadorBaixas.Estado(nota));
    }
}
