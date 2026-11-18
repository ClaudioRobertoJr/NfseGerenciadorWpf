# Proposta — padrão operacional das NFS-e nacionais

Data: 17/11/2026. Situação: proposta para aprovação, sem implementação.

## Base verificada

- Release e master 1.1.8, commit `744d8a0873ffef56ed51230dea90fdb37da2b15f`.
- Cópia isolada em `work/NfseGerenciadorWpf`; checkout antigo preservado.
- `NacionalImportService` recebe documentos pela API ADN, decodifica o XML em memória, usa `NacionalXml.Parse` e grava notas e NSU na mesma transação. A chave nacional tem índice único no banco.
- `Nfse` não possui os campos fiscais necessários nem o documento recebido. O parser não interpreta a versão do leiaute.
- `ConsultaNotasService` filtra e pagina no SQL. A tela já separa a listagem e os controles de pagamentos/parcelas.
- Não foi acessado banco, certificado ou documento fiscal real. Não foi verificada a versão efetivamente presente nos documentos do usuário.

## Objetivo e limite

Sinalizar o padrão operacional informado pelo usuário nas notas recebidas pela API nacional cujo prestador seja `72547623000190` (72.547.623/0001-90). O CNPJ foi fornecido explicitamente nesta conversa. Não é uma regra tributária universal nem comprovação de direito à suspensão.

A importação manual de XML municipal/legado permanece fora da alteração. A extensão é no caminho da API; o XML mencionado aqui é o documento transportado por ela. Não haverá emissão, alteração fiscal, assinatura, cancelamento ou mudança automática em pagamentos.

## Abordagem proposta

Persistir uma avaliação e o documento recebido, vinculados à nota, em estrutura própria com relacionamento um-para-um. Isso permite filtro no SQL, detalhes e reavaliação sem misturar o resultado com o estado financeiro. Usar migração aditiva, sem reescrever registros financeiros existentes. O formato exato das classes e os nomes das tabelas ficam a cargo da implementação, seguindo os padrões do repositório.

Alternativa descartada: avaliar apenas em memória. Perderia a avaliação ao fechar o aplicativo e impediria filtro consistente sobre todas as páginas e reavaliação sem consulta externa. Não será criado um motor genérico de regras ou editor de regras nesta etapa: o padrão e o CNPJ serão explícitos e versionados no código.

## Comparações

Referência: pacote oficial `NFSe-ESQUEMAS_XSD-v1.01-20260209`, que inclui esquemas 1.00 e 1.01. Ler namespace e atributo de versão, aceitar apenas as versões efetivamente cobertas pelos testes. Versão ausente/desconhecida gera avaliação inconclusiva, sem aplicar silenciosamente interpretação de outra versão.

Dentro de `NFSe/infNFSe/DPS/infDPS`:

| Campo | Caminho relativo | Esperado |
|---|---|---|
| Tributação nacional | `serv/cServ/cTribNac` | `040301` |
| NBS | `serv/cServ/cNBS` | `123011900` |
| Suspensão | `valores/trib/tribMun/exigSusp/tpSusp` | `2`, administrativa |
| Processo | `valores/trib/tribMun/exigSusp/nProcesso` | `000000000000000000000087462013` |
| Retenção | `valores/trib/tribMun/tpRetISSQN` | `1`, não retido |

No XSD, o grupo `exigSusp` é opcional, mas tipo de suspensão e processo são obrigatórios dentro dele. O processo é texto com 30 dígitos. Não confundir suspensão com o campo `tribISSQN`, que possui outro significado. Não criar uma exigência adicional de valor desse campo sem justificativa específica.

Manter os valores originais e comparar como texto. Aceitar apenas pontuação de apresentação reconhecida para os códigos, sem eliminar letras ou caracteres arbitrários para fazer um valor inválido parecer válido. Nunca converter processo em número, preencher zeros ou fabricar valores ausentes. Preservar o documento sem serializá-lo novamente; guardar os bytes originais descompactados ou o conteúdo compactado original recebido, com hash para conferência de integridade.

## Resultado e diagnóstico

- **Conforme:** todas as cinco comparações puderam ser feitas e coincidem.
- **Divergente:** existe ao menos uma comparação válida com valor diferente; os demais campos continuam sendo analisados e ausências também são exibidas. Se houver lacunas, informar nos detalhes que a avaliação é parcial.
- **Não avaliada:** não há divergência comprovada e faltam campos, há erro de leitura/estrutura ou leiaute não suportado. Motivo sempre explícito.
- **Não se aplica:** nota municipal ou prestador diferente do CNPJ configurado. A chave nacional existente permite distinguir registros nacionais antigos sem avaliação, mas não autoriza avaliar notas municipais.

