using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NfseGerenciador.Models;
using NfseGerenciador.Models.ImportacaoBaixas;
namespace NfseGerenciador.Services.ImportacaoBaixas;
public sealed class ConciliadorBaixas
{
    private static readonly Regex Referencia = new(@"\b(?:NOTA(?:\s+FISCAL)?|NFS[- ]?E|NF)\s*(?:N[º°O.]\s*)?[.:º°#-]*\s*(?<numero>\d+)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    public PreviaBaixas Conciliar(RelatorioBaixas relatorio, IReadOnlyList<Nfse> notas, IReadOnlyList<Pagamento> pagamentos, string prestadorCnpj, IReadOnlyDictionary<int,ConferenciaManual>? conferencias=null)
    {
        if (string.IsNullOrWhiteSpace(prestadorCnpj)) throw new ArgumentException("Selecione o prestador.");
        string provider = Digitos(prestadorCnpj);
        var byNumber = notas.Where(n => Digitos(n.PrestadorCnpj) == provider).GroupBy(n => NumeroNormal(n.Numero)).ToDictionary(g => g.Key, g => g.ToList());
        var refs = relatorio.Lancamentos.ToDictionary(r => r.Id, r => NumeroDaLinha(r.Historico));
        var repetitions = refs.Values.Where(n => n is not null).GroupBy(n => n!).ToDictionary(g => g.Key, g => g.Count());
        var items = new List<ItemPrevia>();
        foreach (var row in relatorio.Lancamentos)
        {
            var manual=conferencias?.GetValueOrDefault(row.Id);
            if(manual is not null && string.IsNullOrWhiteSpace(manual.Justificativa))throw new InvalidOperationException("Informe a justificativa da conferência manual.");
            var linked=manual is null ? null : notas.SingleOrDefault(n=>n.Id==manual.NotaId && Digitos(n.PrestadorCnpj)==provider);
            string number = refs[row.Id] ?? "";
            if(manual is not null)number=linked is null ? "" : NumeroNormal(linked.Numero);
            var item = new ItemPrevia { Origem=row, Numero=number, Status=StatusConciliacao.Revisao,
                Local=LeitorRelatorioBaixas.Normalizar(row.Centro).Contains("LABORATORIO") || row.Centro.Trim().StartsWith("1.1.2.035",StringComparison.Ordinal) ? LocalPagamento.Laboratorio : LocalPagamento.Particular,
                Impressao=Impressao(row,provider), Conferencia=manual };
            ItemPrevia Review(string reason) => item with { Motivo=reason };
            var history = LeitorRelatorioBaixas.Normalizar(row.Historico);
            if (number.Length>0 && byNumber.TryGetValue(number,out var paidMatches) && paidMatches.Count==1 && paidMatches[0].EstaPago)
                item=item with{Status=StatusConciliacao.Ignorada,JaBaixada=true,Motivo="Nota já paga."};
            else if (history.Contains("SIPAG") || history.Contains("CARTAO"))
                item = item with { Status=StatusConciliacao.Ignorada, Motivo="Cartão/SIPAG: baixa manual." };
            else if (row.Erro is not null) item=Review(row.Erro);
            else if (row.Data is null || row.Credito is null || row.Debito is null) item=Review("Data ou valor ausente.");
            else if (row.Debito != 0 || row.Credito <= 0 || history.Contains("ESTORNO"))
                item = item with { Status=StatusConciliacao.Ignorada, Motivo="Débito, estorno ou crédito não positivo." };
            else if (number.Length == 0) item=Review("Não há referência única e explícita à nota fiscal.");
            else if (refs[row.Id] is string original && repetitions[original] > 1) item=Review("Mais de um lançamento para a nota: conferir manualmente.");
            else if (!byNumber.TryGetValue(number,out var matches) || matches.Count == 0) item=Review("Nota não encontrada para o prestador selecionado.");
            else if (matches.Count != 1) item=Review("Número duplicado no cadastro deste prestador.");
            else
            {
                var note=matches[0];
                item=item with { NfseId=note.Id, Tomador=note.TomadorRazaoSocial ?? "", ValorNota=note.ValorLiquidoNfse, EstadoOriginal=Estado(note) };
                if (note.Excluida) item=Review("Nota excluída: não será baixada.");
                else if (note.EstaPago) item=item with { Status=StatusConciliacao.Ignorada, JaBaixada=true, Motivo="Nota já paga." };
                else if (note.ValorLiquidoNfse != row.Credito) item=Review("Crédito diferente do valor líquido da nota.");
                else if (!ParcelasCompativeis(note)) item=Review("Parcelamento ou pagamento anterior: conferir manualmente.");
                else
                {
                    string type=row.Contrapartida switch { "4889" or "230" => "Pix", "132" or "108" => "Dinheiro", _ => "" };
                    var payment=pagamentos.Where(p=>LeitorRelatorioBaixas.Normalizar(p.TipoRecebimento)==LeitorRelatorioBaixas.Normalizar(type) && p.EhAVista && p.NumeroParcelas is null or 1).ToList();
                    if(type.Length==0) item=Review("Contrapartida sem regra de pagamento.");
                    else if(payment.Count!=1) item=Review("Forma de pagamento à vista ausente ou duplicada: "+type+".");
                    else
                    {
                        var warnings=new List<string>();
                        if(manual is null && row.Data.Value.Date<note.DataEmissao.Date)warnings.Add("Recebimento anterior à emissão");
                        if(manual is null && NomeDivergente(row.Historico,note.TomadorRazaoSocial))warnings.Add("Nome no histórico difere do tomador");
                        item=item with { Status=warnings.Count>0 ? StatusConciliacao.Revisao : StatusConciliacao.Apta,
                            PermiteBaixaEmLote=warnings.Count>0,
                            Motivo=warnings.Count>0 ? string.Join("; ",warnings)+". Se reconhecer o recebimento, marque Baixar." : manual is null ? "Nota e valor conferidos." : "Conferida manualmente: "+manual.Justificativa,
                            TipoPagamento=type,PagamentoId=payment[0].Id };
                    }
                }
            }
            items.Add(item);
        }
        return new(relatorio,provider,items.AsReadOnly());
    }
    private static bool ParcelasCompativeis(Nfse n) =>
        n.DataPagamentoEfetivo is null && (n.Pagamento is null || (n.Pagamento.EhAVista && n.Pagamento.NumeroParcelas is null or 1)) &&
        (n.Parcelas.Count==0 || n.Parcelas.Count==1 && n.Parcelas[0].NumeroParcela==1 &&
        !n.Parcelas[0].ParcelaPaga && n.Parcelas[0].DataPagamentoParcela is null && n.Parcelas[0].ValorParcela==n.ValorLiquidoNfse);
    private static string? NumeroDaLinha(string history)
    {
        var matches=Referencia.Matches(history ?? "");
        if(matches.Count!=1) return null;
        var tail=history![(matches[0].Index+matches[0].Length)..];
        if(Regex.IsMatch(tail,@"^\s*(?:[./,+;&-]|E\b|A\b)\s*\d",RegexOptions.IgnoreCase)) return null;
        return NumeroNormal(matches[0].Groups["numero"].Value);
    }
    private static bool NomeDivergente(string history,string? customer)
    {
        var m=Referencia.Match(history);
        var tail=history[(m.Index+m.Length)..].Trim();
        // Only compare a clearly delimited name; do not guess identity from free text.
        if(!tail.StartsWith('-')) return false;
        var name=Regex.Split(tail[1..].Trim(),@"\s+-\s*CF\.?\b",RegexOptions.IgnoreCase)[0].Trim();
        if(name.Length==0 || !name.Any(char.IsLetter)) return false;
        return LeitorRelatorioBaixas.Normalizar(name)!=LeitorRelatorioBaixas.Normalizar(customer ?? "");
    }
    internal static string Digitos(string value) => Regex.Replace(value ?? "",@"\D","");
    private static string NumeroNormal(string value) => (value ?? "").Trim().TrimStart('0');
    internal static string Estado(Nfse n) => Hash(JsonSerializer.Serialize(new {
        n.Id,n.Numero,n.PrestadorCnpj,n.TomadorRazaoSocial,n.TomadorCpfCnpj,n.DataEmissao,n.ValorLiquidoNfse,n.EstaPago,n.Excluida,n.Local,n.PagamentoId,n.DataPagamentoEfetivo,n.DataPrevistaRecebimento,
        Parcelas=n.Parcelas.OrderBy(p=>p.Id).Select(p=>new{p.Id,p.NumeroParcela,p.ValorParcela,p.DataVencimentoParcela,p.ParcelaPaga,p.DataPagamentoParcela}) }));
    private static string Impressao(LancamentoRelatorio r,string provider) => Hash(JsonSerializer.Serialize(new {
        provider,Centro=LeitorRelatorioBaixas.Normalizar(r.Centro),Conta=LeitorRelatorioBaixas.Normalizar(r.Conta),r.Empresa,
        Data=r.Data?.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),Lote=LeitorRelatorioBaixas.Normalizar(r.Lote),
        r.Contrapartida,Historico=LeitorRelatorioBaixas.Normalizar(r.Historico),Debito=r.Debito?.ToString("0.00",CultureInfo.InvariantCulture),Credito=r.Credito?.ToString("0.00",CultureInfo.InvariantCulture) }));
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}

