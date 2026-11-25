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
    foreach($name in @('engine','licenses','audio','ocr')){Copy-Item -LiteralPath (Join-Path $published $name) -Destination (Join-Path $stage $name) -Recurse}
}
$engine=Join-Path $stage 'engine\PdfEngine.exe'
$run=Join-Path $project ('.work\checks-v3-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
& $python (Join-Path $project 'tests\pdf_regression.py') $run $engine
if($LASTEXITCODE -ne 0){throw 'PDF regression failed'}
$officeRun=Join-Path $run 'office'
& $python (Join-Path $project 'tests\office_formats.py') $officeRun $engine
if($LASTEXITCODE -ne 0){throw 'Office/HTML backend checks failed'}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$imageRun=Join-Path $run 'image'
& $python (Join-Path $project 'tests\ocr_lines_regression.py')
if($LASTEXITCODE -ne 0){throw 'OCR line geometry checks failed'}
& $python (Join-Path $project 'tests\image_regression.py') $imageRun $engine
if($LASTEXITCODE -ne 0){throw 'Image backend checks failed'}
& $compiler /nologo /target:exe /platform:x64 /codepage:65001 /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll "/r:$stage\文件万能处理器.exe" "/out:$stage\ImageChecks.exe" (Join-Path $project 'tests\ImageChecks.cs')
if($LASTEXITCODE -ne 0){throw 'Image workflow test compile failed'}
& (Join-Path $stage 'ImageChecks.exe') (Join-Path $imageRun 'ui')
if($LASTEXITCODE -ne 0){throw 'Image workflow checks failed'}
& $compiler /nologo /target:exe /platform:x64 /codepage:65001 /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll "/r:$stage\文件万能处理器.exe" "/out:$stage\ImageInteractionChecks.exe" (Join-Path $project 'tests\ImageInteractionChecks.cs')
if($LASTEXITCODE -ne 0){throw 'Image interaction test compile failed'}
& (Join-Path $stage 'ImageInteractionChecks.exe') (Join-Path $imageRun 'interaction')
if($LASTEXITCODE -ne 0){throw 'Image interaction checks failed'}
& $compiler /nologo /target:exe /platform:x64 /codepage:65001 /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll "/r:$stage\文件万能处理器.exe" "/out:$stage\ImageViewChecks.exe" (Join-Path $project 'tests\ImageViewChecks.cs')
if($LASTEXITCODE -ne 0){throw 'Image view checks compile failed'}
& (Join-Path $stage 'ImageViewChecks.exe') (Join-Path $imageRun 'view')
if($LASTEXITCODE -ne 0){throw 'Image view checks failed'}
& $compiler /nologo /target:exe /platform:x64 /codepage:65001 /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll "/r:$stage\文件万能处理器.exe" "/out:$stage\ImageTextChecks.exe" (Join-Path $project 'tests\ImageTextChecks.cs')
if($LASTEXITCODE -ne 0){throw 'Image text checks compile failed'}
& (Join-Path $stage 'ImageTextChecks.exe') (Join-Path $imageRun 'text')
if($LASTEXITCODE -ne 0){throw 'Image live text checks failed'}
& $compiler /nologo /target:exe /platform:x64 /codepage:65001 /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll "/r:$stage\文件万能处理器.exe" "/out:$stage\ImageUsabilityChecks.exe" (Join-Path $project 'tests\ImageUsabilityChecks.cs')
if($LASTEXITCODE -ne 0){throw 'Image usability checks compile failed'}
& (Join-Path $stage 'ImageUsabilityChecks.exe') (Join-Path $imageRun 'usability')
if($LASTEXITCODE -ne 0){throw 'Image usability checks failed'}
$audioRun=Join-Path $run 'audio'
& $compiler /nologo /target:exe /platform:x64 /codepage:65001 /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll "/r:$stage\文件万能处理器.exe" "/out:$stage\AudioChecks.exe" (Join-Path $project 'tests\AudioChecks.cs')
if($LASTEXITCODE -ne 0){throw 'Audio test compile failed'}
& (Join-Path $stage 'AudioChecks.exe') $audioRun
if($LASTEXITCODE -ne 0){throw 'Audio workflow regression failed'}
& $compiler /nologo /target:exe /platform:x64 /codepage:65001 /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll "/r:$stage\文件万能处理器.exe" "/out:$stage\SpeechNavigationChecks.exe" (Join-Path $project 'tests\SpeechNavigationChecks.cs')
if($LASTEXITCODE -ne 0){throw 'Speech navigation test compile failed'}
& (Join-Path $stage 'SpeechNavigationChecks.exe') (Join-Path $run 'speech-navigation')
if($LASTEXITCODE -ne 0){throw 'Speech navigation checks failed'}
& $compiler /nologo /target:exe /platform:x64 /codepage:65001 /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll "/r:$stage\文件万能处理器.exe" "/out:$stage\EngineBackgroundChecks.exe" (Join-Path $project 'tests\EngineBackgroundChecks.cs')
if($LASTEXITCODE -ne 0){throw 'Engine background test compile failed'}
& (Join-Path $stage 'EngineBackgroundChecks.exe') (Join-Path $run 'engine-background')
if($LASTEXITCODE -ne 0){throw 'Engine background checks failed'}
& $python (Join-Path $project 'tests\audio_quality.py') $audioRun
if($LASTEXITCODE -ne 0){throw 'Audio signal quality regression failed'}
$accessRun=Join-Path $run 'access'
& $python (Join-Path $project 'tests\access_regression.py') $accessRun fixtures
if($LASTEXITCODE -ne 0){throw 'Access fixture generation failed'}
& $python (Join-Path $project 'tests\access_regression.py') $accessRun $engine
if($LASTEXITCODE -ne 0){throw 'Mixed-password PDF regression failed'}
& $compiler /nologo /target:exe /platform:x64 /codepage:65001 /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll "/r:$stage\文件万能处理器.exe" "/out:$stage\AccessChecks.exe" (Join-Path $project 'tests\AccessChecks.cs')
if($LASTEXITCODE -ne 0){throw 'Access test compile failed'}
& (Join-Path $stage 'AccessChecks.exe') $accessRun
if($LASTEXITCODE -ne 0){throw 'Admission and visual picker checks failed'}
$watermarkRun=Join-Path $run 'watermark'
& $python (Join-Path $project 'tests\watermark_regression.py') $watermarkRun fixtures
if($LASTEXITCODE -ne 0){throw 'Watermark fixture generation failed'}
& $compiler /nologo /target:exe /platform:x64 /codepage:65001 /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll "/r:$stage\文件万能处理器.exe" "/out:$stage\WatermarkChecks.exe" (Join-Path $project 'tests\WatermarkChecks.cs')
if($LASTEXITCODE -ne 0){throw 'Watermark test compile failed'}
& (Join-Path $stage 'WatermarkChecks.exe') $watermarkRun
if($LASTEXITCODE -ne 0){throw 'Watermark UI and export regression failed'}
& $python (Join-Path $project 'tests\watermark_regression.py') $watermarkRun
if($LASTEXITCODE -ne 0){throw 'Watermark render regression failed'}
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
