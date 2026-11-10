using NfseGerenciador.Models.ImportacaoBaixas;
using NfseGerenciador.Services.ImportacaoBaixas;
using Xunit;
namespace NfseGerenciador.Tests.ImportacaoBaixas;
public class NacionalBaixasTests
{
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
