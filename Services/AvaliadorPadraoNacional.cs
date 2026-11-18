using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using NfseGerenciador.Models;

namespace NfseGerenciador.Services;

public static class AvaliadorPadraoNacional
{
    public const int VersaoRegra = 1;
    public const string CnpjPrestador = AvaliacaoNacional.CnpjAlvo;
    public const string Processo = "000000000000000000000087462013";
    private static readonly XNamespace Ns = "http://www.sped.fazenda.gov.br/nfse";
    private static readonly (string Campo, string Caminho, string Esperado, string Formato, string Descricao)[] Regras =
    [
        ("Tributação nacional", "serv/cServ/cTribNac", "040301", @"(?:[0-9]{6}|[0-9]{2}\.[0-9]{2}\.[0-9]{2})", "040301 (04.03.01)"),
        ("NBS", "serv/cServ/cNBS", "123011900", @"(?:[0-9]{9}|[0-9]\.[0-9]{4}\.[0-9]{2}\.[0-9]{2})", "123011900 (1.2301.19.00)"),
        ("Suspensão do ISSQN", "valores/trib/tribMun/exigSusp/tpSusp", "2", "[12]", "2 — Processo administrativo"),
        ("Processo de suspensão", "valores/trib/tribMun/exigSusp/nProcesso", Processo, "[0-9]{30}", Processo),
        ("Retenção do ISSQN", "valores/trib/tribMun/tpRetISSQN", "1", "[123]", "1 — Não retido")
    ];

    public static DocumentoNacional CriarDocumento(string arquivoXml) => new()
    {
        ArquivoXmlGzipBase64 = arquivoXml,
        Sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(arquivoXml)))
    };

    public static AvaliacaoNacional Indisponivel(string motivo, string? versao = null) => new()
    {
        VersaoRegra = VersaoRegra, VersaoLeiaute = versao, AvaliadaEmUtc = DateTime.UtcNow,
        Status = StatusPadraoNacional.NaoAvaliada, Resumo = motivo,
        Campos = Regras.Select(r => new CampoAvaliacaoNacional(r.Campo, null, r.Descricao, ResultadoCampoNacional.Indisponivel, motivo)).ToArray()
    };

    public static AvaliacaoNacional Avaliar(string xml, string chave, string prestador)
    {
        if (!AvaliacaoNacional.PrestadorAlvo(prestador)) return new()
        {
            Status = StatusPadraoNacional.NaoSeAplica, VersaoRegra = VersaoRegra,
            AvaliadaEmUtc = DateTime.UtcNow, Resumo = "Prestador fora do padrão operacional configurado."
        };
        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings
            { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 10 * 1024 * 1024 });
            var root = XDocument.Load(reader).Root;
            if (root?.Name != Ns + "NFSe") return Indisponivel("Documento não reconhecido como NFS-e nacional.");
            var versao = (string?)root.Attribute("versao");
            if (versao is not ("1.00" or "1.01")) return Indisponivel("Versão do leiaute ausente ou não suportada.", versao);
            var inf = Unico(root, "infNFSe");
            if ((string?)inf?.Attribute("Id") != "NFS" + chave) return Indisponivel("Chave do documento incompatível com a nota.", versao);
            var emitente = Unico(Unico(inf, "emit"), "CNPJ");
            if (emitente?.Value != CnpjPrestador) return Indisponivel("Prestador do documento incompatível com a regra.", versao);
            var dps = Unico(inf, "DPS");
            if ((string?)dps?.Attribute("versao") is not ("1.00" or "1.01")) return Indisponivel("Versão da DPS ausente ou não suportada.", versao);
            var infDps = Unico(dps, "infDPS");
            if (infDps == null) return Indisponivel("DPS ausente ou ambígua.", versao);
            var campos = new List<CampoAvaliacaoNacional>();
            foreach (var r in Regras)
            {
                XElement? atual = infDps;
                string? erro = null;
                foreach (var parte in r.Caminho.Split('/'))
                {
                    var itens = atual!.Elements(Ns + parte).Take(2).ToArray();
                    if (itens.Length != 1) { erro = itens.Length == 0 ? "Campo ou grupo ausente: " + parte : "Campo ou grupo duplicado: " + parte; atual = null; break; }
                    atual = itens[0];
                }
                var original = atual?.Value;
                if (erro == null && (atual!.HasElements || !Regex.IsMatch(original!, @"\A" + r.Formato + @"\z", RegexOptions.CultureInvariant)))
                    erro = "Formato ou código não reconhecido no leiaute.";
                var resultado = erro != null ? ResultadoCampoNacional.Indisponivel
                    : original!.Replace(".", "") == r.Esperado ? ResultadoCampoNacional.Conforme : ResultadoCampoNacional.Divergente;
                campos.Add(new(r.Campo, original, r.Descricao, resultado, erro ?? (resultado == ResultadoCampoNacional.Conforme ? "Coincide com o padrão." : "Difere do padrão operacional.")));
            }
            bool divergente = campos.Any(c => c.Resultado == ResultadoCampoNacional.Divergente);
            bool parcial = campos.Any(c => c.Resultado == ResultadoCampoNacional.Indisponivel);
            return new()
            {
                Status = divergente ? StatusPadraoNacional.Divergente : parcial ? StatusPadraoNacional.NaoAvaliada : StatusPadraoNacional.Conforme,
                VersaoRegra = VersaoRegra, VersaoLeiaute = versao, AvaliadaEmUtc = DateTime.UtcNow, Campos = campos,
                Resumo = divergente ? (parcial ? "Divergência identificada; avaliação parcial por campos indisponíveis." : "Há campos diferentes do padrão operacional.")
                    : parcial ? "Não foi possível avaliar todos os campos." : "Os cinco campos coincidem com o padrão operacional."
            };
        }
        catch (XmlException) { return Indisponivel("Documento XML ilegível ou estrutura não permitida."); }
    }

    private static XElement? Unico(XElement? pai, string nome)
    {
        var itens = pai?.Elements(Ns + nome).Take(2).ToArray();
        return itens?.Length == 1 ? itens[0] : null;
    }
}
