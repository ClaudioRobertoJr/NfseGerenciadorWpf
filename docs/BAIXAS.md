# Recuperação da importação de baixas — 1.1.4

## Diagnóstico
A master publicada em v1.1.3 (9e8fa22) não contém a importação de baixas. O recurso foi desenvolvido na branch feat/baixas-layout-atualizacao, a partir de 9f9c4b8, e não integrado à master. A janela foi adicionada em 5333b87 e o acesso no topo da tela de notas em c1fc349, com o texto “Importar baixas”. Não houve remoção desse botão pelas correções A3.

## Fluxo recuperado
Na tela de notas, clique em **Importar baixas**, escolha o relatório Excel (.xlsx) e o prestador, e gere a prévia. A prévia não grava alterações. Ela cruza a referência explícita à nota no histórico com o número, CNPJ do prestador e valor líquido. Confirme apenas os recebimentos desejados.

As contrapartidas 4889/230 correspondem a Pix; 132/108, Dinheiro. A data é a do lançamento no relatório. Cartão/SIPAG, estornos, valores divergentes e parcelamentos não são baixados automaticamente. Divergências de nome/data aparecem em Conferir e exigem seleção explícita. Notas já pagas são omitidas. O histórico transacional impede repetir lançamentos e revalida o estado antes de confirmar.

O Excel deve conter Centro de Custo, Conta, Empresa, Data, Lote, Contra Partida, Histórico, Débito e Crédito. Este é o funcionamento recuperado da branch, não uma regra nova de baixa.

## Integração com a versão nacional
Notas fiscalmente canceladas não podem receber novas baixas. Cancelamento fiscal e recebimento financeiro permanecem distintos. O histórico usa o mesmo identificador de migração da branch antiga, preservando tabelas já existentes em instalações que a tenham usado. Na 1.1.3 são acrescentadas apenas as duas tabelas de auditoria de baixas e seus índices/relações. A importação nacional, o NSU e o auxiliar NfseAdnWorker permanecem preservados.

## Validação na máquina de uso
Após backup do banco e atualização pelo Atualizar.cmd, confira o botão na tela de notas. Gere uma prévia com um relatório conhecido e confira nota, prestador, valor, forma e data antes de confirmar um pequeno lote. Verifique o resultado na lista, abra novamente o mesmo relatório e confira que os recebimentos pagos não reaparecem. Confirme também a abertura da importação nacional com o cartão A3 e a continuidade do NSU. Os testes automatizados não substituem essa validação com o relatório e o cartão reais.
