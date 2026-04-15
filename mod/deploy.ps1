# STS2Agent Deploy Script
# Package MOD as ZIP for SlaySP2Manager

param(
    [string]$SlaySP2Path = "$env:APPDATA\SlaySP2Manager",
    [string]$GameName = "Slay the Spire 2",
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

# Package output
$TempDir = "$env:TEMP\STS2Agent_Package"
$ZipOutput = "$ProjectRoot\publish\$ModName.zip"
$TargetDir = "$SlaySP2Path\mods"

Write-Host "=== STS2Agent Deploy Script ===" -ForegroundColor Cyan
Write-Host "SlaySP2Manager: $SlaySP2Path"
Write-Host "Game: $GameName"
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

if (-not (Test-Path $SourcePck)) {
    Write-Host "[WARN] template.pck not found: $SourcePck" -ForegroundColor Yellow
    Write-Host "  [TIP] Copy a .pck file from another MOD to libs\template.pck, or create an empty one" -ForegroundColor Cyan
    # 不退出，而是跳过PCK打包
}

# Check SlaySP2Manager directory
if (-not (Test-Path $SlaySP2Path)) {
    Write-Host "[WARN] SlaySP2Manager directory not found, will create" -ForegroundColor Yellow
}

# Create temp package directory
if (Test-Path $TempDir) {
    Remove-Item $TempDir -Recurse -Force
}
New-Item -ItemType Directory -Path $TempDir -Force | Out-Null

# Copy files to temp directory
Write-Host "[Package] Copying files..."
Copy-Item $SourceDll -Destination $TempDir -Force
Write-Host "  [+] STS2Agent.dll" -ForegroundColor DarkGray

if (Test-Path $SourcePdb) {
    Copy-Item $SourcePdb -Destination $TempDir -Force
    Write-Host "  [+] STS2Agent.pdb" -ForegroundColor DarkGray
} else {
    Write-Host "  [~] STS2Agent.pdb (not found, skip)" -ForegroundColor DarkGray
}

if (Test-Path $SourceManifest) {
    Copy-Item $SourceManifest -Destination $TempDir -Force
    Write-Host "  [+] STS2Agent.json" -ForegroundColor DarkGray
} else {
    Write-Host "  [!] STS2Agent.json (not found, skip)" -ForegroundColor Yellow
}

if (Test-Path $SourcePck) {
    Copy-Item $SourcePck -Destination "$TempDir\$ModName.pck" -Force
    Write-Host "  [+] STS2Agent.pck" -ForegroundColor DarkGray
} else {
    Write-Host "  [~] template.pck (not found, skipping PCK packaging)" -ForegroundColor DarkGray
}

# Create output directory
$PublishDir = Split-Path -Parent $ZipOutput
if (-not (Test-Path $PublishDir)) {
    New-Item -ItemType Directory -Path $PublishDir -Force | Out-Null
}

# Create ZIP package
Write-Host "[Package] Creating ZIP..." -ForegroundColor Yellow
if (Test-Path $ZipOutput) {
    Remove-Item $ZipOutput -Force
}
Compress-Archive -Path "$TempDir\*" -DestinationPath $ZipOutput -CompressionLevel Optimal

# Copy to SlaySP2Manager mods directory
$TargetZip = "$TargetDir\$ModName.zip"
if (-not (Test-Path $TargetDir)) {
    New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
}
Copy-Item $ZipOutput -Destination $TargetZip -Force

# Cleanup temp directory
Remove-Item $TempDir -Recurse -Force

# Stats
$ZipSize = (Get-Item $ZipOutput).Length / 1KB

Write-Host ""
Write-Host "=== Deploy Complete ===" -ForegroundColor Cyan
Write-Host "ZIP: $ZipOutput" -ForegroundColor Green
Write-Host "Size: $([math]::Round($ZipSize, 1)) KB"
Write-Host "Copied to: $TargetZip" -ForegroundColor Green
Write-Host ""
Write-Host "Enable MOD in SlaySP2Manager, then start game" -ForegroundColor Yellow
Write-Host "API: http://localhost:8080" -ForegroundColor Yellow
