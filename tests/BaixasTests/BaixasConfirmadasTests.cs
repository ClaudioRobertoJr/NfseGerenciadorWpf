using NfseGerenciador.Models;
using NfseGerenciador.Models.ImportacaoBaixas;
using NfseGerenciador.Services.ImportacaoBaixas;
using Xunit;
namespace NfseGerenciador.Tests.ImportacaoBaixas;

public class BaixasConfirmadasTests
{
    internal static LancamentoRelatorio Row(string code = "CONFIRMADO_CRED_6_V1") => ConciliadorBaixasTests.Linha() with
    { Contrapartida=code, Historico="BAIXA CONFIRMADA PELO USUARIO V1 NF 8000", Centro="1.1.2.035 LABORATORIO", Data=new(2026,1,31) };
    internal static Pagamento[] Payments() => [new(){Id=3,TipoRecebimento="Cartão de Crédito",EhAVista=false},new(){Id=4,TipoRecebimento="Cartão de Débito",EhAVista=true,NumeroParcelas=1}];
    private static ItemPrevia Match(LancamentoRelatorio? row=null,Nfse? note=null,Pagamento[]? payments=null,bool manual=false) =>
        Assert.Single(new ConciliadorBaixas().Conciliar(new("TEST",[row??Row()],[]),[note??ConciliadorBaixasTests.Nota()],payments??Payments(),ConciliadorBaixasTests.Cnpj,
            manual ? new Dictionary<int,ConferenciaManual>{{1,new(1,"Confirmado")}} : null).Itens);

    [Theory] [InlineData("CONFIRMADO_CRED_6_V1",3)] [InlineData("CONFIRMADO_CRED_1_V1",3)] [InlineData("CONFIRMADO_DEB_1_V1",4)] [InlineData("CONFIRMADO_CRED_99_V1",3)]
    public void Confirmed_rows_are_eligible_with_exact_existing_type(string code,int id)
    {var item=Match(Row(code));Assert.Equal(StatusConciliacao.Apta,item.Status);Assert.Equal(id,item.PagamentoId);Assert.Equal(LocalPagamento.Laboratorio,item.Local);Assert.Equal(new DateTime(2026,1,31),item.Origem.Data);}

    [Theory] [InlineData("CONFIRMADO_DEB_2_V1")] [InlineData("CONFIRMADO_CRED_0_V1")] [InlineData("CONFIRMADO_CRED_100_V1")] [InlineData("CONFIRMADO_CRED_06_V1")] [InlineData("CONFIRMADO_CRED_6_V1 ")] [InlineData("4889")]
    public void Invalid_marker_cannot_be_promoted_manually(string code)
    {Assert.False(Match(Row(code)).PodeBaixar);Assert.False(Match(Row(code),manual:true).PodeBaixar);}

    [Theory] [InlineData("NOTA 8000")] [InlineData("BAIXA CONFIRMADA PELO USUARIO V1 NF 8000 / 8001")] [InlineData("BAIXA CONFIRMADA PELO USUARIO V1 NF 8000 NF 8001")] [InlineData("BAIXA CONFIRMADA PELO USUARIO V1 NF 8000.1")]
    public void Invalid_history_cannot_be_promoted_manually(string history) => Assert.False(Match(Row() with{Historico=history},manual:true).PodeBaixar);

    [Fact] public void Credit_and_debit_are_not_substituted_or_created()
    {Assert.False(Match(payments:[Payments()[1]]).PodeBaixar);Assert.False(Match(Row("CONFIRMADO_DEB_1_V1"),payments:[Payments()[0]]).PodeBaixar);Assert.False(Match(payments:ConciliadorBaixasTests.Pagamentos()).PodeBaixar);}

    [Fact] public void Incompatible_registered_terms_are_not_reused()
    {Assert.False(Match(payments:[new(){Id=3,TipoRecebimento="Cartão de Crédito",EhAVista=false,NumeroParcelas=3}]).PodeBaixar);Assert.False(Match(payments:[new(){Id=3,TipoRecebimento="Cartão de Crédito",EhAVista=true,NumeroParcelas=1}]).PodeBaixar);}

