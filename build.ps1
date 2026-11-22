<#
.SYNOPSIS
  Builds the mod, stages the Workshop content folder, and optionally installs it locally.

.EXAMPLE
  .\build.ps1
  .\build.ps1 -Install
  .\build.ps1 -Sts2Dir "D:\Games\Slay the Spire 2" -Install
#>
param(
    [string] $Sts2Dir = "C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2",
    [switch] $Install
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

if (-not (Test-Path (Join-Path $Sts2Dir 'data_sts2_windows_x86_64\sts2.dll'))) {
    throw "No sts2.dll under '$Sts2Dir'. Pass the game folder with -Sts2Dir."
}

Write-Host "Building..." -ForegroundColor Cyan
dotnet build (Join-Path $root 'mod\ultrawide.csproj') -c Release -p:Sts2Dir="$($Sts2Dir.Replace([char]92, [char]47))"
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

$staged = Join-Path $root 'release\content\ultrawide'
New-Item -ItemType Directory -Force -Path $staged | Out-Null
Copy-Item (Join-Path $root 'mod\bin\Release\ultrawide.dll') $staged -Force
Copy-Item (Join-Path $root 'mod\ultrawide.json') $staged -Force
Write-Host "Staged -> $staged" -ForegroundColor Green

if ($Install) {
    $dest = Join-Path $Sts2Dir 'mods\ultrawide'
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    Copy-Item "$staged\*" $dest -Force
    Write-Host "Installed -> $dest" -ForegroundColor Green
    Write-Host "Restart the game to load it." -ForegroundColor Yellow
}
