using NfseGerenciador.Services;
using Xunit;

namespace NfseGerenciador.Tests.ImportacaoBaixas;

public class ChavesImportacaoXmlTests
{
    [Fact]
    public void Mesma_nota_em_dois_arquivos_do_lote_so_e_registrada_uma_vez()
    {
        var lote = new ChavesImportacaoXml();
        Assert.True(lote.Registrar("123", "11111111000111"));
        Assert.False(lote.Registrar("123", "11111111000111"));
    }

    [Fact]
    public void Mesmo_numero_de_prestadores_distintos_e_aceito()
    {
        var lote = new ChavesImportacaoXml();
        Assert.True(lote.Registrar("123", "11111111000111"));
        Assert.True(lote.Registrar("123", "22222222000122"));
        Assert.True(lote.Registrar("124", "11111111000111"));
    }

    [Fact]
    public void Novo_lote_pode_examinar_a_mesma_nota_novamente()
    {
        Assert.True(new ChavesImportacaoXml().Registrar("123", "11111111000111"));
        Assert.True(new ChavesImportacaoXml().Registrar("123", "11111111000111"));
    }
}
