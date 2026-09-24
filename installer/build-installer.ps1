<#
.SYNOPSIS
    Builds the standalone release and compiles the Windows Installer for Video Piper.

.PARAMETER Version
    The application version to package (defaults to "1.0.0").

.PARAMETER Publish
    Forces a fresh `dotnet publish` before compiling the installer.

.PARAMETER OutputDir
    Directory where the installer executable will be saved (defaults to "installer\output").

.PARAMETER IsccPath
    Explicit path to ISCC.exe (Inno Setup Compiler). If omitted, standard locations and PATH are searched.
#>
[CmdletBinding()]
param(
    [string]$Version = "1.0.0",
    [switch]$Publish,
    [string]$OutputDir = "",
    [string]$IsccPath = ""
)

$ErrorActionPreference = "Stop"

if (-not $OutputDir) {
    $OutputDir = Join-Path $PSScriptRoot "output"
}

$repoRoot = (Resolve-Path "$PSScriptRoot\..").Path
$publishDir = "$repoRoot\video-piper\publish"
$targetExe = "$publishDir\VideoPiper.exe"

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  Video Piper Installer Builder v$Version" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

# 1. Locate or install Inno Setup compiler
function Find-Iscc {
    param([string]$explicit)
    if ($explicit -and (Test-Path $explicit)) { return $explicit }

    $cmd = Get-Command "iscc.exe" -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $candidates = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 7\ISCC.exe"
    )
    foreach ($cand in $candidates) {
        if (Test-Path $cand) { return $cand }
    }
    return $null
}

$iscc = Find-Iscc $IsccPath
if (-not $iscc) {
    Write-Warning "Inno Setup compiler (ISCC.exe) not found."
    $winget = Get-Command "winget" -ErrorAction SilentlyContinue
    if ($winget) {
        Write-Host "Attempting to install Inno Setup via winget..." -ForegroundColor Yellow
        try {
            & winget install --id JRSoftware.InnoSetup --exact --accept-package-agreements --accept-source-agreements --silent
            $iscc = Find-Iscc ""
        } catch {
            Write-Warning "Automatic winget install failed: $_"
        }
    }
}

if (-not $iscc) {
    Write-Error @"
Inno Setup Compiler (ISCC.exe) is required to build the installer.
Please install it via:
  winget install JRSoftware.InnoSetup
or download from:
  https://jrsoftware.org/isdl.php
"@
    exit 1
}
Write-Host "[OK] Inno Setup compiler: $iscc" -ForegroundColor Green

# 2. Publish release if needed
if ($Publish -or (-not (Test-Path $targetExe))) {
    Write-Host "`nPublishing Video Piper standalone Release (win-x64)..." -ForegroundColor Yellow
    $slnDir = "$repoRoot\video-piper"
    $proj = "$slnDir\VideoPiper\VideoPiper.csproj"

    & dotnet publish $proj `
        -f net10.0-windows10.0.26100 `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -o $publishDir

    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet publish failed with exit code $LASTEXITCODE."
        exit $LASTEXITCODE
    }
}

if (-not (Test-Path $targetExe)) {
    Write-Error "Executable not found at '$targetExe'. Cannot continue."
    exit 1
}
Write-Host "[OK] Application bundle ready at: $publishDir" -ForegroundColor Green

# 3. Ensure icon exists
$icoPath = "$PSScriptRoot\app.ico"
if (-not (Test-Path $icoPath)) {
    Write-Host "Generating installer icon from app-icon.png..." -ForegroundColor Yellow
    & "$PSScriptRoot\convert-icon.ps1"
}

# 4. Ensure output directory exists
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}
$resolvedOutput = (Resolve-Path $OutputDir).Path

# 5. Compile Inno Setup Script
Write-Host "`nCompiling installer with Inno Setup..." -ForegroundColor Yellow
$issPath = "$PSScriptRoot\VideoPiper.iss"

$isccArgs = @(
    "/Qp",
    "/DMyAppVersion=$Version",
    "/DSourceDir=$publishDir",
    "/O$resolvedOutput",
    "/FVideoPiper-Setup-$Version",
    $issPath
)

& $iscc @isccArgs

if ($LASTEXITCODE -ne 0) {
    Write-Error "ISCC compiler failed with exit code $LASTEXITCODE."
    exit $LASTEXITCODE
}

# 6. Verify result
$installerExe = "$resolvedOutput\VideoPiper-Setup-$Version.exe"
if (Test-Path $installerExe) {
    $item = Get-Item $installerExe
    $sizeMb = [math]::Round($item.Length / 1MB, 2)
    $hash = (Get-FileHash $installerExe -Algorithm SHA256).Hash

    Write-Host "`n=========================================" -ForegroundColor Green
    Write-Host "  Installer generated successfully!" -ForegroundColor Green
    Write-Host "=========================================" -ForegroundColor Green
    Write-Host "File:   $($item.FullName)"
    Write-Host "Size:   $sizeMb MB ($($item.Length) bytes)"
    Write-Host "SHA256: $hash"
    Write-Host ""
} else {
    Write-Error "Installer executable was not found at expected path: $installerExe"
    exit 1
}
