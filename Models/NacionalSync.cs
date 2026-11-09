namespace NfseGerenciador.Models;

public class NacionalSync
{
    public string Cnpj { get; set; } = "";
    public long UltimoNsu { get; set; }
}

public class NacionalEvento
{
    public string Cnpj { get; set; } = "";
    public long Nsu { get; set; }
    public string Chave { get; set; } = "";
    public string Tipo { get; set; } = "";
    public bool Cancela { get; set; }
}
