using Microsoft.EntityFrameworkCore;
using NfseGerenciador.Models;
using NfseGerenciador.Services;
using Xunit;
namespace NfseGerenciador.Tests.ImportacaoBaixas;
public class BaixaEmpresasClassificadasSqlTests
{
    private static async Task Setup(SqlServerFixture f)
    {
        await f.InitializeAsync();await using var db=f.CreateDbContext();db.EmpresasClassificadas.Add(BaixaEmpresasClassificadasTests.Empresa());
        db.EmpresasClassificadas.Add(BaixaEmpresasClassificadasTests.Empresa("Convênio","99888777000166"));
        var n=BaixaEmpresasClassificadasTests.Nota();n.Id=0;n.Parcelas.Add(new(){NumeroParcela=1,ValorParcela=100,DataVencimentoParcela=n.DataEmissao.AddDays(30)});db.Nfses.Add(n);
        var other=BaixaEmpresasClassificadasTests.Nota();other.Id=0;other.Numero="8001";other.TomadorCpfCnpj="99888777000166";other.PrestadorCnpj="98765432000199";other.Local=LocalPagamento.Laboratorio;db.Nfses.Add(other);await db.SaveChangesAsync();
    }
    [SqlServerFact] public async Task Pays_all_providers_preserves_sector_and_uses_issue_date_with_audit()
    {
        await using var f=new SqlServerFixture();await Setup(f);var result=await new BaixaEmpresasClassificadasService(f).BaixarAsync();Assert.Equal(2,result.Quantidade);Assert.Equal(200m,result.Valor);
        await using var db=f.CreateDbContext();var notes=await db.Nfses.Include(n=>n.Parcelas).OrderBy(n=>n.Numero).ToListAsync();
        Assert.Equal(6,notes[0].PagamentoId);Assert.Equal(7,notes[1].PagamentoId);Assert.Equal(LocalPagamento.Radiologia,notes[0].Local);Assert.Equal(LocalPagamento.Laboratorio,notes[1].Local);
        Assert.All(notes,n=>{Assert.True(n.EstaPago);Assert.Equal(n.DataEmissao.Date,n.DataPagamentoEfetivo);Assert.Equal(n.DataEmissao.Date,n.DataPrevistaRecebimento);var p=Assert.Single(n.Parcelas);Assert.True(p.ParcelaPaga);Assert.Equal(n.DataEmissao.Date,p.DataPagamentoParcela);Assert.Equal(n.DataEmissao.Date,p.DataVencimentoParcela);Assert.Equal(n.ValorLiquidoNfse,p.ValorParcela);});
        Assert.Equal("Plano de Saúde",notes[0].ClassificacaoEspecial);Assert.Equal("Convênio",notes[1].ClassificacaoEspecial);Assert.Equal(2,await db.LotesBaixas.CountAsync());Assert.Equal(2,await db.RegistrosBaixas.CountAsync());Assert.Equal(7,await db.Pagamentos.CountAsync());
        Assert.All(await db.RegistrosBaixas.ToListAsync(),r=>{Assert.NotEmpty(r.EstadoAnterior);Assert.Contains("Classificacao",r.EstadoPosterior);Assert.Equal("EMPRESAS_CLASSIFICADAS_V1",r.Contrapartida);});
    }
    [SqlServerFact] public async Task Repeated_click_and_manually_reset_note_do_not_duplicate_audit()
    {
        await using var f=new SqlServerFixture();await Setup(f);var service=new BaixaEmpresasClassificadasService(f);await service.BaixarAsync();Assert.Equal(0,(await service.BaixarAsync()).Quantidade);
        await using(var db=f.CreateDbContext()){var n=await db.Nfses.Include(n=>n.Parcelas).FirstAsync();n.EstaPago=false;n.PagamentoId=null;n.DataPagamentoEfetivo=null;db.NfseParcelas.RemoveRange(n.Parcelas);n.Parcelas.Clear();await db.SaveChangesAsync();}
        Assert.Equal(0,(await service.BaixarAsync()).Quantidade);await using var check=f.CreateDbContext();Assert.Equal(2,await check.RegistrosBaixas.CountAsync());Assert.Equal(2,await check.LotesBaixas.CountAsync());
    }
    [SqlServerFact] public async Task Missing_type_and_protected_history_are_reported_and_preserved()
    {
        await using var f=new SqlServerFixture();await Setup(f);
        await using(var db=f.CreateDbContext()){db.Pagamentos.Remove(await db.Pagamentos.SingleAsync(p=>p.Id==7));var n=await db.Nfses.Include(n=>n.Parcelas).SingleAsync(n=>n.Numero=="8000");n.Parcelas[0].ParcelaPaga=true;n.Parcelas[0].DataPagamentoParcela=new(2026,1,1);await db.SaveChangesAsync();}
        var result=await new BaixaEmpresasClassificadasService(f).BaixarAsync();Assert.Equal(0,result.Quantidade);Assert.Equal(2,result.Ignoradas.Values.Sum());
        await using var check=f.CreateDbContext();Assert.Empty(await check.RegistrosBaixas.ToListAsync());Assert.True((await check.NfseParcelas.SingleAsync()).ParcelaPaga);Assert.All(await check.Nfses.ToListAsync(),n=>Assert.False(n.EstaPago));
    }
    [SqlServerFact] public async Task Audit_failure_rolls_back_notes_parcels_and_lots()
    {
        await using var f=new SqlServerFixture();await Setup(f);
        await using(var db=f.CreateDbContext())await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectCompanyReceipt ON RegistrosBaixas AFTER INSERT AS THROW 51000, 'Induced failure', 1;");
        await Assert.ThrowsAnyAsync<Exception>(()=>new BaixaEmpresasClassificadasService(f).BaixarAsync());
        await using var check=f.CreateDbContext();Assert.All(await check.Nfses.ToListAsync(),n=>Assert.False(n.EstaPago));Assert.False((await check.NfseParcelas.SingleAsync()).ParcelaPaga);Assert.Empty(await check.LotesBaixas.ToListAsync());Assert.Empty(await check.RegistrosBaixas.ToListAsync());
    }
    [SqlServerFact] public async Task New_import_can_be_paid_on_a_later_click()
    {
        await using var f=new SqlServerFixture();await Setup(f);var service=new BaixaEmpresasClassificadasService(f);await service.BaixarAsync();
        await using(var db=f.CreateDbContext()){var n=BaixaEmpresasClassificadasTests.Nota();n.Id=0;n.Numero="8002";db.Nfses.Add(n);await db.SaveChangesAsync();}
        Assert.Equal(1,(await service.BaixarAsync()).Quantidade);
    }
}

