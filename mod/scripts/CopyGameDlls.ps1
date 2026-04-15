# STS2Agent - Copy Game DLLs Script
# Run this script to copy required DLL files from game directory

param(
    [string]$GamePath = "D:\steam\steamapps\common\Slay the Spire 2"
)

$managedPath = Join-Path $GamePath "Slay the Spire 2_Data\Managed"
$libsPath = Join-Path $PSScriptRoot "..\libs"

Write-Host "Game path: $managedPath"
Write-Host "Target path: $libsPath"
Write-Host ""

# Required DLLs
$requiredDlls = @(
    "Assembly-CSharp.dll",
    "UnityEngine.CoreModule.dll",
    "UnityEngine.dll",
    "BaseLib.dll"
)

foreach ($dll in $requiredDlls) {
    $sourcePath = Join-Path $managedPath $dll
    $destPath = Join-Path $libsPath $dll

    if (Test-Path $sourcePath) {
        Write-Host "Copy: $dll" -ForegroundColor Green
        Copy-Item $sourcePath -Destination $destPath -Force
    } else {
        Write-Host "Not found: $dll" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Done! DLL files copied to libs directory." -ForegroundColor Cyan
