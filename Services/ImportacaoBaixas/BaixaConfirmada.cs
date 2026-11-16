using System.Globalization;
using System.Text.RegularExpressions;
using NfseGerenciador.Models;
using NfseGerenciador.Models.ImportacaoBaixas;
namespace NfseGerenciador.Services.ImportacaoBaixas;

internal sealed record BaixaConfirmada(string TipoPagamento, int Parcelas)
{
    private static readonly Regex Codigo = new(@"\ACONFIRMADO_(CRED|DEB)_([1-9][0-9]?)_V1\z", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex Historico = new(@"\ABAIXA CONFIRMADA PELO USUARIO V1 NF [0-9]+(?:\s|$)", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    internal static bool TemMarcador(LancamentoRelatorio row) => row.Contrapartida.StartsWith("CONFIRMADO_",StringComparison.Ordinal) || row.Historico.StartsWith("BAIXA CONFIRMADA",StringComparison.Ordinal);
    internal static BaixaConfirmada? Ler(LancamentoRelatorio row)
    {
        var m=Codigo.Match(row.Contrapartida);
        if(!m.Success || !Historico.IsMatch(row.Historico))return null;
        int count=int.Parse(m.Groups[2].Value,CultureInfo.InvariantCulture);
        bool credit=m.Groups[1].Value=="CRED";
        return !credit && count!=1 ? null : new(credit ? "Cartão de Crédito" : "Cartão de Débito",count);
    }
    internal bool Aceita(Pagamento p) =>
        LeitorRelatorioBaixas.Normalizar(p.TipoRecebimento)==LeitorRelatorioBaixas.Normalizar(TipoPagamento) &&
        (TipoPagamento=="Cartão de Débito" ? p.EhAVista && p.NumeroParcelas is null or 1 :
            (p.NumeroParcelas is null || p.NumeroParcelas==Parcelas) && (!p.EhAVista || Parcelas==1));
    internal static bool SemHistoricoAnterior(Nfse n) =>
        n.DataPagamentoEfetivo is null && n.PagamentoId is null && n.Pagamento is null &&
        (n.Parcelas.Count==0 || n.Parcelas.Count==1 && n.Parcelas[0].NumeroParcela==1 &&
         !n.Parcelas[0].ParcelaPaga && n.Parcelas[0].DataPagamentoParcela is null &&
         n.Parcelas[0].ValorParcela==n.ValorLiquidoNfse && n.Parcelas[0].DataVencimentoParcela==n.DataEmissao.AddDays(30));
    internal bool ValoresEDatasValidos(decimal value,DateTime date)
    {
        decimal part=Math.Round(value/Parcelas,2);
        return part>0 && value-part*(Parcelas-1)>0 && date.Date<=DateTime.MaxValue.AddMonths(-(Parcelas-1)).Date;
    }
}

