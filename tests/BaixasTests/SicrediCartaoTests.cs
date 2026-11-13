using NfseGerenciador.Models;
using NfseGerenciador.Models.ImportacaoBaixas;
using NfseGerenciador.Services.ImportacaoBaixas;
using NfseGerenciador.ViewModels;
using Xunit;

namespace NfseGerenciador.Tests.ImportacaoBaixas;

public class SicrediCartaoTests
{
    private const string Marker = "SICREDI_CARTAO_UNICO_PAGO_V1";
    private static LancamentoRelatorio Row() => ConciliadorBaixasTests.Linha() with
    {
        Contrapartida = Marker,
        Historico = "CARTAO SICREDI CONCILIADO V1 NF 8000"
    };
    private static Pagamento[] Payments() => [new() { Id = 30, TipoRecebimento = "Cartão", EhAVista = true, NumeroParcelas = 1 }];
    private static ItemPrevia Match(LancamentoRelatorio? row = null, Nfse? note = null,
        Pagamento[]? payments = null, bool manual = false)
    {
        var mapping = manual ? new Dictionary<int, ConferenciaManual> { [1] = new(1, "Comprovante conferido") } : null;
        return Assert.Single(new ConciliadorBaixas().Conciliar(new("TEST", [row ?? Row()], []),
            [note ?? ConciliadorBaixasTests.Nota()], payments ?? Payments(), ConciliadorBaixasTests.Cnpj, mapping).Itens);
    }

    [Fact]
    public void Reconciled_card_requires_explicit_review_and_reuses_existing_payment()
    {
        var item = Match();
        Assert.Equal(StatusConciliacao.Revisao, item.Status);
        Assert.True(item.PermiteBaixaEmLote);
        Assert.True(item.PodeBaixar);
        Assert.Equal(30, item.PagamentoId);
        Assert.Equal("Cartao", item.TipoPagamento);
        Assert.Contains("Sicredi", item.Motivo);
        Assert.Equal(new DateTime(2026, 10, 15), item.Origem.Data);
        var line = new LinhaBaixa(item);
        Assert.False(line.Selecionada);
        line.Selecionada = true;
        Assert.True(line.Selecionada);
    }

    [Fact]
    public void Explicit_manual_review_can_become_eligible() => Assert.Equal(StatusConciliacao.Apta, Match(manual: true).Status);

    [Fact]
    public void Manual_mapping_cannot_redirect_the_explicit_reference()
    {
        var note = ConciliadorBaixasTests.Nota(); note.Numero = "8001";
        Assert.False(Match(note: note, manual: true).PodeBaixar);
    }

    [Theory]
    [InlineData("4889", "CARTAO SICREDI CONCILIADO V1 NF 8000")]
    [InlineData("SICREDI_CARTAO_UNICO_PAGO_V1 ", "CARTAO SICREDI CONCILIADO V1 NF 8000")]
    [InlineData(Marker, "CARTAO NF 8000")]
    [InlineData(Marker, "PIX NF 8000")]
    [InlineData(Marker, "cartao sicredi conciliado v1 NF 8000")]
    [InlineData(Marker, "CARTAO SICREDI CONCILIADO V1 SIPAG NF 8000")]
    [InlineData("4889", "SIPAG NF 8000")]
    public void Invalid_contract_or_generic_card_cannot_be_enabled_by_manual_mapping(string code, string history)
    {
        foreach (bool manual in new[] { false, true })
            Assert.False(Match(Row() with { Contrapartida = code, Historico = history }, manual: manual).PodeBaixar);
    }

    [Theory]
    [InlineData("CARTAO SICREDI CONCILIADO V1 DOC 8000")]
    [InlineData("CARTAO SICREDI CONCILIADO V1 NF 8000 / 8001")]
    [InlineData("CARTAO SICREDI CONCILIADO V1 NF 8000.1")]
    public void Unique_explicit_reference_is_required_even_with_manual_mapping(string history)
    {
        Assert.False(Match(Row() with { Historico = history }, manual: true).PodeBaixar);
    }

    [Theory]
    [InlineData(99.99)] [InlineData(100.01)] [InlineData(0)] [InlineData(-100)]
    public void Exact_positive_amount_is_required(double amount) => Assert.False(Match(Row() with { Credito = (decimal)amount }).PodeBaixar);

