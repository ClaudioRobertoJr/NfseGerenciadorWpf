using NfseGerenciador.Models;
using NfseGerenciador.Services;
using Xunit;
namespace NfseGerenciador.Tests.ImportacaoBaixas;
public class BaixaEmpresasClassificadasTests
{
    internal const string Company="11222333000144";
    internal static Nfse Nota() {var n=ConciliadorBaixasTests.Nota();n.TomadorCpfCnpj="11.222.333/0001-44";n.DataEmissao=new(2026,5,1,14,30,0);n.Local=LocalPagamento.Radiologia;return n;}
    internal static EmpresaClassificada Empresa(string type="Plano de Saúde",string cnpj=Company) => new(){Cnpj=cnpj,RazaoSocial="EMPRESA TESTE",TipoClassificacao=type};
    internal static Pagamento[] Pagamentos() => [new(){Id=6,TipoRecebimento="Plano de Saúde",EhAVista=true,NumeroParcelas=1},new(){Id=7,TipoRecebimento="Convênio",EhAVista=true}];
    private static CandidatoBaixaEmpresa Plan(Nfse? note=null,EmpresaClassificada[]? companies=null,Pagamento[]? payments=null) => Assert.Single(BaixaEmpresasClassificadasService.Planejar([note??Nota()],companies??[Empresa()],payments??Pagamentos()));

    [Theory] [InlineData("PLANO DE SAUDE",6,"Plano de Saúde")] [InlineData("CONVENIO",7,"Convênio")]
    public void Matches_formatted_company_document_and_exact_normalized_type(string type,int id,string canonical)
    {var item=Plan(companies:[Empresa(type)]);Assert.Null(item.Motivo);Assert.Equal(id,item.PagamentoId);Assert.Equal(canonical,item.Classificacao);Assert.Equal(LocalPagamento.Radiologia,item.Nota.Local);}
    [Fact] public void Unknown_classification_or_unregistered_document_cannot_pay()
    {Assert.NotNull(Plan(companies:[Empresa("Outro")]).Motivo);Assert.NotNull(Plan(companies:[]).Motivo);var n=Nota();n.TomadorCpfCnpj="11222333000";Assert.NotNull(Plan(n).Motivo);}
    [Fact] public void Duplicate_normalized_company_document_is_ambiguous_even_with_equal_types()
    {Assert.NotNull(Plan(companies:[Empresa(),Empresa(cnpj:"11.222.333/0001-44")]).Motivo);}
    [Fact] public void Missing_or_installment_payment_type_is_not_created_or_substituted()
    {Assert.NotNull(Plan(payments:[]).Motivo);Assert.NotNull(Plan(payments:[new(){Id=7,TipoRecebimento="Convênio",EhAVista=true}]).Motivo);Assert.NotNull(Plan(payments:[new(){Id=6,TipoRecebimento="Plano de Saúde",EhAVista=false}]).Motivo);Assert.NotNull(Plan(payments:[new(){Id=6,TipoRecebimento="Plano de Saúde",EhAVista=true,NumeroParcelas=2}]).Motivo);}
    [Fact] public void Prior_payment_or_custom_schedule_is_never_replaced()
    {
        var n=Nota();n.Parcelas.Add(new(){NumeroParcela=1,ValorParcela=100,DataVencimentoParcela=n.DataEmissao.AddDays(30)});Assert.Null(Plan(n).Motivo);
        n.Parcelas[0].ParcelaPaga=true;Assert.NotNull(Plan(n).Motivo);n.Parcelas[0].ParcelaPaga=false;n.Parcelas[0].DataPagamentoParcela=n.DataEmissao;Assert.NotNull(Plan(n).Motivo);
        n.Parcelas[0].DataPagamentoParcela=null;n.Parcelas[0].DataVencimentoParcela=n.DataEmissao.AddDays(45);Assert.NotNull(Plan(n).Motivo);
        n.Parcelas.Clear();n.PagamentoId=6;Assert.NotNull(Plan(n).Motivo);n.PagamentoId=null;n.DataPagamentoEfetivo=n.DataEmissao;Assert.NotNull(Plan(n).Motivo);
    }
    [Fact] public void Invalid_note_states_are_not_eligible()
    {var n=Nota();n.Cancelada=true;Assert.NotNull(Plan(n).Motivo);n.Cancelada=false;n.Excluida=true;Assert.NotNull(Plan(n).Motivo);n.Excluida=false;n.EstaPago=true;Assert.NotNull(Plan(n).Motivo);n.EstaPago=false;n.ValorLiquidoNfse=0;Assert.NotNull(Plan(n).Motivo);}
    [Fact] public void Equivalent_payment_duplicates_choose_lowest_id_without_mutation()
    {var p=Pagamentos().Concat([new Pagamento{Id=2,TipoRecebimento="PLANO DE SAUDE",EhAVista=true}]).ToArray();Assert.Equal(2,Plan(payments:p).PagamentoId);Assert.Equal(3,p.Length);}
}

