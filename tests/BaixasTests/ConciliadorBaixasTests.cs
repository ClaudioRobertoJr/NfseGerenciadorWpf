using NfseGerenciador.Models;
using NfseGerenciador.Models.ImportacaoBaixas;
using NfseGerenciador.Services.ImportacaoBaixas;
using Xunit;
namespace NfseGerenciador.Tests.ImportacaoBaixas;
public class ConciliadorBaixasTests
{
    internal const string Cnpj = "12345678000190";
    internal static Nfse Nota() => new() { Id=1, Numero="8000", PrestadorCnpj=Cnpj, DataEmissao=new(2026,5,1), ValorLiquidoNfse=100m, TomadorRazaoSocial="PESSOA TESTE", TomadorCpfCnpj="", CodigoVerificacao="ABC", PrestadorRazaoSocial="TESTE" };
    internal static LancamentoRelatorio Linha() => new() { Id=1, Linha=8, Planilha="Dados", Centro="1.1.2.003 ALA PARTICULAR", Conta="620 RECEITAS", Empresa="1", Historico="RECEB. DE PARTICULAR NOTA 8000 - PESSOA TESTE - CF. AV. SICREDI", Data=new(2026,10,15), Contrapartida="4889", Debito=0, Credito=100 };
    internal static Pagamento[] Pagamentos() => [new(){Id=10,TipoRecebimento="Pix",EhAVista=true,NumeroParcelas=1},new(){Id=20,TipoRecebimento="Dinheiro",EhAVista=true,NumeroParcelas=1}];
    internal static ItemPrevia Match(LancamentoRelatorio? row=null,Nfse? nota=null,Pagamento[]? pagamentos=null) =>
        Assert.Single(new ConciliadorBaixas().Conciliar(new("HASH",[row??Linha()],[]),[nota??Nota()],pagamentos??Pagamentos(),Cnpj).Itens);
    [Theory] [InlineData("4889","Pix",10)] [InlineData("230","Pix",10)] [InlineData("132","Dinheiro",20)] [InlineData("108","Dinheiro",20)]
    public void Maps_receipt_and_keeps_report_date(string code,string payment,int id)
    { var item=Match(Linha() with { Contrapartida=code });Assert.Equal(StatusConciliacao.Apta,item.Status);Assert.Equal(payment,item.TipoPagamento);Assert.Equal(id,item.PagamentoId);Assert.Equal(new DateTime(2026,10,15),item.Origem.Data);Assert.Equal(LocalPagamento.Particular,item.Local); }
    [Theory] [InlineData("SIPAG NOTA 8000")] [InlineData("RECEB. CARTÃO NOTA 8000")]
    public void Card_receipts_stay_manual(string history) => Assert.NotEqual(StatusConciliacao.Apta,Match(Linha() with{Historico=history}).Status);
    [Theory] [InlineData("DOC. 8000")] [InlineData("NOTA 8000 E 8001")] [InlineData("NOTA 8000 / 8001")] [InlineData("NOTA 8000,8001")] [InlineData("NOTA 8000 NOTA 8001")]
    public void Ambiguous_or_DOC_numbers_do_not_pay(string history) => Assert.NotEqual(StatusConciliacao.Apta,Match(Linha() with{Historico=history}).Status);
    [Theory] [InlineData("NOTA 8000.1", "8000")] [InlineData("NOTA 8.000", "8")] [InlineData("NOTA 8000 . 1", "8000")]
    public void Dotted_reference_cannot_pay_a_numeric_prefix(string history, string number)
    { var note=Nota(); note.Numero=number; Assert.Equal(StatusConciliacao.Revisao,Match(Linha() with{Historico=history},note).Status); }
    [Theory] [InlineData(99.99)] [InlineData(100.01)] [InlineData(0)] [InlineData(-100)]
    public void Divergent_or_nonpositive_credit_does_not_pay(double amount) => Assert.NotEqual(StatusConciliacao.Apta,Match(Linha() with{Credito=(decimal)amount}).Status);
    [Fact] public void Debit_is_not_a_receipt() => Assert.NotEqual(StatusConciliacao.Apta,Match(Linha() with{Debito=100}).Status);
    [Fact] public void Invalid_cell_does_not_pay() => Assert.Equal(StatusConciliacao.Revisao,Match(Linha() with{Erro="valor inválido"}).Status);
    [Fact] public void Unknown_payment_code_requires_review() => Assert.Equal(StatusConciliacao.Revisao,Match(Linha() with{Contrapartida="999"}).Status);
    [Fact] public void Missing_date_requires_review() => Assert.Equal(StatusConciliacao.Revisao,Match(Linha() with{Data=null}).Status);
    [Fact] public void Receipt_before_issue_requires_review() => Assert.Equal(StatusConciliacao.Revisao,Match(Linha() with{Data=new(2026,4,30)}).Status);
    [Fact] public void Different_named_customer_requires_review() => Assert.Equal(StatusConciliacao.Revisao,Match(Linha() with{Historico="NOTA 8000 - OUTRA PESSOA"}).Status);
    [Fact] public void Already_paid_is_ignored() {var n=Nota();n.EstaPago=true;Assert.Equal(StatusConciliacao.Ignorada,Match(nota:n).Status);}
    [Fact] public void Excluded_is_not_paid() {var n=Nota();n.Excluida=true;Assert.NotEqual(StatusConciliacao.Apta,Match(nota:n).Status);}
    [Fact] public void Standard_pending_parcel_is_eligible()
    {var n=Nota();n.Parcelas.Add(new(){Id=3,NumeroParcela=1,ValorParcela=100,ParcelaPaga=false});Assert.Equal(StatusConciliacao.Apta,Match(nota:n).Status);}
    [Fact] public void Paid_parcel_is_not_overwritten()
    {var n=Nota();n.Parcelas.Add(new(){Id=3,NumeroParcela=1,ValorParcela=100,ParcelaPaga=true});Assert.Equal(StatusConciliacao.Revisao,Match(nota:n).Status);}
    [Fact] public void Installments_stay_manual()
    {var n=Nota();n.Parcelas.Add(new(){NumeroParcela=1,ValorParcela=50});n.Parcelas.Add(new(){NumeroParcela=2,ValorParcela=50});Assert.Equal(StatusConciliacao.Revisao,Match(nota:n).Status);}
    [Fact] public void Multiple_report_rows_are_not_summed()
    {var p=new ConciliadorBaixas().Conciliar(new("H",[Linha() with{Credito=50},Linha() with{Id=2,Linha=9,Credito=50}],[]),[Nota()],Pagamentos(),Cnpj);Assert.All(p.Itens,x=>Assert.Equal(StatusConciliacao.Revisao,x.Status));}
    [Fact] public void Duplicate_database_numbers_require_review()
    {var n=Nota();n.Id=2;var p=new ConciliadorBaixas().Conciliar(new("H",[Linha()],[]),[Nota(),n],Pagamentos(),Cnpj);Assert.Equal(StatusConciliacao.Revisao,p.Itens[0].Status);}
    [Fact] public void Limits_match_to_selected_provider()
    {var n=Nota();n.Id=2;n.PrestadorCnpj="99999999000199";var p=new ConciliadorBaixas().Conciliar(new("H",[Linha()],[]),[Nota(),n],Pagamentos(),Cnpj);Assert.Equal(1,p.Itens[0].NfseId);Assert.Equal(StatusConciliacao.Apta,p.Itens[0].Status);}
    [Fact] public void Missing_payment_is_not_assumed()
    {Assert.Equal(StatusConciliacao.Revisao,Match(pagamentos:[]).Status);}
    [Fact] public void Equivalent_cash_duplicates_choose_lowest_id_independent_of_order_without_changing_records()
    {
        var payments=Enumerable.Range(1,1102).Select(id=>new Pagamento{Id=id,TipoRecebimento=id==1?"Dinheiro":"DINHEIRO",EhAVista=true,NumeroParcelas=1}).Reverse().ToArray();
        foreach(var source in new[]{payments,payments.Reverse().ToArray()}){
            var item=Match(Linha() with{Contrapartida="132"},pagamentos:source);
            Assert.Equal(StatusConciliacao.Apta,item.Status);Assert.Equal(1,item.PagamentoId);
        }
        Assert.Equal(1102,payments.Length);Assert.All(payments,p=>Assert.Equal(1,p.NumeroParcelas));
    }
    [Fact] public void Canonical_payment_must_still_match_type_and_cash_terms()
    {
        Pagamento[] payments=[new(){Id=1,TipoRecebimento="Dinheiro",EhAVista=false,NumeroParcelas=1},new(){Id=2,TipoRecebimento="Dinheiro",EhAVista=true,NumeroParcelas=2},new(){Id=3,TipoRecebimento="Pix",EhAVista=true,NumeroParcelas=1},new(){Id=20,TipoRecebimento=" DINHEIRO ",EhAVista=true,NumeroParcelas=null},new(){Id=30,TipoRecebimento="Dinheiro",EhAVista=true,NumeroParcelas=1}];
        Assert.Equal(20,Match(Linha() with{Contrapartida="108"},pagamentos:payments).PagamentoId);
        Assert.False(Match(Linha() with{Contrapartida="108"},pagamentos:payments.Take(3).ToArray()).PodeBaixar);
    }
    [Fact] public void Laboratory_is_preserved() => Assert.Equal(LocalPagamento.Laboratorio,Match(Linha() with{Centro="1.1.2.035 LABORATORIO DE ANALISES CLINIC"}).Local);
    [Fact] public void Fingerprint_ignores_file_position_but_not_payment_details()
    {var a=Match();var b=Match(Linha() with{Id=500,Linha=99,Planilha="Outra"});var c=Match(Linha() with{Data=new(2026,10,14)});Assert.Equal(a.Impressao,b.Impressao);Assert.NotEqual(a.Impressao,c.Impressao);}
}

