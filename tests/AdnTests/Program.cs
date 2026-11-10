using NfseGerenciador.Services;
using System.Net;
using System.Text.Json;

if (args.FirstOrDefault() == "--fake-worker")
{
    var mode = args[1];
    while (await Console.In.ReadLineAsync() is { } line)
    {
        var request = JsonSerializer.Deserialize<AdnWorkerRequest>(line)!;
        if (request.Command == "Select")
        {
            Console.WriteLine(JsonSerializer.Serialize(new AdnWorkerResponse(null, null, Selected: mode != "cancel", Cancelled: mode == "cancel")));
            continue;
        }
        if (mode == "exit") return;
        if (mode == "hang") { await Task.Delay(30000); return; }
        if (mode == "malformed") { Console.WriteLine("invalid-json"); return; }
        Console.WriteLine(JsonSerializer.Serialize(mode == "error"
            ? new AdnWorkerResponse(null, "Falha TLS simulada")
            : new AdnWorkerResponse([new() { NSU=request.Nsu+1, ArquivoXml="sintetico" }], null)));
    }
    return;
}

const string key = "12345678901234567890123456789012345678901234567890";
string Xml(string date = "2026-11-01T00:30:00-03:00", string emit = "12345678000195") => $"""
<NFSe xmlns="http://www.sped.fazenda.gov.br/nfse"><infNFSe Id="NFS{key}">
<nNFSe>42</nNFSe><dhProc>{date}</dhProc>
<emit><CNPJ>{emit}</CNPJ><xNome>Emitente sintético</xNome></emit>
<valores><vLiq>123.45</vLiq></valores>
<DPS><infDPS><dhEmi>2026-10-31T20:00:00-03:00</dhEmi><nDPS>999</nDPS><toma><CPF>12345678901</CPF><xNome>Tomador sintético</xNome></toma></infDPS></DPS>
</infNFSe></NFSe>
""";
void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
var n = NacionalXml.Parse(Xml(), key);
Check(n.Numero == "42", "Usa número da NFS-e, não da DPS");
Check(n.DataEmissao == new DateTime(2026,11,1,0,30,0), "Preserva data local da emissão");
Check(n.ValorLiquidoNfse == 123.45m && n.ChaveAcessoNacional == key, "Valor líquido e chave");
Check(NacionalXml.ShouldImport(n, "12345678000195"), "Inclui limite de novembro");
Check(!NacionalXml.ShouldImport(NacionalXml.Parse(Xml("2026-10-31T23:59:59-03:00"),key), "12345678000195"), "Exclui outubro");
Check(!NacionalXml.ShouldImport(n,"98765432000198"), "Exclui notas recebidas");
Check(!NacionalXml.IsCancellation("CANCELAMENTO_INDEFERIDO_ANALISE_FISCAL"), "Indeferimento não cancela");
Check(!NacionalXml.IsCancellation("SOLICITACAO_CANCELAMENTO_ANALISE_FISCAL"), "Solicitação não cancela");
Check(NacionalXml.IsCancellation("CANCELAMENTO_POR_SUBSTITUICAO"), "Substituição cancela anterior");
try { NacionalXml.Parse(Xml().Replace("123.45", "inválido"), key); throw new Exception("Aceitou valor inválido"); } catch (FormatException) { Console.WriteLine("PASS rejeita valor inválido"); }
try { NacionalXml.Parse(Xml(), new string('9',50)); throw new Exception("Aceitou chave divergente"); } catch (InvalidDataException) { Console.WriteLine("PASS rejeita chave divergente"); }

async Task<List<AdnDocument>> Query(HttpStatusCode code, AdnResponse response)
{
    using var http = new HttpClient(new FakeHandler(code, JsonSerializer.Serialize(response)));
    return await new AdnClient(http).ReadAsync(50,"12345678000195",CancellationToken.None);
}
var end = new AdnResponse { TipoAmbiente="PRODUCAO", StatusProcessamento="NENHUM_DOCUMENTO_LOCALIZADO", Erros=[new() { Codigo="E2220" }] };
Check((await Query(HttpStatusCode.NotFound,end)).Count == 0, "Fim documentado encerra paginação");
end.Erros[0].Codigo = "E2245";
try { await Query(HttpStatusCode.NotFound,end); throw new Exception("Procuração tratada como fim"); }
catch (InvalidOperationException) { Console.WriteLine("PASS erro de procuração interrompe"); }
var batch = new AdnResponse { TipoAmbiente="PRODUCAO", StatusProcessamento="DOCUMENTOS_LOCALIZADOS", LoteDFe=[new() { NSU=51 }, new() { NSU=52 }] };
using (var httpNull = new HttpClient(new FakeHandler(HttpStatusCode.OK,
    """{"TipoAmbiente":"PRODUCAO","StatusProcessamento":"DOCUMENTOS_LOCALIZADOS","Erros":null,"LoteDFe":[{"NSU":51}]}""")))
    Check((await new AdnClient(httpNull).ReadAsync(50,"12345678000195",CancellationToken.None)).Count == 1, "Aceita Erros null conforme OpenAPI");
