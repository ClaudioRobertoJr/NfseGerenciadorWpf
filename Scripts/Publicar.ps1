param([string]$Dotnet='dotnet',[Parameter(Mandatory=$true)][string]$Destino)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$package=[IO.Path]::GetFullPath($Destino)
if((Test-Path -LiteralPath $package) -and (Get-ChildItem -LiteralPath $package -Force | Select-Object -First 1)){throw 'Escolha uma pasta de saida vazia.'}
New-Item -ItemType Directory -Force -Path $package | Out-Null
$app=Join-Path $package 'app'
& $Dotnet publish (Join-Path $repo 'NfseGerenciador.csproj') -c Release -r win-x64 --self-contained true -o $app --nologo '-p:DebugType=None' '-p:DebugSymbols=false'
if($LASTEXITCODE -ne 0){throw 'A publicacao falhou.'}
if(Test-Path -LiteralPath (Join-Path $app 'appsettings.json')){throw 'Configuracao real encontrada no pacote; publicacao interrompida.'}
$entries=@(Get-ChildItem -LiteralPath $app -Recurse -File | Where-Object {$_.Name -notlike 'appsettings*.json'} | ForEach-Object {
    $relative=$_.FullName.Substring($app.Length+1)
    if($_.Extension -in '.bak','.mdf','.ldf'){throw 'Arquivo de dados encontrado na publicacao.'}
    [pscustomobject]@{path=$relative;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
})
[pscustomobject]@{version='2.0.2';files=$entries} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $app 'manifest.json') -Encoding UTF8
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Atualizar.ps1') -Destination $package
Copy-Item -LiteralPath (Join-Path $repo 'docs/ATUALIZACAO.md') -Destination (Join-Path $package 'LEIA-ME.md')
Copy-Item -LiteralPath (Join-Path $repo 'docs/VALIDACAO.md') -Destination (Join-Path $package 'VALIDACAO.md')
Write-Output "Pacote criado em $package"

