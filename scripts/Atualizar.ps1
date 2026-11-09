param([string]$PastaInstalada, [switch]$NonInteractive)
$ErrorActionPreference = 'Stop'
try {
    if (-not $PastaInstalada) {
        if ($NonInteractive) { throw 'Informe PastaInstalada.' }
        $PastaInstalada = Read-Host 'Cole a pasta onde está o NfseGerenciador.exe atual'
    }
    $target = (Resolve-Path -LiteralPath $PastaInstalada).Path
    $source = Join-Path $PSScriptRoot 'app'
    if (-not (Test-Path -LiteralPath (Join-Path $target 'NfseGerenciador.exe'))) { throw 'A pasta escolhida não contém o gerenciador instalado.' }
    if (-not (Test-Path -LiteralPath (Join-Path $target 'appsettings.json'))) { throw 'Configuração existente não encontrada. Não foi feita nenhuma alteração.' }
    if (-not (Test-Path -LiteralPath (Join-Path $source 'NfseGerenciador.exe'))) { throw 'Extraia todo o ZIP antes de atualizar.' }
    if (Get-Process -Name NfseGerenciador -ErrorAction SilentlyContinue) { throw 'Feche o gerenciador antes de atualizar.' }
    $backup = Join-Path $target ('backup-versao-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Path $backup | Out-Null
    # Only files shipped in this package are replaced. No database file is touched.
    $files = @(Get-ChildItem -LiteralPath $source -File | Where-Object Name -ne 'appsettings.json')
    foreach ($file in $files) {
        $old = Join-Path $target $file.Name
        if (Test-Path -LiteralPath $old) { Copy-Item -LiteralPath $old -Destination $backup }
    }
    Copy-Item -LiteralPath (Join-Path $target 'appsettings.json') -Destination $backup
    foreach ($file in $files) { Copy-Item -LiteralPath $file.FullName -Destination $target -Force }
    Write-Host "Atualizado. Versão anterior em: $backup"
    Write-Host 'Abra o gerenciador pelo atalho habitual. Faça backup do banco antes da primeira abertura da nova versão.'
} catch {
    Write-Host ('Não foi possível concluir: ' + $_.Exception.Message) -ForegroundColor Red
    Write-Host 'Se iniciou a cópia, a versão anterior está na pasta backup-versao dentro da instalação.'
    if (-not $NonInteractive) { Read-Host 'Pressione Enter para fechar' }
    exit 1
}
if (-not $NonInteractive) { Read-Host 'Pressione Enter para fechar' }
