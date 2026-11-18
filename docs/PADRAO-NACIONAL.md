# Padrão operacional das NFS-e nacionais

Esta funcionalidade avalia somente novas notas recebidas pelo botão **Importar NFS-e Nacional** (API ADN), para o prestador **72.547.623/0001-90**. A importação manual de XML antigo não foi modificada. O documento fiscal vem dentro da resposta da API; não é necessário selecionar arquivos.

## Regra v1

| Campo | Esperado |
|---|---|
| Tributação nacional | `040301` / `04.03.01` |
| NBS | `123011900` / `1.2301.19.00` |
| Suspensão | `exigSusp/tpSusp = 2` — processo administrativo |
| Processo | `000000000000000000000087462013` |
| Retenção ISSQN | `tpRetISSQN = 1` — não retido |

O padrão foi informado pelo usuário. A avaliação não comprova direito a tratamento fiscal, não corrige a emissão e não determina exclusões, cancelamentos ou bloqueios financeiros.

Os caminhos e domínios foram conferidos no [pacote oficial de XSD v1.01-20260209](https://www.gov.br/nfse/pt-br/biblioteca/documentacao-tecnica/documentacao-atual/nfse-esquemas_xsd-v1-01-20260209.zip), que contém esquemas 1.00 e 1.01. `cTribNac` e `cNBS` ficam em `DPS/infDPS/serv/cServ`; suspensão e retenção ficam em `DPS/infDPS/valores/trib/tribMun`. O grupo `exigSusp` contém tanto `tpSusp` quanto `nProcesso`; este é texto de 30 dígitos. O padrão não confunde suspensão com `tribISSQN`.

## Uso

- **Padrão nacional** é uma coluna e um filtro independente da situação financeira. O filtro considera todo o resultado no banco antes da paginação.
- Selecione uma nota e use **Ver avaliação** para ver campo, original encontrado, esperado, resultado e motivo. Notas pagas ou canceladas também podem ser consultadas.
- **Conforme** exige os cinco campos reconhecidos e coincidentes. **Divergente** indica pelo menos um valor reconhecido diferente; eventuais campos ausentes são mostrados como avaliação parcial. **Não avaliada** indica ausência/erro ou leiaute não suportado sem divergência comprovada. **Não se aplica** identifica notas municipais ou de outro prestador.
- Ao terminar a importação, o resumo agrega as avaliações das notas novas, sem abrir avisos individuais.
- **Reavaliar padrão nacional** trabalha apenas com documentos já armazenados no banco para o prestador configurado, inclusive notas pagas, canceladas ou excluídas, independentemente dos filtros. Clique em **Reavaliar documentos disponíveis**; **Parar** preserva os lotes concluídos. Fechar a janela cancela e aguarda a interrupção.

## Preservação e notas anteriores

O conteúdo gzip/base64 recebido é armazenado sem reserialização, com SHA-256 desse conteúdo textual original. Os valores lidos são mantidos como texto, inclusive zeros iniciais. O XML completo não é carregado na lista de notas.

Notas nacionais importadas antes desta mudança não possuem documento armazenado pelo fluxo antigo; ficam **Não avaliadas — documento não disponível**. Reavaliar não consulta a API, não reinicia NSU nem recupera histórico. Reimportação de chave existente mantém o comportamento anterior de não sobrescrever a nota e seu financeiro.

O serviço de reavaliação altera apenas seus registros de avaliação. Há trava por sessão no SQL Server para impedir execuções simultâneas, lotes de 50, verificação de integridade do documento e proteção contra sobrescrever uma versão de regra mais recente. Os documentos permanecem intactos mesmo quando estão ilegíveis ou inconsistentes.

## Banco e atualização

A migração `20261117170947_PadraoNacional` adiciona `AvaliacoesNacionais` e `DocumentosNacionais`, vinculadas à nota. Não transforma as notas antigas nem modifica tabelas de pagamentos, parcelas ou NSU. A gravação de nova nota, documento, avaliação e NSU permanece na mesma transação da importação.

O aplicativo já aplica migrações ao iniciar. Antes de distribuir uma versão com esta alteração, validar a migração e as transações em SQL Server isolado e realizar backup pelo procedimento existente. O armazenamento dos documentos aumenta o tamanho do banco e de seus backups. Nenhuma dependência foi adicionada.

## Testes

`dotnet test tests/BaixasTests/BaixasTests.csproj -c Release` cobre avaliador, exibição, paginação, modelo e geração do SQL da migração. Os testes com `[SqlServerFact]` exigem `NFSE_TEST_CONNECTION`; a fixture cria um banco `NfseTests_<GUID>` e não usa o banco indicado em `appsettings.json`.

`dotnet run --project tests/AdnTests/AdnTests.csproj -c Release` exercita o parser/cliente ADN e o processo auxiliar simulado. A1/A3 e acesso real ao ADN precisam de validação no computador de destino; os testes sintéticos não substituem isso.

Possível duplicidade entre notas e recuperação histórica pela API não fazem parte desta etapa.
