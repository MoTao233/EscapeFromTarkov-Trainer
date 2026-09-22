param([Parameter(Mandatory = $true)][string]$GamePath)
$ErrorActionPreference = 'Stop'
$gameRoot = (Resolve-Path -LiteralPath $GamePath).Path
$gameExe = Join-Path $gameRoot 'EscapeFromTarkov.exe'
if (!(Test-Path -LiteralPath $gameExe) -or (Get-Item -LiteralPath $gameExe).VersionInfo.FileVersion -ne '0.16.1.35392') { throw 'This build requires EFT 0.16.1.35392.' }
if (Get-Process -Name EscapeFromTarkov -ErrorAction SilentlyContinue) { throw 'Close EscapeFromTarkov before installing.' }
$managed = Join-Path $gameRoot 'EscapeFromTarkov_Data\Managed'
$files = @(
    [pscustomobject]@{ Name = 'NLog.EFT.Trainer.dll'; Destination = (Join-Path $managed 'NLog.EFT.Trainer.dll'); Existed = $false; Hash = '' },
    [pscustomobject]@{ Name = 'trainer-highlights'; Destination = (Join-Path $gameRoot 'EscapeFromTarkov_Data\trainer-highlights'); Existed = $false; Hash = '' }
)
if (!(Test-Path -LiteralPath $files[0].Destination)) { throw 'An existing trainer installation is required; install the upstream trainer first.' }
foreach ($file in $files) {
    $source = Join-Path $PSScriptRoot $file.Name
    if (!(Test-Path -LiteralPath $source)) { throw "Missing $($file.Name). Run this installer from a complete built package." }
    $file.Hash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    $file.Existed = Test-Path -LiteralPath $file.Destination
}
$metadata = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'trainer-highlights.build.json') -Raw | ConvertFrom-Json
if ($files[1].Hash -ne $metadata.BundleSHA256) { throw 'Shader bundle verification failed; rebuild the package.' }
$backup = Join-Path $gameRoot ('TrainerBackups\' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$null = New-Item -ItemType Directory -Path $backup
foreach ($file in $files) {
    if (!$file.Existed) { continue }
    Copy-Item -LiteralPath $file.Destination -Destination (Join-Path $backup $file.Name)
    if ((Get-FileHash -LiteralPath (Join-Path $backup $file.Name)).Hash -ne (Get-FileHash -LiteralPath $file.Destination).Hash) {
        throw 'Backup verification failed; installation cancelled.'
    }
}
$files | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $backup 'installation.json') -Encoding UTF8
try {
    foreach ($file in $files) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file.Name) -Destination $file.Destination -Force
        if ($file.Hash -ne (Get-FileHash -LiteralPath $file.Destination).Hash) { throw 'Installed file verification failed.' }
    }
} catch {
    foreach ($file in $files) {
        if ($file.Existed) {
            Copy-Item -LiteralPath (Join-Path $backup $file.Name) -Destination $file.Destination -Force
        } elseif (Test-Path -LiteralPath $file.Destination) {
            Remove-Item -LiteralPath $file.Destination -Force
        }
    }
    throw
}
foreach ($file in $files) { Write-Host "Installed: $($file.Destination) SHA256=$($file.Hash)" }
Write-Host "Backup: $backup"
