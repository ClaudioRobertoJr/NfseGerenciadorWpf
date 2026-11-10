using System.IO;
using NfseGerenciador.Services;
using Xunit;

namespace NfseGerenciador.Tests.ImportacaoBaixas;

public class XmlParserTests
{
    private const string NotaValida = """
        <CompNfse><Nfse><InfNfse><Numero>123</Numero><CodigoVerificacao>ABC</CodigoVerificacao>
        <DataEmissao>2026-10-01T10:00:00</DataEmissao>
        <ValoresNfse><ValorLiquidoNfse>100.00</ValorLiquidoNfse></ValoresNfse>
        <PrestadorServico><IdentificacaoPrestador><CpfCnpj><Cnpj>11111111000111</Cnpj></CpfCnpj></IdentificacaoPrestador>
        <RazaoSocial>Prestador</RazaoSocial></PrestadorServico></InfNfse></Nfse></CompNfse>
        """;

    [Fact]
    public void Nota_valida_seguida_de_nota_invalida_nao_retorna_lista_parcial() =>
        DeveFalhar("<Lista xmlns='http://www.abrasf.org.br/nfse.xsd'>" + NotaValida + "<CompNfse><Nfse /></CompNfse></Lista>");

    [Fact]
    public void Arquivo_malformado_nao_retorna_sucesso_vazio() => DeveFalhar("<Lista>");

    [Fact]
    public void Formato_sem_compnfse_e_rejeitado() => DeveFalhar("<NFSe xmlns='http://www.sped.fazenda.gov.br/nfse'><infNFSe /></NFSe>");

    [Fact]
    public void Arquivo_legado_valido_continua_importavel()
    {
        ComArquivo("<Lista xmlns='http://www.abrasf.org.br/nfse.xsd'>" + NotaValida + "</Lista>", caminho =>
        {
            var nota = Assert.Single(new NfseXmlParser(new LogService()).Parse(caminho));
            Assert.Equal("123", nota.Numero);
            Assert.Equal(100m, nota.ValorLiquidoNfse);
        });
    }

    private static void DeveFalhar(string xml) => ComArquivo(xml, caminho =>
    {
        var erro = Assert.Throws<InvalidDataException>(() => new NfseXmlParser(new LogService()).Parse(caminho));
        Assert.Contains(caminho, erro.Message);
        Assert.NotNull(erro.InnerException);
    });

    private static void ComArquivo(string xml, Action<string> verificar)
    {
        string caminho = Path.GetTempFileName();
        try { File.WriteAllText(caminho, xml); verificar(caminho); }
        finally { File.Delete(caminho); }
    }
}
