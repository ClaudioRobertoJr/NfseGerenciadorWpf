using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using NfseGerenciador.Models;

namespace NfseGerenciador.Services;

public static class NacionalXml
{
    public static readonly DateTime DataInicial = new(2026, 11, 1);
    public static string Digits(string value) => new(value.Where(char.IsAsciiDigit).ToArray());
    public static bool ShouldImport(Nfse n, string cnpj) =>
        Digits(n.PrestadorCnpj) == Digits(cnpj) && n.DataEmissao >= DataInicial;
    public static bool IsCancellation(string? type) => type is "CANCELAMENTO" or
        "CANCELAMENTO_POR_SUBSTITUICAO" or "CANCELAMENTO_DEFERIDO_ANALISE_FISCAL" or "CANCELAMENTO_POR_OFICIO";

    public static string Decode(string encoded)
    {
        using var input = new MemoryStream(Convert.FromBase64String(encoded));
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = gzip.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (output.Length + count > 10 * 1024 * 1024) throw new InvalidDataException("XML excede 10 MB.");
            output.Write(buffer, 0, count);
        }
        return System.Text.Encoding.UTF8.GetString(output.ToArray());
    }

    public static Nfse Parse(string xml, string expectedKey)
    {
        using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings
        { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 10 * 1024 * 1024 });
        var doc = XDocument.Load(reader);
        XNamespace ns = "http://www.sped.fazenda.gov.br/nfse";
        var inf = doc.Root?.Element(ns + "infNFSe") ?? throw new InvalidDataException("XML não é NFS-e nacional.");
        string Required(XElement? parent, string name) => parent?.Element(ns + name)?.Value is { Length: > 0 } value
            ? value : throw new InvalidDataException($"Campo obrigatório ausente: {name}.");
        var key = (string?)inf.Attribute("Id");
        if (key != "NFS" + expectedKey || expectedKey.Length != 50 || !expectedKey.All(char.IsAsciiDigit))
            throw new InvalidDataException("Chave do XML diverge da chave distribuída.");
        var dps = inf.Element(ns + "DPS")?.Element(ns + "infDPS");
        var emit = inf.Element(ns + "emit");
        var toma = dps?.Element(ns + "toma");
        return new Nfse
        {
            Numero = Required(inf, "nNFSe"),
            ChaveAcessoNacional = expectedKey,
            CodigoVerificacao = expectedKey,
            // NFS-e generation is distinct from the earlier DPS emission date.
            DataEmissao = DateTimeOffset.Parse(Required(inf, "dhProc"), CultureInfo.InvariantCulture).DateTime,
            ValorLiquidoNfse = decimal.Parse(Required(inf.Element(ns + "valores"), "vLiq"), CultureInfo.InvariantCulture),
            PrestadorCnpj = emit?.Element(ns + "CNPJ")?.Value ?? emit?.Element(ns + "CPF")?.Value
                ?? throw new InvalidDataException("Emitente não identificado."),
            PrestadorRazaoSocial = Required(emit, "xNome"),
            TomadorCpfCnpj = toma?.Element(ns + "CNPJ")?.Value ?? toma?.Element(ns + "CPF")?.Value ?? toma?.Element(ns + "NIF")?.Value ?? "",
            TomadorRazaoSocial = toma?.Element(ns + "xNome")?.Value ?? "Não identificado",
            Local = LocalPagamento.Outros
        };
    }
}
