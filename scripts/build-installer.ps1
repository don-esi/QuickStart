<#
.SYNOPSIS
  Publishes QuickStartMenu (self-contained win-x64 single-file) and builds the Inno Setup installer.

.EXAMPLE
  .\scripts\build-installer.ps1
  .\scripts\build-installer.ps1 -Configuration Release -Version 1.0.1
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [string] $Version = '',

    [string] $InnoCompiler = ''
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $root

$csproj = Join-Path $root 'QuickStartMenu.csproj'
if (-not $Version) {
    [xml] $proj = Get-Content $csproj
    $Version = $proj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
    if (-not $Version) { $Version = '1.0.0' }
}

$publishDir = Join-Path $root 'artifacts\publish\win-x64'
$installerDir = Join-Path $root 'artifacts\installer'
$iss = Join-Path $root 'installer\QuickStartMenu.iss'

Write-Host "=== Quick Start Menu installer ===" -ForegroundColor Cyan
Write-Host "Version:       $Version"
Write-Host "Configuration: $Configuration"
Write-Host "Publish dir:   $publishDir"
Write-Host "Output dir:    $installerDir"

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $publishDir, $installerDir | Out-Null

Write-Host "`n--- dotnet publish (self-contained single-file win-x64) ---" -ForegroundColor Cyan
dotnet publish $csproj `
    -c $Configuration `
    -p:PublishInstaller=true `
    -p:Version=$Version `
    "-p:AssemblyVersion=${Version}.0" `
    "-p:FileVersion=${Version}.0" `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

$exe = Join-Path $publishDir 'QuickStartMenu.exe'
if (-not (Test-Path $exe)) { throw "Published exe missing: $exe" }
Write-Host "Published: $exe ($([math]::Round((Get-Item $exe).Length / 1MB, 1)) MB)"

if (-not $InnoCompiler) {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "${env:LocalAppData}\Programs\Inno Setup 6\ISCC.exe"
    )
    $InnoCompiler = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $InnoCompiler -or -not (Test-Path $InnoCompiler)) {
    throw "Inno Setup compiler (ISCC.exe) not found. Install with: winget install JRSoftware.InnoSetup"
}

Write-Host "`n--- Inno Setup ---" -ForegroundColor Cyan
Write-Host "ISCC: $InnoCompiler"
& $InnoCompiler `
    "/DAppVersion=$Version" `
    "/DPublishDir=$publishDir" `
    "/DOutputDir=$installerDir" `
    $iss
if ($LASTEXITCODE -ne 0) { throw "ISCC failed ($LASTEXITCODE)" }

$setup = Get-ChildItem $installerDir -Filter "QuickStartMenu-Setup-*.exe" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $setup) { throw "Installer exe not produced in $installerDir" }

Write-Host "`n=== Done ===" -ForegroundColor Green
Write-Host "Installer: $($setup.FullName)"
Write-Host "Size:      $([math]::Round($setup.Length / 1MB, 1)) MB"
Write-Host "Note: 'Start with Windows' remains an in-app setting (HKCU Run). Uninstall clears that key."
