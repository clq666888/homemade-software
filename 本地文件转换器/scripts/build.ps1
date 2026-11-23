param([switch]$GuiOnly)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'build-v3.ps1') -GuiOnly:$GuiOnly
if (!$?) { throw 'Staging build failed' }
& (Join-Path $PSScriptRoot 'publish.ps1')
