# SocketGhost WebSocket IPC Documentation

## Overview

SocketGhost uses WebSocket communication between the UI and Core for real-time flow events and interceptor control. The WebSocket server runs on `ws://127.0.0.1:9000`.

## Message Format

All messages use JSON with a version field (`v`) for future compatibility.

```json
{
  "v": "0.1",
  "type": "message_type",
  ...
}
```

## UI → Core (Control Messages)

### interceptor.set

Enable or disable the interceptor for a specific PID.

**Message:**
```json
{
  "v": "0.1",
  "type": "interceptor.set",
  "pid": 1234,
  "enabled": true
}
```

**Fields:**
- `pid` (number): Process ID to intercept
- `enabled` (boolean): `true` to enable, `false` to disable

**Behavior:**
- When enabled, matching flows will be paused instead of forwarded
- When disabled, flows proceed normally
- Multiple PIDs can be intercepted simultaneously

### flow.action

Perform an action on a paused flow.

**Message:**
```json
{
  "v": "0.1",
  "type": "flow.action",
  "action": "forward",
  "flowId": "f-0001"
}
```

**Fields:**
- `action` (string): `"forward"` or `"drop"`
- `flowId` (string): ID of the paused flow

**Actions:**
- `forward`: Resume the request and send it to the target server
- `drop`: Abort the request and respond with HTTP 502 Bad Gateway

### flow.update

Update headers and/or body of a paused flow before forwarding.

**Message:**
```json
{
  "v": "0.1",
  "type": "flow.update",
  "flowId": "f-0001",
  "update": {
    "headers": {
      "Content-Type": "application/json",
      "X-Custom-Header": "value"
    },
    "body": "{\"task\":\"buy milk - MODIFIED\"}"
  }
}
```

**Fields:**
- `flowId` (string): ID of the paused flow to update
- `update` (object): Update payload
  - `headers` (object, optional): Updated headers (replaces original)
  - `body` (string, optional): Updated body as string

**Behavior:**
- Updates are stored with the paused flow
- Subsequent `flow.action` with `"forward"` will use modified content
- Core emits `flow.updated` event immediately
- If flow times out before forward, auto-forwards with latest update

**Example:**
```javascript
// UI sends update
ws.send(JSON.stringify({
  v: '0.1',
  type: 'flow.update',
  flowId: 'abc123',
  update: {
    headers: { 'X-Modified': 'true' },
    body: '{"modified": true}'
  }
}));

// Then forward
ws.send(JSON.stringify({
  v: '0.1',
  type: 'flow.action',
  action: 'forward',
  flowId: 'abc123'
}));
```

## Core → UI (Event Messages)

### flow.new

Standard flow event (existing behavior).

**Message:**
```json
{
  "v": "0.1",
  "type": "flow.new",
  "flow": {
    "flowId": "f-0001",
    "pid": 1234,
    "method": "POST",
    "url": "http://localhost:3000/api/tasks",
    "headers": {
      "Content-Type": "application/json"
    },
    "bodyPreview": "{\"task\":\"example\"}"
  }
}
```

### flow.paused

Emitted when a flow is paused by the interceptor.

**Message:**
```json
{
  "v": "0.1",
  "type": "flow.paused",
  "flow": {
    "flowId": "f-0001",
    "pid": 1234,
    "method": "POST",
    "url": "http://localhost:3000/api/tasks",
    "headers": {
      "Content-Type": "application/json"
    },
    "bodyPreview": "{\"task\":\"example\"}",
    "receivedAt": "2025-11-20T00:00:00.000Z"
  }
}
```

**Fields:**
- `receivedAt` (string): ISO 8601 timestamp when flow was paused

**Behavior:**
- Flow is held in memory and not forwarded to server
- Auto-forwards after 60 seconds if no action taken
- Application may block waiting for response

### flow.updated

Emitted when a paused flow is updated via flow.update message.

**Message:**
```json
{
  "v": "0.1",
  "type": "flow.updated",
  "flowId": "f-0001",
  "update": {
    "headers": {
      "X-Custom": "value"
    },
    "body": "{\"modified\":true}"
  },
  "timestamp": "2025-11-20T00:00:03.000Z"
}
```

**Console Log Format:**
```json
{
  "type": "flow.updated",
  "flowId": "f-0001",
  "pid": 1234,
  "updateSummary": {
    "headersChanged": ["X-Custom", "Content-Type"],
    "bodyLength": 18
  },
  "timestamp": "2025-11-20T00:00:03.000Z"
}
```

### flow.forwarded

Emitted when a paused flow is manually forwarded.

**Message:**
```json
{
  "v": "0.1",
  "type": "flow.forwarded",
  "flowId": "f-0001",
  "viaUpdate": true,
  "timestamp": "2025-11-20T00:00:05.000Z"
}
```

**Fields:**
- `viaUpdate` (boolean): `true` if flow was modified before forwarding, `false` otherwise

**Console Log Format:**
```json
{
  "type": "flow.forwarded",
  "flowId": "f-0001",
  "pid": 1234,
  "viaUpdate": true,
  "timestamp": "2025-11-20T00:00:05.000Z"
}
```

### flow.dropped

Emitted when a paused flow is manually dropped.

**Message:**
```json
{
  "v": "0.1",
  "type": "flow.dropped",
  "flowId": "f-0001",
  "timestamp": "2025-11-20T00:00:05.000Z"
}
```

**Behavior:**
- Application receives HTTP 502 Bad Gateway response
- Response body: `{"error": "SocketGhost: flow dropped by user"}`

### flow.manual_resend

Emitted when a flow is re-sent manually (e.g. after modification).

