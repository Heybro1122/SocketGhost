# HTTP Interception Options - Research & Recommendation

## Problem Statement

Titanium.Web.Proxy (current engine) only triggers `BeforeRequest`/`BeforeResponse` events for HTTPS/SSL traffic when used as an explicit proxy. Plain HTTP requests are forwarded transparently without interception, preventing flow capture, scripting, and pause/replay for HTTP traffic.

## Evaluated Options

### Option 1: mitmproxy/mitmdump Adapter (Sidecar)

**Description**: Run mitmproxy as a subprocess with a Python addon that posts flow events to SocketGhost core via WebSocket/HTTP.

**Implementation Complexity**: Medium
- Python addon script (~150 lines)
- Subprocess management in C# (~200 lines)
- WebSocket/HTTP ingestion endpoint
- Message translation layer

**Required Privileges**: None (user-mode)

**User Experience Impact**:
- **Install**: `pip install mitmproxy` (one-time)
- **Cert Trust**: Trust mitmproxy CA for HTTPS (documented process)
- **Runtime**: Transparent (core spawns mitmdump automatically)

**Compatibility with Current Architecture**: ✅ Excellent
- Emits same `flow.new`, `flow.paused` WebSocket messages
- No changes to UI, storage, or scripting layers
- Can run alongside Titanium (engine selection via config)

**Pros**:
- ✅ Works for HTTP **and** HTTPS
- ✅ No admin rights or kernel drivers
- ✅ Python is common in dev environments
- ✅ Mature, well-documented (mitmproxy used widely)
- ✅ Can be optional/opt-in feature

**Cons**:
- ❌ Python dependency (not bundled)
- ❌ Cannot get client PID (mitmproxy limitation)
- ❌ Slightly higher latency than native
- ❌ Requires separate port (e.g., 8081 for mitm, 8080 for Titanium)

