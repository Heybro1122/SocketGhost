# mitmproxy Adapter for SocketGhost

This directory contains the mitmproxy integration adapter that enables SocketGhost to intercept **both HTTP and HTTPS traffic**, overcoming the limitation of Titanium.Web.Proxy which only intercepts HTTPS.

## Overview

The adapter runs `mitmdump` (mitmproxy's command-line tool) as a separate proxy on port 8081 with a Python addon script that translates mitmproxy flow events into SocketGhost's WebSocket message format.

**Architecture:**
```
Client App
    ↓ (HTTP_PROXY=http://127.0.0.1:8081)
mitmdump + adapter_addon.py
    ↓ (POST to http://127.0.0.1:9001/ingest)
SocketGhost Core (MitmAdapterManager)
    ↓ (Existing flow pipeline)
UI, Storage, Scripts (unchanged)
```

## Installation

### 1. Install Python and mitmproxy

**Option A: Using pip (recommended)**
```bash
# Create virtual environment (optional but recommended)
python3 -m venv venv
source venv/bin/activate  # On Windows: venv\Scripts\activate

# Install mitmproxy
pip install mitmproxy

# Verify installation
mitmdump --version
```

**Option B: Using system package manager**
```bash
# macOS
brew install mitmproxy

# Linux (Debian/Ubuntu)
sudo apt install mitmproxy

# Windows (via Chocolatey)
choco install mitmproxy
```

### 2. Trust mitmproxy Certificate (for HTTPS)

mitmproxy generates its own CA certificate for HTTPS interception. You need to trust it:

**First run** (generates cert):
```bash
mitmdump
# Press Ctrl+C after it starts
```

**Install certificate:**
- **macOS/Linux**: `~/.mitmproxy/mitmproxy-ca-cert.pem`
- **Windows**: `%USERPROFILE%\.mitmproxy\mitmproxy-ca-cert.p12`

See [mitmproxy docs](https://docs.mitmproxy.org/stable/concepts-certificates/) for detailed instructions.

## Usage

### Option 1: Manual Start (Development)

Start SocketGhost core (it will listen on 9001 for adapter ingestion):
```bash
cd d:/projects/SocketGhost/socketghost-core
dotnet run --engine mitm
```

In a separate terminal, start the adapter:
```bash
cd d:/projects/SocketGhost/socketghost-core/mitm_adapter

# Linux/macOS
./start_mitm.sh

# Windows
start_mitm.bat
```

### Option 2: Automatic Start (when implemented)

When `MitmAdapterManager` is complete, core will spawn mitmdump automatically:
```bash
dotnet run -- --engine mitm
```

## Testing

### HTTP Request
```bash
# Set proxy and make request
curl -x http://127.0.0.1:8081 http://localhost:3000/todo \
    -H "Content-Type: application/json" \
    -d '{"task":"Test HTTP capture"}'
```

Expected output in **core console**:
```json
{
  "v": "0.1",
  "type": "flow.new",
  "flow": {
    "flowId": "...",
    "method": "POST",
    "url": "http://localhost:3000/todo",
    "responseStatusCode": 200,
    "_adapter": {
      "source": "mitmproxy"
    }
  }
}
```

### HTTPS Request
```bash
curl -x http://127.0.0.1:8081 https://httpbin.org/get
```

## Configuration

Environment variables for `start_mitm.sh`/`start_mitm.bat`:
- `SOCKETGHOST_MITM_HOST`: Listen address (default: `127.0.0.1`)
- `SOCKETGHOST_MITM_PORT`: Listen port (default: `8081`)
- `SOCKETGHOST_INGEST_URL`: Core ingestion endpoint (default: `http://127.0.0.1:9001/ingest`)

Example:
```bash
export SOCKETGHOST_MITM_PORT=9090
export SOCKETGHOST_INGEST_URL=http://127.0.0.1:9002/ingest
./start_mitm.sh
```

## Files

- **`adapter_addon.py`**: mitmproxy addon that hooks request/response and sends to core
- **`start_mitm.sh`**: Bash script to launch mitmdump with addon
- **`start_mitm.bat`**: Windows batch script to launch mitmdump with addon
- **`README.md`**: This file

## Limitations

1. **No PID Resolution**: mitmproxy cannot determine client process PID. Flows will have `pid: null`. This is a fundamental limitation of user-mode HTTP proxies.

2. **Separate Port**: Runs on port 8081 (mitm) vs 8080 (Titanium). Clients must choose which proxy to use.

3. **Python Dependency**: Requires Python and mitmproxy. Not bundled with SocketGhost.

## Troubleshooting

### "mitmdump: command not found"
Install mitmproxy: `pip install mitmproxy`

### "Failed to send to core"
Ensure SocketGhost core is running and ingestion endpoint is accessible:
```bash
curl http://127.0.0.1:9001/ingest -X POST -d '{"test":true}'
```

### HTTPS not working
Trust mitmproxy CA certificate (see Installation step 2).

### Port already in use
Change port via environment variable:
```bash
export SOCKETGHOST_MITM_PORT=9090
./start_mitm.sh
```

## Future Enhancements

- [ ] Auto-spawn mitmdump from core (`MitmAdapterManager`)
- [ ] Bi-directional control (pause/resume from core)
- [ ] PID resolution via external tool (WinDivert integration?)
- [ ] Bundle mitmproxy with standalone distribution

## References

- [mitmproxy Documentation](https://docs.mitmproxy.org/)
- [mitmproxy Addons](https://docs.mitmproxy.org/stable/addons-overview/)
- [SocketGhost HTTP Interception Options](../../docs/http-interception-options.md)
