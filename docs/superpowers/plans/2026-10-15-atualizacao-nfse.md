# Atualização do NFSe — plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** importar baixas do relatório Excel com conferência, melhorar as telas WPF e entregar atualização que preserve o SQL Server existente.

**Architecture:** manter a aplicação WPF e o banco atuais. Separar leitura do Excel, conciliação pura e gravação transacional; usar migração aditiva para auditoria. Renovar as telas com estilos compartilhados e publicar um pacote que preserve a configuração instalada.

**Tech Stack:** C#, .NET 8 WPF, EF Core/SQL Server existentes; leitura XLSX com System.IO.Compression e System.Xml.Linq; xUnit para regras e integração com SQL Server de teste. Não introduzir outro framework de interface.

**Spec:** ../../../../../outputs/proposta-atualizacao-nfse.md (aprovada pelo usuário em 15/10/2026).

## Restrições globais

- Usar a data da linha do relatório; 4889/230 = Pix, 132/108 = Dinheiro.
- SIPAG, referência duvidosa, valor divergente e parcelas incompatíveis permanecem para revisão manual.
- Particular para centros diferentes de laboratório. Identificar laboratório pelo código 1.1.2.035 do relatório fornecido e descrição normalizada; centro desconhecido deve aparecer na prévia.
- Nenhuma publicação no GitHub; código e pacote são entregues localmente.
- Não sobrescrever appsettings.json instalado, recriar banco, apagar notas ou alterar migrações antigas.
- Não conectar ao banco real para desenvolvimento, testes ou migrações.
- Manter nomes e valores do enum LocalPagamento existentes.
- Preservar o cadastro atual de formas de pagamento; resolver Pix/Dinheiro pelo tipo e condição à vista, sem presumir IDs.
- Parcelas: admitir zero parcelas ou uma parcela 1, pendente, sem data de pagamento, de valor integral e compatível com pagamento à vista. Outros estados vão para revisão. Não remover parcelas em lote.
- Dados pessoais dos anexos não entram no Git; fixtures de testes são sintéticas.
- A entrega não pode afirmar atualização validada sem executar o teste de migração em banco com dados preexistentes. Registrar limitações se o ambiente impedir esse teste.

## Foco da revisão

1. Excel com cabeçalhos repetidos, células de texto, números seriais de data e calendário 1904: interpretação consistente ou rejeição explícita — tarefa 1.
2. Mesmo número para prestadores diferentes, múltiplos números no histórico e número de DOC: nunca escolher uma nota silenciosamente — tarefa 2.
3. Parcela padrão da importação XML versus parcelamento real: admitir a primeira e preservar o segundo — tarefas 2/3.
4. Nota alterada entre prévia e confirmação ou importação concorrente: revalidar e abortar sem gravação parcial — tarefa 3.
5. Atualização iniciada por atalho em outra pasta ou cópia que falha no meio: manter configuração e não iniciar instalação incompleta — tarefa 6.

## Sequência e arquivos

Raiz de trabalho: work/NfseGerenciadorWpf. Os caminhos abaixo são relativos a ela. Trabalhar em branch local própria, sem alterar o remoto. Preparar SDK .NET 8 em diretório de trabalho se necessário; usar SQL Server isolado de teste, com conexão fornecida por variável de ambiente, nunca a conexão do appsettings do repositório.

### Tarefa 1 — Leitura confiável do relatório

