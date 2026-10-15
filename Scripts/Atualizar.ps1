param(
    [Parameter(Mandatory=$true)][string]$Destino,
    [string]$Origem=(Join-Path $PSScriptRoot 'app')
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$source=[IO.Path]::GetFullPath($Origem).TrimEnd([IO.Path]::DirectorySeparatorChar)
$target=[IO.Path]::GetFullPath($Destino).TrimEnd([IO.Path]::DirectorySeparatorChar)
if($source -eq $target -or $source.StartsWith($target+'\',[StringComparison]::OrdinalIgnoreCase) -or $target.StartsWith($source+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Origem e destino devem ser pastas separadas.'}
if(!(Test-Path -LiteralPath (Join-Path $target 'NfseGerenciador.exe')) -or !(Test-Path -LiteralPath (Join-Path $target 'appsettings.json'))){throw 'Selecione a pasta da instalacao existente, com NfseGerenciador.exe e appsettings.json.'}
if(Get-Process -Name 'NfseGerenciador' -ErrorAction SilentlyContinue){throw 'Feche todas as janelas do NFSe antes de atualizar.'}
function SafePath([string]$root,[string]$relative){
    if([IO.Path]::IsPathRooted($relative) -or $relative.Contains(':') -or ($relative -split '[\\/]' | Where-Object {$_ -eq '..' -or $_ -eq '.' -or $_ -eq ''})){throw 'Caminho invalido no pacote.'}
    $full=[IO.Path]::GetFullPath((Join-Path $root $relative))
    if(!$full.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Arquivo fora da pasta autorizada.'}
    $part=$root
    if((Get-Item -LiteralPath $part).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Pastas com links nao sao aceitas.'}
    foreach($segment in ($relative -split '[\\/]')){
        $part=Join-Path $part $segment
        if((Test-Path -LiteralPath $part) -and ((Get-Item -LiteralPath $part).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Links nao sao aceitos no pacote ou destino.'}
    }
    return $full
}
$manifest=Get-Content -LiteralPath (Join-Path $source 'manifest.json') -Raw | ConvertFrom-Json
$entries=@($manifest.files)
if($entries.Count -eq 0 -or @($entries | Where-Object {$_.path -eq 'NfseGerenciador.exe'}).Count -ne 1){throw 'Pacote incompleto.'}
$paths=@{}
foreach($entry in $entries){
    $leaf=[IO.Path]::GetFileName($entry.path)
    if($leaf -like 'appsettings*.json' -or [IO.Path]::GetExtension($leaf) -in '.bak','.mdf','.ldf'){throw 'O pacote nao pode substituir configuracao ou dados.'}
    if($paths.ContainsKey($entry.path)){throw 'Arquivo duplicado no pacote.'}
    $paths[$entry.path]=$true
    $file=SafePath $source $entry.path
    $null=SafePath $target $entry.path
    if((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $entry.sha256){throw "Falha de integridade: $($entry.path)"}
}
$config=Join-Path $target 'appsettings.json'
$configHash=(Get-FileHash -LiteralPath $config -Algorithm SHA256).Hash
$backup=Join-Path (Split-Path $target -Parent) ('nfse-backup-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $backup | Out-Null
Copy-Item -LiteralPath $config -Destination (Join-Path $backup 'appsettings.json')
# Prepare every backup before replacing the first application file.
foreach($entry in $entries){
    $to=SafePath $target $entry.path
    if(Test-Path -LiteralPath $to){
        $saved=SafePath $backup $entry.path
        New-Item -ItemType Directory -Force -Path (Split-Path $saved -Parent) | Out-Null
        Copy-Item -LiteralPath $to -Destination $saved
    }
}
$touched=[Collections.Generic.List[string]]::new()
try{
    foreach($entry in $entries){
        $from=SafePath $source $entry.path
        $to=SafePath $target $entry.path
        New-Item -ItemType Directory -Force -Path (Split-Path $to -Parent) | Out-Null
        $touched.Add($entry.path)
        Copy-Item -LiteralPath $from -Destination $to -Force
        if((Get-FileHash -LiteralPath $to -Algorithm SHA256).Hash -ne $entry.sha256){throw 'Arquivo copiado nao confere.'}
    }
    if((Get-FileHash -LiteralPath $config -Algorithm SHA256).Hash -ne $configHash){throw 'A configuracao foi alterada durante a atualizacao.'}
    Write-Output "Atualizacao de arquivos concluida. Configuracao preservada. Copia de seguranca: $backup"
    Write-Output 'Abra NfseGerenciador.exe. O programa verificara o backup do banco antes de aplicar a migracao.'
}
catch{
    $original=$_
    $restoreErrors=[Collections.Generic.List[string]]::new()
    foreach($relative in $touched){
        try{
            $to=SafePath $target $relative
            $saved=SafePath $backup $relative
            if(Test-Path -LiteralPath $saved){
                if(!(Test-Path -LiteralPath $to) -or (Get-FileHash -LiteralPath $to).Hash -ne (Get-FileHash -LiteralPath $saved).Hash){Copy-Item -LiteralPath $saved -Destination $to -Force}
            }elseif(Test-Path -LiteralPath $to){Remove-Item -LiteralPath $to -Force}
        }catch{$restoreErrors.Add($relative)}
    }
    if($restoreErrors.Count -gt 0){throw "Atualizacao interrompida. Recupere os arquivos de $backup antes de abrir o programa. Falhas: $($restoreErrors -join ', ')"}
    throw "Atualizacao interrompida; arquivos anteriores recuperados. $($original.Exception.Message)"
}

