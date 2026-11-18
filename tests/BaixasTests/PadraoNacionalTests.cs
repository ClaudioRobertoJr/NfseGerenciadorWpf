using System.IO;
using System.IO.Compression;
using System.Text;
using NfseGerenciador.Models;
using NfseGerenciador.Services;
using Xunit;

namespace NfseGerenciador.Tests.ImportacaoBaixas;

public class PadraoNacionalTests
{
    internal const string Chave = "12345678901234567890123456789012345678901234567890";
    internal const string Cnpj = "72547623000190";
    internal const string Processo = "000000000000000000000087462013";
    internal static string Xml(string versao = "1.01") => $"""
        <NFSe xmlns="http://www.sped.fazenda.gov.br/nfse" versao="{versao}">
          <infNFSe Id="NFS{Chave}"><nNFSe>42</nNFSe><dhProc>2026-11-17T10:30:00-03:00</dhProc>
            <emit><CNPJ>{Cnpj}</CNPJ><xNome>Prestador sintético</xNome></emit><valores><vLiq>100.00</vLiq></valores>
            <DPS versao="{versao}"><infDPS><toma><CPF>12345678901</CPF><xNome>Tomador sintético</xNome></toma>
              <serv><cServ><cTribNac>040301</cTribNac><cNBS>123011900</cNBS></cServ></serv>
              <valores><trib><tribMun><tribISSQN>1</tribISSQN><exigSusp><tpSusp>2</tpSusp><nProcesso>{Processo}</nProcesso></exigSusp><tpRetISSQN>1</tpRetISSQN></tribMun></trib></valores>
            </infDPS></DPS>
          </infNFSe>
        </NFSe>
        """;
    internal static string Compactar(string xml)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionMode.Compress, true)) gzip.Write(Encoding.UTF8.GetBytes(xml));
        return Convert.ToBase64String(output.ToArray());
    }
    static AvaliacaoNacional Avaliar(string xml) => AvaliadorPadraoNacional.Avaliar(xml, Chave, Cnpj);

    [Theory][InlineData("1.00")][InlineData("1.01")]
    public void Cinco_campos_conformes_nos_leiautes_suportados(string versao)
    {
        var r = Avaliar(Xml(versao));
        Assert.Equal(StatusPadraoNacional.Conforme, r.Status);
        Assert.Equal(5, r.Campos.Count);
        Assert.Equal(Processo, r.Campos.Single(c => c.Campo == "Processo de suspensão").Encontrado);
        Assert.Equal(versao, r.VersaoLeiaute);
    }
    [Theory]
    [InlineData("040301", "040302", "Tributação nacional")]
    [InlineData("123011900", "123011901", "NBS")]
    [InlineData("<tpSusp>2", "<tpSusp>1", "Suspensão do ISSQN")]
    [InlineData(Processo, "000000000000000000000087462014", "Processo de suspensão")]
    [InlineData("<tpRetISSQN>1", "<tpRetISSQN>2", "Retenção do ISSQN")]
    public void Divergencia_identifica_campo_e_valores(string original, string diferente, string campo)
    {
        var r = Avaliar(Xml().Replace(original, diferente));
        Assert.Equal(StatusPadraoNacional.Divergente, r.Status);
        var c = r.Campos.Single(x => x.Campo == campo);
        Assert.Equal(ResultadoCampoNacional.Divergente, c.Resultado);
        Assert.NotEqual(c.Esperado, c.Encontrado);
    }
    [Theory]
    [InlineData("<cNBS>123011900</cNBS>", "")]
    [InlineData("<tpSusp>2</tpSusp>", "")]
    [InlineData("<tpRetISSQN>1</tpRetISSQN>", "<tpRetISSQN>9</tpRetISSQN>")]
    [InlineData("<cNBS>123011900</cNBS>", "<cNBS>123011900</cNBS><cNBS>123011900</cNBS>")]
    [InlineData("040301", "ABC040301")]
    [InlineData(Processo, "87462013")]
    [InlineData("versao=\"1.01\"", "versao=\"9.99\"")]
    [InlineData(" versao=\"1.01\"", "")]
    public void Ausencia_erro_ou_versao_desconhecida_nunca_confirma(string original, string substituto)
    {
        var r = Avaliar(Xml().Replace(original, substituto));
        Assert.Equal(StatusPadraoNacional.NaoAvaliada, r.Status);
        Assert.Contains(r.Campos, c => c.Resultado == ResultadoCampoNacional.Indisponivel);
    }
    [Fact] public void Grupo_de_suspensao_ausente_tem_dois_campos_indisponiveis()
    {
        var r = Avaliar(Xml().Replace($"<exigSusp><tpSusp>2</tpSusp><nProcesso>{Processo}</nProcesso></exigSusp>", ""));
        Assert.Equal(StatusPadraoNacional.NaoAvaliada, r.Status);
        Assert.Equal(2, r.Campos.Count(x => x.Resultado == ResultadoCampoNacional.Indisponivel));
    }
    [Fact] public void Divergencia_e_ausencia_sao_exibidas_juntas()
    {
        var r = Avaliar(Xml().Replace("040301", "040302").Replace("<cNBS>123011900</cNBS>", ""));
        Assert.Equal(StatusPadraoNacional.Divergente, r.Status);
        Assert.Contains(r.Campos, c => c.Resultado == ResultadoCampoNacional.Indisponivel);
        Assert.Contains("parcial", r.Resumo);
    }
    [Fact] public void Pontuacao_reconhecida_compara_sem_perder_original()
    {
        var r = Avaliar(Xml().Replace("040301", "04.03.01").Replace("123011900", "1.2301.19.00"));
        Assert.Equal(StatusPadraoNacional.Conforme, r.Status);
        Assert.Equal("04.03.01", r.Campos[0].Encontrado);
    }
    [Fact] public void Outro_prestador_nao_recebe_regra()
    {
        var r = AvaliadorPadraoNacional.Avaliar(Xml(), Chave, "12345678000199");
        Assert.Equal(StatusPadraoNacional.NaoSeAplica, r.Status);
    }
    [Fact] public void Documento_incompativel_nao_e_avaliado()
    {
        Assert.Equal(StatusPadraoNacional.NaoAvaliada, Avaliar(Xml().Replace("NFS" + Chave, "NFS" + new string('9',50))).Status);
        Assert.Equal(StatusPadraoNacional.NaoAvaliada, Avaliar(Xml().Replace(Cnpj,"12345678000199")).Status);
        Assert.Equal(StatusPadraoNacional.NaoAvaliada, Avaliar("<quebrado>").Status);
    }
    [Fact] public void Documento_original_e_hash_preservados_sem_reserializar()
    {
        var original = Compactar(Xml());
        var doc = AvaliadorPadraoNacional.CriarDocumento(original);
        Assert.Equal(original, doc.ArquivoXmlGzipBase64);
        Assert.Equal(64, doc.Sha256.Length);
        Assert.Equal(Xml(), NacionalXml.Decode(doc.ArquivoXmlGzipBase64));
    }
    [Fact] public void Reavaliacao_detecta_corrupcao_e_preserva_conteudo_original()
    {
        var doc=AvaliadorPadraoNacional.CriarDocumento(Compactar(Xml()));
        doc.Sha256=new string('0',64);var original=doc.ArquivoXmlGzipBase64;
        var r=ReavaliacaoNacionalService.AvaliarDocumento(doc,Chave,Cnpj);
        Assert.Equal(StatusPadraoNacional.NaoAvaliada,r.Status);Assert.Contains("integridade",r.Resumo);
        Assert.Equal(original,doc.ArquivoXmlGzipBase64);
        doc=AvaliadorPadraoNacional.CriarDocumento("não é gzip");
        Assert.Equal(StatusPadraoNacional.NaoAvaliada,ReavaliacaoNacionalService.AvaliarDocumento(doc,Chave,Cnpj).Status);
    }
    [Fact] public void Valor_nao_lido_nao_e_apresentado_como_ausencia_comprovada()
    {
        var r=Avaliar(Xml("desconhecida"));
        Assert.All(r.Campos,c=>Assert.Equal("Indisponível",c.ValorExibido));
        var ausente=Avaliar(Xml().Replace("<cNBS>123011900</cNBS>",""));
        Assert.Contains("ausente",ausente.Campos.Single(c=>c.Campo=="NBS").Motivo);
    }
}
