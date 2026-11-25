param([switch]$DownloadFixtures,[switch]$NetworkDownloads)
$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $project
$stage=Join-Path $project '.work\release'
if(!(Test-Path -LiteralPath (Join-Path $stage '文件万能处理器.exe'))){& (Join-Path $PSScriptRoot 'build-v3.ps1') -GuiOnly}
$pythonArgs=@('scripts/prepare-speech-tests.py');if($DownloadFixtures){$pythonArgs+='--download'}
& '.\.venv\Scripts\python.exe' @pythonArgs
if($LASTEXITCODE -ne 0){throw 'Speech test resources missing or invalid'}
$run=Join-Path $project ('.work\speech-checks-'+[guid]::NewGuid().ToString('N'))
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
foreach($name in @('SpeechChecks','SpeechNavigationChecks','EngineDownloadChecks','EngineBackgroundChecks')){
    & $compiler /nologo /target:exe /platform:x64 /codepage:65001 /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /r:System.Web.Extensions.dll "/r:$stage\文件万能处理器.exe" "/out:$stage\$name.exe" (Join-Path $project "tests\$name.cs")
    if($LASTEXITCODE -ne 0){throw 'Speech test compilation failed'}
}
& (Join-Path $stage 'SpeechChecks.exe') (Join-Path $run 'speech') (Join-Path $project '.work\speech-artifacts')
if($LASTEXITCODE -ne 0){throw 'Native speech checks failed'}
& (Join-Path $stage 'SpeechNavigationChecks.exe') (Join-Path $run 'navigation')
if($LASTEXITCODE -ne 0){throw 'Speech navigation checks failed'}
& (Join-Path $stage 'EngineBackgroundChecks.exe') (Join-Path $run 'background')
if($LASTEXITCODE -ne 0){throw 'Engine background checks failed'}
if($NetworkDownloads){& (Join-Path $stage 'EngineDownloadChecks.exe') (Join-Path $run 'network');if($LASTEXITCODE -ne 0){throw 'HTTPS/resume checks failed'}}
Write-Output "SPEECH_CHECKS=$run"