    [Fact] public void Only_an_unpaid_default_parcel_can_be_replaced()
    {
        var n=ConciliadorBaixasTests.Nota();n.Parcelas.Add(new(){NumeroParcela=1,ValorParcela=100,DataVencimentoParcela=n.DataEmissao.AddDays(30)});Assert.True(Match(note:n).PodeBaixar);
        n.Parcelas[0].ParcelaPaga=true;Assert.False(Match(note:n).PodeBaixar);n.Parcelas[0].ParcelaPaga=false;n.Parcelas[0].DataPagamentoParcela=new(2026,1,1);Assert.False(Match(note:n).PodeBaixar);
        n.Parcelas[0].DataPagamentoParcela=null;n.Parcelas.Add(new(){NumeroParcela=2,ValorParcela=100});Assert.False(Match(note:n).PodeBaixar);
        n.Parcelas.Clear();n.PagamentoId=3;n.Pagamento=Payments()[0];Assert.False(Match(note:n).PodeBaixar);
    }

    [Fact] public void Financial_and_note_guards_remain_enforced()
    {
        Assert.False(Match(Row() with{Credito=99.99m}).PodeBaixar);Assert.False(Match(Row() with{Credito=0}).PodeBaixar);Assert.False(Match(Row() with{Data=null}).PodeBaixar);Assert.False(Match(Row() with{Debito=1}).PodeBaixar);
        var n=ConciliadorBaixasTests.Nota();n.Cancelada=true;Assert.False(Match(note:n).PodeBaixar);n.Cancelada=false;n.Excluida=true;Assert.False(Match(note:n).PodeBaixar);n.Excluida=false;n.EstaPago=true;Assert.False(Match(note:n).PodeBaixar);
        n.EstaPago=false;n.DataPagamentoEfetivo=new(2026,1,1);Assert.False(Match(note:n).PodeBaixar);
    }

    [Fact] public void Custom_pending_schedule_is_not_overwritten()
    {
        var n=ConciliadorBaixasTests.Nota();n.Parcelas.Add(new(){NumeroParcela=1,ValorParcela=100,DataVencimentoParcela=n.DataEmissao.AddDays(45)});
        Assert.False(Match(note:n).PodeBaixar);
    }
    [Fact] public void Confirmed_count_and_payment_fingerprint_are_bound_to_preview()
    {
        var six=Match();var three=Match(Row("CONFIRMADO_CRED_3_V1"));
        Assert.Equal(6,six.NumeroParcelasConfirmadas);Assert.Equal(3,three.NumeroParcelasConfirmadas);Assert.NotEqual(six.Impressao,three.Impressao);
        Assert.Equal("Cartão de Crédito (6x)",six.TipoPagamento);
        Assert.Equal("Cartão de Débito (1x)",Match(Row("CONFIRMADO_DEB_1_V1")).TipoPagamento);
    }
    [Fact] public void Invalid_rounding_or_overflowing_dates_are_blocked()
    {
        var n=ConciliadorBaixasTests.Nota();n.ValorLiquidoNfse=0.01m;
        Assert.False(Match(Row() with{Credito=0.01m},n).PodeBaixar);
        Assert.False(Match(Row() with{Data=DateTime.MaxValue.Date}).PodeBaixar);
    }
    [Fact] public void Duplicate_note_numbers_and_wrong_provider_are_blocked()
    {
        var n=ConciliadorBaixasTests.Nota();n.Id=2;
        var p=new ConciliadorBaixas().Conciliar(new("T",[Row()],[]),[ConciliadorBaixasTests.Nota(),n],Payments(),ConciliadorBaixasTests.Cnpj);
        Assert.False(Assert.Single(p.Itens).PodeBaixar);n.PrestadorCnpj="99999999000199";Assert.False(Match(note:n).PodeBaixar);
    }
    [Fact] public void Duplicate_rows_or_manual_redirection_are_blocked()
    {
        var p=new ConciliadorBaixas().Conciliar(new("T",[Row(),Row() with{Id=2}],[]),[ConciliadorBaixasTests.Nota()],Payments(),ConciliadorBaixasTests.Cnpj);Assert.All(p.Itens,i=>Assert.False(i.PodeBaixar));
        var n=ConciliadorBaixasTests.Nota();n.Numero="8001";Assert.False(Match(note:n,manual:true).PodeBaixar);
    }
}

