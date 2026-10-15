using Microsoft.EntityFrameworkCore;
using NfseGerenciador.Models.ImportacaoBaixas;
using NfseGerenciador.Services.ImportacaoBaixas;
using Xunit;
namespace NfseGerenciador.Tests.ImportacaoBaixas;
public class ImportadorBaixasTests
{
    private sealed class NoDatabase : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => throw new Exception("Não deve abrir banco para seleção inválida.");
    }
    [Theory] [InlineData(0)] [InlineData(99)] [InlineData(-1)]
    public async Task Rejects_invalid_selection_before_database(int id)
    {
        var p=new PreviaBaixas(new("HASH",[],[]),"12345678000190",[]);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>new ImportadorBaixas(new NoDatabase()).ConfirmarAsync(p,id==0?[]:[id]));
    }
    [Fact] public async Task Cannot_confirm_a_review_row()
    {
        var row=ConciliadorBaixasTests.Linha() with { Credito=50 };
        var p=new ConciliadorBaixas().Conciliar(new("HASH",[row],[]),[ConciliadorBaixasTests.Nota()],ConciliadorBaixasTests.Pagamentos(),ConciliadorBaixasTests.Cnpj);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>new ImportadorBaixas(new NoDatabase()).ConfirmarAsync(p,[1]));
    }
}