    [Fact]
    public void Missing_date_debit_and_reversal_are_blocked()
    {
        Assert.False(Match(Row() with { Data = null }).PodeBaixar);
        Assert.False(Match(Row() with { Debito = 1 }).PodeBaixar);
        Assert.False(Match(Row() with { Historico = Row().Historico + " ESTORNO" }).PodeBaixar);
        Assert.False(Match(Row() with { Erro = "Célula inválida" }).PodeBaixar);
    }

    [Theory]
    [InlineData("Cartao", true, 1, true)]
    [InlineData(" Cartão ", true, null, true)]
    [InlineData("Cartao", false, 1, false)]
    [InlineData("Cartao", true, 2, false)]
    [InlineData("Pix", true, 1, false)]
    public void Only_existing_single_payment_card_terms_are_accepted(string type, bool cash, int? count, bool expected)
    {
        Assert.Equal(expected, Match(payments: [new() { Id = 44, TipoRecebimento = type, EhAVista = cash, NumeroParcelas = count }]).PodeBaixar);
    }

    [Fact]
    public void Missing_card_registration_does_not_create_or_substitute_pix() => Assert.False(Match(payments: ConciliadorBaixasTests.Pagamentos()).PodeBaixar);

    [Fact]
    public void Duplicate_equivalent_card_registrations_choose_lowest_id()
    {
        var payments = Payments().Concat([new Pagamento { Id = 12, TipoRecebimento = "Cartao", EhAVista = true }]).ToArray();
        Assert.Equal(12, Match(payments: payments).PagamentoId);
        Assert.Equal(12, Match(payments: payments.Reverse().ToArray()).PagamentoId);
        Assert.Equal(2, payments.Length);
    }

    [Fact]
    public void Existing_installments_and_previous_payments_remain_blocked()
    {
        var note = ConciliadorBaixasTests.Nota();
        note.Parcelas.Add(new() { NumeroParcela = 1, ValorParcela = 50 });
        note.Parcelas.Add(new() { NumeroParcela = 2, ValorParcela = 50 });
        Assert.False(Match(note: note, manual: true).PodeBaixar);
        note = ConciliadorBaixasTests.Nota();
        note.Parcelas.Add(new() { NumeroParcela = 1, ValorParcela = 100, ParcelaPaga = true });
        Assert.False(Match(note: note, manual: true).PodeBaixar);
        note = ConciliadorBaixasTests.Nota();
        note.DataPagamentoEfetivo = new(2026, 10, 1);
        Assert.False(Match(note: note, manual: true).PodeBaixar);
        note = ConciliadorBaixasTests.Nota();
        note.EstaPago = true;
        Assert.True(Match(note: note).JaBaixada);
    }

    [Fact]
    public void Cancelled_excluded_or_wrong_provider_cannot_be_paid()
    {
        var note = ConciliadorBaixasTests.Nota(); note.Cancelada = true;
        Assert.False(Match(note: note).PodeBaixar);
        note = ConciliadorBaixasTests.Nota(); note.Excluida = true;
        Assert.False(Match(note: note).PodeBaixar);
        note = ConciliadorBaixasTests.Nota(); note.PrestadorCnpj = "99999999000199";
        Assert.False(Match(note: note).PodeBaixar);
    }

    [Fact]
    public void Multiple_rows_or_duplicate_database_numbers_remain_blocked()
    {
        var engine = new ConciliadorBaixas();
        var report = new RelatorioBaixas("TEST", [Row(), Row() with { Id = 2, Linha = 9 }], []);
        Assert.All(engine.Conciliar(report, [ConciliadorBaixasTests.Nota()], Payments(), ConciliadorBaixasTests.Cnpj).Itens, item => Assert.False(item.PodeBaixar));
        var duplicate = ConciliadorBaixasTests.Nota(); duplicate.Id = 2;
        report = new("TEST", [Row()], []);
        Assert.False(Assert.Single(engine.Conciliar(report, [ConciliadorBaixasTests.Nota(), duplicate], Payments(), ConciliadorBaixasTests.Cnpj).Itens).PodeBaixar);
    }
}

