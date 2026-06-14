<#
.SYNOPSIS
  Build + bundle a release of MSFS Media Player (companion exe + EFB Community package).

.DESCRIPTION
  1. Builds the EFB app JS (npm run build).
  2. Publishes the companion (dotnet publish, framework-dependent win-x64).
  3. Stages both + an INSTALL.md and zips them to dist/release/.

  NOTE: the EFB MSFS package itself is built in-sim via DevMode (bare fspackagetool hangs). Build it
  first so efb-app/Packages/msfs-mediaplayer exists; this script bundles whatever is there.

.PARAMETER Version
  Release version (default 0.1.0). Used for the zip/folder name.

.PARAMETER SelfContained
  Publish a self-contained companion (no .NET runtime needed on the target). Default: framework-dependent.
#>
param(
  [string]$Version = "0.1.0",
  [switch]$SelfContained
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) { $dotnet = "C:\Program Files\dotnet\dotnet.exe" }

$stageName = "MsfsMediaPlayer-v$Version"
$releaseDir = Join-Path $root "dist\release"
$stage = Join-Path $releaseDir $stageName
$companionProj = Join-Path $root "companion-app\MsfsMediaPlayer.Companion.csproj"
$efbApp = Join-Path $root "efb-app\MediaPlayer"
$efbPackage = Join-Path $root "efb-app\Packages\msfs-mediaplayer"

Write-Host "== MSFS Media Player release v$Version ==" -ForegroundColor Cyan

# Fresh staging
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage | Out-Null

# Native tools (npm/dotnet) write to stderr; in PS 5.1 that trips ErrorActionPreference=Stop.
# Run them with Continue and check the exit code instead.
function Invoke-Native([scriptblock]$Cmd, [string]$What) {
  $ErrorActionPreference = "Continue"
  & $Cmd 2>&1 | Out-Host
  $code = $LASTEXITCODE
  $ErrorActionPreference = "Stop"
  if ($code -ne 0) { throw "$What failed (exit $code)" }
}

# 1. EFB app JS
Write-Host "Building EFB app..." -ForegroundColor Yellow
Push-Location $efbApp
try { Invoke-Native { & npm run build } "EFB build" } finally { Pop-Location }

# 2. Companion
Write-Host "Publishing companion..." -ForegroundColor Yellow
$companionOut = Join-Path $stage "Companion"
$scFlag = if ($SelfContained) { "true" } else { "false" }
Invoke-Native { & $dotnet publish $companionProj -c Release -r win-x64 -o $companionOut --nologo --self-contained $scFlag } "dotnet publish"

# 3. EFB Community package (DevMode-built)
if (Test-Path $efbPackage) {
  $commOut = Join-Path $stage "Community\msfs-mediaplayer"
  New-Item -ItemType Directory -Force -Path $commOut | Out-Null
  Copy-Item "$efbPackage\*" $commOut -Recurse -Force
  Write-Host "Bundled EFB Community package." -ForegroundColor Green
} else {
  Write-Warning "EFB package not found at $efbPackage. Build it in MSFS DevMode (MediaPlayerProject.xml), then re-run."
}

# 4. INSTALL.md
$install = @"
# MSFS Media Player v$Version

## Install
1. **Companion app:** copy the ``Companion`` folder anywhere and run ``MsfsMediaPlayer.Companion.exe``.
   (Framework-dependent build needs the .NET 8 Desktop Runtime; the self-contained build does not.)
   In the tray menu, optionally enable **Start with Windows**.
2. **EFB app:** copy ``Community\msfs-mediaplayer`` into your MSFS 2024 Community folder.
3. Launch MSFS, open the **Media Player** app on the EFB tablet.

Control local media (Spotify/YouTube/etc.) and internet radio from the tablet. Radio audio is gated
by avionics power. Edit stations via the companion tray ("Edit stations...").
"@
Set-Content -Path (Join-Path $stage "INSTALL.md") -Value $install -Encoding utf8

# 5. Zip
$zip = Join-Path $releaseDir "$stageName.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path "$stage\*" -DestinationPath $zip
Write-Host "Release ready: $zip" -ForegroundColor Green
