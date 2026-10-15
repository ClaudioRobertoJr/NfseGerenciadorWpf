using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using NfseGerenciador.Models.ImportacaoBaixas;
namespace NfseGerenciador.Services.ImportacaoBaixas;

public sealed class LeitorRelatorioBaixas
{
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly string[] Campos = ["CENTRODECUSTO", "CONTA", "EMPRESA", "DATA", "LOTE", "CONTRAPARTIDA", "HISTORICO", "DEBITO", "CREDITO"];
    private sealed record Celula(string Valor, bool Numerica, bool Formula);
    public RelatorioBaixas Ler(Stream arquivo)
    {
        using var bytes = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = arquivo.Read(buffer)) > 0)
        {
            if (bytes.Length + read > 50_000_000) throw new InvalidDataException("O relatório excede 50 MB.");
            bytes.Write(buffer, 0, read);
        }
        var hash = Convert.ToHexString(SHA256.HashData(bytes.ToArray()));
        bytes.Position = 0;
        try
        {
            using var zip = new ZipArchive(bytes, ZipArchiveMode.Read);
            if (zip.Entries.Sum(e => e.Length) > 150_000_000) throw new InvalidDataException("O conteúdo descompactado é muito grande.");
            var workbook = LerXml(zip, "xl/workbook.xml");
            var rels = LerXml(zip, "xl/_rels/workbook.xml.rels");
            var shared = zip.GetEntry("xl/sharedStrings.xml") is null ? [] :
                LerXml(zip, "xl/sharedStrings.xml").Descendants(Ns + "si")
                    .Select(s => string.Concat(s.Descendants(Ns + "t").Select(t => t.Value))).ToArray();
            var date1904 = (string?)workbook.Root?.Element(Ns + "workbookPr")?.Attribute("date1904") is "1" or "true";
            var result = new List<LancamentoRelatorio>();
            var diagnostics = new List<string>();
            bool encontrouCabecalho = false;
            XNamespace rns = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            foreach (var sheet in workbook.Descendants(Ns + "sheet"))
            {
                var name = (string?)sheet.Attribute("name") ?? "Planilha";
                var rid = (string?)sheet.Attribute(rns + "id");
                var rel = rels.Root?.Elements().SingleOrDefault(e => (string?)e.Attribute("Id") == rid);
                if (rel is null || (string?)rel.Attribute("TargetMode") == "External") throw new InvalidDataException("Referência de planilha inválida.");
                var target = (string?)rel.Attribute("Target") ?? "";
                var path = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
                if (path.Contains("..") || path.Contains('\\')) throw new InvalidDataException("Caminho de planilha inválido.");
                var data = LerXml(zip, path);
                Dictionary<string, string>? headers = null;
                foreach (var row in data.Descendants(Ns + "sheetData").Elements(Ns + "row"))
                {
                    int.TryParse((string?)row.Attribute("r"), out var line);
                    var cells = row.Elements(Ns + "c").ToDictionary(c => Regex.Replace((string?)c.Attribute("r") ?? "", "[0-9]", ""), c => Decodificar(c, shared));
                    var found = cells.Where(c => Campos.Contains(Normalizar(c.Value.Valor)))
                        .GroupBy(c => Normalizar(c.Value.Valor)).ToDictionary(g => g.Key, g => g.First().Key);
                    if (Campos.All(found.ContainsKey)) { headers = found; encontrouCabecalho = true; continue; }
                    if (headers is null) continue;
                    Celula Get(string key) => cells.GetValueOrDefault(headers[key]) ?? new("", false, false);
                    var history = Get("HISTORICO").Valor.Trim();
                    var normalized = Normalizar(history);
                    if (string.IsNullOrWhiteSpace(history) && string.IsNullOrWhiteSpace(Get("DATA").Valor)) continue;
                    if (normalized.StartsWith("SALDOANTERIOR") || normalized.StartsWith("SALDOATUAL") || normalized.StartsWith("TOTAL") || normalized == "HISTORICO") continue;
                    var errors = new List<string>();
                    var date = LerData(Get("DATA"), date1904);
                    if (date is null) errors.Add("Data inválida: " + Get("DATA").Valor);
                    var debit = LerValor(Get("DEBITO"), true);
                    var credit = LerValor(Get("CREDITO"), false);
                    if (debit is null) errors.Add("Débito inválido: " + Get("DEBITO").Valor);
                    if (credit is null) errors.Add("Crédito inválido: " + Get("CREDITO").Valor);
                    if (Campos.Any(c => Get(c).Formula)) errors.Add("Há fórmula em campo necessário; conferir valores no relatório.");
                    var issue = errors.Count == 0 ? null : string.Join(" | ", errors);
                    if (issue is not null) diagnostics.Add(name + ", linha " + line + ": " + issue);
                    result.Add(new LancamentoRelatorio {
                        Id = result.Count + 1, Planilha = name, Linha = line, Centro = Get("CENTRODECUSTO").Valor.Trim(),
                        Conta = Get("CONTA").Valor.Trim(), Empresa = Get("EMPRESA").Valor.Trim(), Data = date, Lote = Get("LOTE").Valor.Trim(),
                        Contrapartida = Get("CONTRAPARTIDA").Valor.Trim(), Historico = history, Debito = debit, Credito = credit, Erro = issue });
                }
            }
            if (!encontrouCabecalho) throw new InvalidDataException("Não encontrei todas as colunas: Centro de Custo, Conta, Empresa, Data, Lote, Contra Partida, Histórico, Débito e Crédito.");
            return new RelatorioBaixas(hash, result.AsReadOnly(), diagnostics.AsReadOnly());
        }
        catch (Exception ex) when (ex is XmlException or FormatException or ArgumentException or InvalidOperationException or OverflowException)
        { throw new InvalidDataException("O arquivo não é um relatório Excel válido: " + ex.Message, ex); }
    }
    private static XDocument LerXml(ZipArchive zip, string path)
    {
        var entry = zip.GetEntry(path) ?? throw new InvalidDataException("Parte ausente no Excel: " + path);
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 100_000_000 });
        return XDocument.Load(reader);
    }
    private static Celula Decodificar(XElement c, string[] strings)
    {
        string type = (string?)c.Attribute("t") ?? "";
        var value = c.Element(Ns + "v")?.Value ?? "";
        if (type == "s") {
            if (!int.TryParse(value, out int index) || index < 0 || index >= strings.Length) throw new InvalidDataException("Índice de texto inválido no Excel.");
            value = strings[index];
        }
        if (type == "inlineStr") value = string.Concat(c.Descendants(Ns + "t").Select(t => t.Value));
        return new Celula(value, type is "" or "n", c.Element(Ns + "f") is not null);
    }
    private static DateTime? LerData(Celula c, bool date1904)
    {
        if (c.Numerica && double.TryParse(c.Valor, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)) {
            try { return (date1904 ? new DateTime(1904, 1, 1).AddDays(value) : DateTime.FromOADate(value)).Date; }
            catch (ArgumentException) { return null; }
        }
        return DateTime.TryParseExact(c.Valor.Trim(), ["d/M/yyyy", "dd/MM/yyyy", "yyyy-MM-dd"], Br, DateTimeStyles.None, out var date) ? date.Date : null;
    }
    private static decimal? LerValor(Celula c, bool permiteVazio)
    {
        if (string.IsNullOrWhiteSpace(c.Valor)) return permiteVazio ? 0m : null;
        if (c.Numerica) return decimal.TryParse(c.Valor, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;
        var text = c.Valor.Replace("R$", "").Replace("\u00a0", "").Trim();
        return decimal.TryParse(text, NumberStyles.Number | NumberStyles.AllowParentheses, Br, out var value) ? value : null;
    }
    internal static string Normalizar(string text) => Regex.Replace(
        string.Concat((text ?? "").Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).ToUpperInvariant(), "[^A-Z0-9]", "");
}

