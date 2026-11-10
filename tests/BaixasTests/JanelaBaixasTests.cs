using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Xml.Linq;
using Xunit;
namespace NfseGerenciador.Tests.ImportacaoBaixas;
public class JanelaBaixasTests
{
    [Theory]
    [InlineData("ImportacaoBaixasWindow.xaml", "Importar baixas • NFSe")]
    [InlineData("GerenciadorNfseWindow.xaml", "Gerenciador de Notas Fiscais Eletrônicas")]
    public async Task Janela_recuperada_carrega_com_recursos_da_versao_atual(string arquivo, string titulo)
    {
        var done = new TaskCompletionSource();
        var thread = new Thread(() => {
            try {
                var dir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
                XNamespace p="http://schemas.microsoft.com/winfx/2006/xaml/presentation";
                XNamespace x="http://schemas.microsoft.com/winfx/2006/xaml";
                var app=XDocument.Load(Path.Combine(dir,"App.xaml"));
                var window=XDocument.Load(Path.Combine(dir,"Views/"+arquivo));
                var root=window.Root!;root.Attribute(x+"Class")!.Remove();
                foreach(var attr in root.DescendantsAndSelf().Attributes().Where(a=>a.Name.LocalName is "Click" or "Loaded" or "Closing").ToList()) attr.Remove();
                foreach(var attr in app.Root!.Attributes().Where(a=>a.IsNamespaceDeclaration))
                    root.SetAttributeValue(attr.Name,attr.Value.StartsWith("clr-namespace:") && !attr.Value.Contains(";assembly=")?attr.Value+";assembly=NfseGerenciador":attr.Value);
                var resources=root.Element(p+"Window.Resources")!;
                resources.AddFirst(app.Descendants(p+"ResourceDictionary").Single().Elements().Select(e=>new XElement(e)));
                var view=(Window)XamlReader.Parse(root.ToString().Replace("clr-namespace:NfseGerenciador.Converters\"", "clr-namespace:NfseGerenciador.Converters;assembly=NfseGerenciador\""));
                Assert.Equal(titulo,view.Title);
                view.Measure(new Size(1240,760)); view.Arrange(new Rect(0,0,1240,760));view.UpdateLayout();view.Close();
                done.SetResult();
            } catch(Exception ex) {done.SetException(ex);}
        });
        thread.SetApartmentState(ApartmentState.STA);thread.IsBackground=true;thread.Start();
        await done.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }
}
