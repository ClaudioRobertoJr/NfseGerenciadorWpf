# Verificação — plano 2026-11-17-padrao-nacional.md

Base: 744d8a0873ffef56ed51230dea90fdb37da2b15f; execução nativa em cópia isolada, branch feat/padrao-nacional.

## Evidências
- Avaliador: testes inicialmente não compilavam por ausência dos tipos novos; após implementação, 21 casos passaram. Acrescentados testes de integridade e diagnóstico.
- Persistência: testes inicialmente não compilavam por ausência da navegação e do serviço; geração da migração e verificação de modelo/SQL passaram sem conexão a banco.
- Leiaute desconhecido: teste de limite do campo de versão falhou antes da correção. Removido limite de 20 caracteres para que metadados desconhecidos não façam a gravação falhar. O documento continua não avaliado.
- Interface: testes inicialmente não compilavam por ausência de filtro/janelas; após implementação, filtro, reset e carregamento de janelas em STA passaram. Renderizações com dados sintéticos conferidas visualmente para lista e detalhes.
- Revisão independente somente leitura apontou cobertura insuficiente do serviço de importação e rótulo inexato para valor não lido. Ambos tratados.
- Diagnóstico: teste `Valor_nao_lido_nao_e_apresentado_como_ausencia_comprovada` falhou com o texto antigo e passou com `Indisponível`. A ausência comprovada permanece descrita no motivo.
- Integração: acrescentada factory interna de `AdnProcessClient` apenas para permitir auxiliar sintético nos testes; o construtor público continua criando exatamente o cliente anterior. `AdnClient`, `AdnProcessClient`, parser nacional essencial e auxiliar de produção não foram modificados.
- O novo teste do auxiliar sintético passou com seleção, lote e fim por IPC. Testes de `ImportAsync` real com banco foram escritos para enriquecimento, reentrega, preservação de baixas, NSU e rollback por falha no segundo documento, mas NÃO executados por ausência de SQL Server.
- Última suíte: `dotnet test tests/BaixasTests/BaixasTests.csproj -c Release --no-restore`: **193 aprovados, 35 ignorados, 0 falhas**, total 228. Os ignorados usam `SqlServerFact`.
- `dotnet run --project tests/AdnTests/AdnTests.csproj -c Release --no-build --no-restore`: terminou com código 0. Cobre parser, HTTP simulado, seleção/cancelamento e IPC com processo auxiliar sintético.
- Build Release realizado durante a suíte, sem erros. Permanecem avisos legados de nulabilidade; nenhuma dependência externa foi adicionada/atualizada.

## Decisões e limitações
- Ruling: usar a cópia isolada já criada e execução nativa, sem nova worktree ou confirmação de plano; escopo já autorizado pelo usuário.
- Ruling: correção do rótulo tratada como precisão funcional, pois leitura impossível não comprova ausência fiscal; custo de manter o rótulo antigo seria um diagnóstico enganoso.
- Ruling: expandir os testes do serviço com uma factory interna em vez de modificar a comunicação ou introduzir uma interface/adaptador novo; custo é um ponto de injeção restrito à assembly de testes.
- Ruling: não instalar SQL Server ou dependências sem autorização. Execução de migração, transações, concorrência entre conexões e preservação em banco permanecem pendentes em SQL Server isolado. A revisão estática não equivale a essas execuções.
- Nenhum acesso a banco real, XML real, certificado ou API ADN real. A3 no computador de destino ainda precisa de validação.
- Sem push, merge em master, release ou instalação. Versão do projeto permanece 1.1.8; este é código local ainda não distribuído.
- Possível duplicidade, recuperação histórica pela API e ajustes de UX não relacionados continuam fora desta entrega.
