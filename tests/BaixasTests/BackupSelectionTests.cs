using System.Data;
using System.Reflection;
using System.Runtime.CompilerServices;
using NfseGerenciador.Services;
using NfseGerenciador.ViewModels;
using Xunit;

namespace NfseGerenciador.Tests;

public class BackupSelectionTests
{
    [Fact]
    public void Csv_preserves_quotes_delimiters_and_line_breaks_in_company_names()
    {
        Assert.Equal("\"Clínica \"\"A\"\";\nB\"", ReportCsv.Quote("Clínica \"A\";\nB"));
    }

    private static DataTable Headers()
    {
        var table = new DataTable();
        table.Columns.Add("Position", typeof(int));
        table.Columns.Add("BackupType", typeof(int));
        table.Columns.Add("DatabaseName", typeof(string));
        table.Columns.Add("BackupFinishDate", typeof(DateTime));
        table.Columns.Add("IsDamaged", typeof(bool));
        return table;
    }

    [Fact]
    public void Selects_latest_completed_full_backup_of_target_database()
    {
        var table = Headers();
        table.Rows.Add(1, 1, "Target", new DateTime(2026, 1, 1), false);
        table.Rows.Add(2, 1, "Target", new DateTime(2026, 2, 1), false);
        table.Rows.Add(3, 2, "Target", new DateTime(2026, 3, 1), false);
        table.Rows.Add(4, 1, "Other", new DateTime(2026, 4, 1), false);
        table.Rows.Add(5, 1, "Target", DBNull.Value, false);
        table.Rows.Add(6, 1, "Target", new DateTime(2026, 6, 1), true);
        Assert.Equal(2, BackupSetSelector.SelectLatestFull(table, "Target"));
    }

    [Theory]
    [InlineData(2, "Target")]
    [InlineData(1, "Other")]
    public void Rejects_files_without_a_full_backup_for_target(int type, string database)
    {
        var table = Headers();
        table.Rows.Add(1, type, database, DateTime.Now, false);
        Assert.Throws<InvalidOperationException>(() => BackupSetSelector.SelectLatestFull(table, "Target"));
    }

    [Fact]
    public void Reports_busy_changes_to_view()
    {
        var vm = (RelatoriosViewModel)RuntimeHelpers.GetUninitializedObject(typeof(RelatoriosViewModel));
        var changes = new List<string?>();
        vm.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        vm.IsBusy = true;
        vm.IsBusy = false;
        Assert.Equal(new[] { "IsBusy", "IsBusy" }, changes);
    }

    [Fact]
    public void New_clears_form_even_when_no_company_is_selected()
    {
        var vm = (EmpresaClassificadaViewModel)RuntimeHelpers.GetUninitializedObject(typeof(EmpresaClassificadaViewModel));
        vm.RazaoSocial = "Draft";
        vm.Cnpj = "12345678000199";
        vm.TipoClassificacao = "Convênio";
        typeof(EmpresaClassificadaViewModel).GetMethod("Novo", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(vm, null);
        Assert.Equal(string.Empty, vm.RazaoSocial);
        Assert.Equal(string.Empty, vm.Cnpj);
        Assert.Null(vm.TipoClassificacao);
        Assert.Equal(0, vm.Id);
    }
}
