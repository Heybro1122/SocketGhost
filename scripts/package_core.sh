#!/usr/bin/env bash
# package_core.sh - Build and package SocketGhost Core

set -e

VERSION="1.0.0"
OUTPUT_DIR="artifacts"
PUBLISH_DIR="publish"
ARCHIVE_NAME="socketghost-core-v${VERSION}.zip"

echo "Building SocketGhost Core v${VERSION}..."

# Clean previous builds
rm -rf "${PUBLISH_DIR}"
mkdir -p "${OUTPUT_DIR}"

# Publish .NET app
dotnet publish -c Release -o "./${PUBLISH_DIR}" socketghost-core/socketghost-core.csproj

# Copy config sample and docs
cp socketghost-core/README.md "${PUBLISH_DIR}/"
echo '{"Engine":"titanium","FlowRetentionDays":30,"FlowStoreMaxTotalBytes":524288000,"FlowStoreMaxInlineBytes":131072,"AutoPruneOnStart":true}' > "${PUBLISH_DIR}/config.json"

# Create README for release
cat > "${PUBLISH_DIR}/README_RELEASE.txt" << 'EOF'
SocketGhost Core v1.0.0
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
EOF

# Create ZIP archive
if command -v zip &> /dev/null; then
    cd "${PUBLISH_DIR}"
    zip -r "../${OUTPUT_DIR}/${ARCHIVE_NAME}" .
    cd ..
else
    echo "zip not found, using tar.gz instead"
    tar -czf "${OUTPUT_DIR}/socketghost-core-v${VERSION}.tar.gz" -C "${PUBLISH_DIR}" .
fi

echo "✅ Core packaged: ${OUTPUT_DIR}/${ARCHIVE_NAME}"
ls -lh "${OUTPUT_DIR}/${ARCHIVE_NAME}" || ls -lh "${OUTPUT_DIR}/socketghost-core-v${VERSION}.tar.gz"