Check((await Query(HttpStatusCode.OK,batch)).Last().NSU == 52,"Avança lote sem confundir NSU e data");
batch.LoteDFe[0].NSU=50;
try { await Query(HttpStatusCode.OK,batch); throw new Exception("Aceitou lote repetido"); }
catch (InvalidDataException) { Console.WriteLine("PASS rejeita lote que não avança"); }

var failureSequence = new List<string>();
var windowError = new System.ComponentModel.Win32Exception(0);
await ImportWindowGuard.RunAsync(
    () => throw windowError,
    () => { failureSequence.Add("stop"); return Task.CompletedTask; },
    error => { Check(ReferenceEquals(error,windowError), "Preserva erro original da janela"); failureSequence.Add("report"); });
Check(failureSequence.SequenceEqual(new[] { "stop", "report" }), "Falha Win32 interrompe operação e informa sem escapar");
await ImportWindowGuard.RunAsync(() => {}, () => throw new Exception("Não deve interromper"), _ => throw new Exception("Não deve informar erro"));
Console.WriteLine("PASS fechamento normal não cancela nem reporta falha");
var cleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
bool reportedBeforeCleanup = false;
var guarded = ImportWindowGuard.RunAsync(() => throw new System.ComponentModel.Win32Exception(0),
    () => cleanup.Task, _ => reportedBeforeCleanup = true);
Check(reportedBeforeCleanup && !guarded.IsCompleted, "Informa falha sem esperar cartão responder ao cancelamento");
cleanup.SetResult();
await guarded;

System.Diagnostics.ProcessStartInfo FakeWorker(string mode)
{
    var info = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!);
    if (Path.GetFileNameWithoutExtension(Environment.ProcessPath!).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        info.ArgumentList.Add(typeof(FakeHandler).Assembly.Location);
    info.ArgumentList.Add("--fake-worker"); info.ArgumentList.Add(mode);
    return info;
}
using (var selection = new AdnProcessClient(FakeWorker("success")))
{
    await selection.SelectAsync(default);
    Check((await selection.ReadAsync(0, "12345678000195", default)).Count == 1, "Selecao no auxiliar permite consulta posterior");
}
using (var selection = new AdnProcessClient(FakeWorker("cancel")))
{
    try { await selection.SelectAsync(default); throw new Exception("Cancelamento da selecao ignorado"); }
    catch (OperationCanceledException) { Console.WriteLine("PASS cancelar selecao nao vira lote vazio ou erro TLS"); }
}
using (var worker = new AdnProcessClient(FakeWorker("success")))
{
    Check((await worker.ReadAsync(50, "12345678000195", default))[0].NSU == 51, "IPC retorna lote do processo filho");
    Check((await worker.ReadAsync(51, "12345678000195", default))[0].NSU == 52, "Reutiliza processo entre lotes");
}
using (var worker = new AdnProcessClient(FakeWorker("error")))
{
    try { await worker.ReadAsync(0, "12345678000195", default); throw new Exception("Erro ignorado"); }
    catch (InvalidOperationException ex) { Check(ex.Message.Contains("Falha TLS simulada"), "Preserva falha TLS do processo filho"); }
}
using (var worker = new AdnProcessClient(FakeWorker("exit")))
{
    try { await worker.ReadAsync(0, "12345678000195", default); throw new Exception("Saida ignorada"); }
    catch (IOException) { Console.WriteLine("PASS encerramento do filho interrompe sem lote vazio"); }
}
using (var worker = new AdnProcessClient(FakeWorker("malformed")))
{
    try { await worker.ReadAsync(0, "12345678000195", default); throw new Exception("JSON invalido aceito"); }
    catch (JsonException) { Console.WriteLine("PASS rejeita resposta IPC malformada"); }
}
using (var worker = new AdnProcessClient(FakeWorker("hang")))
using (var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(500)))
{
    var watch = System.Diagnostics.Stopwatch.StartNew();
    try { await worker.ReadAsync(0, "12345678000195", cancel.Token); throw new Exception("Cancelamento ignorado"); }
    catch (OperationCanceledException) { Check(watch.Elapsed < TimeSpan.FromSeconds(5), "Cancelar interrompe espera do PIN sem travar interface"); }
}

// Optional local verification; user data is never committed or used by CI.
foreach (var path in args)
{
    var sample = JsonSerializer.Deserialize<AdnResponse>(File.ReadAllText(path))!;
    int parsed = 0;
    foreach (var d in sample.LoteDFe.Where(x => x.TipoDocumento == "NFSE"))
    {
        var note = NacionalXml.Parse(NacionalXml.Decode(d.ArquivoXml), d.ChaveAcesso);
        if (string.IsNullOrWhiteSpace(note.Numero)) throw new Exception("Número ausente no lote real");
        parsed++;
    }
    Console.WriteLine($"PASS lote local: {parsed} XMLs decodificados e lidos");
}

sealed class FakeHandler(HttpStatusCode code, string body) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!request.RequestUri!.Query.Contains("cnpjConsulta=12345678000195")) throw new Exception("CNPJ ausente");
        return Task.FromResult(new HttpResponseMessage(code) { Content=new StringContent(body) });
    }
}
