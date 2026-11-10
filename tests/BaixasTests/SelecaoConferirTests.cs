using NfseGerenciador.Models.ImportacaoBaixas;
using NfseGerenciador.ViewModels;
using Xunit;
namespace NfseGerenciador.Tests.ImportacaoBaixas;
public class SelecaoConferirTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public void Date_or_name_warning_can_be_selected_without_individual_review(bool oldDate)
    {
        var row=ConciliadorBaixasTests.Linha() with{Historico="NOTA 8000 - OUTRO NOME",Data=oldDate ? new DateTime(2025,1,1) : new DateTime(2026,10,15)};
        var item=ConciliadorBaixasTests.Match(row);
        Assert.Equal(StatusConciliacao.Revisao,item.Status);Assert.True(item.PodeBaixar);Assert.NotNull(item.PagamentoId);Assert.Null(item.Conferencia);
        var line=new LinhaBaixa(item);Assert.False(line.Selecionada);line.Selecionada=true;Assert.True(line.Selecionada);
    }
    [Theory] [InlineData("amount")] [InlineData("missing")] [InlineData("installments")] [InlineData("payment")]
    public void Other_blocking_conditions_cannot_be_hidden_by_a_date_warning(string scenario)
    {
        var row=ConciliadorBaixasTests.Linha() with{Data=new DateTime(2025,1,1)};var n=ConciliadorBaixasTests.Nota();
        if(scenario=="amount")row=row with{Credito=99};
        if(scenario=="missing")row=row with{Historico="NOTA 9999"};
        if(scenario=="installments")n.Parcelas.Add(new(){NumeroParcela=1,ValorParcela=50});
        if(scenario=="payment")row=row with{Contrapartida="999"};
        var item=ConciliadorBaixasTests.Match(row,n);Assert.False(item.PodeBaixar);
        var line=new LinhaBaixa(item);line.Selecionada=true;Assert.False(line.Selecionada);
    }
    [Theory] [InlineData("NOTA 8000")] [InlineData("SIPAG NOTA 8000")]
    public void Paid_note_is_marked_for_removal_even_if_report_line_has_other_warnings(string history)
    {
        var n=ConciliadorBaixasTests.Nota();n.EstaPago=true;
        var item=ConciliadorBaixasTests.Match(ConciliadorBaixasTests.Linha() with{Historico=history},n);
        Assert.True(item.JaBaixada);Assert.False(item.PodeBaixar);
    }
}
