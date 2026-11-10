using Microsoft.EntityFrameworkCore;
using NfseGerenciador.Models;
namespace NfseGerenciador.Services;

internal sealed record FiltroConsultaNotas(string? Status, string? Texto, string? Local, DateTime? Inicio, DateTime? Fim, int Pagina, int Tamanho);
internal sealed record PaginaNotas(IReadOnlyList<Nfse> Notas, int Total, int Pagina);
internal sealed class ConsultaNotasService(IDbContextFactory<ApplicationDbContext> factory)
{
    public async Task<PaginaNotas> CarregarAsync(FiltroConsultaNotas filtro)
    {
        if (filtro.Tamanho <= 0) throw new ArgumentOutOfRangeException(nameof(filtro.Tamanho));
        if (filtro.Inicio?.Date > filtro.Fim?.Date) throw new InvalidOperationException("A data inicial deve ser anterior ou igual à data final.");
        await using var db = await factory.CreateDbContextAsync();
        IQueryable<Nfse> query = db.Nfses.AsNoTracking().Where(n => !n.Excluida).Include(n => n.Pagamento);
        if (filtro.Status == "Pagas") query = query.Where(n => n.EstaPago);
        else if (filtro.Status == "Em Aberto") query = query.Where(n => !n.EstaPago && !n.Cancelada);
        if (Enum.TryParse<LocalPagamento>(filtro.Local, out var local)) query = query.Where(n => n.Local == local);
        var inicio = filtro.Inicio?.Date;
        var fim = filtro.Fim?.Date;
        // Capture boundaries before composing SQL; filter edits cannot mutate an in-flight query.
        DateTime? limite = fim.HasValue && fim.Value < DateTime.MaxValue.Date ? fim.Value.AddDays(1) : null;
        if (filtro.Status == "Pagas")
        {
            if (inicio.HasValue) query = query.Where(n => n.DataPagamentoEfetivo >= inicio.Value);
            if (limite.HasValue) query = query.Where(n => n.DataPagamentoEfetivo < limite.Value);
        }
        else
        {
            if (inicio.HasValue) query = query.Where(n => n.DataEmissao >= inicio.Value);
            if (limite.HasValue) query = query.Where(n => n.DataEmissao < limite.Value);
        }
        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var texto = filtro.Texto.Trim().ToLowerInvariant();
            var documento = new string(texto.Where(char.IsDigit).ToArray());
            bool buscaDocumento = (documento.Length is 11 or 14) && texto.All(c => char.IsDigit(c) || c is '.' or '-' or '/' or ' ');
            query = query.Where(n => (n.TomadorRazaoSocial != null && n.TomadorRazaoSocial.ToLower().Contains(texto)) ||
                n.Numero.Contains(texto) || (buscaDocumento && n.TomadorCpfCnpj != null && n.TomadorCpfCnpj.Replace(".", "").Replace("-", "").Replace("/", "").Replace(" ", "") == documento));
        }
        int total = await query.CountAsync();
        int pagina = Math.Clamp(filtro.Pagina, 1, Math.Max(1, (int)Math.Ceiling((double)total / filtro.Tamanho)));
        var notas = await query.OrderByDescending(n => n.DataEmissao).ThenBy(n => n.Numero).ThenBy(n => n.Id)
            .Skip((pagina - 1) * filtro.Tamanho).Take(filtro.Tamanho).ToListAsync();
        return new(notas, total, pagina);
    }
}
