param([switch]$Check, [switch]$BuildOnly)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$buildDir = Join-Path $root '.build'
$appDir = Join-Path $buildDir 'app'
$toolsDir = Join-Path $buildDir 'tools'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework C# compiler was not found.' }
$previousTemp = $env:TEMP
$previousTmp = $env:TMP
$success = $false
try {
    New-Item -ItemType Directory -Path $appDir, $toolsDir -Force | Out-Null
    $env:TEMP = $buildDir
    $env:TMP = $buildDir
    $icon = Join-Path $root 'assets\app.ico'
    if (-not (Test-Path -LiteralPath $icon)) {
        New-Item -ItemType Directory -Path (Split-Path -Parent $icon) -Force | Out-Null
        $iconTool = Join-Path $toolsDir 'ExportIcon.exe'
        & $compiler /nologo /target:exe "/out:$iconTool" (Join-Path $root 'tools\ExportIcon.cs')
        if ($LASTEXITCODE -ne 0) { throw 'Icon helper compilation failed.' }
        & $iconTool (Join-Path $root '磁盘清理工具.exe') $icon
        if ($LASTEXITCODE -ne 0) { throw 'Icon recovery failed.' }
    }
    $sources = @((Join-Path $root 'Program.cs')) + @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' -Recurse | Sort-Object FullName | ForEach-Object { $_.FullName })
    $references = @('/r:System.Windows.Forms.dll', '/r:System.Drawing.dll', '/r:System.Web.Extensions.dll', '/r:Microsoft.VisualBasic.dll')
    $candidate = Join-Path $appDir '磁盘清理工具.exe'
    & $compiler /nologo /target:winexe "/out:$candidate" "/win32manifest:$(Join-Path $root 'app.manifest')" "/win32icon:$icon" @references @sources
    if ($LASTEXITCODE -ne 0) { throw 'Application compilation failed.' }
    if ($Check) {
        $checkDir = Join-Path $buildDir 'checks'
        New-Item -ItemType Directory -Path $checkDir -Force | Out-Null
        $checker = Join-Path $checkDir 'RegressionChecks.exe'
        & $compiler /nologo /target:exe "/out:$checker" /r:System.Windows.Forms.dll /r:System.Drawing.dll (Join-Path $root 'tests\RegressionChecks.cs')
        if ($LASTEXITCODE -ne 0) { throw 'Check compilation failed.' }
        & $checker $candidate
        if ($LASTEXITCODE -ne 0) { throw 'Regression checks failed.' }
    }
    if ($BuildOnly) { Write-Host "Built: $candidate"; $success = $true; return }
    $target = Join-Path $root '磁盘清理工具.exe'
    foreach ($process in @(Get-Process -Name '磁盘清理工具' -ErrorAction SilentlyContinue)) {
        if ($process.Path -and $process.Path -ne $target) { continue }
        $null = $process.CloseMainWindow()
        if (-not $process.WaitForExit(8000)) { Stop-Process -Id $process.Id -Force }
    }
    Copy-Item -LiteralPath $candidate -Destination $target -Force
    Write-Host "Updated: $target"
    $success = $true
}
finally {
    $env:TEMP = $previousTemp
    $env:TMP = $previousTmp
    if ($success -and -not $BuildOnly -and (Test-Path -LiteralPath $buildDir)) {
        $resolved = (Resolve-Path -LiteralPath $buildDir).Path
        $expected = [IO.Path]::GetFullPath((Join-Path $root '.build'))
        if ($resolved -ne $expected -or (Split-Path -Parent $resolved) -ne $root) { throw 'Invalid build cleanup path.' }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
