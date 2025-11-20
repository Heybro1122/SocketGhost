#!/bin/bash

# start_mitm.sh - Start mitmproxy adapter for SocketGhost

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ADDON_SCRIPT="$SCRIPT_DIR/adapter_addon.py"

# Check if mitmproxy is installed
if ! command -v mitmdump &> /dev/null; then
    echo "Error: mitmdump not found. Please install mitmproxy:"
    echo "  pip install mitmproxy"
    echo "or"
    echo "  python3 -m pip install mitmproxy"
    exit 1
fi

# Default values
LISTEN_HOST="${SOCKETGHOST_MITM_HOST:-127.0.0.1}"
LISTEN_PORT="${SOCKETGHOST_MITM_PORT:-8081}"
INGEST_URL="${SOCKETGHOST_INGEST_URL:-http://127.0.0.1:9001/ingest}"

echo "Starting mitmproxy adapter for SocketGhost..."
echo "Proxy: http://$LISTEN_HOST:$LISTEN_PORT"
echo "Ingest endpoint: $INGEST_URL"

# Export for addon script
export SOCKETGHOST_INGEST_URL="$INGEST_URL"

# Start mitmdump
exec mitmdump \
    -s "$ADDON_SCRIPT" \
    --listen-host "$LISTEN_HOST" \
    --listen-port "$LISTEN_PORT" \
    --set flow_detail=2 \
    "$@"
