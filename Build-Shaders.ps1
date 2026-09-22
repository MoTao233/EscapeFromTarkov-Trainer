param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\2022.3.43f1\Editor\Unity.exe',
    [switch]$Test
)
$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath $UnityPath)) { throw 'Unity 2022.3.43f1 not found; pass -UnityPath to specify Unity.exe.' }
$shaderProject = Join-Path $PSScriptRoot 'ShaderProject'
$runtime = Join-Path $shaderProject 'Assets\Runtime'
$null = New-Item -ItemType Directory -Path $runtime -Force
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Rendering') -Filter '*.cs' | Copy-Item -Destination $runtime -Force
$output = Join-Path $PSScriptRoot 'artifacts\shaders'
$null = New-Item -ItemType Directory -Path $output -Force
$log = Join-Path $output 'unity-build.log'
$arguments = @('-batchmode', '-force-d3d11', '-projectPath', ('"' + $shaderProject + '"'), '-executeMethod', 'TrainerShaderBuild.Build', '-logFile', ('"' + $log + '"'))
if ($Test) { $arguments += '-trainerGraphicsChecks' }
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Host "Unity PID: $($process.Id); log: $log"
$process.WaitForExit()
$process.Refresh()
if ($process.ExitCode -ne 0) { throw "Unity shader build failed. See $log" }
$logText = Get-Content -LiteralPath $log -Raw
if ($logText -notmatch 'TRAINER_SHADER_BUILD_OK') { throw "Unity did not complete the build. See $log" }
if ($Test -and $logText -notmatch 'TRAINER_GRAPHICS_CHECKS_OK') { throw "Unity did not complete graphics checks. See $log" }
$bundle = Join-Path $output 'trainer-highlights'
if (!(Test-Path -LiteralPath $bundle)) { throw 'Unity did not produce the highlight bundle.' }
Copy-Item -LiteralPath $bundle -Destination (Join-Path $PSScriptRoot 'Files\trainer-highlights') -Force
$sources = @{}
foreach ($name in @('HighlightMask.shader', 'HighlightComposite.shader')) {
    $sources[$name] = (Get-FileHash -LiteralPath (Join-Path $shaderProject "Assets\Trainer\$name") -Algorithm SHA256).Hash
}
@{ UnityVersion = '2022.3.43f1'; BundleSHA256 = (Get-FileHash -LiteralPath $bundle -Algorithm SHA256).Hash; Sources = $sources } |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'Files\trainer-highlights.build.json') -Encoding UTF8
Write-Host "Built: $bundle"
