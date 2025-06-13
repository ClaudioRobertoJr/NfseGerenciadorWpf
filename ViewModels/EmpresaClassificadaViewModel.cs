// NfseGerenciador/ViewModels/EmpresaClassificadaViewModel.cs
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using NfseGerenciador.Models;
using NfseGerenciador.Services;

namespace NfseGerenciador.ViewModels
{
    public class EmpresaClassificadaViewModel : INotifyPropertyChanged
    {
        // --- DEPENDÊNCIAS INJETADAS ---
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly LogService _logService;
        public List<string> TiposClassificacaoDisponiveis { get; } = new List<string> { "Plano de Saúde", "Convênio" };
        public ObservableCollection<EmpresaClassificada> Empresas { get; set; }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(nameof(IsBusy)); }
        }

        private bool _isViewEnabled = true;
        public bool IsViewEnabled
        {
            get => _isViewEnabled;
            set { _isViewEnabled = value; OnPropertyChanged(nameof(IsViewEnabled)); }
        }

        private EmpresaClassificada _empresaSelecionada;
        private int _id;
        private string _razaoSocial;
        private string _cnpj;
        private string _tipoClassificacao;

        public ICommand SalvarCommand { get; }
        public ICommand NovoCommand { get; }
        public ICommand ExcluirCommand { get; }

        public EmpresaClassificadaViewModel(IDbContextFactory<ApplicationDbContext> contextFactory, LogService logService)
        {
            _contextFactory = contextFactory;
            _logService = logService;

            SalvarCommand = new RelayCommand(Salvar, PodeSalvar);
            NovoCommand = new RelayCommand(Novo);
            ExcluirCommand = new RelayCommand(Excluir, PodeExcluir);

            CarregarEmpresas();
        }

        private async void CarregarEmpresas()
        {
            using (var context = _contextFactory.CreateDbContext())
            {
                var empresasList = await context.EmpresasClassificadas.OrderBy(e => e.RazaoSocial).ToListAsync();
                Empresas = new ObservableCollection<EmpresaClassificada>(empresasList);
                OnPropertyChanged(nameof(Empresas));
            }
        }

        #region Propriedades para Binding
        public EmpresaClassificada EmpresaSelecionada
        {
            get => _empresaSelecionada;
            set
            {
                if (_empresaSelecionada != value)
                {
                    _empresaSelecionada = value;
                    CarregarDadosDaEmpresaSelecionada();
                    OnPropertyChanged(nameof(EmpresaSelecionada));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }
        public int Id { get => _id; set { _id = value; OnPropertyChanged(nameof(Id)); } }
        public string RazaoSocial { get => _razaoSocial; set { _razaoSocial = value; OnPropertyChanged(nameof(RazaoSocial)); } }
        public string Cnpj { get => _cnpj; set { _cnpj = value; OnPropertyChanged(nameof(Cnpj)); } }
        public string TipoClassificacao { get => _tipoClassificacao; set { _tipoClassificacao = value; OnPropertyChanged(nameof(TipoClassificacao)); } }
        #endregion

        #region Implementação dos Comandos
        private bool PodeSalvar() => !string.IsNullOrWhiteSpace(RazaoSocial) && !string.IsNullOrWhiteSpace(Cnpj) && !string.IsNullOrWhiteSpace(TipoClassificacao);

        private async Task Salvar()
        {
            string cnpjLimpo = Regex.Replace(Cnpj ?? "", @"[^\d]", "");
            if (cnpjLimpo.Length != 14)
            {
                MessageBox.Show("CNPJ inválido. Deve conter 14 dígitos.", "Validação", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            IsViewEnabled = false;
            IsBusy = true;
            try
            {
                using (var context = _contextFactory.CreateDbContext())
                {
                    EmpresaClassificada empresa;
                    bool isNew = (Id == 0);
                    string tipoClassificacaoAntigo = string.Empty;

                    if (isNew)
                    {
                        if (await context.EmpresasClassificadas.AnyAsync(emp => emp.Cnpj == cnpjLimpo))
                        {
                            MessageBox.Show("Já existe uma empresa cadastrada com este CNPJ.", "CNPJ Duplicado", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        empresa = new EmpresaClassificada();
                        context.EmpresasClassificadas.Add(empresa);
                    }
                    else
                    {
                        empresa = await context.EmpresasClassificadas.FindAsync(Id);
                        if (empresa == null) return;
                        tipoClassificacaoAntigo = empresa.TipoClassificacao;
                    }

                    empresa.RazaoSocial = RazaoSocial.Trim();
                    empresa.Cnpj = cnpjLimpo;
                    empresa.TipoClassificacao = TipoClassificacao;
                    await context.SaveChangesAsync();

                    MessageBox.Show(isNew ? "Empresa adicionada!" : "Empresa atualizada!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);

                    if (!isNew && tipoClassificacaoAntigo != empresa.TipoClassificacao)
                    {
                        var result = MessageBox.Show("Aplicar esta nova classificação a todas as notas fiscais existentes com este CNPJ?", "Atualização em Massa", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (result == MessageBoxResult.Yes)
                        {
                            int notasAfetadas = await context.Nfses
                                .Where(n => n.TomadorCpfCnpj == empresa.Cnpj)
                                .ExecuteUpdateAsync(s => s.SetProperty(n => n.ClassificacaoEspecial, empresa.TipoClassificacao));
                            MessageBox.Show($"{notasAfetadas} nota(s) fiscal(is) foram atualizadas.", "Atualização Concluída", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
                CarregarEmpresas();
                Novo();
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "Erro ao salvar empresa");
                MessageBox.Show($"Erro ao salvar empresa. Consulte o log para detalhes.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
                IsViewEnabled = true;
            }
        }

        private void Novo() => EmpresaSelecionada = null;
        private bool PodeExcluir() => EmpresaSelecionada != null;

        private async Task Excluir()
        {
            var result = MessageBox.Show($"Tem certeza que deseja excluir a empresa '{RazaoSocial}'?", "Confirmar Exclusão", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using (var context = _contextFactory.CreateDbContext())
                    {
                        var empresaParaExcluir = await context.EmpresasClassificadas.FindAsync(Id);
                        if (empresaParaExcluir != null)
                        {
                            context.EmpresasClassificadas.Remove(empresaParaExcluir);
                            await context.SaveChangesAsync();
                            MessageBox.Show("Empresa excluída com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    CarregarEmpresas();
                    Novo();
                }
                catch (Exception ex)
                {
                    _logService.LogError(ex, $"Erro ao excluir empresa ID: {Id}");
                    MessageBox.Show($"Erro ao excluir empresa. Consulte o log para detalhes.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        #endregion

        private void CarregarDadosDaEmpresaSelecionada()
        {
            if (_empresaSelecionada != null)
            {
                Id = _empresaSelecionada.Id;
                RazaoSocial = _empresaSelecionada.RazaoSocial;
                Cnpj = _empresaSelecionada.Cnpj;
                TipoClassificacao = _empresaSelecionada.TipoClassificacao;
            }
            else
            {
                Id = 0; RazaoSocial = string.Empty; Cnpj = string.Empty; TipoClassificacao = null;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}