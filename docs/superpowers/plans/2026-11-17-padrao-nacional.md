# Padrão nacional — Implementation Plan

> Execução nativa nesta sessão com superpowers:executing-plans. Escopo aprovado em 17/11/2026; a API deve manter seu funcionamento atual.

**Goal:** Avaliação somente das novas notas ADN do prestador 72547623000190, com documento preservado, filtro, detalhes e reavaliação local.
**Architecture:** Avaliador puro separado do parser essencial, entidade de avaliação 1:1 e documento 1:1 separado para não carregar XML na lista. Migração aditiva. O fluxo de certificado/HTTP/NSU permanece igual.
**Tech Stack:** C#/.NET 8, WPF, EF Core 9, SQL Server, xUnit existentes.
**Spec:** docs/superpowers/specs/2026-11-17-padrao-nacional.md (aprovada nesta conversa).

## Global Constraints
- Não alterar importação manual XML, baixas, parcelas, exclusão lógica ou os clientes ADN.
- Regra v1 para CNPJ 72547623000190. Cinco valores e estados conforme proposta aprovada.
- Nenhum push, release, instalação de dependência, acesso ao banco real ou reconsulta histórica à API.
- Branch feat/padrao-nacional na cópia isolada baseada em 744d8a0.

## Review Focus
- Campos duplicados, leiautes desconhecidos e caracteres inválidos não produzem conformidade (tarefa 1).
- Nota cancelada/paga continua acessível na avaliação sem mudança financeira (tarefa 2).
- Reavaliação corrompida não sobrescreve documento nem regra mais recente (tarefa 2).
- Filtro de avaliação opera antes de paginação; documento não é carregado na lista (tarefa 3).
- Cancelamento de reavaliação preserva lotes confirmados e permite retomada (tarefa 2/3).

### Task 1: Avaliador puro e documento
Files: Models/AvaliacaoNacional.cs, Services/AvaliadorPadraoNacional.cs, tests/BaixasTests/PadraoNacionalTests.cs.
Interfaces: AvaliadorPadraoNacional.Avaliar(string xml, string chave, string prestador) retorna AvaliacaoNacional; constantes CnpjPrestador e VersaoRegra. CriarDocumento(string arquivoXml) guarda o base64 gzip original e hash do conteúdo recebido.
- [x] Testar XML sintético conforme, cinco divergências, campos ausentes/duplicados, desconhecidos, pontuação, processo com zeros e outro prestador.
- [x] Executar teste RED; implementar avaliador sem mudanças no parser nacional essencial; executar GREEN.

### Task 2: Persistência e integração
Files: Models/Nfse.cs, Data/AplicationDbContext.cs, Migrations/*, Services/NacionalImportService.cs, Services/ReavaliacaoNacionalService.cs, tests/BaixasTests/PadraoNacionalSqlTests.cs.
Interfaces: Navegação Nfse.AvaliacaoNacional, AvaliacaoNacional.Documento; ReavaliarAsync(IProgress<string>, CancellationToken) em lotes de 50, com lock SQL entre processos e rechecagem da versão. Documento original nunca é atualizado.
- [ ] Executar testes SQL de migração aditiva, reavaliação idempotente, preservação financeira/NSU, filtro e rollback: testes escritos e compilados, ignorados porque não há SQL Server de teste disponível.
- [x] Integrar exclusivamente dentro de !exists da API, salvar junto do lote; contadores somente após commit.
- [x] Gerar migração com ferramenta EF já disponível; testar modelo/migração sem banco real.
- [x] Reavaliar somente linhas existentes do prestador alvo, com chave consistente; proteger regras futuras, hash e cancelamento.

### Task 3: Interface e regressão
Files: Services/ConsultaNotasService.cs, ViewModels/GerenciadorNfseViewModel.cs, Views/GerenciadorNfseWindow.xaml*, Views/AvaliacaoNacionalWindow.xaml*, Views/ReavaliacaoNacionalWindow.xaml*, tests/BaixasTests/PadraoNacionalUiTests.cs.
- [x] Testar filtro independente, reset para primeira página, estado antigo sem documento e não aplicável, abertura das novas janelas em STA.
- [x] Coluna de status, filtro e detalhes somente leitura; janela de reavaliação com progresso/parar e resumo.
- [x] Executar suíte BaixasTests, programa AdnTests, build Release e revisão independente do diff. Ver relatório de verificação para testes ignorados.
- [x] Documentar evidências/limitações e entregar patch/código sem publicação.
