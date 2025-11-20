# package_ui.ps1 - Build and package SocketGhost UI (Windows)

$VERSION = "1.0.0"
$OUTPUT_DIR = "artifacts"
$UI_DIR = "socketghost-ui"
$DIST_DIR = "$UI_DIR\dist"
$ARCHIVE_NAME = "socketghost-ui-web-v$VERSION.zip"

Write-Host "Building SocketGhost UI v$VERSION..." -ForegroundColor Cyan

# Create output directory
if (!(Test-Path $OUTPUT_DIR)) { New-Item -ItemType Directory -Path $OUTPUT_DIR | Out-Null }

# Build web bundle
Push-Location $UI_DIR
npm run build
Pop-Location

# Copy dist and create release README
Copy-Item "$UI_DIR\README.md" -Destination "$DIST_DIR\"

$releaseReadme = @"
SocketGhost UI v$VERSION - Web Bundle
===================================

This is the web bundle for SocketGhost UI.

To serve locally:
1. Use any static file server:
   - Python: python -m http.server 5173
   - Node.js: npx serve
   - VS Code: Live Server extension

2. Open http://localhost:5173 in your browser

3. Ensure SocketGhost Core is running on localhost

For native desktop app, see:
https://github.com/Heybro1122/SocketGhost#native-installers
"@

Set-Content -Path "$DIST_DIR\README_RELEASE.txt" -Value $releaseReadme

# Create ZIP archive
Compress-Archive -Path "$DIST_DIR\*" -DestinationPath "$OUTPUT_DIR\$ARCHIVE_NAME" -Force

Write-Host "UI web bundle packaged: $OUTPUT_DIR\$ARCHIVE_NAME" -ForegroundColor Green
Get-Item "$OUTPUT_DIR\$ARCHIVE_NAME" | Select-Object Name, Length, LastWriteTime

# Rust check
if (Get-Command cargo -ErrorAction SilentlyContinue) {
    Write-Host "Rust detected - skipping native build for now" -ForegroundColor Yellow
}
else {
    Write-Host "Rust not found - skipping native installer build" -ForegroundColor Cyan
}
