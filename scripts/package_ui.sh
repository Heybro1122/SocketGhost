#!/usr/bin/env bash
# package_ui.sh - Build and package SocketGhost UI

set -e

VERSION="1.0.0"
OUTPUT_DIR="artifacts"
UI_DIR="socketghost-ui"
DIST_DIR="${UI_DIR}/dist"
ARCHIVE_NAME="socketghost-ui-web-v${VERSION}.zip"

echo "Building SocketGhost UI v${VERSION}..."

# Create output directory
mkdir -p "${OUTPUT_DIR}"

# Build web bundle
cd "${UI_DIR}"
npm ci
npm run build
cd ..

# Copy dist and create release README
cp "${UI_DIR}/README.md" "${DIST_DIR}/"

cat > "${DIST_DIR}/README_RELEASE.txt" << 'EOF'
SocketGhost UI v1.0.0 - Web Bundle
===================================

This is the web bundle for SocketGhost UI.

To serve locally:
1. Use any static file server:
   - Python: python3 -m http.server 5173
   - Node.js: npx serve
   - VS Code: Live Server extension

2. Open http://localhost:5173 in your browser

3. Ensure SocketGhost Core is running on localhost

For native desktop app, see:
https://github.com/YOUR_ORG/SocketGhost#native-installers

EOF

# Create ZIP archive
if command -v zip &> /dev/null; then
    cd "${DIST_DIR}"
    zip -r "../../${OUTPUT_DIR}/${ARCHIVE_NAME}" .
    cd ../..
else
    echo "zip not found, using tar.gz instead"
    tar -czf "${OUTPUT_DIR}/socketghost-ui-web-v${VERSION}.tar.gz" -C "${DIST_DIR}" .
fi

echo "✅ UI web bundle packaged: ${OUTPUT_DIR}/${ARCHIVE_NAME}"
ls -lh "${OUTPUT_DIR}/${ARCHIVE_NAME}" || ls -lh "${OUTPUT_DIR}/socketghost-ui-web-v${VERSION}.tar.gz"

# Attempt Tauri build if Rust is available
if command -v cargo &> /dev/null; then
    echo "Rust detected - attempting Tauri native build..."
    cd "${UI_DIR}"
    npm run tauri build || echo "⚠️  Tauri build failed - native installer not created"
    cd ..
    
    # Look for installer artifacts
    if [ -f "${UI_DIR}/src-tauri/target/release/bundle/msi/SocketGhost_${VERSION}_x64_en-US.msi" ]; then
        cp "${UI_DIR}/src-tauri/target/release/bundle/msi/"*.msi "${OUTPUT_DIR}/"
        echo "✅ Windows installer: ${OUTPUT_DIR}/SocketGhost_${VERSION}_x64_en-US.msi"
    fi
    
    if [ -f "${UI_DIR}/src-tauri/target/release/bundle/dmg/SocketGhost_${VERSION}_x64.dmg" ]; then
        cp "${UI_DIR}/src-tauri/target/release/bundle/dmg/"*.dmg "${OUTPUT_DIR}/"
        echo "✅ macOS installer: ${OUTPUT_DIR}/SocketGhost_${VERSION}_x64.dmg"
    fi
else
    echo "ℹ️  Rust not found - skipping native installer build"
    echo "   Install Rust: https://rust lang.org/tools/install"
    echo "   Then run: cd socketghost-ui && npm run tauri build"
fi
