# Validação da versão 2.0 — 16/10/2026

## Versão 2.0.3 — cadastros equivalentes de Dinheiro

101 testes encontrados: 88 aprovados, 13 SQL ignorados, nenhuma falha. Dois testes reproduziram antes da correção o bloqueio com 1.102 cadastros de dinheiro equivalentes e com candidatos incompatíveis de menor ID. A resolução agora seleciona o menor ID somente entre cadastros compatíveis, independentemente da ordem da consulta. Teste SQL de preservação de pagamentos históricos acrescentado, não executado por ausência de instância. Revisão direcionada sem achados importantes; não há migração ou limpeza de registros.

## Versão 2.0.2 — 19/10/2026

98 testes encontrados: 86 aprovados, 12 SQL ignorados, nenhuma falha. Quatro regressões reproduziram o bloqueio de seleção de avisos nome/data e a ausência da marcação de notas já pagas antes da correção. Cobertos checkbox real WPF habilitado em Conferir, seleção múltipla sem formulário, totais, remoção imediata das baixadas e ocultação de pagas de todas as abas. A revalidação do lote aceita avisos selecionáveis e mantém bloqueios financeiros. Teste SQL de baixa de duas notas em Conferir sem aprovação individual acrescentado, não executado por ausência de instância local. Sem nova migração de esquema.

## Correção 2.0.1 — conferência manual

Resultado atualizado: 88 testes encontrados, 77 aprovados, 11 SQL ignorados, nenhuma falha. Reproduzido o erro anterior do grid: `InvalidOperationException: EditItem não é permitido para esse modo de exibição` com a configuração antiga; a configuração corrigida passa mantendo o checkbox funcional. Cobertos associação manual, bloqueios financeiros, confirmação/cancelamento, preservação de seleção e inicialização padrão do atualizador no Windows PowerShell. Dois novos testes SQL cobrem auditoria e rejeição de nota alterada após conferência, mas não foram executados neste ambiente. Revisão direcionada do patch concluída sem achados importantes residuais. Sem mudança de esquema do banco nesta correção.

Os resultados abaixo registram a entrega original 2.0.

## Resultados executados

- `dotnet test Tests/NfseGerenciador.Tests.csproj -c Release`: 73 testes encontrados, 64 aprovados, 9 ignorados, nenhuma falha.
- A revisão identificou que referências com ponto poderiam usar apenas o prefixo numérico. Três testes reproduziram a falha antes da correção; referências como `NOTA 8000.1`, `NOTA 8.000` e `NOTA 8000 . 1` agora ficam para conferência.
- Outros ajustes da revisão: backup manual gera arquivo com identificador único e mostra o caminho retornado; limpar filtros notifica também o controle de local. A geração de caminhos de backup foi testada com caminhos locais e UNC.
- Publicação Release Windows x64, com runtime .NET incluído: concluída.
- Regras verificadas: leitura Excel, classificação conservadora das baixas, valores/datas/nomes divergentes, cartões, duplicidades, seleção e ciclo da prévia, configuração e atualizador de arquivos.
- Atualizador verificado com instalação descartável: preservação da configuração, rejeição de origem/destino iguais, hashes, caminhos inválidos e recuperação após falha de cópia.
- Pacote publicado também aplicado em uma instalação descartável: 503 arquivos verificados por SHA-256, configuração preservada e cópia dos arquivos anteriores criada. O pacote não contém `appsettings.json` real nem banco de dados.
- Telas renderizadas com dados fictícios, incluindo largura menor: início, notas e importação. Isso verifica apresentação, não substitui teste interativo conectado ao SQL Server.
- O projeto ainda apresenta avisos de compilação, principalmente de nulabilidade herdados da base. Não houve erro de compilação.

## Conferência com os anexos

A leitura do Excel fornecido encontrou 721 lançamentos, sem diagnóstico de célula inválida. Saldos de abertura/fechamento não são pagamentos. O importador XML existente retornou 1.306 notas após desconsiderar canceladas.

| Resultado preliminar | Quantidade |
|---|---:|
| Nota e valor conferidos | 390 |
| Cartão/SIPAG: manual | 148 |
| Sem referência única e explícita à NF | 79 |
| Nome diferente do tomador | 48 |
| Recebimento anterior à emissão | 35 |
| Mais de um lançamento para a mesma nota | 12 |
| Nota não encontrada para o prestador | 9 |

As 390 candidatas somam R$ 611.958,50. Essa é uma simulação usando os dois anexos; o banco da instalação pode conter notas já pagas, exclusões, parcelas ou importações anteriores. A prévia no aplicativo reavalia esses estados e pode apresentar outro total. Nenhuma baixa real foi executada.

## Validação pendente em SQL Server

Os 9 testes de integração foram ignorados porque não há SQL Server neste ambiente. A tentativa de instalar LocalDB foi interrompida na elevação do Windows. Portanto, persistência, concorrência, backup e migração ainda precisam de execução em SQL Server antes de considerar a atualização validada para produção.

Os testes usam `NFSE_TEST_CONNECTION` para criar bancos descartáveis com nomes `NfseTests_...`; não devem ser apontados a credenciais sem autorização para testes. Use uma instância de desenvolvimento e autenticação integrada, sem registrar senhas no repositório.

```powershell
$env:NFSE_TEST_CONNECTION = 'Server=SUA_INSTANCIA_DE_TESTES;Integrated Security=True;TrustServerCertificate=True'
dotnet test Tests/NfseGerenciador.Tests.csproj -c Release
```

Além dos testes automatizados, ensaie a atualização em uma cópia restaurada do banco existente e confira notas, parcelas, pagamentos e backup antes/depois. Consulte `ATUALIZACAO.md`.

Nenhuma conexão ao banco real foi realizada; nenhum arquivo foi enviado ao GitHub.

## Limitações preexistentes observadas na revisão

A importação XML original pode admitir referências repetidas dentro do mesmo lote ainda não salvo; nesse caso, a conciliação de baixas bloqueia o número duplicado para conferência. A edição manual de pagamentos ainda recria as parcelas; a importação de baixas nova preserva a parcela única compatível. A restauração manual e o instalador antigo não foram reformulados.
