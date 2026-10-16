<#
.SYNOPSIS
Installs the shared UberStrike community client patch from a local NekoNexus source checkout.

.DESCRIPTION
This reuses NekoNexus's UniversalUnityPatcher, bootstrap and LiteNetLib shim. By default,
the client uses the shared Paradise community endpoint. That hostname must route the legacy
UberStrike SOAP web-service paths to Paradise and /api/community/servers to the directory API;
the JSON API alone is not a replacement for the legacy game web service.

.EXAMPLE
./Install-UberStrikeCommunity.ps1 -UberStrikePath 'C:\Games\UberStrike' `
  -NekoNexusRepoPath 'D:\DEV\Forked Projects\NekoNexus' `
  -WebServiceHost 'play.example.org'
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)][string]$UberStrikePath,
  [Parameter(Mandatory)][string]$NekoNexusRepoPath,
  [string]$WebServiceHost = 'paradise-directory.nekosunevr.co.uk',
  [int]$WebPort = 8080,
  [int]$FilePort = 8081,
  [switch]$InsecureHttp,
  [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

function Require-Path([string]$Path, [string]$Description) {
  if (-not (Test-Path -LiteralPath $Path)) { throw "$Description not found: $Path" }
}

$game = (Resolve-Path -LiteralPath $UberStrikePath).Path
$nexus = (Resolve-Path -LiteralPath $NekoNexusRepoPath).Path
$gameManaged = Join-Path $game 'UberStrike_Data\Managed'
$installer = Join-Path $nexus 'packages\Client\make-client-patch.ps1'
$patcher = Join-Path $nexus 'packages\Patcher\bin\Release\UniversalUnityPatcher.exe'

Require-Path (Join-Path $gameManaged 'Assembly-CSharp.dll') 'UberStrike Assembly-CSharp.dll'
Require-Path $installer 'NekoNexus client patch script'
Require-Path (Join-Path $nexus 'NekoNexus.sln') 'NekoNexus solution'

if ($WebServiceHost -match '[/\\:@\s]' -or $WebServiceHost -match '^https?://') {
  throw 'WebServiceHost must be a hostname or IPv4 address only; do not include a scheme, port, path, or credentials.'
}
if ($WebPort -lt 1 -or $WebPort -gt 65535 -or $FilePort -lt 1 -or $FilePort -gt 65535) {
  throw 'WebPort and FilePort must be between 1 and 65535.'
}
if (Get-Process UberStrike -ErrorAction SilentlyContinue) {
  throw 'UberStrike is running. Close the game and Steam before patching.'
}

if (-not (Test-Path -LiteralPath $patcher)) {
  $nuget = Get-Command nuget -ErrorAction SilentlyContinue
  $msbuild = Get-Command msbuild -ErrorAction SilentlyContinue
  if (-not $nuget -or -not $msbuild) {
    throw "UniversalUnityPatcher is not built. Install NuGet and MSBuild, then build '$nexus\packages\Patcher\UniversalUnityPatcher.sln'."
  }
  Push-Location $nexus
  try {
    & $nuget.Source restore 'packages\Patcher\UniversalUnityPatcher.sln'
    if ($LASTEXITCODE -ne 0) { throw "NuGet restore failed with exit code $LASTEXITCODE." }
    & $msbuild.Source 'packages\Patcher\UniversalUnityPatcher.sln' /p:Configuration=Release
    if ($LASTEXITCODE -ne 0) { throw "UniversalUnityPatcher build failed with exit code $LASTEXITCODE." }
  } finally { Pop-Location }
  Require-Path $patcher 'Built UniversalUnityPatcher.exe'
}

$arguments = @{
  UberStrikePath = $game
  ServerHost = $WebServiceHost
  WebPort = $WebPort
  FilePort = $FilePort
  RepoRoot = $nexus
}
if (-not $InsecureHttp) { $arguments.Https = $true }
if ($SkipBuild) { $arguments.SkipBuild = $true }

Write-Host 'Installing the shared UberStrike community client patch.' -ForegroundColor Cyan
Write-Host "Legacy game-service host: $WebServiceHost" -ForegroundColor DarkCyan
Write-Host 'The shared host must expose Paradise legacy SOAP services and the community directory API.' -ForegroundColor Yellow
& $installer @arguments
if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
  throw "NekoNexus patch/install script failed with exit code $LASTEXITCODE."
}

Write-Host 'Client patch installed. Server discovery is supplied by the shared host, not by a per-server patcher.' -ForegroundColor Green
