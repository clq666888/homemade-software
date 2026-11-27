param([switch]$DownloadFixture)
$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $project
& (Join-Path $PSScriptRoot 'build-v3.ps1') -GuiOnly
if(!$?){throw 'Video GUI build failed'}
$arguments=@('scripts/prepare-video-tests.py');if($DownloadFixture){$arguments+='--download'}
& '.\.venv\Scripts\python.exe' @arguments
if($LASTEXITCODE -ne 0){throw 'Video fixtures missing or invalid'}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$stage=Join-Path $project '.work\release'
& $compiler /nologo /target:exe /platform:x64 /codepage:65001 /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll "/r:$stage\文件万能处理器.exe" "/out:$stage\VideoChecks.exe" (Join-Path $project 'tests\VideoChecks.cs')
if($LASTEXITCODE -ne 0){throw 'Video test compilation failed'}
$run=Join-Path $project ('.work\video-checks-'+[guid]::NewGuid().ToString('N'))
& (Join-Path $stage 'VideoChecks.exe') $run (Join-Path $project '.work\video-runtime.zip')
if($LASTEXITCODE -ne 0){throw 'Video checks failed'}
Write-Output "VIDEO_CHECKS=$run"
