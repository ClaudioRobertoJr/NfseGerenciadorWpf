using NfseGerenciador.Models.ImportacaoBaixas;
using NfseGerenciador.Services.ImportacaoBaixas;
using Xunit;
namespace NfseGerenciador.Tests.ImportacaoBaixas;
public class ConferenciaManualTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public void Explicit_review_can_link_wrong_reference_and_accept_name_or_date(bool oldDate)
    {
        var row=ConciliadorBaixasTests.Linha() with {Historico="NOTA 9999 - OUTRO NOME",Data=oldDate ? new DateTime(2025,1,1) : new DateTime(2026,10,15)};
        var n=ConciliadorBaixasTests.Nota();var report=new RelatorioBaixas("H",[row],[]);
        var matcher=new ConciliadorBaixas();
        Assert.Equal(StatusConciliacao.Revisao,matcher.Conciliar(report,[n],ConciliadorBaixasTests.Pagamentos(),ConciliadorBaixasTests.Cnpj).Itens[0].Status);
        var approved=matcher.Conciliar(report,[n],ConciliadorBaixasTests.Pagamentos(),ConciliadorBaixasTests.Cnpj,new Dictionary<int,ConferenciaManual>{{row.Id,new(n.Id,"Conferido com recibo")}}).Itens[0];
        Assert.Equal(StatusConciliacao.Apta,approved.Status);Assert.Equal(n.Id,approved.NfseId);Assert.Equal("8000",approved.Numero);Assert.Equal(row,approved.Origem);Assert.Equal(row.Data,approved.Origem.Data);
    }
    [Theory] [InlineData("paid")] [InlineData("amount")] [InlineData("missing")] [InlineData("provider")] [InlineData("card")] [InlineData("installments")]
    public void Review_never_bypasses_financial_or_provider_guards(string scenario)
    {
        var row=ConciliadorBaixasTests.Linha();var n=ConciliadorBaixasTests.Nota();
        if(scenario=="paid")n.EstaPago=true;
        if(scenario=="amount")row=row with{Credito=99};
        if(scenario=="provider")n.PrestadorCnpj="99999999000199";
        if(scenario=="card")row=row with{Historico="SIPAG NOTA 8000"};
        if(scenario=="installments")n.Parcelas.Add(new(){NumeroParcela=1,ValorParcela=50});
        var item=new ConciliadorBaixas().Conciliar(new("H",[row],[]),[n],ConciliadorBaixasTests.Pagamentos(),ConciliadorBaixasTests.Cnpj,new Dictionary<int,ConferenciaManual>{{row.Id,new(scenario=="missing" ? 999 : n.Id,"Conferido")}}).Itens[0];
        Assert.NotEqual(StatusConciliacao.Apta,item.Status);
    }
}
