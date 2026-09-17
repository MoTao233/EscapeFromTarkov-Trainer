param([Parameter(Mandatory = $true)][string]$GamePath)
$ErrorActionPreference = 'Stop'
$gameRoot = (Resolve-Path -LiteralPath $GamePath).Path
$gameExe = Join-Path $gameRoot 'EscapeFromTarkov.exe'
if (!(Test-Path -LiteralPath $gameExe) -or (Get-Item -LiteralPath $gameExe).VersionInfo.FileVersion -ne '0.16.1.35392') { throw 'This build requires EFT 0.16.1.35392.' }
if (Get-Process -Name EscapeFromTarkov -ErrorAction SilentlyContinue) { throw 'Close EscapeFromTarkov before installing.' }
$source = Join-Path $PSScriptRoot 'NLog.EFT.Trainer.dll'
if (!(Test-Path -LiteralPath $source)) { throw 'Run this installer from a built visible-esp package.' }
$managed = Join-Path $gameRoot 'EscapeFromTarkov_Data\Managed'
$destination = Join-Path $managed 'NLog.EFT.Trainer.dll'
if (!(Test-Path -LiteralPath $destination)) { throw 'An existing trainer installation is required; install the upstream trainer first.' }
$backup = Join-Path $gameRoot ('TrainerBackups\' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$null = New-Item -ItemType Directory -Path $backup
Copy-Item -LiteralPath $destination -Destination $backup
$backupDll = Join-Path $backup 'NLog.EFT.Trainer.dll'
if ((Get-FileHash -LiteralPath $backupDll).Hash -ne (Get-FileHash -LiteralPath $destination).Hash) { throw 'Backup verification failed; installation cancelled.' }
try {
    Copy-Item -LiteralPath $source -Destination $destination -Force
    if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $destination).Hash) { throw 'Installed file verification failed.' }
} catch {
    Copy-Item -LiteralPath $backupDll -Destination $destination -Force
    throw
}
Write-Host "Installed: $destination"
Write-Host "Backup: $backup"
