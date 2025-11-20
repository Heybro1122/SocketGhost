# Changelog

All notable changes to SocketGhost will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2025-11-20

### Added
- **HTTP/HTTPS Proxy Server** with TLS/SSL interception using Titanium.Web.Proxy
- **Real-time WebSocket Server** for UI communication on port 9000
- **Process Selection & PID Resolution** - Monitor specific applications by PID
- **Flow Interception & Pause/Resume** - Pause HTTP/HTTPS requests for inspection/modification
- **Flow Editor (Monaco)** - Edit request headers and bodies before forwarding
- **Manual Resend Fallback** - Reliable request forwarding when proxy modification fails
- **JavaScript Scripting Engine** - Automate request/response modifications with Jint
- **Script API** (port 9200) - Manage and execute scripts via HTTP API
- **Flow History & Saved Flows** - Persist captured flows to SQLite database
- **Flow History API** (port 9300) - Query, retrieve, export/import flows
- **Flow Replay** - Re-execute captured flows with safety confirmation modal
- **mitmproxy Adapter** - Optional HTTP+HTTPS capture mode (requires Python + mitmproxy)
- **React + Tauri UI** - Professional desktop application with:
  - Process picker with live system processes
  - Real-time flow monitoring dashboard
  - Paused flows list with edit capabilities
  - Monaco code editor for request/response editing
  - Scripts management page
  - Saved Flows browser with export/replay
- **Configuration System** - JSON-based config for retention policies, storage limits
- **Auto-Pruning** - Configurable flow retention (default: 30 days, 500MB limit)
- **Large Body Handling** - Automatic file storage for bodies >128KB
- **CORS Support** - All APIs properly handle OPTIONS preflight requests

### Features
- Intercept HTTPS traffic (Titanium.Web.Proxy engine, default)
- Intercept HTTP+HTTPS traffic (mitmproxy adapter mode, opt-in)
- Pause flows based on PID-specific rules (script-driven or manual)
- Edit request headers and JSON bodies in Monaco editor
- Force-forward invalid requests with confirmation
- Apply JavaScript scripts to modify requests/responses automatically
- Store flows with full request/response data
- Export/import flows as JSON
- Replay stored flows with editable parameters
- Real-time WebSocket updates for all flow events

### Important Notes
- **HTTPS-Only by Default**: Titanium.Web.Proxy (default engine) only intercepts HTTPS. For HTTP capture, use `"engine": "mitm"` in config.json (requires mitmproxy)
- **PID Resolution**: mitmproxy adapter cannot resolve client PID (flows will have `pid: null`)
- **Certificate Trust**: First run generates root CA certificate - must be trusted for HTTPS interception
- **Port Usage**: WebSocket (9000), Process API (9100), Script API (9200), Flow API (9300), Proxy (8080/8081)

### Security
- Local-only bindings (127.0.0.1) for all APIs
- No remote access by default
- Self-signed CA certificate for TLS interception
- Replay confirmation required for safety

### Technical Details
- **.NET 7.0** runtime required for core
- **Node.js 18+** for UI development
- **Rust + Tauri** for native desktop builds
- **SQLite** for flow persistence (with JSONL fallback)
- **Titanium.Web.Proxy** for HTTPS interception
- **Monaco Editor** for code editing
- **React 18** + Vite for UI

### Known Limitations
- HTTP requests not captured with default Titanium engine (use mitm adapter)
- Process command-line arguments not captured (WMI elevation required)
- No code signing for v1.0.0 binaries (instructions provided for maintainers)
- Native installers require local Rust toolchain build

### Breaking Changes
None (initial release)

---

## [Unreleased]

Future enhancements:
- Bi-directional control for mitmproxy adapter (pause/resume from core)
- WinDivert integration for kernel-level HTTP capture with PID resolution
- Flow comparison and diff tools
- Request/response templates for replay
- Certificate pinning controls
- Additional script examples and templates

[1.0.0]: https://github.com/yourusername/SocketGhost/releases/tag/v1.0.0
