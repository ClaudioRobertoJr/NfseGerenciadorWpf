using NfseGerenciador.ViewModels;
using System.Windows;

namespace NfseGerenciador.Views
{
    public partial class EmpresasClassificadasWindow : Window
    {
        public EmpresasClassificadasWindow(EmpresaClassificadaViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel; // Define o ViewModel como o contexto de dados da View
        }
    }
}