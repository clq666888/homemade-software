param([switch]$NetworkDownloads,[switch]$DpiAware)
$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $project
& (Join-Path $PSScriptRoot 'build-v3.ps1') -GuiOnly
if(!$?){throw 'GUI build failed'}
$stage=Join-Path $project '.work\release'
$run=Join-Path $project ('.work\management-checks-'+[guid]::NewGuid().ToString('N'))
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$names=@('EngineManagementChecks','EngineBackgroundChecks','SpeechNavigationChecks','SpeechSampleChecks');if($NetworkDownloads){$names+='EngineManagementNetworkChecks'}
foreach($name in $names){
    & $compiler /nologo /target:exe /platform:x64 /codepage:65001 /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /r:System.Web.Extensions.dll "/r:$stage\文件万能处理器.exe" "/out:$stage\$name.exe" (Join-Path $project "tests\$name.cs")
    if($LASTEXITCODE -ne 0){throw 'Management test compilation failed'}
    $testArgs=@((Join-Path $run $name));if($DpiAware -and $name -eq 'EngineManagementChecks'){$testArgs+='--dpi-aware'}
    & (Join-Path $stage "$name.exe") @testArgs
    if($LASTEXITCODE -ne 0){throw "$name checks failed"}
}
Write-Output "MANAGEMENT_CHECKS=$run"
