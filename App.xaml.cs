// NfseGerenciador/App.xaml.cs
using Microsoft.Extensions.DependencyInjection;
using NfseGerenciador.Services;
using NfseGerenciador.ViewModels;
using NfseGerenciador.Views;
using System.Windows;
using Microsoft.Extensions.Configuration;
using System.IO;
using Microsoft.EntityFrameworkCore;
namespace NfseGerenciador
{
    public partial class App : Application
    {
        // Torna o ServiceProvider acessível publicamente e estaticamente
        public static ServiceProvider ServiceProvider { get; private set; }
        public static IConfiguration Configuration { get; private set; } // Adicione esta linha

        public App()
        {
            var builder = new ConfigurationBuilder()
           .SetBasePath(Directory.GetCurrentDirectory())
           .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            Configuration = builder.Build(); // Adicione esta linha

            ServiceCollection services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();

        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(Configuration);


            // Registra o DbContext para ser criado uma vez por requisição (escopo)
            services.AddDbContextFactory<ApplicationDbContext>();

            // Registra os ViewModels para serem criados sempre que forem solicitados
            services.AddTransient<GerenciadorNfseViewModel>();
            services.AddTransient<EmpresaClassificadaViewModel>();
            services.AddTransient<RelatoriosViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<BackupRestoreViewModel>();
            services.AddTransient<ConfiguracoesViewModel>();
            // Registra as Views (Janelas)
            services.AddTransient<GerenciadorNfseWindow>();
            services.AddTransient<EmpresasClassificadasWindow>();
            services.AddTransient<RelatoriosWindow>();
            services.AddTransient<DashboardWindow>();
            services.AddTransient<JanelaPrincipal>();
            services.AddTransient<NfseXmlParser>();
            services.AddTransient<BackupRestoreWindow>();
            services.AddTransient<ConfiguracoesWindow>();

            // Registra Serviços como Singleton (uma única instância para toda a aplicação)
            services.AddSingleton<NfseXmlParser>();
            services.AddSingleton<LogService>();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var contextFactory = ServiceProvider.GetService<IDbContextFactory<ApplicationDbContext>>();
            using (var context = contextFactory.CreateDbContext())
            {
                try
                {
                    context.Database.Migrate();
                }
                catch (Exception ex)
                {
                    // Loga o erro e informa ao usuário.
                    var logService = ServiceProvider.GetService<LogService>();
                    logService.LogError(ex, "Falha ao aplicar migrações do banco de dados na inicialização.");
                    MessageBox.Show(
                        $"Não foi possível conectar ou criar o banco de dados. Verifique se o SQL Server está em execução e se a configuração está correta.\n\nErro: {ex.Message}",
                        "Erro de Banco de Dados",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    // Fecha a aplicação se não for possível configurar o banco de dados.
                    Application.Current.Shutdown();
                    return;
                }
            }
            // Abre a janela principal através do container de DI
            var janelaPrincipal = ServiceProvider.GetService<JanelaPrincipal>();
            janelaPrincipal.Show();
        }
    }
}