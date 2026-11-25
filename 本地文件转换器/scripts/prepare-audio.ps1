param([string]$DenoiseExe)
$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSScriptRoot
$lock=Get-Content -LiteralPath (Join-Path $project 'assets\audio-runtime.lock.json') -Raw | ConvertFrom-Json
$stage=Join-Path $project '.work\release\audio'
$published=Join-Path $project 'dist\文件万能处理器\audio'
New-Item -ItemType Directory -Force -Path $stage | Out-Null
Copy-Item -LiteralPath (Join-Path $project 'assets\audio-sources.txt') -Destination (Join-Path $stage 'SOURCES.txt') -Force
function Test-Runtime([string]$folder){
    foreach($file in $lock.files){$path=Join-Path $folder $file.name;if(!(Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant() -ne $file.sha256){return $false}}
    return $true
}
if(Test-Runtime $stage){Write-Output 'Audio runtime verified';exit 0}
if(Test-Runtime $published){New-Item -ItemType Directory -Force -Path $stage | Out-Null;foreach($file in $lock.files){Copy-Item -LiteralPath (Join-Path $published $file.name) -Destination (Join-Path $stage $file.name) -Force};Write-Output 'Audio runtime restored from current release';exit 0}
# Source builds of the tiny filter helper use the documented FFmpeg source and script.
# Different toolchains may produce different hashes; revalidate and update the lock for a new release.
if(!$DenoiseExe -or !(Test-Path -LiteralPath $DenoiseExe)){throw 'Build the filter helper with scripts/build-audio-denoise.sh, then pass -DenoiseExe. See assets/audio-runtime.lock.json for source URL/hash.'}
$filter=$lock.files | Where-Object {$_.name -eq 'AudioDenoise.exe'}
if((Get-FileHash -LiteralPath $DenoiseExe).Hash.ToLowerInvariant() -ne $filter.sha256){throw 'Filter helper hash differs from approved release; validate the source build and update the lock before release.'}
$work=Join-Path $project '.work\audio-fetch'
New-Item -ItemType Directory -Force -Path $work,$stage | Out-Null
$archive=Join-Path $work 'audio.zip'
Invoke-WebRequest -Uri $lock.url -OutFile $archive
if((Get-FileHash -LiteralPath $archive).Hash.ToLowerInvariant() -ne $lock.archiveSha256){throw 'Audio download changed; refusing unverified runtime'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=[IO.Compression.ZipFile]::OpenRead($archive)
try{
    foreach($file in $lock.files){
        if($file.name -eq 'AudioDenoise.exe'){Copy-Item -LiteralPath $DenoiseExe -Destination (Join-Path $stage $file.name) -Force;continue}
        if($file.name -eq 'LICENSE-AudioDenoise.txt'){continue}
        $entryName=if($file.name -eq 'LICENSE-FFmpeg-Audio.txt'){'LICENSE.txt'}else{$file.name}
        $entry=@($zip.Entries | Where-Object {$_.Name -eq $entryName})
        if($entry.Count -ne 1){throw 'Runtime archive layout changed'}
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry[0],(Join-Path $stage $file.name),$true)
    }
}finally{$zip.Dispose()}
$license=Join-Path (Split-Path -Parent $DenoiseExe) 'COPYING.LGPLv2.1'
if(!(Test-Path -LiteralPath $license)){throw 'Keep COPYING.LGPLv2.1 alongside the source-built helper'}
Copy-Item -LiteralPath $license -Destination (Join-Path $stage 'LICENSE-AudioDenoise.txt') -Force
if(!(Test-Runtime $stage)){throw 'Audio runtime verification failed'}
Write-Output 'Audio runtime prepared and verified'
