using NfseGerenciador.Services;

// Synthetic test-only protocol. Never included in the application or the real worker.
public sealed record AdnTestBatch(long Esperado, List<AdnDocument> Documentos);
