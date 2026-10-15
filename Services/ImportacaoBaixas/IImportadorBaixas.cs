using NfseGerenciador.Models.ImportacaoBaixas;
namespace NfseGerenciador.Services.ImportacaoBaixas;
public interface IImportadorBaixas
{
    Task<PreviaBaixas> PrepararAsync(string caminho,string prestadorCnpj,CancellationToken ct=default);
    Task<ResultadoImportacao> ConfirmarAsync(PreviaBaixas previa,IReadOnlyCollection<int> linhasSelecionadas,CancellationToken ct=default);
}

