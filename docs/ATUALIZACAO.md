# Novidades da versão 1.1.4

Recuperado o botão **Importar baixas** no topo da tela de notas, com o fluxo Excel, prévia e confirmação da branch antiga. Notas fiscalmente canceladas ficam bloqueadas para novas baixas. Notas, parcelas, pagamentos e NSU são preservados. [Como usar e validar](BAIXAS.md).

# Atualização 1.1.3 — seleção e conexão com o cartão no auxiliar

A versão 1.1.2 separou o HTTPS, mas ainda enumerava e selecionava certificados dentro do gerenciador; a falha da janela persistiu. A 1.1.3 move também essas etapas para `NfseAdnWorker.exe`. O gerenciador não abre o repositório de certificados nem recebe objetos de certificado. O auxiliar seleciona o certificado na thread principal STA e executa o HTTPS no pool de threads, como no teste isolado que solicitou o PIN e retornou 50 documentos em 7,1 segundos na máquina afetada. A importação integrada ainda precisa ser validada nessa máquina.

A seleção acontece antes de iniciar a transação do banco. Fechar o seletor cancela a operação; Parar também encerra o auxiliar enquanto aguarda seleção ou PIN. Os limites são 5 minutos para selecionar o certificado, 120 segundos para cada consulta HTTPS e 150 segundos para aguardar a resposta do auxiliar à consulta.

Também registra as etapas de seleção do certificado, acesso ao banco e chamada HTTPS em `%LOCALAPPDATA%\NfseGerenciador\Logs\importacao-nacional.log`. Para localizar, pressione Win+R, cole `%LOCALAPPDATA%\NfseGerenciador\Logs` e pressione Enter.

O auxiliar permanece aberto entre os lotes e transmite os documentos em memória ao gerenciador. A gravação e o NSU continuam na mesma transação do banco. Parar ou falhar encerra o auxiliar; a próxima tentativa parte do último lote confirmado. O PIN continua sendo solicitado pelo driver e não é lido nem salvo pelo aplicativo. Cancelamento não é mais apresentado como possível tempo esgotado. Não há mudança de esquema do banco em relação à 1.1.0.

Extraia e atualize o pacote completo: `NfseAdnWorker.exe` precisa ficar junto de `NfseGerenciador.exe`. O atualizador inclui ambos. Os executáveis ainda não possuem assinatura de editor; esta alteração não resolve o aviso de reputação do SmartScreen.

Pacote para atualizar uma instalação Windows x64 existente. Não é instalador de SQL Server nem cria uma cópia dos dados em outra máquina.

1. No gerenciador antigo, faça um backup do banco em Backup/Restauração e guarde-o antes de atualizar. Anote a conexão atual em Configurações.
2. Feche o aplicativo. Baixe `NfseGerenciador-win-x64.zip` na seção Releases deste repositório e extraia todo o conteúdo.
3. Execute `Atualizar.cmd` e informe a pasta que contém o `NfseGerenciador.exe` instalado (use Abrir local do arquivo no atalho). Se a pasta exigir administrador, execute o atualizador como administrador.
4. O atualizador copia os binários anteriores para `backup-versao-*` e preserva `appsettings.json`. Nenhum arquivo de banco é substituído.
5. Abra pelo atalho habitual. A migração adiciona campos e tabelas ao mesmo banco SQL Server, mantendo notas, parcelas e baixas. A conta de conexão precisa de permissão para alterações de estrutura.

## Importação

Abra Gerenciar Notas → Importar NFS-e Nacional. Informe o CNPJ representado, conecte o A3 e selecione seu e-CPF. O PIN é solicitado pelo driver; não é salvo pelo aplicativo. A1 instalado no Windows também pode ser selecionado.

A primeira consulta percorre o histórico disponível na API. Só entram notas em que o CNPJ informado é emitente e a data de geração da NFS-e (`dhProc`, distinta da emissão da DPS) é 01/11/2026 ou posterior. O NSU é salvo no banco junto com cada lote confirmado. Parar/erro desfaz apenas o lote em andamento; repetir continua do último lote. Reimportação por chave não modifica pagamentos anteriores.

Cancelamentos efetivos ficam marcados na coluna Cancelada. Notas canceladas não geram previsão em aberto; recebimentos já realizados continuam preservados. Solicitação ou indeferimento de cancelamento não cancelam uma nota. Não é possível registrar nova baixa numa nota fiscal cancelada nesta versão.

A API não oferece filtro por data e exige o certificado durante as consultas. Computadores que usam o mesmo banco compartilham o progresso. Bancos separados precisam sincronizar separadamente; esta atualização não transfere banco entre máquinas.

## Validação e limites

O pacote passa por compilação e testes com documentos sintéticos. O acesso por procuração foi validado com o certificado do usuário pelo navegador; a execução do novo aplicativo com o A3 e o banco real precisa ser conferida no computador de destino. Nenhum certificado, PIN, XML real ou banco integra este pacote.

Se a atualização falhar, mantenha o aplicativo fechado e recupere os binários do diretório `backup-versao-*`. A migração é aditiva; não reverta o banco sem avaliar alterações posteriores à atualização. O instalador antigo da pasta Instalador não contém esta versão.
