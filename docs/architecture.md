# SocketGhost Architecture (MVP)

## High-Level Overview

```mermaid
graph TD
    A[Target App (e.g., spotify.exe)] -->|Proxy 127.0.0.1:8080| B(SocketGhost Core);
    B -->|Forward| C[Internet / Server];
    C -->|Response| B;
    B -->|Response| A;
    
    B -.->|WebSocket flow.new| D[Frontend UI (Future)];
```

## Components

### 1. SocketGhost Core (.NET 8)
- **Proxy Server:** Uses `Titanium.Web.Proxy` to handle HTTP/HTTPS traffic.
- **WebSocket Server:** Broadcasts traffic events to the UI.
- **PID Mapping (TODO):** Will map sockets to Process IDs using WFP or `GetExtendedTcpTable`.
- **Script Sandbox (TODO):** Will execute user JS hooks.

### 2. Frontend UI (Future)
- **React + Tauri:** Visualizes the node graph.
- **Monaco Editor:** Allows editing requests/responses.

## IPC Contract (WebSocket)
**Version:** 0.1

**Message: `flow.new`**
```json
{
  "v": "0.1",
  "type": "flow.new",
  "flow": {
    "flowId": "guid",
    "pid": null, 
    "method": "GET",
    "url": "https://example.com",
    "headers": {},
    "bodyPreview": "..."
  }
}
```
