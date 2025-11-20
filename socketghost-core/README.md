# SocketGhost Core

Local HTTP/HTTPS proxy with PID mapping, WebSocket event streaming, and flow interception for debugging network traffic.

## Features

- **Transparent Proxy**: HTTP/HTTPS proxy on port 8080
- **PID Mapping**: Resolves process IDs for each network connection (Windows)
- **WebSocket Streaming**: Real-time flow events on `ws://127.0.0.1:9000`
- **Process API**: List running processes via `http://127.0.0.1:9100/processes`
- **Flow Interceptor**: Pause, inspect, forward, or drop requests from specific processes

## Quick Start

```bash
# Build and run
dotnet build
dotnet run

# Set proxy in your app
export HTTP_PROXY=http://127.0.0.1:8080
export HTTPS_PROXY=http://127.0.0.1:8080
```

## Interceptor Usage

### Enable Interception

Send a WebSocket message to enable interception for a specific PID:

```json
{
  "v": "0.1",
  "type": "interceptor.set",
  "pid": 1234,
  "enabled": true
}
```

### Paused Flows

When interceptor is enabled:
- Matching flows are **paused** instead of forwarded
- Core emits `flow.paused` event via WebSocket
- Flow waits for manual action or **auto-forwards after 60 seconds**

### Flow Actions

**Forward** a paused flow:
```json
{
  "v": "0.1",
  "type": "flow.action",
  "action": "forward",
  "flowId": "f-0001"
}
```

**Drop** a paused flow:
```json
{
  "v": "0.1",
  "type": "flow.action",
  "action": "drop",
  "flowId": "f-0001"
}
```

Dropped flows receive HTTP 502 Bad Gateway:
```json
{"error": "SocketGhost: flow dropped by user"}
```

## Architecture

```
┌─────────────┐
│ Your App    │ ──→ HTTP_PROXY=127.0.0.1:8080
└─────────────┘
       │
       ▼
┌─────────────────────────────────────┐
│ SocketGhost Core                    │
│  ├─ Proxy (8080)                    │
│  ├─ PID Resolver                    │
│  ├─ Interceptor Manager             │
│  ├─ WebSocket Server (9000)         │
│  └─ Process API (9100)              │
└─────────────────────────────────────┘
       │
       ▼
┌─────────────┐
│ Target      │
│ Server      │
└─────────────┘
```

## Components

### ProxyServer
- Intercepts HTTP/HTTPS traffic
- Resolves process IDs for each request
- Checks InterceptorManager for pause/resume
- Emits flow events

### InterceptorManager
- Manages intercepted PIDs
- Pauses matching flows
- Handles forward/drop actions
- Auto-forwards after 60s timeout

### WebSocketServer
- Broadcasts flow events to clients
- Receives control messages (interceptor.set, flow.action)
- Supports multiple concurrent clients

### ProcessApi
- Lists all running processes
- Returns `{pid, exe, cmdline}` for each process

## Safety Features

### Auto-Forward Timeout

## Configuration

### Timeout Duration
Default: 60 seconds

Change in `InterceptorManager.cs`:
```csharp
public const int DEFAULT_PAUSE_TIMEOUT_SECONDS = 60;
```

### Port Configuration
- Proxy: `8080` (in `Program.cs`)
- WebSocket: `9000` (in `Program.cs`)
- Process API: `9100` (in `Program.cs`)

## Logging

Flow events are logged to console as JSON:

```json
{"type":"flow.new","flow":{"flowId":"...","pid":1234,"method":"POST","url":"..."}}
{"type":"flow.paused","flowId":"...","pid":1234,"method":"POST","url":"..."}
{"type":"flow.forwarded","flowId":"...","timestamp":"..."}
{"type":"flow.dropped","flowId":"...","timestamp":"..."}
{"type":"flow.auto_forwarded","flowId":"...","timestamp":"...","reason":"timeout"}
```

## Platform Support

- **Windows**: Full support (PID mapping via GetExtendedTcpTable)
- **Linux/macOS**: Partial support (no PID mapping, flows show `pid: null`)

## IPC Documentation

See [`docs/ipc.md`](../docs/ipc.md) for complete WebSocket message specifications.

## Development

```bash
# Build
dotnet build

# Run
dotnet run

# Clean
dotnet clean
```

## Security Warning

⚠️ **Do not use in production.** SocketGhost is designed for local development and debugging only.

- No authentication
- No encryption (beyond HTTPS proxying)
- Binds to localhost only
- Not hardened against malicious input

## Requirements

- .NET 8.0 SDK
- Windows (for PID mapping)
- Administrator rights may be required for certificate installation

## Troubleshooting

### Certificate Errors

Titanium.Web.Proxy generates a root certificate. If you see SSL/TLS errors:
1. Check console output for certificate location
2. Install the certificate if prompted
3. Trust the certificate in your system

### Port Conflicts

If ports 8080, 9000, or 9100 are in use:
1. Stop conflicting processes
2. Or modify ports in `Program.cs`

### PID Resolution Failures

If `pid` is always `null`:
- Ensure running on Windows
- Check Windows Firewall isn't blocking
- Verify app is using the proxy (127.0.0.1:8080)