**Arquivos:** criar Models/ImportacaoBaixas/RelatorioBaixas.cs, Services/ImportacaoBaixas/LeitorRelatorioBaixas.cs, Tests/NfseGerenciador.Tests.csproj e Tests/ImportacaoBaixas/LeitorRelatorioBaixasTests.cs; ajustar NfseGerenciador.csproj para excluir Tests/** da compilação WPF.

**Interfaces:** LeitorRelatorioBaixas.Ler(Stream arquivo) retorna RelatorioBaixas, com SHA-256 dos bytes, lista de LancamentoRelatorio e diagnósticos. LancamentoRelatorio contém planilha, linha, centro, conta, empresa, data, lote, contrapartida, histórico, débito e crédito. Manter valores brutos nos diagnósticos.

- [ ] Criar testes sintéticos: cabeçalhos em posições diferentes; NOTA e DOC preservados; data serial e textual; calendário 1904; crédito 1.234,56; saldo anterior e totais ignorados; célula inválida diagnosticada; débito não tratado como recebimento; arquivo corrompido sem resultado parcial utilizável.
- [ ] Executar dotnet test Tests/NfseGerenciador.Tests.csproj --filter LeitorRelatorioBaixasTests e confirmar falha por implementação ausente.
- [ ] Implementar leitura de workbook/relationships, sharedStrings, inline strings, referências esparsas de células e cabeçalhos normalizados. Fórmula em coluna necessária sem valor armazenado confiável deve gerar revisão, não zero. Não executar conteúdo do arquivo nem buscar referências externas.
- [ ] Reexecutar o comando e exigir todos os testes aprovados.
- [ ] Comparar o relatório real em modo somente leitura com as contagens exploratórias, investigando diferenças sem fixar 473 como resultado obrigatório. Commit local da tarefa.

### Tarefa 2 — Conciliação e classificação

**Arquivos:** criar Models/ImportacaoBaixas/PreviaBaixas.cs, Services/ImportacaoBaixas/ConciliadorBaixas.cs e Tests/ImportacaoBaixas/ConciliadorBaixasTests.cs.

**Interfaces:** ConciliadorBaixas.Conciliar(RelatorioBaixas relatorio, IReadOnlyList<Nfse> notas, IReadOnlyList<Pagamento> pagamentos, string prestadorCnpj) retorna PreviaBaixas. ItemPrevia possui origem, NfseId opcional, status Apta/Revisao/Ignorada, motivo, data, tipo de pagamento, local, valor e estado original relevante da nota/parcelas.

- [ ] Escrever testes para 4889/230 como Pix, 132/108 como Dinheiro, SIPAG ignorado mesmo com número de nota, DOC não reconhecido como NF e vários números enviados para revisão.
- [ ] Escrever testes para igualdade monetária em centavos, débito/estorno, nota ausente/excluída/já paga, data inválida/anterior à emissão, duplicidade de nota no banco e vários lançamentos para a mesma nota. Datas anteriores à emissão exigem revisão, sem impedir tratamento manual.
- [ ] Escrever testes para dois prestadores, parcela padrão, parcelamento, parcela paga inconsistente e forma de pagamento ausente/ambígua. O relatório não tem CNPJ do prestador: a prévia exige escolha explícita quando houver mais de um.
- [ ] Executar dotnet test Tests/NfseGerenciador.Tests.csproj --filter ConciliadorBaixasTests e confirmar falhas antes de implementar.
- [ ] Implementar regras puras. Divergência de nome claramente identificável gera revisão; sem nome extraível não inventar identidade. Não fazer correspondência apenas por nome/valor. Não somar automaticamente vários lançamentos.
- [ ] Reexecutar testes e conferir exemplos dos anexos. Commit local.

### Tarefa 3 — Gravação transacional e histórico

**Arquivos:** criar Models/ImportacaoBaixas/LoteBaixa.cs, Models/ImportacaoBaixas/RegistroBaixa.cs, Services/ImportacaoBaixas/ImportadorBaixas.cs, Tests/ImportacaoBaixas/ImportadorBaixasSqlServerTests.cs; modificar Data/AplicationDbContext.cs e gerar Migrations/*_HistoricoImportacaoBaixas.cs com designer e snapshot.

**Interfaces:** ImportadorBaixas.PrepararAsync(string caminho, string prestadorCnpj, CancellationToken ct) retorna PreviaBaixas. ConfirmarAsync(PreviaBaixas previa, IReadOnlyCollection<int> linhasSelecionadas, CancellationToken ct) retorna ResultadoImportacao com lote, quantidade e valor. Validar seleção pelo identificador interno da prévia, nunca só pelo número físico da linha entre planilhas.

- [ ] Criar testes SQL Server isolados: commit integral; rollback após erro induzido; mesma linha reimportada; arquivo renomeado; período sobreposto; dois processos tentando baixar a mesma nota; alteração manual após a prévia; forma de pagamento removida; preservação do ID da parcela padrão.
- [ ] Executar dotnet test Tests/NfseGerenciador.Tests.csproj --filter ImportadorBaixasSqlServerTests com NFSE_TEST_CONNECTION apontando exclusivamente para banco de teste. Sem servidor disponível, informar bloqueio; não substituir por EF InMemory como prova transacional.
- [ ] Implementar lote com hash de arquivo, origem e data de importação; registro por baixa com nota, valores anteriores/novos, data do relatório e impressão SHA-256 do lançamento normalizado + prestador. Índice único de impressão impede repetir a mesma origem mesmo após reabrir manualmente a nota; reabrir não autoriza reimportação silenciosa.
- [ ] Gravar somente linhas selecionadas aptas após releitura dentro de transação Serializable. Revalidar notas, parcelas e cadastros; estados alterados abortam o lote e exigem nova prévia. Atualizar parcela padrão em lugar de excluí-la; criar parcela única paga apenas se não houver parcela. Marcar nota e parcela com a data do relatório.
- [ ] Criar migração exclusivamente aditiva para histórico e índices, sem alterar registros de negócio ou migrações anteriores. Não permitir exclusão em cascata do histórico ao excluir logicamente uma nota.
- [ ] Reexecutar testes e testar migração sobre o esquema anterior preenchido, comparando notas, pagamentos, parcelas e configurações antes/depois. Commit local.

### Tarefa 4 — Fluxo de importação no WPF

**Arquivos:** criar Views/ImportacaoBaixasWindow.xaml e .xaml.cs, ViewModels/ImportacaoBaixasViewModel.cs; modificar App.xaml.cs, ViewModels/GerenciadorNfseViewModel.cs e Views/GerenciadorNfseWindow.xaml; criar Tests/ImportacaoBaixas/ImportacaoBaixasViewModelTests.cs.

**Interfaces:** comando ImportarBaixasCommand abre a janela; ViewModel expõe CaminhoArquivo, PrestadorSelecionado, Itens, EstaOcupado, QuantidadeSelecionada, ValorSelecionado, PrepararCommand e ConfirmarCommand. A janela chama apenas ImportadorBaixas; operações longas não bloqueiam a interface.

- [ ] Criar testes para seleção restrita às aptas, totais filtrados/selecionados, confirmação desabilitada durante operação e prévia invalidada ao trocar arquivo ou prestador.
- [ ] Executar os testes, confirmar falha e implementar a janela com abas Aptas/Revisão/Ignoradas, motivo por linha e confirmação que mostra quantidade/valor.
- [ ] Registrar dependências no container e atualizar a lista principal após sucesso. Não confirmar por duplo clique nem permitir dois lotes concorrentes na mesma janela.
- [ ] Reexecutar testes e verificar manualmente cancelamento, erro de arquivo, erro de banco e novo arquivo após resultado anterior. Commit local.

### Tarefa 5 — Layout e melhorias de operação

**Arquivos:** criar Themes/Controls.xaml; modificar App.xaml, Views/JanelaPrincipal.xaml, Views/GerenciadorNfseWindow.xaml, Views/DashboardWindow.xaml, Views/RelatoriosWindow.xaml, Views/ConfiguracoesWindow.xaml, Views/EmpresasClassificadasWindow.xaml e Views/BackupRestoreWindow.xaml. Modificar ViewModels/GerenciadorNfseViewModel.cs somente para resumo filtrado e estado ocupado necessários ao fluxo.

- [ ] Aplicar tema claro compartilhado com azul discreto, fontes legíveis, botões e status consistentes; preservar bindings e funções atuais.
- [ ] Organizar navegação principal, filtros compactos, tabela ampla, resumo do conjunto filtrado (não apenas da página), painel da nota selecionada e parcelas expansíveis.
- [ ] Melhorar rótulos e mensagens de pagamento; bloquear salvamentos sem nota/dados obrigatórios e ações concorrentes. Não mudar regras contábeis de operações manuais fora do necessário.
- [ ] Compilar em Release e verificar telas em 1366×768 e 1920×1080, escala de 100% e 125%, teclado, contraste, rolagem, valores longos e nenhum item selecionado. Usar screenshots quando houver ferramenta disponível; registrar se não for possível executar a inspeção visual.
- [ ] Testar filtros, paginação, pagamento manual e navegação após o redesenho. Não criar testes que apenas repitam XAML. Commit local.

### Tarefa 6 — Configuração, atualização e pacote

**Arquivos:** criar Services/ConfiguracaoInstalacao.cs, Services/AtualizacaoBancoService.cs, Scripts/Publicar.ps1, Scripts/Atualizar.ps1, appsettings.example.json e docs/ATUALIZACAO.md; modificar App.xaml.cs, NfseGerenciador.csproj, ViewModels/BackupRestoreViewModel.cs; criar Tests/Atualizacao/ConfiguracaoInstalacaoTests.cs e Tests/Atualizacao/AtualizacaoBancoSqlServerTests.cs.

**Interfaces:** ConfiguracaoInstalacao.Carregar(string diretorioInstalacao) retorna IConfigurationRoot usando o appsettings.json existente. AtualizacaoBancoService.PrepararAsync(CancellationToken ct) detecta migrações pendentes e solicita backup confirmado antes de alterar esquema de instalação existente. Não registrar credenciais no log.

- [ ] Criar testes para diretório corrente diferente, configuração ausente/inválida, atualização sem migração pendente e falha de migração sem tentativa de recriar banco.
- [ ] Fixar base da configuração em AppContext.BaseDirectory; preservar conexão existente. Se faltante, orientar configuração explicitamente, sem selecionar automaticamente o servidor do desenvolvedor.
- [ ] Encaminhar backup pelo SQL Server com caminho/permissão do serviço verificados; bloquear alteração de esquema quando backup necessário falhar. Testar backup/restauração somente no ambiente isolado.
- [ ] Implementar publicação win-x64 Release autocontida, em pasta, com appsettings.example.json e sem appsettings.json, .bak, .mdf ou .ldf. Não reutilizar o instalador compilado sem fonte.
- [ ] Implementar atualização com programa fechado: validar origem/destino, preparar cópia de segurança dos binários e configuração, copiar pacote excluindo configuração, abortar ao primeiro erro e recuperar arquivos substituídos se cópia falhar. Não apagar diretórios recursivamente nem tocar no SQL Server via script.
- [ ] Testar o atualizador em diretórios descartáveis com configuração sentinela: conteúdo permanece idêntico; origem/destino coincidentes são rejeitados; falha de cópia não inicia o aplicativo.
- [ ] Executar dotnet test Tests/NfseGerenciador.Tests.csproj -c Release, dotnet build NfseGerenciador.csproj -c Release e Scripts/Publicar.ps1. Verificar conteúdo do pacote.
- [ ] Documentar backup, atualização, verificação de servidor/banco e recuperação. Reversão do EXE não desfaz migração; recuperar banco somente por procedimento deliberado de restore.
- [ ] Commit local e revisão final do diff, migração e pacote. Entregar ZIP de código e ZIP de atualização em outputs, instruções e relação precisa dos testes executados/impedidos.

## Critérios de entrega

Importar relatório sem baixar exceções; usar a data do relatório; preservar parcelamentos e histórico; não repetir baixas; telas utilizáveis; pacote não contém nem sobrescreve configuração instalada. Nenhuma alteração no GitHub ou banco real. Relatar separadamente validação de regras, compilação, interface e migração.

## Revisão do plano

Cobertura conferida contra a proposta aprovada: importação (1–4), interface (5), banco/atualização (3/6), documentação e entrega (6). As cinco classes de risco acima têm verificações nas tarefas correspondentes.

Execução sugerida: nativa, nesta conversa, por etapas, porque a conciliação e a persistência compartilham regras sensíveis. Alternativa: subagentes com revisão de cada tarefa. A escolha do usuário e a revisão deste plano precedem a implementação, conforme a skill writing-plans.

