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

        private readonly Func<FiltroConsultaNotas, Task<PaginaNotas>> _consultar;
        private int _consultaVersao;
        private bool _isCarregandoLista;
        private string _erroConsulta = "";
        public Task CarregamentoAtual { get; private set; } = Task.CompletedTask;
        public bool IsCarregandoLista { get => _isCarregandoLista; private set { _isCarregandoLista=value; OnPropertyChanged(nameof(IsCarregandoLista)); CommandManager.InvalidateRequerySuggested(); } }
        public string ErroConsulta { get => _erroConsulta; private set { _erroConsulta=value; OnPropertyChanged(nameof(ErroConsulta)); } }
        public string RotuloDataFiltro => FiltroStatus == "Pagas" ? "Pagamento de:" : "Emissão de:";
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
            : this(contextFactory, parser, logService, null, null) { }

        internal GerenciadorNfseViewModel(IDbContextFactory<ApplicationDbContext> contextFactory, NfseXmlParser parser, LogService logService,
            Func<FiltroConsultaNotas, Task<PaginaNotas>>? consultar, IEnumerable<Pagamento>? pagamentos)
        {
            _contextFactory = contextFactory;
            _parser = parser;
            _logService = logService;

            _consultar = consultar ?? new ConsultaNotasService(contextFactory).CarregarAsync;
            if (pagamentos != null) TiposPagamento = new ObservableCollection<Pagamento>(pagamentos);
            else
            {
                using var context = _contextFactory.CreateDbContext();
                TiposPagamento = new ObservableCollection<Pagamento>(context.Pagamentos.OrderBy(p => p.TipoRecebimento).ToList());
            }

            ImportarXmlCommand = new RelayCommand(ImportarXmlAsync);
            AtualizarListaCommand = new RelayCommand(async () => await AplicarFiltroCompleto(true));
            ExcluirNotaCommand = new RelayCommand(ExcluirNotaAsync, () => NfseSelecionada != null);
            LimparFiltrosCommand = new RelayCommand(LimparFiltros);
            SalvarPagamentoCommand = new RelayCommand(SalvarPagamentoAsync, () => NfseSelecionada != null && PodeSalvarPagamento());
            PaginaAnteriorCommand = new RelayCommand(async () => await MudarPagina(-1), () => !IsCarregandoLista && CurrentPage > 1);
            ProximaPaginaCommand = new RelayCommand(async () => await MudarPagina(1), () => !IsCarregandoLista && CurrentPage < TotalPages);

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
            set { if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); _pageSize = value; OnPropertyChanged(nameof(PageSize)); AplicarFiltroCompleto(true); }
        }

        public int TotalItems
        {
            get => _totalItems;
            set { _totalItems = value; OnPropertyChanged(nameof(TotalItems)); OnPropertyChanged(nameof(TotalPages)); OnPropertyChanged(nameof(StatusPaginacao)); }
        }

        public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalItems / PageSize));

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
        public string FiltroStatus { get => _filtroStatus; set { _filtroStatus = value; OnPropertyChanged(nameof(FiltroStatus)); OnPropertyChanged(nameof(RotuloDataFiltro)); AplicarFiltroCompleto(true); } }
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
                        var chavesDoLote = new ChavesImportacaoXml();
                        using var importContext = _contextFactory.CreateDbContext();

                        foreach (string filePath in openFileDialog.FileNames)
                        {
                            var nfsesDoArquivo = _parser.Parse(filePath);
                            foreach (var nfseNova in nfsesDoArquivo)
                            {
                                if (chavesDoLote.Registrar(nfseNova.Numero, nfseNova.PrestadorCnpj) && !await importContext.Nfses.AnyAsync(n => n.Numero == nfseNova.Numero && n.PrestadorCnpj == nfseNova.PrestadorCnpj))
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
                    MessageBox.Show(ex is System.IO.InvalidDataException ? ex.Message + "\nNenhuma nota deste lote foi gravada." : "Ocorreu um erro inesperado. Verifique o arquivo de log para detalhes.", "Erro de Importação", MessageBoxButton.OK, MessageBoxImage.Error);
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
            var selecionada = NfseSelecionada;
            if (selecionada == null || !PodeSalvarPagamento()) return;
            var local = DetalhesLocalPagamento;
            var pago = DetalhesEstaPago;
            var data = DetalhesDataPagamento;
            var pagamentoId = DetalhesTipoRecebimento?.Id;
            var parcelas = DetalhesNumParcelas;
            IsBusy = true;
            try
            {
                using var context = _contextFactory.CreateDbContext();
                var notaParaAtualizar = await context.Nfses.Include(n => n.Parcelas).FirstOrDefaultAsync(n => n.Id == selecionada.Id);
                    if (notaParaAtualizar == null) return;
                    if (notaParaAtualizar.Cancelada)
                    {
                        MessageBox.Show("Esta nota está cancelada fiscalmente. As baixas anteriores foram preservadas; confira os recebimentos antes de qualquer ajuste.");
                        return;
                    }

                notaParaAtualizar.Local = local;
                notaParaAtualizar.EstaPago = pago;
                context.NfseParcelas.RemoveRange(notaParaAtualizar.Parcelas);
                notaParaAtualizar.Parcelas.Clear();

                if (pago)
                {
                    notaParaAtualizar.DataPagamentoEfetivo = data;
                    notaParaAtualizar.PagamentoId = pagamentoId;
                    decimal valorParcela = Math.Round(notaParaAtualizar.ValorLiquidoNfse / parcelas, 2);
                    for (int i = 1; i <= parcelas; i++)
                    {
                        notaParaAtualizar.Parcelas.Add(new NfseParcela { NumeroParcela = i, ValorParcela = (i == parcelas) ? notaParaAtualizar.ValorLiquidoNfse - (valorParcela * (parcelas - 1)) : valorParcela, DataVencimentoParcela = data.Value.AddMonths(i - 1), DataPagamentoParcela = data, ParcelaPaga = true });
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
                await AplicarFiltroCompleto(false);
                MessageBox.Show("Pagamento salvo com sucesso!");
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"Erro ao salvar pagamento da nota ID: {NfseSelecionada?.Id}");
                MessageBox.Show("Não foi possível salvar o pagamento. Verifique o log.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { IsBusy = false; }
        }

        private void LimparFiltros()
        {
            _filtroStatus = "Todas"; _filtroDataInicial = null; _filtroDataFinal = null; _filtroTexto = ""; _filtroLocal = null;
            foreach (var name in new[] { nameof(FiltroStatus), nameof(FiltroDataInicial), nameof(FiltroDataFinal), nameof(FiltroTexto), nameof(RotuloDataFiltro) }) OnPropertyChanged(name);
            AplicarFiltroCompleto(true);
        }
        #endregion

        #region Lógica Principal
        private Task AplicarFiltroCompleto(bool resetarPagina = false, int? pagina = null)
            => CarregamentoAtual = CarregarListaAsync(resetarPagina ? 1 : pagina ?? CurrentPage);

        private async Task CarregarListaAsync(int pagina)
        {
            int versao = ++_consultaVersao;
            var filtro = new FiltroConsultaNotas(FiltroStatus, FiltroTexto, _filtroLocal, FiltroDataInicial, FiltroDataFinal, pagina, PageSize);
            IsCarregandoLista = true;
            ErroConsulta = "";
            try
            {
                var resultado = await _consultar(filtro);
                if (versao != _consultaVersao) return;
                NfseSelecionada = null;
                // Replace the collection atomically: the view never sees a partially updated page.
                Nfses = new ObservableCollection<Nfse>(resultado.Notas);
                OnPropertyChanged(nameof(Nfses));
                TotalItems = resultado.Total;
                CurrentPage = resultado.Pagina;
            }
            catch (Exception ex)
            {
                if (versao != _consultaVersao) return;
                _logService.LogError(ex, "Consultar notas e paginação");
                ErroConsulta = ex is InvalidOperationException && filtro.Inicio?.Date > filtro.Fim?.Date
                    ? ex.Message : "Não foi possível carregar a lista. A página anterior foi mantida. Clique em Atualizar para tentar novamente; detalhes no log.";
            }
            finally { if (versao == _consultaVersao) IsCarregandoLista = false; }
        }

        private Task MudarPagina(int direcao)
            => AplicarFiltroCompleto(false, Math.Clamp(CurrentPage + direcao, 1, TotalPages));

        private async void CarregarDetalhesPagamento()
        {
            var nota = NfseSelecionada;
            IsDetalhesVisible = false;
            if (nota == null) return;
            try
            {
                using var context = _contextFactory.CreateDbContext();
                var parcelas = await context.NfseParcelas.AsNoTracking().Where(p => p.NfseId == nota.Id).ToListAsync();
                if (!ReferenceEquals(nota, NfseSelecionada)) return;
                nota.Parcelas = parcelas;
                DetalhesEstaPago = nota.EstaPago;
                DetalhesLocalPagamento = nota.Local;
                DetalhesDataPagamento = nota.DataPagamentoEfetivo ?? (nota.EstaPago ? DateTime.Today : (DateTime?)null);
                DetalhesTipoRecebimento = TiposPagamento.FirstOrDefault(p => p.Id == nota.PagamentoId);
                DetalhesNumParcelas = parcelas.Count > 0 ? parcelas.Count : 1;
                DetalhesEhAVista = DetalhesNumParcelas <= 1;
                IsDetalhesVisible = true;
            }
            catch (Exception ex) { _logService.LogError(ex, "Carregar parcelas da nota"); }
        }

        private bool PodeSalvarPagamento()
        {
            if (!IsDetalhesVisible || NfseSelecionada == null || NfseSelecionada.Cancelada) return false;
            return !DetalhesEstaPago || (DetalhesDataPagamento.HasValue && DetalhesTipoRecebimento != null && DetalhesNumParcelas > 0);
        }
        #endregion

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