**Distribution Complexity**: Low-Medium
- Document Python install in README
- Provide helper scripts for mitmdump setup
- Optional feature (doesn't break existing users)

---

### Option 2: WinDivert / WFP / NDIS Driver

**Description**: Kernel-level packet interception using Windows Filtering Platform or NDIS intermediate driver.

**Implementation Complexity**: High
- Kernel driver development or WinDivert integration (~1000+ lines)
- Packet parsing and TCP stream reconstruction
- Connection tracking per PID
- Requires deep Windows networking knowledge

**Required Privileges**: Administrator (driver install/load)

**User Experience Impact**:
- **Install**: Driver installation (may require reboot)
- **Privileges**: Admin rights to load driver
- **Distribution**: Driver signing for production
- **Compatibility**: Potential conflicts with VPNs, antivirus

**Compatibility with Current Architecture**: ⚠️ Moderate
- Would need new packet processing layer
- Can integrate with existing flow pipeline
- Significant refactoring required

**Pros**:
- ✅ Complete traffic capture (all processes)
- ✅ Can resolve client PID accurately
- ✅ Low-level control

**Cons**:
- ❌ Requires Administrator privileges
- ❌ High implementation complexity
- ❌ Driver signing required for distribution
- ❌ Potential security/stability risks
- ❌ Difficult to debug

**Distribution Complexity**: Very High
- Driver signing costs
- Install/uninstall complexity
- Support burden for driver issues

---

### Option 3: Npcap/WinPcap + Packet Capture

**Description**: Use Npcap driver to capture raw packets, reconstruct TCP streams in user-mode.

**Implementation Complexity**: High
- Packet capture via pcap (~500 lines)
- TCP stream reassembly
- HTTP parsing from raw streams
- Connection-to-PID mapping (requires additional APIs)

**Required Privileges**: Admin (Npcap driver install)

**User Experience Impact**:
- **Install**: Npcap installer (separate download)
- **Privileges**: May need admin for capture
- **Compatibility**: Works with VPNs/adapters

**Compatibility with Current Architecture**: ⚠️ Moderate
- Requires new packet processing layer
- Can feed into existing flow pipeline

**Pros**:
- ✅ Low-level packet visibility
- ✅ Can capture non-proxied traffic

**Cons**:
- ❌ Complex TCP reassembly
- ❌ Requires Npcap driver (admin)
- ❌ High CPU for packet processing
- ❌ Difficult PID resolution

**Distribution Complexity**: High
- Bundle or require Npcap installer
- Driver dependency

---

### Option 4: System Proxy / PAC Configuration

**Description**: Configure Windows system proxy settings to point to SocketGhost, or use PAC file.

**Implementation Complexity**: Low
- Registry modifications for system proxy (~50 lines)
- PAC file generation

**Required Privileges**: None (for user-level proxy settings)

**User Experience Impact**:
- **Setup**: Manual system proxy configuration
- **Compatibility**: Only works for proxy-aware apps (browsers, curl, etc.)
- **Limitations**: Many native apps ignore system proxy

**Compatibility with Current Architecture**: ✅ Perfect
- No code changes needed
- Users configure apps to use existing proxy

**Pros**:
- ✅ Simple implementation
- ✅ No new dependencies
- ✅ Works with current Titanium proxy (if configured for HTTP)

**Cons**:
- ❌ Doesn't solve Titanium HTTP limitation
- ❌ Only works for proxy-aware applications
- ❌ Not suitable for monitoring arbitrary processes

**Distribution Complexity**: Very Low

**Note**: This doesn't actually solve the core problem - Titanium still won't intercept HTTP.

---

### Option 5: Launch-Target-with-Proxy Wrapper

**Description**: Provide scripts/wrappers that launch target processes with `HTTP_PROXY` environment variable set.

**Implementation Complexity**: Very Low
- Shell scripts or launcher tool (~100 lines)
- Set `HTTP_PROXY=http://127.0.0.1:8080` before exec

**Required Privileges**: None

**User Experience Impact**:
- **Setup**: Launch apps via wrapper script
- **Limitations**: Only works for processes launched this way
- **Compatibility**: Apps must respect HTTP_PROXY env var

**Compatibility with Current Architecture**: ✅ Good
- No core changes needed
- Works with existing Titanium proxy

**Pros**:
- ✅ Extremely simple
- ✅ Good for demos/development
- ✅ No dependencies

**Cons**:
- ❌ Not general-purpose (can't monitor arbitrary running processes)
- ❌ Doesn't work for apps that ignore HTTP_PROXY
- ❌ Still doesn't solve Titanium HTTP interception issue

**Distribution Complexity**: Very Low

---

## Comparison Matrix

| Option | Complexity | Privileges | HTTP Support | HTTPS Support | PID Resolution | Distribution | MVP Fit |
|--------|-----------|-----------|--------------|---------------|----------------|--------------|---------|
| **mitmproxy adapter** | Medium | None | ✅ | ✅ | ❌ (null) | Low-Med | ✅ **Best** |
| WinDivert/WFP | High | Admin | ✅ | ✅ | ✅ | Very High | ❌ |
| Npcap | High | Admin | ✅ | ⚠️ | ⚠️ | High | ❌ |
| System Proxy | Low | None | ❌ | ❌ | N/A | Very Low | ❌ |
| Launch Wrapper | Very Low | None | ⚠️ | ⚠️ | ✅ | Very Low | ⚠️ (limited) |

## Recommendation: **mitmproxy Adapter Mode**

### Rationale

For the MVP, **mitmproxy adapter mode** is the clear winner:

1. **Solves the core problem**: Captures both HTTP and HTTPS traffic
2. **No admin/drivers**: Runs entirely in user-mode, no kernel components
3. **Minimal friction**: Python is widely available; one-time `pip install`
4. **Architecturally sound**: Emits same messages, no UI/storage changes needed
5. **Opt-in**: Can be optional feature alongside Titanium default
6. **Production-ready**: mitmproxy is mature, battle-tested

### Tradeoffs Accepted

- **Python dependency**: Acceptable for developer tool; document clearly
- **No PID resolution**: Set `pid: null` in flows from adapter; PID already nullable
- **Separate port**: mitm on 8081, Titanium on 8080 - clear separation

### Rejected Alternatives

- **Kernel drivers** (WinDivert, Npcap): Too complex, admin required, hard to distribute
- **System proxy/wrappers**: Don't actually solve Titanium's HTTP limitation
- **Future consideration**: If PID is critical, could layer WinDivert later for enterprise

## Implementation Plan (Phase B)

If proceeding with mitmproxy adapter:

### Architecture
```
┌─────────────────┐
│  Client App     │
└────────┬────────┘
         │ HTTP_PROXY=:8081
         ▼
┌─────────────────┐
│  mitmdump       │ (Python, port 8081)
│  + addon script │
└────────┬────────┘
         │ WebSocket: ws://127.0.0.1:9001/ingest
         ▼
┌─────────────────┐
│ SocketGhost Core│
│ (engine=mitm)   │
│ MitmAdapter-    │
│ Manager         │
└─────────────────┘
```

### Components

1. **`mitm_adapter/adapter_addon.py`** (~150 lines)
   - mitmproxy addon class
   - Hook `request()` and `response()` events
   - Extract method, URL, headers, body
   - POST to `http://127.0.0.1:9001/ingest` as flow.new JSON

2. **`MitmAdapterManager.cs`** (~250 lines)
   - Spawn `mitmdump -s adapter_addon.py -p 8081`
   - HTTP ingestion endpoint on 9001
   - Translate adapter JSON → internal flow pipeline
   - Process monitoring/restart

3. **Config changes**
   ```json
   {
     "engine": "titanium",  // or "mitm"
     "mitm": {
       "pythonPath": "python3",
       "port": 8081,
       "ingestPort": 9001
     }
   }
   ```

4. **Documentation**
   - `mitm_adapter/README.md`: Install/setup
   - Update main README: HTTPS limitation + mitm solution

### Testing
```bash
# Install
pip install mitmproxy

# Start core with mitm
dotnet run --engine mitm

# Test HTTP capture
curl -x http://127.0.0.1:8081 http://localhost:3000/todo -d '{"task":"test"}'

# Expect: core logs flow.new with URL http://localhost:3000/todo
```

## Alternative: Document HTTPS-Only

If mitmproxy path is rejected, recommendation is:

### Add to README.md

```markdown
## ⚠️ HTTP Interception Limitation

SocketGhost currently uses Titanium.Web.Proxy, which **only intercepts HTTPS traffic** 
in explicit proxy mode. Plain HTTP requests are forwarded transparently without 
triggering interception events.

**Impact**: Flow capture, pause/resume, scripting, and replay work for HTTPS only.

**Workarounds**:
1. **Use HTTPS endpoints** for testing (recommended)
2. **mitmproxy mode** (future): Run with `--engine mitm` for HTTP+HTTPS support
3. **Environment wrapper**: Launch target apps with `HTTP_PROXY` env var
   ```bash
   HTTP_PROXY=http://127.0.0.1:8080 curl http://example.com
   ```

**Future**: Investigating kernel-level capture (WinDivert) for full HTTP+HTTPS support 
without Python dependencies.
```

## Conclusion

**Recommended: Implement mitmproxy adapter mode (Phase B)**

This provides robust HTTP+HTTPS capture with minimal friction, keeping Titanium as default for users who don't need HTTP or don't want Python dependency.
