using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using NfseGerenciador.Models;
using NfseGerenciador.Services; // Adicionado
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace NfseGerenciador.ViewModels
{
    public class RelatoriosViewModel : INotifyPropertyChanged
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly LogService _logService; // Adicionado

        public bool IsBusy { get; set; }
        public ObservableCollection<NfseParcela> Resultados { get; set; } = new ObservableCollection<NfseParcela>();
        public ObservableCollection<Pagamento> TiposPagamento { get; set; }
        public List<string> OpcoesFiltroData { get; } = new List<string> { "Data de Emissão da NF-e", "Data de Vencimento da Parcela", "Data de Pagamento Efetivo da NF-e" };
        public List<string> OpcoesStatusPagamento { get; } = new List<string> { "Todas", "A Receber", "Pagas" };
        public List<string> OpcoesClassificacao { get; } = new List<string> { "Todas", "Plano de Saúde", "Convênio", "Normal" };
        public Array LocaisPagamento { get; } = Enum.GetValues(typeof(LocalPagamento));

        private string _filtroTipoData = "Data de Emissão da NF-e";
        private DateTime? _filtroDataInicial;
        private DateTime? _filtroDataFinal;
        private string _filtroStatusPagamento = "Todas";
        private Pagamento _filtroTipoRecebimento;
        private string _filtroClassificacao = "Todas";
        private string _filtroTomador;
        private LocalPagamento? _filtroLocalPagamento;
        private bool _isTipoRecebimentoEnabled = true;
        private bool _isLocalPagamentoEnabled = true;
        private int _totalRegistros;
        private decimal _valorTotal;

        public ICommand GerarRelatorioCommand { get; }
        public ICommand LimparFiltrosCommand { get; }
        public ICommand ExportarCsvCommand { get; }

        public RelatoriosViewModel(IDbContextFactory<ApplicationDbContext> contextFactory, LogService logService)
        {
            _contextFactory = contextFactory;
            _logService = logService; // Adicionado

            using (var context = _contextFactory.CreateDbContext())
            {
                var pagamentos = context.Pagamentos.OrderBy(p => p.TipoRecebimento).ToList();
                pagamentos.Insert(0, new Pagamento { Id = 0, TipoRecebimento = "Todos" });
                TiposPagamento = new ObservableCollection<Pagamento>(pagamentos);
            }

            _filtroTipoRecebimento = TiposPagamento.First();

            GerarRelatorioCommand = new RelayCommand(GerarRelatorio);
            LimparFiltrosCommand = new RelayCommand(LimparFiltros);
            ExportarCsvCommand = new RelayCommand(ExportarCsv, () => Resultados.Any());

            UpdateFilterStates();
        }

        #region Propriedades
        public string FiltroTipoData { get => _filtroTipoData; set { _filtroTipoData = value; OnPropertyChanged(nameof(FiltroTipoData)); } }
        public DateTime? FiltroDataInicial { get => _filtroDataInicial; set { _filtroDataInicial = value; OnPropertyChanged(nameof(FiltroDataInicial)); } }
        public DateTime? FiltroDataFinal { get => _filtroDataFinal; set { _filtroDataFinal = value; OnPropertyChanged(nameof(FiltroDataFinal)); } }
        public string FiltroStatusPagamento { get => _filtroStatusPagamento; set { _filtroStatusPagamento = value; OnPropertyChanged(nameof(FiltroStatusPagamento)); UpdateFilterStates(); } }
        public Pagamento FiltroTipoRecebimento { get => _filtroTipoRecebimento; set { _filtroTipoRecebimento = value; OnPropertyChanged(nameof(FiltroTipoRecebimento)); } }
        public string FiltroClassificacao { get => _filtroClassificacao; set { _filtroClassificacao = value; OnPropertyChanged(nameof(FiltroClassificacao)); } }
        public string FiltroTomador { get => _filtroTomador; set { _filtroTomador = value; OnPropertyChanged(nameof(FiltroTomador)); } }
        public LocalPagamento? FiltroLocalPagamento { get => _filtroLocalPagamento; set { _filtroLocalPagamento = value; OnPropertyChanged(nameof(FiltroLocalPagamento)); } }
        public bool IsTipoRecebimentoEnabled { get => _isTipoRecebimentoEnabled; set { _isTipoRecebimentoEnabled = value; OnPropertyChanged(nameof(IsTipoRecebimentoEnabled)); } }
        public bool IsLocalPagamentoEnabled { get => _isLocalPagamentoEnabled; set { _isLocalPagamentoEnabled = value; OnPropertyChanged(nameof(IsLocalPagamentoEnabled)); } }
        public int TotalRegistros { get => _totalRegistros; set { _totalRegistros = value; OnPropertyChanged(nameof(TotalRegistros)); } }
        public decimal ValorTotal { get => _valorTotal; set { _valorTotal = value; OnPropertyChanged(nameof(ValorTotal)); } }
        #endregion

        #region Comandos
        private void UpdateFilterStates()
        {
            bool isReceberSelected = FiltroStatusPagamento == "A Receber";
            IsTipoRecebimentoEnabled = !isReceberSelected;
            IsLocalPagamentoEnabled = !isReceberSelected;
            if (isReceberSelected)
            {
                FiltroTipoRecebimento = TiposPagamento.FirstOrDefault(p => p.Id == 0);
                FiltroLocalPagamento = null;
            }
        }

        private async Task GerarRelatorio()
        {
            IsBusy = true;
            try
            {
                using (var context = _contextFactory.CreateDbContext())
                {
                    IQueryable<NfseParcela> query = context.NfseParcelas
                        .AsNoTracking()
                        .Include(p => p.Nfse)
                        .ThenInclude(nfs => nfs.Pagamento)
                        .Where(p => !p.Nfse.Excluida);

                    // Filtros... (a lógica interna permanece a mesma)
                    #region Lógica de Filtros
                    if (FiltroStatusPagamento == "Pagas")
                    {
                        query = query.Where(p => p.Nfse.EstaPago);
                        if (FiltroLocalPagamento.HasValue)
                        {
                            query = query.Where(p => p.Nfse.Local == FiltroLocalPagamento.Value);
                        }
                    }
                    else if (FiltroStatusPagamento == "A Receber")
                    {
                        query = query.Where(p => !p.Nfse.EstaPago);
                    }

                    DateTime? dataInicio = FiltroDataInicial?.Date;
                    DateTime? dataFim = FiltroDataFinal?.Date.AddDays(1).AddTicks(-1);

                    if (dataInicio.HasValue || dataFim.HasValue)
                    {
                        switch (FiltroTipoData)
                        {
                            case "Data de Emissão da NF-e":
                                if (dataInicio.HasValue) query = query.Where(p => p.Nfse.DataEmissao >= dataInicio.Value);
                                if (dataFim.HasValue) query = query.Where(p => p.Nfse.DataEmissao <= dataFim.Value);
                                break;
                            case "Data de Pagamento Efetivo da NF-e":
                                if (FiltroStatusPagamento != "A Receber")
                                {
                                    if (dataInicio.HasValue) query = query.Where(p => p.Nfse.EstaPago && p.Nfse.DataPagamentoEfetivo.HasValue && p.Nfse.DataPagamentoEfetivo.Value >= dataInicio.Value);
                                    if (dataFim.HasValue) query = query.Where(p => p.Nfse.EstaPago && p.Nfse.DataPagamentoEfetivo.HasValue && p.Nfse.DataPagamentoEfetivo.Value <= dataFim.Value);
                                }
                                break;
                            case "Data de Vencimento da Parcela":
                                if (dataInicio.HasValue) query = query.Where(p => p.DataVencimentoParcela >= dataInicio.Value);
                                if (dataFim.HasValue) query = query.Where(p => p.DataVencimentoParcela <= dataFim.Value);
                                break;
                        }
                    }
                    if (FiltroTipoRecebimento?.Id > 0)
                    {
                        query = query.Where(p => p.Nfse.PagamentoId == FiltroTipoRecebimento.Id);
                    }
                    if (FiltroClassificacao != "Todas")
                    {
                        if (FiltroClassificacao == "Normal")
                            query = query.Where(p => string.IsNullOrEmpty(p.Nfse.ClassificacaoEspecial));
                        else
                            query = query.Where(p => p.Nfse.ClassificacaoEspecial == FiltroClassificacao);
                    }
                    if (!string.IsNullOrWhiteSpace(FiltroTomador))
                    {
                        string filtro = FiltroTomador.ToLower().Trim();
                        string filtroNumerico = new string(filtro.Where(char.IsDigit).ToArray());
                        if (!string.IsNullOrEmpty(filtroNumerico))
                        {
                            query = query.Where(p => (p.Nfse.TomadorRazaoSocial != null && p.Nfse.TomadorRazaoSocial.ToLower().Contains(filtro)) ||
                                                  (p.Nfse.TomadorCpfCnpj != null && p.Nfse.TomadorCpfCnpj.Contains(filtroNumerico)));
                        }
                        else
                        {
                            query = query.Where(p => p.Nfse.TomadorRazaoSocial != null && p.Nfse.TomadorRazaoSocial.ToLower().Contains(filtro));
                        }
                    }
                    #endregion

                    var resultadosFinais = await query.OrderBy(p => p.DataVencimentoParcela)
                                                .ThenBy(p => p.Nfse.Numero)
                                                .ThenBy(p => p.NumeroParcela)
                                                .ToListAsync();

                    Resultados.Clear();
                    resultadosFinais.ForEach(r => Resultados.Add(r));
                }
                TotalRegistros = Resultados.Count;
                ValorTotal = Resultados.Sum(p => p.ValorParcela);
                (ExportarCsvCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "Erro ao gerar relatório");
                MessageBox.Show("Ocorreu um erro ao gerar o relatório. Consulte o log para detalhes.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void LimparFiltros()
        {
            FiltroTipoData = OpcoesFiltroData.First();
            FiltroDataInicial = null;
            FiltroDataFinal = null;
            FiltroStatusPagamento = OpcoesStatusPagamento.First();
            FiltroTipoRecebimento = TiposPagamento.First();
            FiltroClassificacao = OpcoesClassificacao.First();
            FiltroTomador = string.Empty;
            FiltroLocalPagamento = null;
            UpdateFilterStates();
            Resultados.Clear();
            TotalRegistros = 0;
            ValorTotal = 0;
        }

        private void ExportarCsv()
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Arquivo CSV (*.csv)|*.csv",
                FileName = $"Relatorio_Parcelas_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("NumeroNFe;EmissaoNFe;PagamentoEfetivoNFe;LocalPagamento;Tomador;CNPJTomador;NumeroParcela;ValorParcela;VencimentoParcela;StatusNFe;TipoPagamento;Classificacao");
                    foreach (var item in Resultados)
                    {
                        var linha = string.Join(";",
                            $"\"{item.Nfse.Numero}\"", item.Nfse.DataEmissao.ToString("dd/MM/yyyy"),
                            item.Nfse.DataPagamentoEfetivo?.ToString("dd/MM/yyyy") ?? "N/A",
                            item.Nfse.EstaPago ? item.Nfse.Local.ToString() : "N/A",
                            $"\"{item.Nfse.TomadorRazaoSocial}\"", $"\"{item.Nfse.TomadorCpfCnpj}\"",
                            item.NumeroParcela, item.ValorParcela.ToString("F2", CultureInfo.InvariantCulture),
                            item.DataVencimentoParcela.ToString("dd/MM/yyyy"),
                            item.Nfse.EstaPago ? "Paga" : "Pendente",
                            $"\"{item.Nfse.Pagamento?.TipoRecebimento ?? "N/A"}\"",
                            $"\"{item.Nfse.ClassificacaoEspecial ?? "Normal"}\""
                        );
                        sb.AppendLine(linha);
                    }
                    File.WriteAllText(saveFileDialog.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show("Relatório exportado com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    _logService.LogError(ex, $"Erro ao exportar CSV para: {saveFileDialog.FileName}");
                    MessageBox.Show($"Ocorreu um erro ao exportar o arquivo: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        #endregion

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}