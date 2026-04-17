# STS2Agent Deploy Script
# Direct deployment to Slay the Spire 2 mods folder (no ZIP packaging)
#
# Usage:
#   .\deploy.ps1                       # Deploy to default game path
#   .\deploy.ps1 -GamePath "D:\..."    # Specify custom game path
#   .\deploy.ps1 -SkipBuild            # Skip build, only deploy

param(
    [string]$GamePath = "D:\steam\steamapps\common\Slay the Spire 2",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

# Project root
$ProjectRoot = $PSScriptRoot
$ModName = "STS2Agent"

# Source files
$SourceDll = "$ProjectRoot\.godot\mono\temp\bin\Release\$ModName.dll"
$SourcePdb = "$ProjectRoot\.godot\mono\temp\bin\Release\$ModName.pdb"
$SourceManifest = "$ProjectRoot\$ModName.json"
$SourcePck = "$ProjectRoot\libs\template.pck"

# Target directory (game mods folder)
$TargetModDir = "$GamePath\mods\$ModName"

Write-Host "=== STS2Agent Deploy Script ===" -ForegroundColor Cyan
Write-Host "Game Path: $GamePath"
Write-Host "Target: $TargetModDir"
Write-Host ""

# Build project
if (-not $SkipBuild) {
    Write-Host "[Build] dotnet build -c Release..." -ForegroundColor Yellow
    $originalDir = Get-Location
    Set-Location $ProjectRoot
    dotnet build -c Release -f net9.0 2>&1 | Out-Null
    $buildExit = $LASTEXITCODE
    Set-Location $originalDir
    if ($buildExit -ne 0) {
        Write-Host "[ERROR] Build failed" -ForegroundColor Red
        exit 1
    }
    Write-Host "[OK] Build succeeded" -ForegroundColor Green
}

# Check source files
if (-not (Test-Path $SourceDll)) {
    Write-Host "[ERROR] DLL not found: $SourceDll" -ForegroundColor Red
    Write-Host "Run: dotnet build -c Release" -ForegroundColor Yellow
    exit 1
}

# Create target mods directory (clean if exists)
if (Test-Path $TargetModDir) {
    Write-Host "[Clean] Removing existing mod folder..." -ForegroundColor Yellow
    Remove-Item $TargetModDir -Recurse -Force
}
New-Item -ItemType Directory -Path $TargetModDir -Force | Out-Null
Write-Host "[Deploy] Copying files to $TargetModDir..." -ForegroundColor Yellow

# Copy files directly to target directory
Copy-Item $SourceDll -Destination $TargetModDir -Force
Write-Host "  [+] STS2Agent.dll" -ForegroundColor DarkGray

if (Test-Path $SourcePdb) {
    Copy-Item $SourcePdb -Destination $TargetModDir -Force
    Write-Host "  [+] STS2Agent.pdb" -ForegroundColor DarkGray
} else {
    Write-Host "  [~] STS2Agent.pdb (not found, skip)" -ForegroundColor DarkGray
}

if (Test-Path $SourceManifest) {
    Copy-Item $SourceManifest -Destination $TargetModDir -Force
    Write-Host "  [+] STS2Agent.json" -ForegroundColor DarkGray
} else {
    Write-Host "  [!] STS2Agent.json (not found, skip)" -ForegroundColor Yellow
}

# Optionally copy PCK file if available
if (Test-Path $SourcePck) {
    Copy-Item $SourcePck -Destination "$TargetModDir\STS2Agent.pck" -Force
    Write-Host "  [+] STS2Agent.pck" -ForegroundColor DarkGray
} else {
    Write-Host "  [~] template.pck not found, skipping PCK file" -ForegroundColor DarkGray
}

# Stats
$DllSize = (Get-Item $SourceDll).Length / 1KB

Write-Host ""
Write-Host "=== Deploy Complete ===" -ForegroundColor Cyan
Write-Host "Files deployed to: $TargetModDir" -ForegroundColor Green
Write-Host "DLL Size: $([math]::Round($DllSize, 1)) KB"
Write-Host ""
Write-Host "Restart game to load the updated MOD" -ForegroundColor Yellow
Write-Host "API: http://localhost:8888" -ForegroundColor Yellow
