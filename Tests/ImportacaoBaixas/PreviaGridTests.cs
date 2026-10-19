using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Xml.Linq;
using NfseGerenciador.Models.ImportacaoBaixas;
using NfseGerenciador.ViewModels;
using Xunit;
namespace NfseGerenciador.Tests.ImportacaoBaixas;
public class PreviaGridTests
{
    [Fact] public async Task Grid_does_not_start_collection_edit_but_checkbox_still_selects()
    {
        var done=new TaskCompletionSource();
        var thread=new Thread(()=>{try{
            var path=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../Views/ImportacaoBaixasWindow.xaml"));
            var root=XDocument.Load(path);XNamespace p="http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            var xml=new XElement(root.Descendants(p+"DataTemplate").First());
            xml.Attributes().Where(a=>a.Name.LocalName=="Key").Remove();xml.Descendants().Attributes("Click").Remove();
            var grid=(DataGrid)((DataTemplate)XamlReader.Parse(xml.ToString())).LoadContent();
            var row=new LinhaBaixa(new(){Origem=ConciliadorBaixasTests.Linha(),Status=StatusConciliacao.Revisao,PermiteBaixaEmLote=true});
            grid.DataContext=new[]{row}.Where(_=>true);
            grid.Measure(new Size(1000,500));grid.Arrange(new Rect(0,0,1000,500));grid.UpdateLayout();
            grid.SelectedItem=row;grid.CurrentCell=new DataGridCellInfo(row,grid.Columns[0]);
            Assert.False(grid.BeginEdit());
            var checkbox=(CheckBox)((DataGridTemplateColumn)grid.Columns[0]).CellTemplate.LoadContent();checkbox.DataContext=row;
            checkbox.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.DataBind);
            checkbox.GetBindingExpression(CheckBox.IsCheckedProperty)!.UpdateTarget();
            Assert.True(checkbox.IsEnabled);
            checkbox.SetCurrentValue(CheckBox.IsCheckedProperty,false);checkbox.GetBindingExpression(CheckBox.IsCheckedProperty)!.UpdateSource();
            Assert.False(row.Selecionada);
            checkbox.SetCurrentValue(CheckBox.IsCheckedProperty,true);checkbox.GetBindingExpression(CheckBox.IsCheckedProperty)!.UpdateSource();
            Assert.True(row.Selecionada);done.SetResult();
        }catch(Exception e){done.SetException(e);}});
        thread.SetApartmentState(ApartmentState.STA);thread.IsBackground=true;thread.Start();
        await done.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }
}
