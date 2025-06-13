// NfseGerenciador/ViewModels/DashboardViewModel.cs
using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NfseGerenciador.Models;
using NfseGerenciador.Services;
using NfseGerenciador.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace NfseGerenciador.ViewModels
{
    public class DashboardViewModel : INotifyPropertyChanged
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly LogService _logService;

        // Propriedades de Feedback e Estado
        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set { _isBusy = value; OnPropertyChanged(nameof(IsBusy)); } }

        // Propriedades para os KPIs
        private string _totalRecebido;
        private string _totalPendente;
        private string _totalNotas;
        private string _ticketMedio;

        // Propriedades para os Gráficos
        public SeriesCollection FluxoCaixaSeries { get; set; }
        public List<string> FluxoCaixaLabels { get; set; }
        public Func<double, string> FormatterY { get; set; }
        public SeriesCollection ReceitaLocalSeries { get; set; }

        // Propriedades para Filtros
        public List<string> Periodos { get; } = new List<string> { "Últimos 30 dias", "Mês Atual", "Mês Anterior", "Ano Atual", "Todo o Período" };
        private string _periodoSelecionado = "Últimos 30 dias";
        public string PeriodoSelecionado
        {
            get => _periodoSelecionado;
            set { _periodoSelecionado = value; OnPropertyChanged(nameof(PeriodoSelecionado)); CarregarDadosDashboard(); }
        }

        public ICommand ChartDataClickCommand { get; }

        public DashboardViewModel(IDbContextFactory<ApplicationDbContext> contextFactory, LogService logService)
        {
            _contextFactory = contextFactory;
            _logService = logService;

            FluxoCaixaSeries = new SeriesCollection();
            ReceitaLocalSeries = new SeriesCollection();
            FluxoCaixaLabels = new List<string>();
            FormatterY = value => value.ToString("C0", CultureInfo.GetCultureInfo("pt-BR"));

            // --- CORREÇÃO AQUI ---
            // Usamos o construtor que aceita Action<object>
            ChartDataClickCommand = new RelayCommand(param => OnChartDataClick(param));

            CarregarDadosDashboard();
        }

        // --- CORREÇÃO AQUI ---
        // O método agora recebe um 'object' e faz a conversão (cast)
        private void OnChartDataClick(object parameter)
        {
            if (parameter is ChartPoint chartPoint)
            {
                // Garante que toda a lógica de criação da janela seja executada na Thread da UI
                Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        if (chartPoint.SeriesView.Title is string localSelecionado)
                        {
                            (DateTime dataInicio, DateTime dataFim) = GetPeriodoSelecionado();

                            var gerenciadorViewModel = new GerenciadorNfseViewModel(
                                _contextFactory,
                                App.ServiceProvider.GetService<NfseXmlParser>(),
                                _logService,
                                localSelecionado,
                                dataInicio,
                                dataFim
                            );

                            var notasWindow = new GerenciadorNfseWindow(gerenciadorViewModel)
                            {
                                Owner = Application.Current.MainWindow, // Define a janela principal como "pai"
                                Title = $"Notas Fiscais - Local: {localSelecionado}"
                            };
                            notasWindow.Show();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.LogError(ex, "Erro ao tentar abrir janela de detalhes do gráfico.");
                        MessageBox.Show("Não foi possível abrir os detalhes. Verifique o log de erros.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });
            }
        }

        #region Propriedades de Binding dos KPIs
        public string TotalRecebido { get => _totalRecebido; set { _totalRecebido = value; OnPropertyChanged(nameof(TotalRecebido)); } }
        public string TotalPendente { get => _totalPendente; set { _totalPendente = value; OnPropertyChanged(nameof(TotalPendente)); } }
        public string TotalNotas { get => _totalNotas; set { _totalNotas = value; OnPropertyChanged(nameof(TotalNotas)); } }
        public string TicketMedio { get => _ticketMedio; set { _ticketMedio = value; OnPropertyChanged(nameof(TicketMedio)); } }
        #endregion

        private async void CarregarDadosDashboard()
        {
            IsBusy = true;
            try
            {
                await Task.Run(async () =>
                {
                    using (var context = _contextFactory.CreateDbContext())
                    {
                        (DateTime dataInicio, DateTime dataFim) = GetPeriodoSelecionado();

                        var notasBase = context.Nfses.AsNoTracking()
                            .Where(n => !n.Excluida && n.ClassificacaoEspecial != "Convênio" && n.ClassificacaoEspecial != "Plano de Saúde");

                        var notasPagas = await notasBase
                                              .Include(n => n.Parcelas)
                                              .Where(n => n.EstaPago && n.DataPagamentoEfetivo >= dataInicio && n.DataPagamentoEfetivo <= dataFim)
                                              .ToListAsync();

                        var notasPendentes = await notasBase
                                                   .Where(n => !n.EstaPago && n.DataEmissao >= dataInicio && n.DataEmissao <= dataFim)
                                                   .ToListAsync();

                        var notasEmitidas = await notasBase
                                                  .Where(n => n.DataEmissao >= dataInicio && n.DataEmissao <= dataFim)
                                                  .ToListAsync();

                        decimal totalRecebidoValue = notasPagas.Sum(n => n.ValorLiquidoNfse);

                        TotalRecebido = totalRecebidoValue.ToString("C", CultureInfo.GetCultureInfo("pt-BR"));
                        TotalPendente = notasPendentes.Sum(n => n.ValorLiquidoNfse).ToString("C", CultureInfo.GetCultureInfo("pt-BR"));
                        TotalNotas = notasEmitidas.Count.ToString();
                        TicketMedio = notasPagas.Any() ? (totalRecebidoValue / notasPagas.Count).ToString("C", CultureInfo.GetCultureInfo("pt-BR")) : 0.ToString("C", CultureInfo.GetCultureInfo("pt-BR"));

                        var fluxoRealizado = notasPagas.SelectMany(n => n.Parcelas)
                            .GroupBy(p => new { p.DataPagamentoParcela.Value.Year, p.DataPagamentoParcela.Value.Month })
                            .Select(g => new { MesAno = $"{g.Key.Month:00}/{g.Key.Year}", Valor = g.Sum(p => p.ValorParcela) })
                            .ToList();

                        var fluxoPrevisto = await context.NfseParcelas.AsNoTracking().Include(p => p.Nfse)
                            .Where(p => !p.Nfse.Excluida && !p.ParcelaPaga && p.DataVencimentoParcela >= DateTime.Today)
                            .ToListAsync();

                        var fluxoPrevistoAgrupado = fluxoPrevisto
                            .GroupBy(p => new { p.DataVencimentoParcela.Year, p.DataVencimentoParcela.Month })
                            .Select(g => new { MesAno = $"{g.Key.Month:00}/{g.Key.Year}", Valor = g.Sum(p => p.ValorParcela) })
                            .ToList();

                        var todosOsMeses = fluxoRealizado.Select(f => f.MesAno).Union(fluxoPrevistoAgrupado.Select(f => f.MesAno)).Distinct().OrderBy(m => m).ToList();
                        var receitaPorLocal = notasPagas.GroupBy(n => n.Local).Select(g => new { Local = g.Key.ToString(), Total = g.Sum(n => n.ValorLiquidoNfse) }).ToList();

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            FluxoCaixaSeries.Clear();
                            FluxoCaixaSeries.Add(new ColumnSeries { Title = "Realizado", Values = new ChartValues<decimal>(todosOsMeses.Select(m => fluxoRealizado.FirstOrDefault(f => f.MesAno == m)?.Valor ?? 0)) });
                            FluxoCaixaSeries.Add(new ColumnSeries { Title = "Previsto", Values = new ChartValues<decimal>(todosOsMeses.Select(m => fluxoPrevistoAgrupado.FirstOrDefault(f => f.MesAno == m)?.Valor ?? 0)) });
                            FluxoCaixaLabels = todosOsMeses;
                            OnPropertyChanged(nameof(FluxoCaixaLabels));

                            ReceitaLocalSeries.Clear();
                            foreach (var item in receitaPorLocal)
                            {
                                ReceitaLocalSeries.Add(new PieSeries { Title = item.Local, Values = new ChartValues<decimal> { item.Total }, DataLabels = true, LabelPoint = cp => $"{cp.Y:C}" });
                            }
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "Erro ao carregar dados do dashboard.");
                MessageBox.Show("Ocorreu um erro ao carregar os dados do dashboard. Consulte o arquivo de log para detalhes.", "Erro de Dashboard", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private (DateTime, DateTime) GetPeriodoSelecionado()
        {
            DateTime hoje = DateTime.Today;
            switch (PeriodoSelecionado)
            {
                case "Mês Atual": return (new DateTime(hoje.Year, hoje.Month, 1), new DateTime(hoje.Year, hoje.Month, 1).AddMonths(1).AddTicks(-1));
                case "Mês Anterior": var mesAnterior = hoje.AddMonths(-1); return (new DateTime(mesAnterior.Year, mesAnterior.Month, 1), new DateTime(mesAnterior.Year, mesAnterior.Month, 1).AddMonths(1).AddTicks(-1));
                case "Ano Atual": return (new DateTime(hoje.Year, 1, 1), new DateTime(hoje.Year, 1, 1).AddYears(1).AddTicks(-1));
                case "Todo o Período": return (DateTime.MinValue, DateTime.MaxValue);
                default: return (hoje.AddDays(-29), hoje.AddDays(1).AddTicks(-1));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}