**Message:**
```json
{
  "v": "0.1",
  "type": "flow.manual_resend",
  "flowId": "f-0001",
  "pid": 1234,
  "info": {
    "remoteHost": "example.com",
    "bodyLength": 1024,
    "durationMs": 150.5,
    "statusCode": 200
  },
  "timestamp": "2025-11-20T00:00:05.000Z"
}
```

### flow.auto_forwarded

Emitted when a paused flow is automatically forwarded due to timeout.

**Message:**
```json
{
  "v": "0.1",
  "type": "flow.auto_forwarded",
  "flowId": "f-0001",
  "viaUpdate": true,
  "reason": "timeout",
  "timestamp": "2025-11-20T00:01:00.000Z"
}
```

**Fields:**
- `viaUpdate` (boolean): `true` if flow had updates when auto-forwarded
- `reason` (string): Always `"timeout"` for auto-forwards

**Behavior:**
- Occurs after 60 seconds of inactivity
- Prevents indefinite application blocking
- If flow was updated, forwards with modifications
- If not updated, forwards original request

## Flow State Diagram

```
┌─────────────────┐
│  New Request    │
└────────┬────────┘
         │
         ▼
    Interceptor
     enabled?
         │
    ┌────┴────┐
    │         │
   Yes       No
    │         │
    ▼         ▼
┌───────┐  ┌────────┐
│ Pause │  │Forward │
└───┬───┘  └────────┘
    │
    ├─── flow.update ──────→ (Store modifications)
    │
    ├─── User: Forward ───→ ┌────────────┐
    │                        │Forward     │
    │                        │(viaUpdate) │
    │                        └────────────┘
    ├─── User: Drop ──────→ ┌────────┐
    │                        │ Drop   │
    │                        └────────┘
    └─── 60s timeout ─────→ ┌────────────────┐
                             │ Auto-Forward   │
                             │ (viaUpdate if  │
                             │  was updated)  │
                             └────────────────┘
```

## Error Handling

### Unknown Message Types

The core logs unknown message types and ignores them:

```
[WebSocket] Unknown control message type: unknown_type
```

### Missing WebSocket Connection

UI hooks check WebSocket state before sending:

```typescript
if (!ws || ws.readyState !== WebSocket.OPEN) {
  console.error('[useInterceptor] WebSocket not connected');
  return;
}
```

### Flow Not Found

If a flow action references a non-existent flow:

```
[Interceptor] Warning: Flow f-0001 not found for forward
[Interceptor] Warning: Flow f-0001 not found for update
```

## Configuration

### Timeout Duration

Default: 60 seconds

Defined in `InterceptorManager.cs`:
```csharp
public const int DEFAULT_PAUSE_TIMEOUT_SECONDS = 60;
```

### WebSocket URL

Default: `ws://127.0.0.1:9000`

Configurable in:
- Core: `Program.cs` (server)
- UI: `hooks/useWebSocket.ts` (client)

### Body Size Limit (UI)

Default: 1MB

For bodies larger than 1MB, the UI Flow Editor:
- Disables body editing
- Allows header-only edits
- Shows warning message

## Security Considerations

1. **System Process Protection**: UI blocks interceptor for critical system processes (PID 4, services.exe, etc.)
2. **Auto-Forward Timeout**: Prevents indefinite application blocking
3. **Localhost Only**: WebSocket server only accepts connections from 127.0.0.1
4. **No Authentication**: Designed for local dev/debug use only
5. **Content Validation**: UI provides JSON validation with optional force-forward

## Example Usage

### Enable Interceptor

```javascript
// UI sends
ws.send(JSON.stringify({
  v: '0.1',
  type: 'interceptor.set',
  pid: 1234,
  enabled: true
}));

// Core logs
[Interceptor] Enabled for PID 1234
```

### Pause Flow

```json
// Core emits
{"type":"flow.paused","flowId":"abc123","pid":1234,"method":"POST","url":"http://localhost:3000/api"}

// Core broadcasts WS event
{
  "v": "0.1",
  "type": "flow.paused",
  "flow": { ... }
}
```

### Update and Forward Flow

```javascript
// Step 1: Update flow
ws.send(JSON.stringify({
  v: '0.1',
  type: 'flow.update',
  flowId: 'abc123',
  update: {
    body: '{"task":"buy milk - FREE"}'
  }
}));

// Core logs
{"type":"flow.updated","flowId":"abc123","pid":1234,"updateSummary":{"headersChanged":[],"bodyLength":27},...}

// Step 2: Forward flow
ws.send(JSON.stringify({
  v: '0.1',
  type: 'flow.action',
  action: 'forward',
  flowId: 'abc123'
}));

// Core logs
{"type":"flow.forwarded","flowId":"abc123","pid":1234,"viaUpdate":true,...}

// Server receives modified body
```

## Versioning

Current version: `0.1`

Future versions will maintain backwards compatibility. Unknown message types or versions are ignored with a warning.

## Implementation Notes

### Request Modification Limitations

Due to Titanium.Web.Proxy's architecture, paused requests may have already timed out on the client side when flow.update is applied. The core logs the modification intent with `viaUpdate:true`, but actual request modification depends on client timeout settings.

Future implementations may include:
- Manual HTTP client re-send for modified requests
- Custom header `X-SocketGhost-Forwarded: true` to identify re-sent requests
- Response relay back to original client

### Monaco Editor Features (UI)

The Flow Editor provides:
- Syntax highlighting for JSON/XML/HTML/JavaScript
- JSON validation with visual feedback
- "Force forward invalid JSON" option
- Auto-detect content type for language mode
- Format JSON button
- localStorage preferences (tab size, auto-parse)
- Binary content detection (disables editing)
- Header add/remove UI
