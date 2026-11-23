param([switch]$SkipWord)
$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $project
$python=Join-Path $project '.venv\Scripts\python.exe'
$stage=Join-Path $project '.work\release'
if(!(Test-Path -LiteralPath (Join-Path $stage '文件万能处理器.exe'))){
    New-Item -ItemType Directory -Force -Path $stage | Out-Null
    $published=Join-Path $project 'dist\文件万能处理器'
    Copy-Item -LiteralPath (Join-Path $published '文件万能处理器.exe') -Destination $stage
    foreach($name in @('engine','licenses')){Copy-Item -LiteralPath (Join-Path $published $name) -Destination (Join-Path $stage $name) -Recurse}
}
$engine=Join-Path $stage 'engine\PdfEngine.exe'
$run=Join-Path $project ('.work\checks-v3-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
& $python (Join-Path $project 'tests\pdf_regression.py') $run $engine
if($LASTEXITCODE -ne 0){throw 'PDF regression failed'}
$officeRun=Join-Path $run 'office'
& $python (Join-Path $project 'tests\office_formats.py') $officeRun $engine
if($LASTEXITCODE -ne 0){throw 'Office/HTML backend checks failed'}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:x64 /codepage:65001 /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll "/r:$stage\文件万能处理器.exe" "/out:$stage\DownloadChecks.exe" (Join-Path $project 'tests\DownloadChecks.cs')
if($LASTEXITCODE -ne 0){throw 'Download test compile failed'}
& (Join-Path $stage 'DownloadChecks.exe') (Join-Path $run 'download')
if($LASTEXITCODE -ne 0){throw 'Download regression failed'}
& $compiler /nologo /target:exe /platform:x64 /codepage:65001 /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll "/r:$stage\文件万能处理器.exe" "/out:$stage\LayoutChecks.exe" (Join-Path $project 'tests\LayoutChecks.cs')
if($LASTEXITCODE -ne 0){throw 'Layout test compile failed'}
& (Join-Path $stage 'LayoutChecks.exe') $run
if($LASTEXITCODE -ne 0){throw 'Layout and UI flow checks failed'}
& $compiler /nologo /target:exe /platform:x64 /codepage:65001 /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll "/r:$stage\文件万能处理器.exe" "/out:$stage\PreviewChecks.exe" (Join-Path $project 'tests\PreviewChecks.cs')
if($LASTEXITCODE -ne 0){throw 'Preview test compile failed'}
& (Join-Path $stage 'PreviewChecks.exe') $run
if($LASTEXITCODE -ne 0){throw 'Preview regression failed'}
if(!$SkipWord){
    & $compiler /nologo /target:exe /platform:x64 /codepage:65001 /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll "/r:$stage\文件万能处理器.exe" "/out:$stage\IntegrationV3.exe" (Join-Path $project 'tests\IntegrationV3.cs')
    if($LASTEXITCODE -ne 0){throw 'Integration test compile failed'}
    & (Join-Path $stage 'IntegrationV3.exe') $run
    if($LASTEXITCODE -ne 0){throw 'Integration regression failed'}
    & $compiler /nologo /target:exe /platform:x64 /codepage:65001 /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll "/r:$stage\文件万能处理器.exe" "/out:$stage\OfficeIntegration.exe" (Join-Path $project 'tests\OfficeIntegration.cs')
    if($LASTEXITCODE -ne 0){throw 'Office integration test compile failed'}
    & (Join-Path $stage 'OfficeIntegration.exe') $officeRun
    if($LASTEXITCODE -ne 0){throw 'Office desktop integration failed'}
}
Write-Output "TEST_OUTPUT=$run"
