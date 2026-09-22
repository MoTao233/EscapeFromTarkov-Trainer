param(
    [Parameter(Mandatory = $true)][string]$GamePath,
    [ValidateSet('zh-cn', 'en')][string]$Language = 'zh-cn',
    [switch]$Test,
    [switch]$BuildShaders,
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\2022.3.43f1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
if ($BuildShaders) { & (Join-Path $projectRoot 'Build-Shaders.ps1') -UnityPath $UnityPath -Test:$Test }
$shaderBundle = Join-Path $projectRoot 'Files\trainer-highlights'
$shaderBuild = Join-Path $projectRoot 'Files\trainer-highlights.build.json'
if (!(Test-Path -LiteralPath $shaderBundle) -or !(Test-Path -LiteralPath $shaderBuild)) { throw 'Run Build-Shaders.ps1 first, or pass -BuildShaders.' }
$metadata = Get-Content -LiteralPath $shaderBuild -Raw | ConvertFrom-Json
if ((Get-FileHash -LiteralPath $shaderBundle).Hash -ne $metadata.BundleSHA256) { throw 'Shader bundle hash mismatch; rebuild shaders.' }
foreach ($source in $metadata.Sources.PSObject.Properties) {
    $path = Join-Path $projectRoot ('ShaderProject\Assets\Trainer\' + $source.Name)
    if ((Get-FileHash -LiteralPath $path).Hash -ne $source.Value) { throw 'Shader sources changed; run Build-Shaders.ps1 or pass -BuildShaders.' }
}
$gameRoot = (Resolve-Path -LiteralPath $GamePath).Path
$managed = Join-Path $gameRoot 'EscapeFromTarkov_Data\Managed'
if (!(Test-Path -LiteralPath (Join-Path $managed 'Assembly-CSharp.dll'))) { throw 'Game managed assemblies not found.' }
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$vsRoot = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -property installationPath
if (!$vsRoot) { throw 'Visual Studio MSBuild not found.' }
$msbuild = Join-Path $vsRoot 'MSBuild\Current\Bin\MSBuild.exe'
$output = Join-Path $projectRoot "artifacts\build-$Language"
& $msbuild (Join-Path $projectRoot 'NLog.EFT.Trainer.csproj') -restore -t:Rebuild -nologo -verbosity:minimal '-p:Configuration=Release' "-p:EFTBasePath=$gameRoot" "-p:TrainerLanguage=$Language" "-p:OutputPath=$output\" '-p:DeployToGame=false'
if ($LASTEXITCODE -ne 0) { throw 'Trainer build failed.' }
$package = Join-Path $projectRoot "artifacts\visible-esp-$Language"
$null = New-Item -ItemType Directory -Path $package -Force
Copy-Item -LiteralPath (Join-Path $output 'NLog.EFT.Trainer.dll') -Destination $package
Copy-Item -LiteralPath $shaderBundle,$shaderBuild -Destination $package
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\VISIBLE-ESP.md') -Destination $package
Copy-Item -LiteralPath (Join-Path $projectRoot 'Install-Local.ps1') -Destination $package
if ($Test) {
    $testOutput = Join-Path $projectRoot 'artifacts\tests'
    $null = New-Item -ItemType Directory -Path $testOutput -Force
    $core = Join-Path $managed 'UnityEngine.CoreModule.dll'
    $compiler = Join-Path $vsRoot 'MSBuild\Current\Bin\Roslyn\csc.exe'
    $netstandard = Join-Path $managed 'netstandard.dll'
    & $compiler -nologo -langversion:latest "-r:$core" "-r:$netstandard" "-out:$testOutput\EspGeometryTests.exe" (Join-Path $projectRoot 'Features\EspGeometry.cs') (Join-Path $projectRoot 'Tests\EspGeometryTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Geometry test compilation failed.' }
    Copy-Item -LiteralPath $core -Destination $testOutput -Force
    Copy-Item -LiteralPath $netstandard -Destination $testOutput -Force
    & (Join-Path $testOutput 'EspGeometryTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Geometry regression tests failed.' }
}
Write-Host "Package: $package"
