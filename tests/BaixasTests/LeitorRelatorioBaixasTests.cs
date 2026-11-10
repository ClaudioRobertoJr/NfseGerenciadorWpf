using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using NfseGerenciador.Services.ImportacaoBaixas;
using Xunit;

namespace NfseGerenciador.Tests.ImportacaoBaixas;
public class LeitorRelatorioBaixasTests
{
    internal static MemoryStream Workbook(string date = "46200", string credit = "1234.56", bool date1904 = false, string history = "RECEB. NOTA 8000 - TESTE", bool formula = false, bool missingHeader = false)
    {
        XNamespace n = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XElement Cell(string col, int row, string value, bool number = false) =>
            number ? new XElement(n+"c", new XAttribute("r", col+row), new XElement(n+"v",value)) :
            new XElement(n+"c", new XAttribute("r",col+row),new XAttribute("t","inlineStr"),new XElement(n+"is",new XElement(n+"t",value)));
        string[] headers = ["Centro de Custo...", "Conta ...", "Empresa", "Data", "Lote", "Contra Partida", "Histórico", "Débito", "Crédito"];
        var header = new XElement(n+"row",new XAttribute("r",6),headers.Select((h,i)=>Cell(((char)('A'+i)).ToString(),6,missingHeader && i==8 ? "Outro" : h)));
        var data = new XElement(n+"row",new XAttribute("r",8),
            Cell("A",8,"1.1.2.003 ALA PARTICULAR"),Cell("B",8,"620 RECEITAS"),Cell("C",8,"1",true),
            Cell("D",8,date,double.TryParse(date,out _)),Cell("E",8,"LOTE-TESTE"),Cell("F",8,"4889",true),
            Cell("G",8,history),Cell("H",8,"0",true),Cell("I",8,credit,!credit.Contains(',')));
        if(formula) data.Element(n+"c")!.Parent!.Elements(n+"c").Last().Add(new XElement(n+"f","SUM(A1:A2)"));
        var rows = new XElement(n+"sheetData", new XElement(n+"row",new XAttribute("r",1),Cell("A",1,"Razão por Centro de Custo")),header,
            new XElement(n+"row",new XAttribute("r",7),Cell("G",7,"*** SALDO ANTERIOR ...")),data,
            new XElement(n+"row",new XAttribute("r",9),Cell("G",9,"TOTAL DO CENTRO DE CUSTO")));
        var stream = new MemoryStream();
        using(var z=new ZipArchive(stream,ZipArchiveMode.Create,true))
        {
            void Add(string path,string xml){using var w=new StreamWriter(z.CreateEntry(path).Open());w.Write(xml);}
            Add("xl/workbook.xml",$"""<workbook xmlns="{n}" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><workbookPr date1904="{(date1904 ? 1 : 0)}"/><sheets><sheet name="Relatório" sheetId="1" r:id="rId1"/></sheets></workbook>""");
            Add("xl/_rels/workbook.xml.rels","""<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>""");
            Add("xl/worksheets/sheet1.xml",new XElement(n+"worksheet",rows).ToString());
        }
        stream.Position=0;return stream;
    }

    [Fact] public void Reads_sparse_rows_and_ignores_report_furniture()
    {
        using var s=Workbook(); var r=new LeitorRelatorioBaixas().Ler(s);
        var row=Assert.Single(r.Lancamentos);
        Assert.Equal(8,row.Linha); Assert.Equal("4889",row.Contrapartida);
        Assert.Equal(new DateTime(2026,6,27),row.Data);
        Assert.Equal(1234.56m,row.Credito);Assert.Equal(0m,row.Debito);Assert.Null(row.Erro);
        Assert.Equal(64,r.HashArquivo.Length);
    }
    [Fact] public void Reads_brazilian_text_currency_and_date()
    {
        using var s=Workbook("15/10/2026","R$ 1.234,56");
        var row=Assert.Single(new LeitorRelatorioBaixas().Ler(s).Lancamentos);
        Assert.Equal(new DateTime(2026,10,15),row.Data);Assert.Equal(1234.56m,row.Credito);
    }
    [Fact] public void Supports_1904_calendar()
    {
        using var s=Workbook("1","10",true);
        Assert.Equal(new DateTime(1904,1,2),Assert.Single(new LeitorRelatorioBaixas().Ler(s).Lancamentos).Data);
    }
    [Theory] [InlineData("inválida","10")] [InlineData("46200","não é valor")]
    public void Invalid_values_are_not_silently_zero(string date,string credit)
    {
        using var s=Workbook(date,credit);var row=Assert.Single(new LeitorRelatorioBaixas().Ler(s).Lancamentos);
        Assert.False(string.IsNullOrWhiteSpace(row.Erro));
        Assert.True(row.Data is null || row.Credito is null);
    }
    [Fact] public void Formula_requires_review_even_if_cached()
    {
        using var s=Workbook(formula:true);
        Assert.NotNull(Assert.Single(new LeitorRelatorioBaixas().Ler(s).Lancamentos).Erro);
    }
    [Fact] public void Keeps_DOC_reference_for_matcher_to_reject()
    {
        using var s=Workbook(history:"RECEB. DOC. 217319 - TESTE");
        Assert.Contains("DOC. 217319",Assert.Single(new LeitorRelatorioBaixas().Ler(s).Lancamentos).Historico);
    }
    [Fact] public void Missing_required_header_rejects_file()
    {
        using var s=Workbook(missingHeader:true);
        Assert.Throws<InvalidDataException>(()=>new LeitorRelatorioBaixas().Ler(s));
    }
    [Fact] public void Corrupt_archive_rejects_file()
    {
        using var s=new MemoryStream([1,2,3]);
        Assert.Throws<InvalidDataException>(()=>new LeitorRelatorioBaixas().Ler(s));
    }
    [Fact] public void Closing_balance_is_not_a_receipt()
    {
        using var s=Workbook(history:"*** SALDO ATUAL ...");
        Assert.Empty(new LeitorRelatorioBaixas().Ler(s).Lancamentos);
    }
}

