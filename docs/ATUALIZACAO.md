# Atualização do NFSe — versão 2.0

Este pacote atualiza uma instalação existente do NFSe em Windows x64. Ele contém o aplicativo e o runtime .NET necessários. O SQL Server continua sendo o da instalação atual.

## Antes de atualizar

1. Feche o NFSe em todas as máquinas que usam o mesmo banco.
2. Identifique a pasta que contém o `NfseGerenciador.exe` atual e o seu `appsettings.json`.
3. Guarde uma cópia do `appsettings.json`. Ele contém a conexão da sua instalação. Não o substitua pelo exemplo do pacote.
4. Faça um backup do banco pelo programa antigo ou pelo SQL Server. A versão nova também exigirá um backup verificado antes da migração.

## Aplicar o pacote

Extraia o ZIP de atualização em uma pasta separada, fora da pasta instalada. Abra um PowerShell nessa pasta e execute, ajustando o destino:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Atualizar.ps1 -Destino "C:\Caminho\Da\Instalacao"
```

Se a instalação estiver em `Arquivos de Programas`, poderá ser necessário abrir o PowerShell como administrador. O script exige o programa fechado, verifica os hashes do pacote, guarda os arquivos anteriores em uma pasta `nfse-backup-...` ao lado da instalação e preserva `appsettings.json`. Se uma cópia falhar, tenta recuperar os arquivos substituídos e não abre o aplicativo.

**Não copie somente o EXE:** o aplicativo depende das DLLs incluídas no pacote. Não use o instalador antigo do repositório para aplicar esta atualização.

## Primeira abertura

Abra o executável da instalação atualizada. Ele usa o `appsettings.json` da própria pasta, independentemente do atalho ou diretório de execução.

Ao detectar a nova migração, o programa pede confirmação, cria um backup `COPY_ONLY` com checksum, executa `RESTORE VERIFYONLY` e só então aplica a migração. Serão acrescentadas as tabelas de histórico de lotes e de baixas. Notas, parcelas e pagamentos existentes não são excluídos pela migração.

O backup é gravado **pela máquina/serviço do SQL Server**, na pasta `AppSettings:DefaultBackupPath` quando configurada ou na pasta padrão da instância. O usuário da conexão precisa de permissão de backup e atualização; a conta do serviço SQL precisa poder gravar nessa pasta. Se o backup falhar, a migração não prossegue.

Se não houver conexão, configuração válida ou histórico de migrações reconhecido, o programa interrompe a abertura. Ele não cria outro banco automaticamente. Este pacote é destinado a atualização; uma instalação inicial requer preparar/configurar o banco deliberadamente.

## Importar baixas

1. Importe as notas XML no fluxo existente.
2. Clique em **Importar baixas**, escolha o `.xlsx` e confira o prestador. Se existir mais de um, selecione o correto.
3. Clique em **Gerar prévia**. Consulte as abas **Aptas**, **Conferir** e **Ignoradas**, inclusive as colunas de motivo, histórico e centro de custo.
4. Confirme apenas as linhas desejadas. A data do recebimento será a **data da linha do relatório**.

Regras: contrapartidas 4889/230 → Pix; 132/108 → Dinheiro. Local Particular, exceto laboratório. SIPAG/cartões ficam manuais. DOC não é tratado como nota fiscal. Notas já pagas, lançamentos importados, valores diferentes, nomes divergentes, datas anteriores à emissão e parcelamentos incompatíveis não recebem baixa automática. Vários lançamentos para uma nota não são somados silenciosamente.

A parcela única pendente criada pela importação XML pode ser baixada, mantendo seu registro. Parcelamentos reais e estados inconsistentes ficam para revisão. O lote revalida os dados no momento da confirmação; se a nota mudou, a operação é interrompida e deve ser gerada outra prévia.

## Recuperação

O backup manual acrescenta um identificador ao nome escolhido para sempre gerar um arquivo próprio, sem anexar novos backups a um arquivo antigo. Use o caminho exibido na mensagem de sucesso para localizar o `.bak` criado. Os caminhos de backup continuam sendo interpretados pelo servidor SQL.

A pasta `nfse-backup-...` contém os binários anteriores e uma cópia da configuração, não o backup SQL. Se a cópia do programa falhar, siga a mensagem do atualizador e não abra uma instalação parcialmente copiada.

Restaurar os binários não desfaz uma migração do banco. A restauração de banco é uma operação separada e substitui dados; use o backup SQL correto com orientação técnica. Nunca restaure um banco vazio para atualizar o sistema.

## Validação e limites desta entrega

- Compilação, testes de regras e testes do atualizador são executados nesta entrega; os resultados finais constam em `VALIDACAO.md` no código-fonte.
- A prévia visual foi renderizada com dados fictícios; não comprova a operação de uma instalação conectada ao SQL Server.
- O ambiente desta entrega não possui SQL Server. A tentativa de instalar LocalDB foi interrompida pelo Windows na elevação. Os testes de integração SQL estão preparados, mas sua execução depende desse ambiente. A migração deve ser ensaiada em cópia do banco da instalação antes de usar em produção.

Nenhuma conexão com o banco real foi feita durante o desenvolvimento e nada foi enviado ao GitHub.
