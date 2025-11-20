# package_core.ps1 - Build and package SocketGhost Core (Windows)

$VERSION = "1.0.0"
$OUTPUT_DIR = "artifacts"
$PUBLISH_DIR = "publish"
$ARCHIVE_NAME = "socketghost-core-v$VERSION.zip"

Write-Host "Building SocketGhost Core v$VERSION..." -ForegroundColor Cyan

# Clean previous builds
if (Test-Path $PUBLISH_DIR) { Remove-Item $PUBLISH_DIR -Recurse -Force }
if (!(Test-Path $OUTPUT_DIR)) { New-Item -ItemType Directory -Path $OUTPUT_DIR | Out-Null }

# Publish .NET app
dotnet publish -c Release -o "./$PUBLISH_DIR" socketghost-core/socketghost-core.csproj

# Copy config sample and docs
Copy-Item "socketghost-core/README.md" -Destination "$PUBLISH_DIR/"

$configJson = @{
    Engine = "titanium"
    FlowRetentionDays = 30
    FlowStoreMaxTotalBytes = 524288000
    FlowStoreMaxInlineBytes = 131072
    AutoPruneOnStart = $true
} | ConvertTo-Json

Set-Content -Path "$PUBLISH_DIR/config.json" -Value $configJson

# Create README for release
$releaseReadme = @"
SocketGhost Core v$VERSION
========================

Prerequisites:
- .NET 7.0 Runtime (https://dotnet.microsoft.com/download/dotnet/7.0)

Quick Start:
1. Run: dotnet socketghost-core.dll
2. Core starts on:
   - WebSocket: ws://127.0.0.1:9000
   - Process API: http://127.0.0.1:9100
   - Script API: http://127.0.0.1:9200
   - Flow API: http://127.0.0.1:9300
   - Proxy: http://127.0.0.1:8080

Configuration:
- Edit config.json to change settings
- See README.md for full documentation

Trust SSL Certificate:
- First run generates a root CA certificate
- Install it to trust HTTPS interception

For full documentation, visit:
https://github.com/YOUR_ORG/SocketGhost
"@

Set-Content -Path "$PUBLISH_DIR/README_RELEASE.txt" -Value $releaseReadme

# Create ZIP archive
Compress-Archive -Path "$PUBLISH_DIR\*" -DestinationPath "$OUTPUT_DIR\$ARCHIVE_NAME" -Force

Write-Host "✅ Core packaged: $OUTPUT_DIR\$ARCHIVE_NAME" -ForegroundColor Green
Get-Item "$OUTPUT_DIR\$ARCHIVE_NAME" | Select-Object Name, Length, LastWriteTime
