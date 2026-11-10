$ErrorActionPreference = 'Stop'
[xml]$view = Get-Content (Join-Path $PSScriptRoot '../../Views/GerenciadorNfseWindow.xaml') -Raw
$button = $view.SelectSingleNode("//*[local-name()='Button' and @Content='Importar baixas']")
if ($null -eq $button) { throw 'O acesso Importar baixas está ausente da tela de notas.' }
if ($button.Click -ne 'ImportarBaixas') { throw 'O botão não abre o fluxo de baixas.' }
Write-Output 'PASS: acesso à importação de baixas presente.'
