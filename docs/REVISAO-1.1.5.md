# Revisão funcional — versão 1.1.5

## Paginação e filtros
A lista anterior disparava consultas sem coordenar respostas: filtros e inicialização por dashboard podiam se sobrepor. O indicador avançava antes da consulta terminar e uma exceção podia deixar a página indicada divergente dos dados. A limpeza de filtros passava por comandos síncronos executados em thread de fundo e disparava várias consultas intermediárias.

A consulta agora captura um filtro imutável, ignora respostas antigas, substitui a página inteira e atualiza o indicador somente após sucesso. Falhas mantêm a página anterior com mensagem e permitem repetir. Navegação simultânea é bloqueada durante consulta. A ordenação inclui ID para desempatar notas com mesma data/número. Filtros combinam texto, documento com ou sem pontuação, status e datas. Pagas usam data de pagamento, indicada pelo rótulo; demais usam emissão. Datas invertidas são informadas.

## Outros problemas confirmados e corrigidos
- Comandos síncronos passam a executar na thread da interface, incluindo limpar filtros, abrir janelas e limpar cadastros.
- Importação XML deduplica notas repetidas no mesmo lote antes do SaveChanges; XML inválido ou de formato não suportado falha sem salvar lote parcial.
- Pagamento manual captura nota e valores antes do acesso ao banco; respostas de parcelas de seleção anterior são descartadas. O vencimento de nota reaberta usa a nova parcela.
- Relatórios notificam carregamento, aplicam filtro de local em Todas e escapam aspas no CSV.
- Dashboard ordena meses cronologicamente e identifica o total pendente como emitido no período, sem alegar vencimento inexistente no cálculo.
- Cadastro de empresas limpa Novo mesmo sem seleção e trata erro de carregamento.
- Restauração de arquivo .bak com vários conjuntos escolhe o último backup completo, concluído, não danificado e do banco correto; usa FILE explícito e VERIFYONLY antes da substituição. Caminhos são parâmetros SQL. Não foi restaurado nenhum banco instalado.

## Preservação e limites
Nenhuma nova migração de banco. Mantidos baixas, parcelas, pagamentos, NSU e estrutura de certificado/HTTPS no NfseAdnWorker.exe. O pacote continua usando Atualizar.cmd/Atualizar.ps1 e inclui os dois executáveis.

Revisão abrangeu listagem, pagamento, XML, importação nacional/baixas, relatórios, dashboard, cadastro, backup/restauração e atualização. Não constitui garantia de ausência de outros erros. Limitações identificadas: XML legado ABRASF com NfseCancelamento continua sendo ignorado pelo parser (não sincroniza cancelamento de nota já existente); DefaultBackupPath salvo não preenche o diálogo. Não foi inventada uma regra de cancelamento fiscal para o formato legado.

## Validar na máquina de uso
1. Após backup e atualização, confirme versão 1.1.5. Percorra páginas 1, 2, 3, 4 e volte; confira que números/tomadores mudam junto ao indicador.
2. Combine status, datas e texto/CPF/CNPJ; altere a busca rapidamente e limpe os filtros. Confira também lista vazia e datas invertidas.
3. Confira dados de pagamento ao trocar rapidamente entre notas; valide uma alteração deliberada em nota de teste.
4. Gere relatório, CSV e dashboard; confira períodos e totais com uma amostra conhecida.
5. Gere prévia das baixas e confira importação nacional com cartão A3 real. O cartão não é simulado pelos testes.
6. A restauração real permanece operação destrutiva explicitamente confirmada pelo usuário na tela; teste-a apenas em ambiente separado, nunca para experimentar sobre seu banco de produção.
