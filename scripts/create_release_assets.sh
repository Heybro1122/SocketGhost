#!/usr/bin/env bash
# create_release_assets.sh - Generate SHA256 checksums for release artifacts

set -e

OUTPUT_DIR="artifacts"
CHECKSUMS_FILE="${OUTPUT_DIR}/sha256sums.txt"

echo "Generating SHA256 checksums for release artifacts..."

cd "${OUTPUT_DIR}"

# Clear existing checksums
> sha256sums.txt

# Generate checksums for all artifacts
for file in *.zip *.tar.gz *.msi *.dmg *.exe 2>/dev/null; do
    if [ -f "$file" ]; then
        if command -v sha256sum &> /dev/null; then
            sha256sum "$file" >> sha256sums.txt
        elif command -v shasum &> /dev/null; then
            shasum -a 256 "$file" >> sha256sums.txt
        else
            echo "ERROR: No SHA256 utility found (sha256sum or shasum)"
            exit 1
        fi
        echo "✓ $file"
    fi
done

cd ..

if [ -s "${CHECKSUMS_FILE}" ]; then
    echo ""
    echo "✅ Checksums generated: ${CHECKSUMS_FILE}"
    echo ""
    cat "${CHECKSUMS_FILE}"
else
    echo "⚠️  No artifacts found to checksum"
fi
