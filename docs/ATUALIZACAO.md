# Atualização 1.1.0 — NFS-e Nacional

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