Cada campo terá encontrado, esperado e resultado próprio. Grupo de suspensão ausente não significa confirmação de exigibilidade ou retenção; será explicitado como informação ausente. Grupo presente incompleto é erro de estrutura. Guardar versão da regra e data da avaliação.

Falhas apenas nos campos da avaliação não devem impedir a importação de uma nota que passa pelas validações essenciais existentes. XML ilegível por completo ou chave inconsistente continuam seguindo o erro transacional atual: não inventar uma nota mínima para avançar o NSU.

## Integração e interface

- Avaliar as novas notas elegíveis durante a importação pela API e salvar documento, avaliação, nota e NSU na mesma transação.
- Manter o comportamento de reimportação por chave: não atualizar notas existentes, pagamentos, parcelas ou exclusão lógica. Esta etapa não usa reimportação para recuperar documentos antigos.
- Coluna textual **Padrão nacional**, filtro independente do filtro financeiro e ação **Ver avaliação** disponível também para notas pagas ou canceladas. Cor é auxiliar, nunca a única indicação.
- Detalhes em área ou janela somente de leitura com tabela Campo / Encontrado / Esperado / Resultado. Sem janelas de alerta por nota durante importação.
- Resumo agregado ao terminar a importação, com quantidades por resultado referentes às novas notas.
- A listagem carrega status e metadados; o documento completo não deve ser carregado para cada linha.
- Ação **Reavaliar padrão nacional** para registros do prestador configurado com documento armazenado, em lotes com progresso, cancelamento e resumo. Gravar apenas avaliação; conservar o documento original e não escrever notas, pagamentos, parcelas ou NSU. Proteger contra execuções simultâneas e não sobrescrever resultado de regra mais recente. Notas sem documento permanecem não avaliadas com motivo explícito.

## Notas anteriores e alertas

Os documentos anteriores não são recuperáveis a partir dos campos atualmente persistidos. Recuperação posterior pela API requer investigação específica da rota e autorização de escopo; não reiniciar o NSU, reimportar ou solicitar seleção manual de XML como parte desta alteração.

O alerta de possível duplicidade fica como próxima etapa independente: prestador, tomador com documento identificado, mesma data e valor líquido, chaves distintas e situação fiscal ativa. A mesma data e valor não provam duplicidade; o valor disponível hoje é líquido, não valor bruto do serviço. A comparação precisará abranger todas as páginas e mostrar as notas envolvidas. Não altera a proteção existente contra chave duplicada. Nesta etapa não será implementado esse alerta, evitando misturar comparação fiscal e semelhança entre documentos.

## Verificação planejada

Testes com XMLs sintéticos para os leiautes suportados, caso conforme, cada divergência isolada, ausências, grupo incompleto, códigos inválidos, versões desconhecidas, pontuação, zeros iniciais, prestador diferente e chave inconsistente. Testar precedência de resultados e exibição simultânea de divergência e lacuna.

Testes proporcionais de persistência e consulta para filtro antes da paginação, nota antiga sem documento, isolamento da importação manual e reavaliação preservando documento e estado financeiro. Verificar atomicidade com NSU, chave duplicada e rollback em banco de teste isolado quando disponível. Não executar migração ou testes contra banco real. Se SQL Server de teste ou dependências necessárias não estiverem disponíveis, informar a limitação sem instalar componentes sem autorização.

Compilar o aplicativo e executar a suíte disponível sem atualizações de dependências. Não fazer push, release ou deploy.

## Tecnologia e UX futuras

Recomendação: continuar com C#/WPF neste escopo. O aplicativo já usa a integração Windows para certificados e um auxiliar separado para ADN; reescrever a interface exigiria revalidar essas integrações e os fluxos financeiros sem benefício necessário para esta flag. A atualização de runtime/dependências deve ser planejada separadamente.

Prioridades futuras: separar visualmente situação fiscal e financeira; explicitar o alcance de ações em lote; aperfeiçoar o feedback e o acesso aos detalhes de importação. Há uma inconsistência concreta no XAML: o seletor de tipo de recebimento usa `Grid.Column="6"`, enquanto o grid declara seis colunas (0 a 5). Isso merece verificação visual e correção própria, sem alegar impacto observado em execução.

## Aprovação pendente

Aprovar esta primeira etapa: flag nacional, documento preservado, filtro/detalhes e reavaliação do que estiver disponível, somente para `72547623000190`; possível duplicidade e recuperação histórica pela API ficam para etapas separadas.
