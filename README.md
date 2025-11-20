# SocketGhost

<div align="center">

![SocketGhost Logo](assets/SocketGhost.png)

**The Visual Protocol Interceptor**


[![Release](https://img.shields.io/github/v/release/Heybro1122/SocketGhost)](https://github.com/Heybro1122/SocketGhost/releases)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-7.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/7.0)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey)]()

*Intercept, inspect, modify, and replay HTTP/HTTPS traffic in real-time*

[Download](#-download) • [Features](#-features) • [Quick Start](#-quick-start) • [Documentation](#-documentation)

</div>

---

## 🎯 What is SocketGhost?

**SocketGhost** is a powerful Man-in-the-Middle (MITM) proxy that lets developers intercept, visualize, and modify network traffic from specific applications. Think of it as **Burp Suite meets Charles Proxy** with a modern UI and script automation.

Perfect for:
- 🔍 **API debugging** - Inspect requests/responses between your app and backend
- 🧪 **Testing** - Modify payloads on-the-fly to test edge cases
- 🔒 **Security research** - Analyze encrypted HTTPS traffic
- 🤖 **Automation** - Script transformations with JavaScript
- 📊 **Traffic analysis** - Capture and replay flows for debugging

---

## ⬇️ Download

**Latest Release**: [v1.0.0](https://github.com/Heybro1122/SocketGhost/releases/latest)

| Platform | Download | Size |
|----------|----------|------|
| 🖥️ **Core** (All Platforms) | [socketghost-core-v1.0.0.zip](https://github.com/Heybro1122/SocketGhost/releases/download/v1.0.0/socketghost-core-v1.0.0.zip) | 18.3 MB |
| 🌐 **Web UI** | [socketghost-ui-web-v1.0.0.zip](https://github.com/Heybro1122/SocketGhost/releases/download/v1.0.0/socketghost-ui-web-v1.0.0.zip) | 65 KB |

**Requirements**: [.NET 7.0 Runtime](https://dotnet.microsoft.com/download/dotnet/7.0) (Core) • Modern Browser (UI)

---

## ✨ Features

### 🔌 Core Capabilities
- **HTTPS Interception** - Decrypt and inspect TLS traffic with auto-generated certificates
- **Process Filtering** - Monitor specific applications by PID (no noise from system traffic)
- **Flow Control** - Pause, inspect, modify, and forward requests before they reach the server
- **Real-time WebSocket** - Live flow updates pushed to UI instantly
- **Persistent Storage** - SQLite-based flow history with configurable retention

### 🎨 Professional UI
- **Monaco Editor** - Full-featured code editor with syntax highlighting for request/response editing
- **Scripts Manager** - Write JavaScript to automatically transform flows (Jint-powered)
- **Saved Flows** - Browse, search, export, and replay captured traffic
- **Process Picker** - Select target processes from live system list
- **Dark Theme** - Eye-friendly interface for long debugging sessions

### 🚀 Advanced Features
- **Script Automation** - Transform requests/responses with JavaScript (e.g., auto-login, data sanitization)
- **Flow Replay** - Re-send captured requests with modifications
- **Export/Import** - Share flows as JSON for collaboration
- **Manual Resend Fallback** - Reliable request forwarding even when socket resumption fails
- **Multi-Engine Support** - Titanium.Web.Proxy (HTTPS) or mitmproxy adapter (HTTP+HTTPS)

---

## 🚀 Quick Start

### Option 1: Download Release (Recommended)

1. **Download and extract** the [latest release](https://github.com/Heybro1122/SocketGhost/releases/latest)
2. **Start the core**:
   ```bash
   cd socketghost-core
   dotnet socketghost-core.dll
   ```
3. **Start the UI** (separate terminal):
   ```bash
   cd socketghost-ui-web
   python3 -m http.server 5173
   # or: npx serve -s . -p 5173
   ```
4. **Open browser**: http://localhost:5173

### Option 2: Build from Source

```bash
# Clone repository
git clone https://github.com/Heybro1122/SocketGhost.git
cd SocketGhost

# Build & run core
dotnet run --project socketghost-core

# Build & run UI (separate terminal)
cd socketghost-ui
npm install
npm run dev
```

### Configure Your Application

Point your application to use SocketGhost as a proxy:

**Command Line**:
```bash
# Linux/macOS
export HTTP_PROXY=http://127.0.0.1:8080
export HTTPS_PROXY=http://127.0.0.1:8080

# Windows PowerShell
$env:HTTP_PROXY='http://127.0.0.1:8080'
$env:HTTPS_PROXY='http://127.0.0.1:8080'

# Then run your app
curl https://api.example.com
```

**Application Settings**:
```
Proxy Host: 127.0.0.1
Proxy Port: 8080
```

---

## 📖 Usage

### 1. Select a Process
- Open the UI at http://localhost:5173
- Navigate to **Dashboard**
- Click **Process Picker** and select your target application
- Enable the **Interceptor** toggle

### 2. Capture Traffic
- Run your application with proxy settings configured
- Flows appear in the **Live Flows** panel in real-time
- Click any flow to view full request/response details

### 3. Pause & Edit (Optional)
- With Interceptor enabled, matching flows are **paused**
- Click **Edit** to modify headers or body
- Click **Forward** to continue, or **Drop** to block

### 4. Save & Replay
- Captured flows are automatically saved to **Saved Flows**
- Export, replay, or delete flows as needed
- Replay with modifications using the safety confirmation modal

### 5. Automate with Scripts
- Navigate to **Scripts** page
- Create JavaScript scripts to transform flows automatically
- Enable scripts to apply on matching requests/responses

**Example Script** (Auto-modify login response):
```javascript
function onResponse(flow) {
  if (flow.url.includes('/login')) {
    const body = JSON.parse(flow.response.body);
    body.status = 'success';
    flow.response.body = JSON.stringify(body);
  }
  return flow;
}
```

---

## 🔧 Configuration

Edit `config.json` in the core directory:

```json
{
  "Engine": "titanium",           // "titanium" or "mitm"
  "FlowRetentionDays": 30,       // Auto-delete flows older than N days
  "FlowStoreMaxTotalBytes": 524288000,  // 500 MB max storage
  "FlowStoreMaxInlineBytes": 131072,    // 128 KB per inline body
  "AutoPruneOnStart": true       // Clean old flows on startup
}
```

### HTTP Interception (Optional)

**Default**: SocketGhost only intercepts HTTPS traffic (Titanium engine).

For **HTTP + HTTPS**, use mitmproxy adapter:

1. Install mitmproxy: `pip install mitmproxy`
2. Set `"Engine": "mitm"` in `config.json`
3. Restart core

See [mitm_adapter/README.md](socketghost-core/mitm_adapter/README.md) for details.

---

## 🏗️ Architecture

```
┌─────────────────┐
│   Your App      │
│  (HTTP Client)  │
└────────┬────────┘
         │ Proxy: 127.0.0.1:8080
         ▼
┌─────────────────────────────────────────┐
│        SocketGhost Core (.NET)          │
├─────────────────────────────────────────┤
│ • Proxy Server (8080/8081)              │
│ • WebSocket Server (9000)               │
│ • Process API (9100)                    │
│ • Script API (9200)                     │
│ • Flow API (9300)                       │
│ • SQLite Storage                        │
│ • Jint Script Engine                    │
└────────┬────────────────────────────────┘
         │ WebSocket
         ▼
┌─────────────────────────────────────────┐
│     SocketGhost UI (React + Vite)       │
├─────────────────────────────────────────┤
│ • Live Flows Dashboard                  │
│ • Monaco Flow Editor                    │
│ • Scripts Manager                       │
│ • Saved Flows Browser                   │
│ • Process Picker                        │
└─────────────────────────────────────────┘
```

**APIs**:
- `127.0.0.1:8080` - HTTP/HTTPS Proxy (explicit proxy endpoint)
- `127.0.0.1:8081` - HTTPS Proxy (alternate endpoint)
- `ws://127.0.0.1:9000` - WebSocket (real-time flow events)
- `http://127.0.0.1:9100` - Process API (system process list)
- `http://127.0.0.1:9200` - Script API (CRUD scripts)
- `http://127.0.0.1:9300` - Flow API (query, replay, export flows)

---

## 📚 Documentation

- **[CHANGELOG](CHANGELOG.md)** - Version history and release notes
- **[IPC Protocol](docs/ipc.md)** - WebSocket events and API reference
- **[CI/CD Guide](docs/CI_README.md)** - Build workflows and automation
- **[Architecture](docs/architecture.md)** - System design and components
- **[Security](socketghost-core/scripts/Security.md)** - Script sandboxing and safety

---

## 🛡️ Security Notes

### Certificate Trust Required

On first run, SocketGhost generates a root CA certificate for HTTPS interception. You must trust this certificate:

- **Windows**: Auto-installed to `CurrentUser\Root` store
- **macOS**: Import `~/.mitmproxy/mitmproxy-ca-cert.pem` to Keychain
- **Linux**: Install to `/usr/local/share/ca-certificates/`

### Localhost Only

All APIs bind to `127.0.0.1` (localhost) by default. No remote access without manual configuration changes.

### Script Sandboxing

JavaScript scripts run in a Jint sandbox with:
- 5-second timeout limit
- No file system access
- No network access
- Limited to flow manipulation only

---

## 🐛 Known Limitations

- **HTTPS-Only by Default**: Titanium engine captures HTTPS only. Use mitmproxy adapter for HTTP.
- **Windows Process Info**: Command-line arguments require elevation on Windows.
- **Unsigned Binaries**: Expect SmartScreen warnings (no code signing certificate).
- **mitmproxy PID**: Adapter mode cannot resolve client PID (shows `null`).

---

## 🤝 Contributing

Contributions welcome! Please:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Development Setup

```bash
# Prerequisites
- .NET 7.0 SDK
- Node.js 18+
- (Optional) Docker for container builds

# Build core
dotnet build socketghost-core/

# Run core with hot reload
dotnet watch --project socketghost-core/

# Build UI
cd socketghost-ui
npm install
npm run dev
```

---

## 🗺️ Roadmap

- [ ] Native desktop app (Tauri with code signing)
- [ ] Unit and integration tests
- [ ] WebSocket compression for large flows
- [ ] HTTP/2 and HTTP/3 support
- [ ] Request/response diff viewer
- [ ] Flow search with regex
- [ ] Custom certificate pinning bypass
- [ ] macOS and Linux process info improvements
- [ ] Plugin system for custom transformations

---

## 📄 License

[MIT License](LICENSE) © 2025

Built with:
- [Titanium.Web.Proxy](https://github.com/justcoding121/Titanium-Web-Proxy)
- [mitmproxy](https://mitmproxy.org/)
- [React](https://react.dev/)
- [Monaco Editor](https://microsoft.github.io/monaco-editor/)
- [Jint](https://github.com/sebastienros/jint)

---

## 🙏 Acknowledgments

Inspired by:
- [Fiddler](https://www.telerik.com/fiddler)
- [Charles Proxy](https://www.charlesproxy.com/)
- [Burp Suite](https://portswigger.net/burp)
- [mitmproxy](https://mitmproxy.org/)

---

<div align="center">

**⭐ Star this repo if SocketGhost helps you!**

[Report Bug](https://github.com/Heybro1122/SocketGhost/issues) • [Request Feature](https://github.com/Heybro1122/SocketGhost/issues) • [Discussions](https://github.com/Heybro1122/SocketGhost/discussions)

</div>
