// NfseGerenciador/ViewModels/GerenciadorNfseViewModel.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using NfseGerenciador.Models;
using NfseGerenciador.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace NfseGerenciador.ViewModels
{
    public class GerenciadorNfseViewModel : INotifyPropertyChanged
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly NfseXmlParser _parser;
        private readonly LogService _logService;
        public List<string> OpcoesStatusFiltro { get; } = new List<string> { "Todas", "Pagas", "Em Aberto" };
        // Propriedades de Paginação
        private int _currentPage = 1;
        private int _pageSize = 50; // Quantidade de itens por página
        private int _totalItems;

        private bool _isBusy;
        private bool _isDetalhesVisible;
        private Nfse _nfseSelecionada;
        private string _filtroTexto;
        private string _filtroStatus = "Todas";
        private DateTime? _filtroDataInicial;
        private DateTime? _filtroDataFinal;
        private string _filtroLocal; // Novo campo para o filtro de local
        private bool _detalhesEstaPago;
        private LocalPagamento _detalhesLocalPagamento;
        private DateTime? _detalhesDataPagamento;
        private Pagamento _detalhesTipoRecebimento;
        private bool _detalhesEhAVista;
        private int _detalhesNumParcelas = 1;

        public ObservableCollection<Nfse> Nfses { get; set; } = new ObservableCollection<Nfse>();
        public ObservableCollection<Pagamento> TiposPagamento { get; }
        public Array LocaisPagamento { get; } = Enum.GetValues(typeof(LocalPagamento));

        public ICommand ImportarXmlCommand { get; }
        public ICommand AtualizarListaCommand { get; }
        public ICommand ExcluirNotaCommand { get; }
        public ICommand LimparFiltrosCommand { get; }
        public ICommand SalvarPagamentoCommand { get; }
        public ICommand PaginaAnteriorCommand { get; }
        public ICommand ProximaPaginaCommand { get; }


        // Construtor Padrão
        public GerenciadorNfseViewModel(IDbContextFactory<ApplicationDbContext> contextFactory, NfseXmlParser parser, LogService logService)
        {
            _contextFactory = contextFactory;
            _parser = parser;
            _logService = logService;

            using (var context = _contextFactory.CreateDbContext())
            {
                TiposPagamento = new ObservableCollection<Pagamento>(context.Pagamentos.OrderBy(p => p.TipoRecebimento).ToList());
            }

            ImportarXmlCommand = new RelayCommand(ImportarXmlAsync);
            AtualizarListaCommand = new RelayCommand(async () => await AplicarFiltroCompleto(true));
            ExcluirNotaCommand = new RelayCommand(ExcluirNotaAsync, () => NfseSelecionada != null);
            LimparFiltrosCommand = new RelayCommand(LimparFiltros);
            SalvarPagamentoCommand = new RelayCommand(SalvarPagamentoAsync, () => NfseSelecionada != null && PodeSalvarPagamento());
            PaginaAnteriorCommand = new RelayCommand(async () => await MudarPagina(-1), () => CurrentPage > 1);
            ProximaPaginaCommand = new RelayCommand(async () => await MudarPagina(1), () => CurrentPage < TotalPages);

            AplicarFiltroCompleto(true);
        }

        // --- NOVO CONSTRUTOR PARA DRILL-DOWN ---
        public GerenciadorNfseViewModel(IDbContextFactory<ApplicationDbContext> contextFactory, NfseXmlParser parser, LogService logService, string localFiltro, DateTime dataInicio, DateTime dataFim)
            : this(contextFactory, parser, logService) // Chama o construtor padrão para inicializar comandos e propriedades
        {
            // Aplica os filtros recebidos do dashboard
            _filtroLocal = localFiltro;
            _filtroDataInicial = dataInicio == DateTime.MinValue ? null : dataInicio;
            _filtroDataFinal = dataFim == DateTime.MaxValue ? null : dataFim;
            _filtroStatus = "Pagas"; // O drill-down é sempre sobre notas pagas

            // Carrega os dados com os filtros aplicados
            AplicarFiltroCompleto(true);
        }


        #region Propriedades de Paginação
        public int CurrentPage
        {
            get => _currentPage;
            set { _currentPage = value; OnPropertyChanged(nameof(CurrentPage)); OnPropertyChanged(nameof(StatusPaginacao)); }
        }

        public int PageSize
        {
            get => _pageSize;
            set { _pageSize = value; OnPropertyChanged(nameof(PageSize)); AplicarFiltroCompleto(true); }
        }

        public int TotalItems
        {
            get => _totalItems;
            set { _totalItems = value; OnPropertyChanged(nameof(TotalItems)); OnPropertyChanged(nameof(TotalPages)); OnPropertyChanged(nameof(StatusPaginacao)); }
        }

        public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);

        public string StatusPaginacao => $"Página {CurrentPage} de {TotalPages} ({TotalItems} registros)";

        #endregion

        #region Propriedades para Binding
        public bool IsBusy { get => _isBusy; set { _isBusy = value; OnPropertyChanged(nameof(IsBusy)); } }
        public bool IsDetalhesVisible { get => _isDetalhesVisible; set { _isDetalhesVisible = value; OnPropertyChanged(nameof(IsDetalhesVisible)); } }

        public Nfse NfseSelecionada
        {
            get => _nfseSelecionada;
            set
            {
                _nfseSelecionada = value;
                OnPropertyChanged(nameof(NfseSelecionada));
                CarregarDetalhesPagamento();
                CommandManager.InvalidateRequerySuggested();
            }
        }
        public string FiltroTexto { get => _filtroTexto; set { _filtroTexto = value; OnPropertyChanged(nameof(FiltroTexto)); AplicarFiltroCompleto(true); } }
        public string FiltroStatus { get => _filtroStatus; set { _filtroStatus = value; OnPropertyChanged(nameof(FiltroStatus)); AplicarFiltroCompleto(true); } }
        public DateTime? FiltroDataInicial { get => _filtroDataInicial; set { _filtroDataInicial = value; OnPropertyChanged(nameof(FiltroDataInicial)); AplicarFiltroCompleto(true); } }
        public DateTime? FiltroDataFinal { get => _filtroDataFinal; set { _filtroDataFinal = value; OnPropertyChanged(nameof(FiltroDataFinal)); AplicarFiltroCompleto(true); } }
        public bool DetalhesEstaPago { get => _detalhesEstaPago; set { _detalhesEstaPago = value; OnPropertyChanged(nameof(DetalhesEstaPago)); CommandManager.InvalidateRequerySuggested(); } }
        public LocalPagamento DetalhesLocalPagamento { get => _detalhesLocalPagamento; set { _detalhesLocalPagamento = value; OnPropertyChanged(nameof(DetalhesLocalPagamento)); } }
        public DateTime? DetalhesDataPagamento { get => _detalhesDataPagamento; set { _detalhesDataPagamento = value; OnPropertyChanged(nameof(DetalhesDataPagamento)); CommandManager.InvalidateRequerySuggested(); } }
        public Pagamento DetalhesTipoRecebimento { get => _detalhesTipoRecebimento; set { _detalhesTipoRecebimento = value; OnPropertyChanged(nameof(DetalhesTipoRecebimento)); if (value != null) DetalhesEhAVista = value.EhAVista; CommandManager.InvalidateRequerySuggested(); } }
        public bool DetalhesEhAVista { get => _detalhesEhAVista; set { _detalhesEhAVista = value; if (value) DetalhesNumParcelas = 1; OnPropertyChanged(nameof(DetalhesEhAVista)); CommandManager.InvalidateRequerySuggested(); } }
        public int DetalhesNumParcelas { get => _detalhesNumParcelas; set { _detalhesNumParcelas = value; OnPropertyChanged(nameof(DetalhesNumParcelas)); CommandManager.InvalidateRequerySuggested(); } }
        #endregion

        #region Comandos
        private async Task ImportarXmlAsync()
        {
            var openFileDialog = new OpenFileDialog { Multiselect = true, Filter = "Arquivos XML (*.xml)|*.xml", Title = "Selecionar Arquivos NFS-e" };

            if (openFileDialog.ShowDialog() == true)
            {
                IsBusy = true;
                try
                {
                    await Task.Run(async () =>
                    {
                        int notasImportadas = 0, notasDuplicadas = 0;
                        using var importContext = _contextFactory.CreateDbContext();

                        foreach (string filePath in openFileDialog.FileNames)
                        {
                            var nfsesDoArquivo = _parser.Parse(filePath);
                            foreach (var nfseNova in nfsesDoArquivo)
                            {
                                if (!await importContext.Nfses.AnyAsync(n => n.Numero == nfseNova.Numero && n.PrestadorCnpj == nfseNova.PrestadorCnpj))
                                {
                                    var empresa = await importContext.EmpresasClassificadas.FirstOrDefaultAsync(e => e.Cnpj == nfseNova.TomadorCpfCnpj);
                                    if (empresa != null) nfseNova.ClassificacaoEspecial = empresa.TipoClassificacao;

                                    nfseNova.Parcelas.Add(new NfseParcela { NumeroParcela = 1, ValorParcela = nfseNova.ValorLiquidoNfse, DataVencimentoParcela = nfseNova.DataEmissao.AddDays(30), ParcelaPaga = false });
                                    nfseNova.DataPrevistaRecebimento = nfseNova.Parcelas.First().DataVencimentoParcela;

                                    importContext.Nfses.Add(nfseNova);
                                    notasImportadas++;
                                }
                                else { notasDuplicadas++; }
                            }
                        }
                        if (notasImportadas > 0) await importContext.SaveChangesAsync();
                        MessageBox.Show($"Importação Concluída:\n{notasImportadas} nova(s) importada(s).\n{notasDuplicadas} duplicada(s) ignorada(s).", "Resultado", MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
                catch (Exception ex)
                {
                    _logService.LogError(ex, "Falha crítica na importação de XMLs.");
                    MessageBox.Show("Ocorreu um erro inesperado. Verifique o arquivo de log para detalhes.", "Erro de Importação", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    IsBusy = false;
                    AplicarFiltroCompleto(true);
                }
            }
        }

        private async Task ExcluirNotaAsync()
        {
            if (MessageBox.Show($"Deseja realmente excluir a NF-e número {NfseSelecionada.Numero}?", "Confirmar Exclusão", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    using var context = _contextFactory.CreateDbContext();
                    var nota = await context.Nfses.FindAsync(NfseSelecionada.Id);
                    if (nota != null)
                    {
                        nota.Excluida = true;
                        await context.SaveChangesAsync();
                        AplicarFiltroCompleto(true);
                        MessageBox.Show("Nota excluída com sucesso.");
                    }
                }
                catch (Exception ex)
                {
                    _logService.LogError(ex, $"Erro ao excluir a nota ID: {NfseSelecionada?.Id}");
                    MessageBox.Show("Não foi possível excluir a nota. Verifique o log.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async Task SalvarPagamentoAsync()
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                var notaParaAtualizar = await context.Nfses.Include(n => n.Parcelas).FirstOrDefaultAsync(n => n.Id == NfseSelecionada.Id);
                if (notaParaAtualizar == null) return;

                notaParaAtualizar.Local = DetalhesLocalPagamento;
                notaParaAtualizar.EstaPago = DetalhesEstaPago;
                context.NfseParcelas.RemoveRange(notaParaAtualizar.Parcelas);

                if (DetalhesEstaPago)
                {
                    notaParaAtualizar.DataPagamentoEfetivo = DetalhesDataPagamento;
                    notaParaAtualizar.PagamentoId = DetalhesTipoRecebimento?.Id;
                    decimal valorParcela = Math.Round(notaParaAtualizar.ValorLiquidoNfse / DetalhesNumParcelas, 2);
                    for (int i = 1; i <= DetalhesNumParcelas; i++)
                    {
                        notaParaAtualizar.Parcelas.Add(new NfseParcela { NumeroParcela = i, ValorParcela = (i == DetalhesNumParcelas) ? notaParaAtualizar.ValorLiquidoNfse - (valorParcela * (DetalhesNumParcelas - 1)) : valorParcela, DataVencimentoParcela = DetalhesDataPagamento.Value.AddMonths(i - 1), DataPagamentoParcela = DetalhesDataPagamento, ParcelaPaga = true });
                    }
                    notaParaAtualizar.DataPrevistaRecebimento = notaParaAtualizar.Parcelas.Last().DataVencimentoParcela;
                }
                else
                {
                    notaParaAtualizar.DataPagamentoEfetivo = null;
                    notaParaAtualizar.PagamentoId = null;
                    notaParaAtualizar.Parcelas.Add(new NfseParcela { NumeroParcela = 1, ValorParcela = notaParaAtualizar.ValorLiquidoNfse, DataVencimentoParcela = notaParaAtualizar.DataEmissao.AddDays(30), ParcelaPaga = false });
                    notaParaAtualizar.DataPrevistaRecebimento = notaParaAtualizar.Parcelas.First().DataVencimentoParcela;
                }

                await context.SaveChangesAsync();
                AplicarFiltroCompleto(false);
                MessageBox.Show("Pagamento salvo com sucesso!");
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"Erro ao salvar pagamento da nota ID: {NfseSelecionada?.Id}");
                MessageBox.Show("Não foi possível salvar o pagamento. Verifique o log.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LimparFiltros()
        {
            FiltroStatus = "Todas";
            FiltroDataInicial = null;
            FiltroDataFinal = null;
            FiltroTexto = "";
            _filtroLocal = null; // Limpa o filtro de local também
            AplicarFiltroCompleto(true);
        }
        #endregion

        #region Lógica Principal
        private async Task AplicarFiltroCompleto(bool resetarPagina = false)
        {
            if (resetarPagina)
            {
                CurrentPage = 1;
            }

            using (var context = _contextFactory.CreateDbContext())
            {
                IQueryable<Nfse> query = context.Nfses
                                                 .Where(n => !n.Excluida)
                                                 .Include(n => n.Pagamento);

                if (FiltroStatus == "Pagas") query = query.Where(n => n.EstaPago);
                else if (FiltroStatus == "Em Aberto") query = query.Where(n => !n.EstaPago);

                // Adicionado filtro por Local
                if (!string.IsNullOrEmpty(_filtroLocal) && Enum.TryParse<LocalPagamento>(_filtroLocal, out var local))
                {
                    query = query.Where(n => n.Local == local);
                }

                // Data de Pagamento para notas pagas, Data de Emissão para as demais
                if (FiltroStatus == "Pagas")
                {
                    if (FiltroDataInicial.HasValue) query = query.Where(n => n.DataPagamentoEfetivo >= FiltroDataInicial.Value.Date);
                    if (FiltroDataFinal.HasValue) query = query.Where(n => n.DataPagamentoEfetivo <= FiltroDataFinal.Value.Date.AddDays(1).AddTicks(-1));
                }
                else
                {
                    if (FiltroDataInicial.HasValue) query = query.Where(n => n.DataEmissao >= FiltroDataInicial.Value.Date);
                    if (FiltroDataFinal.HasValue) query = query.Where(n => n.DataEmissao <= FiltroDataFinal.Value.Date.AddDays(1).AddTicks(-1));
                }


                if (!string.IsNullOrWhiteSpace(FiltroTexto))
                {
                    string filtro = FiltroTexto.ToLower().Trim();
                    query = query.Where(n => n.TomadorRazaoSocial.ToLower().Contains(filtro) ||
                                             n.Numero.Contains(filtro) ||
                                             n.TomadorCpfCnpj.Replace(".", "").Replace("-", "").Replace("/", "").Contains(filtro));
                }

                TotalItems = await query.CountAsync();

                var resultados = await query.OrderByDescending(n => n.DataEmissao)
                                            .ThenBy(n => n.Numero)
                                            .Skip((CurrentPage - 1) * PageSize)
                                            .Take(PageSize)
                                            .ToListAsync();
                Nfses.Clear();
                resultados.ForEach(n => Nfses.Add(n));

                CommandManager.InvalidateRequerySuggested();
            }
        }

        private async Task MudarPagina(int direcao)
        {
            CurrentPage += direcao;
            await AplicarFiltroCompleto(false);
        }


        private async void CarregarDetalhesPagamento()
        {
            if (NfseSelecionada == null)
            {
                IsDetalhesVisible = false;
                return;
            }

            DetalhesEstaPago = NfseSelecionada.EstaPago;
            DetalhesLocalPagamento = NfseSelecionada.Local;
            DetalhesDataPagamento = NfseSelecionada.DataPagamentoEfetivo ?? (DetalhesEstaPago ? DateTime.Today : (DateTime?)null);
            DetalhesTipoRecebimento = NfseSelecionada.PagamentoId.HasValue ? TiposPagamento.FirstOrDefault(p => p.Id == NfseSelecionada.PagamentoId) : null;

            using (var context = _contextFactory.CreateDbContext())
            {
                var parcelas = await context.NfseParcelas.Where(p => p.NfseId == NfseSelecionada.Id).ToListAsync();
                DetalhesNumParcelas = parcelas.Any() ? parcelas.Count : 1;
            }

            DetalhesEhAVista = DetalhesNumParcelas <= 1;
            IsDetalhesVisible = true;
        }

        private bool PodeSalvarPagamento()
        {
            if (NfseSelecionada == null) return false;
            return !DetalhesEstaPago || (DetalhesDataPagamento.HasValue && DetalhesTipoRecebimento != null && DetalhesNumParcelas > 0);
        }
        #endregion

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}