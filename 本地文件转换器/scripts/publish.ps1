param()
$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSScriptRoot
$stage=Join-Path $project '.work\release'
$stageExe=Join-Path $stage '文件万能处理器.exe'
if (!(Test-Path -LiteralPath (Join-Path $stage 'engine\PdfEngine.exe'))) { throw 'Staged PDF engine missing' }
if ((Get-Item -LiteralPath $stageExe).VersionInfo.FileVersion -ne '0.4.3.0') { throw 'Unexpected staged version' }
function Remove-Generated([string]$relative){
    $candidate=Join-Path $project $relative
    if(Test-Path -LiteralPath $candidate){
        $resolved=(Resolve-Path -LiteralPath $candidate).Path
        if($resolved -ne $candidate -or !$resolved.StartsWith($project+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe release path'}
        if((Get-Item -LiteralPath $resolved).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Refusing linked release path'}
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
# User authorized closing prior project EXEs; include the previous brand during migration.
$names=@('文件万能处理器.exe','本地文件转换器.exe')
$old=Get-Process | Where-Object { $_.Path -and $_.Path.StartsWith($project+'\',[StringComparison]::OrdinalIgnoreCase) -and $names -contains [IO.Path]::GetFileName($_.Path) }
foreach($process in $old){$process.Kill();$null=$process.WaitForExit(5000)}
$destination=Join-Path $project 'dist\文件万能处理器'
Remove-Generated 'dist\文件万能处理器'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Copy-Item -LiteralPath $stageExe -Destination (Join-Path $destination '文件万能处理器.exe')
foreach($name in @('engine','licenses')){Copy-Item -LiteralPath (Join-Path $stage $name) -Destination (Join-Path $destination $name) -Recurse}
Copy-Item -LiteralPath (Join-Path $project 'README.md') -Destination (Join-Path $destination '使用说明.md')
Copy-Item -LiteralPath $stageExe -Destination (Join-Path $project '文件万能处理器.exe') -Force
# Retire the exact prior generated package only after the new entry is in place.
Remove-Generated 'dist\本地文件转换器'
Remove-Generated '本地文件转换器.exe'
Write-Output "CURRENT_EXE=$(Join-Path $project '文件万能处理器.exe')"
