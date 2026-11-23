param([switch]$GuiOnly)
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $project
$stage = Join-Path $project '.work\release'
New-Item -ItemType Directory -Force -Path $stage | Out-Null
& (Join-Path $PSScriptRoot 'make-icon.ps1')
if ($GuiOnly -and !(Test-Path -LiteralPath (Join-Path $stage 'engine\PdfEngine.exe'))) {
    $published=Join-Path $project 'dist\文件万能处理器'
    if(!(Test-Path -LiteralPath (Join-Path $published 'engine\PdfEngine.exe'))){$published=Join-Path $project 'dist\本地文件转换器'}
    if(!(Test-Path -LiteralPath (Join-Path $published 'engine\PdfEngine.exe'))){throw 'No existing PDF engine; run the full build first.'}
    foreach($name in @('engine','licenses')){if(!(Test-Path -LiteralPath (Join-Path $stage $name))){Copy-Item -LiteralPath (Join-Path $published $name) -Destination (Join-Path $stage $name) -Recurse}}
}
if (!$GuiOnly) {
    & '.\.venv\Scripts\python.exe' -m PyInstaller --noconfirm --clean --onedir --noconsole --name PdfEngine --distpath '.work\engine-dist' --workpath '.work\engine-build' --specpath '.work' --collect-all pypdfium2 --collect-all pypdfium2_raw --collect-all pptx --collect-all pdfminer --copy-metadata pypdf --copy-metadata olefile --copy-metadata Pillow --copy-metadata cryptography engine/pdf_engine.py
    if ($LASTEXITCODE -ne 0) { throw 'PDF engine packaging failed' }
    $stagedEngine = Join-Path $stage 'engine'
    if (Test-Path -LiteralPath $stagedEngine) {
        $resolvedEngine = (Resolve-Path -LiteralPath $stagedEngine).Path
        if ($resolvedEngine -ne (Join-Path $project '.work\release\engine')) { throw 'Unsafe engine staging path' }
        Remove-Item -LiteralPath $resolvedEngine -Recurse -Force
    }
    Copy-Item -LiteralPath '.work\engine-dist\PdfEngine' -Destination (Join-Path $stage 'engine') -Recurse
    & '.\.venv\Scripts\python.exe' scripts/collect-licenses.py $stage
    if ($LASTEXITCODE -ne 0) { throw 'License collection failed' }
}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources=Get-ChildItem -LiteralPath 'src' -Filter '*.cs' -File | Select-Object -ExpandProperty FullName
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /debug- /utf8output /codepage:65001 "/out:$stage\文件万能处理器.exe" '/win32icon:assets\app.ico' '/win32manifest:app.manifest' /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Xml.dll /r:System.Xml.Linq.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /r:System.Web.Extensions.dll /r:Microsoft.CSharp.dll $sources
if ($LASTEXITCODE -ne 0) { throw 'GUI build failed' }
Copy-Item -LiteralPath 'README.md' -Destination (Join-Path $stage '使用说明.md')
Write-Output "STAGED_RELEASE=$stage"
