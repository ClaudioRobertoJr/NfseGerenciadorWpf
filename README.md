# NFS-e Gerenciador

## Atualização 2.0

Importação de baixas por Excel com prévia e conferência por nota, valor e data do relatório; novo layout; histórico transacional e atualização com backup verificado antes da migração.

Consulte [como atualizar preservando o banco e a configuração](docs/ATUALIZACAO.md) e os [resultados e limites da validação](docs/VALIDACAO.md). O pacote é destinado a instalações existentes; não use o instalador antigo para atualizar. Testes de integração SQL ainda precisam ser executados em ambiente de testes.

![.NET](https://img.shields.io/badge/.NET-8-blueviolet) 
![C#](https://img.shields.io/badge/C%23-11-blue)
![WPF](https://img.shields.io/badge/WPF-Framework-blue)
![MVVM](https://img.shields.io/badge/Pattern-MVVM-orange)
![EF Core](https://img.shields.io/badge/EF%20Core-8-9cf)
![SQL Server](https://img.shields.io/badge/DB-SQL%20Server-critical)

Sistema de desktop desenvolvido em WPF para gerenciamento de Notas Fiscais de Serviço Eletrônicas (NFS-e), com foco em clínicas e consultórios. A aplicação permite importar, classificar, gerenciar pagamentos e gerar relatórios financeiros a partir dos arquivos XML das notas fiscais.

## GIF de Demonstração



![Demo do App](caminho/para/seu/gif.gif)

## Principais Funcionalidades

* **Importação de NFS-e**: Importa múltiplas notas fiscais a partir de arquivos XML, validando duplicatas.
* **Gerenciamento de Notas**: Visualização, paginação, filtragem e exclusão lógica de notas fiscais.
* **Controle de Pagamentos**: Registro detalhado de pagamentos, com suporte a parcelamento, data, local e forma de recebimento.
* **Dashboard Financeiro**: Painel visual com indicadores chave de performance (KPIs) e gráficos sobre o fluxo de caixa (realizado vs. previsto) e receita por local.
* **Classificação de Empresas**: Permite classificar CNPJs (ex: "Plano de Saúde", "Convênio") para categorização automática das notas.
* **Relatórios Avançados**: Geração de relatórios customizáveis com múltiplos filtros e exportação para CSV.
* **Backup e Restauração**: Funcionalidade para criar e restaurar backups do banco de dados SQL Server.
* **Configuração Dinâmica**: Permite ao usuário testar e alterar a string de conexão com o banco de dados diretamente pela interface.

## Tecnologias e Arquitetura

O projeto foi construído com foco em boas práticas de desenvolvimento e manutenibilidade, utilizando uma arquitetura robusta e tecnologias modernas do ecossistema .NET.

| Componente | Tecnologia/Padrão | Propósito |
| :--- | :--- | :--- |
| **Interface Gráfica** | WPF (.NET 8) | Construção da interface de usuário para desktop Windows. |
| **Arquitetura** | MVVM (Model-View-ViewModel) | Separação de responsabilidades entre UI (View), lógica de apresentação (ViewModel) e dados (Model). |
| **Acesso a Dados** | Entity Framework Core 8 | ORM para mapeamento de objetos para o banco de dados (Code-First). |
| **Banco de Dados** | SQL Server | Armazenamento persistente dos dados da aplicação. |
| **Injeção de Dependência** | `Microsoft.Extensions.DependencyInjection` | Gerenciamento do ciclo de vida e desacoplamento de componentes. |
| **Configuração**| `appsettings.json` | Configurações externas, como a connection string do banco. |
| **Visualização de Dados** | LiveCharts.Wpf | Criação de gráficos interativos para o dashboard. |

### Detalhes da Arquitetura

O sistema segue estritamente o padrão **MVVM**:
* As **Views** (`/Views`) são "burras" e contêm apenas o XAML da interface e o mínimo de code-behind necessário.
* Os **ViewModels** (`/ViewModels`) orquestram a lógica da aplicação. Eles são instanciados e injetados pelo container de DI e se comunicam com as Views via DataBinding, `ICommand` e `INotifyPropertyChanged`.
* Os **Models** (`/Models`) são classes POCO que representam as entidades do banco de dados.

A **Injeção de Dependência** é central para a aplicação, configurada no `App.xaml.cs`. Serviços como `ApplicationDbContext`, `NfseXmlParser` e `LogService` são injetados nos construtores dos ViewModels, promovendo baixo acoplamento e alta testabilidade.

## Estrutura do Projeto

* `/Data`: Contém o `ApplicationDbContext` e as configurações de migração do EF Core.
* `/Models`: Classes que definem a estrutura dos dados (Nfse, Pagamento, etc.).
* `/Views`: Janelas (Window) e UserControls da aplicação.
* `/ViewModels`: A lógica de cada View.
* `/Services`: Serviços de aplicação, como o parser de XML e o logger de erros.
* `/Converters`: Conversores de valor para o DataBinding do WPF.

## Como Executar o Projeto

1.  **Clone o repositório:**
    ```sh
    git clone [https://github.com/seu-usuario/NfseGerenciador.git](https://github.com/seu-usuario/NfseGerenciador.git)
    ```
2.  **Banco de Dados:**
    * Certifique-se de ter uma instância do SQL Server (ou SQL Express) em execução.
    * Abra o arquivo `NfseGerenciador/appsettings.json` e ajuste a `DefaultConnection` para apontar para sua instância do SQL Server.
3.  **Execução:**
    * Abra a solução no Visual Studio 2022 ou superior.
    * A primeira execução aplicará as migrações e criará o banco de dados `NfseGerenciadorDB_NOVO` automaticamente.

## Considerações e Próximos Passos

* **Segurança**: A string de conexão está no `appsettings.json` para simplicidade. Em um ambiente de produção, seria movida para um sistema de segredos (User Secrets, Azure Key Vault).
* **Testes**: Implementar testes unitários para os ViewModels e serviços para garantir a robustez da lógica de negócio.
* **UI/UX**: Refinar a interface para melhorar a experiência do usuário.
