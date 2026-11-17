using Microsoft.EntityFrameworkCore;
using NfseGerenciador.Models;
namespace NfseGerenciador.Services;

public sealed record ResultadoBaixaEmpresas(int Quantidade, decimal Valor, IReadOnlyDictionary<string,int> Ignoradas);
internal sealed record CandidatoBaixaEmpresa(Nfse Nota, string? Classificacao, int? PagamentoId, string? Motivo);

public sealed class BaixaEmpresasClassificadasService(IDbContextFactory<ApplicationDbContext> factory)
{
    internal static IReadOnlyList<CandidatoBaixaEmpresa> Planejar(IReadOnlyList<Nfse> notas, IReadOnlyList<EmpresaClassificada> empresas, IReadOnlyList<Pagamento> pagamentos)
        => notas.Select(n=>new CandidatoBaixaEmpresa(n,null,null,"Não implementado")).ToList();
    public Task<ResultadoBaixaEmpresas> BaixarAsync(CancellationToken ct=default)
        => Task.FromResult(new ResultadoBaixaEmpresas(0,0,new Dictionary<string,int>()));
}

