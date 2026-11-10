namespace NfseGerenciador.Services;

/// <summary>Identidades já examinadas no lote, inclusive antes do SaveChanges.</summary>
public sealed class ChavesImportacaoXml
{
    private readonly HashSet<(string Numero, string PrestadorCnpj)> _chaves = new();

    // Mantém a identidade usada pela importação legada: número e CNPJ do prestador.
    public bool Registrar(string numero, string prestadorCnpj) =>
        _chaves.Add((numero, prestadorCnpj));
}
