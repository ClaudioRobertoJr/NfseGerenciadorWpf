using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NfseGerenciador.Services;
using NfseGerenciador.Services.ImportacaoBaixas;
using NfseGerenciador.ViewModels;
using NfseGerenciador.Views;
namespace NfseGerenciador;
public partial class App : Application
{
    public static ServiceProvider ServiceProvider {get;private set;}=null!;
    public static IConfiguration Configuration {get;private set;}=null!;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode=ShutdownMode.OnExplicitShutdown;
        CultureInfo.DefaultThreadCurrentCulture=CultureInfo.GetCultureInfo("pt-BR");
        CultureInfo.DefaultThreadCurrentUICulture=CultureInfo.GetCultureInfo("pt-BR");
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("pt-BR")));
        try
        {
            Configuration=ConfiguracaoInstalacao.Carregar(AppContext.BaseDirectory);
            var services=new ServiceCollection();
            services.AddSingleton(Configuration);
            services.AddDbContextFactory<ApplicationDbContext>();
            services.AddSingleton<LogService>();
            services.AddSingleton<NfseXmlParser>();
            services.AddTransient<IImportadorBaixas,ImportadorBaixas>();
            services.AddTransient<AtualizacaoBancoService>();
            services.AddTransient<GerenciadorNfseViewModel>();
            services.AddTransient<EmpresaClassificadaViewModel>();
            services.AddTransient<RelatoriosViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<BackupRestoreViewModel>();
            services.AddTransient<ConfiguracoesViewModel>();
            services.AddTransient<ImportacaoBaixasViewModel>();
            services.AddTransient<GerenciadorNfseWindow>();
            services.AddTransient<EmpresasClassificadasWindow>();
            services.AddTransient<RelatoriosWindow>();
            services.AddTransient<DashboardWindow>();
            services.AddTransient<BackupRestoreWindow>();
            services.AddTransient<ConfiguracoesWindow>();
            services.AddTransient<ImportacaoBaixasWindow>();
            services.AddTransient<JanelaPrincipal>();
            ServiceProvider=services.BuildServiceProvider();
            var updater=ServiceProvider.GetRequiredService<AtualizacaoBancoService>();
            if((await updater.PendentesAsync()).Length>0)
            {
                if(MessageBox.Show("Esta versão precisa acrescentar o histórico de baixas ao banco.\n\nAntes, será criado e verificado um backup na pasta configurada ou na pasta padrão do SQL Server. O banco atual será preservado.\n\nFeche as outras instâncias do programa. Deseja continuar?","Atualização do banco",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes){Shutdown();return;}
                var backup=await updater.AtualizarAsync();
                MessageBox.Show("Atualização concluída. Backup verificado:\n"+backup,"Banco atualizado",MessageBoxButton.OK,MessageBoxImage.Information);
            }
            MainWindow=ServiceProvider.GetRequiredService<JanelaPrincipal>();
            ShutdownMode=ShutdownMode.OnMainWindowClose;
            MainWindow.Show();
        }
        catch(Exception ex)
        {
            new LogService().LogError(ex,"Inicialização/atualização");
            MessageBox.Show(ex is FileNotFoundException or InvalidDataException or InvalidOperationException ? ex.Message :
                "Não foi possível abrir ou atualizar o sistema. Confira a configuração, a conexão e as permissões de backup. Nenhum banco foi recriado. Detalhes no log.",
                "NFSe — inicialização",MessageBoxButton.OK,MessageBoxImage.Error);
            Shutdown(1);
        }
    }
    protected override void OnExit(ExitEventArgs e){ServiceProvider?.Dispose();base.OnExit(e);}
}

