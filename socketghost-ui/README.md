# SocketGhost UI

Minimal interactive desktop UI for SocketGhost using **Vite + React + TypeScript**. Provides a Process Picker and live Flows view for intercepted network traffic.

> **Note**: Tauri desktop app wrapper is optional. The UI runs standalone in web mode via Vite for maximum compatibility.

## Features

- **Process Picker**: Fetch and display running processes from the core API
  - Searchable list with PID, executable name, and command line
  - Select a target process (persists to localStorage)
- **Live Flows**: Real-time WebSocket stream of intercepted traffic
  - Display flow rows with method, URL, PID, and body preview
  - Filter by: All | Targeted PID | Unmapped (pid=null)
  - Click to view full JSON details
- **Demo Mode**: Mock processes and flows for UI testing without core
- **Error Handling**: Retry UI when core services are unreachable

## Prerequisites

- **Node.js** 18+ and npm
- **SocketGhost Core** running on:
  - Process API: `http://127.0.0.1:9100`
  - WebSocket: `ws://127.0.0.1:9000`

## Quick Start

### Install Dependencies

```bash
cd socketghost-ui
npm install
```

### Run in Dev Mode (Web-only)

```bash
npm run dev
```

The UI will start on `http://localhost:5173`. Open in your browser.

### Optional: Run as Tauri Desktop App

If you have the Rust toolchain and Tauri CLI installed:

```bash
npm install @tauri-apps/cli
npm run tauri dev
```

> **Note**: Tauri is optional. The app works fully in web mode.

## Usage

1. **Start the SocketGhost Core**:
   ```bash
   cd ../socketghost-core
   dotnet run
   ```

2. **Start the UI**:
   ```bash
   cd ../socketghost-ui
   npm run dev
   ```

3. **Select a Target Process**:
   - Browse the Process Picker panel
   - Search for a process (e.g., "node", "chrome")
   - Click to select as target

4. **View Live Flows**:
   - Run your application through the proxy (127.0.0.1:8080)
   - Watch flows appear in the Flows panel
   - Click a flow to see full JSON details

5. **Filter Flows**:
   - Use the dropdown to filter by:
     - **All Flows**: Show everything
     - **Only Targeted PID**: Show flows from selected process only
     - **Unmapped**: Show flows with pid=null

6. **Demo Mode**:
   - Toggle "Demo Mode" checkbox to use mock data
   - Test the UI without running the core

## Interceptor (Pause/Resume)

The Interceptor allows you to pause requests from a selected process, inspect them, and manually forward or drop them.

### Enable Interceptor

1. **Select a Target Process** in the Process Picker
2. Click **"Enable Interceptor"** button in the header
3. Confirm the warning dialog

⚠️ **Warning**: Enabling interception will pause requests from the target process. The application may block until flows are forwarded or dropped.

### Paused Flows

When the interceptor is enabled:
- Matching flows appear in the **Paused Flows** panel (yellow section above Live Flows)
- Each paused flow shows:
  - Method, URL, PID, and body preview
  - **Age timer** (how long it's been paused)
  - **Forward** and **Drop** buttons

### Flow Actions

- **Forward**: Resume the request and send it to the target server
- **Drop**: Abort the request and respond with HTTP 502 Bad Gateway
- **Auto-Forward**: After 60 seconds, flows automatically forward to prevent deadlock

### Safety Features

- **System PID Protection**: Cannot intercept critical system processes (PID 4, services.exe, etc.)
- **Confirmation Modal**: Warns before enabling interception
- **Auto-Forward Timeout**: Prevents indefinite application blocking
- **localStorage Persistence**: Interceptor state persists across sessions

### Demo Steps

1. Enable Demo Mode to populate mock processes
2. Select "node" (PID 1234)
3. Click "Enable Interceptor" → Confirm dialog
4. In a real scenario, run your app through the proxy
5. Watch requests pause in the Paused Flows panel
6. Click "Forward" or "Drop" to handle each flow


## Development

### Build for Production

```bash
npm run build
```

Output will be in `dist/`.

### Folder Structure

```
socketghost-ui/
├── src/
│   ├── components/
│   │   ├── ProcessPicker.tsx     # Process list and selection
│   │   ├── FlowsList.tsx         # WebSocket flows stream
│   │   ├── FlowDetails.tsx       # Flow JSON modal
│   │   ├── InterceptorToggle.tsx # Enable/disable interceptor
│   │   └── PausedFlowsList.tsx   # Paused flows panel
│   ├── hooks/
│   │   ├── useProcesses.ts       # Process API + localStorage
│   │   ├── useWebSocket.ts       # WebSocket + auto-reconnect
│   │   ├── useInterceptor.ts     # Interceptor state management
│   │   └── usePausedFlows.ts     # Paused flows handling
│   ├── App.tsx                   # Main layout
│   ├── main.tsx                  # React entry point
│   ├── types.ts                  # TypeScript definitions
│   └── index.css                 # Tailwind CSS
├── package.json
├── vite.config.ts
└── tailwind.config.cjs
```

## Features

### Implemented
- **Process Picker**: Browse and select target processes
- **Live Flows**: Real-time WebSocket stream of traffic
- **Interceptor**: Pause/resume/drop requests from selected processes
- **Demo Mode**: Mock data for testing without core

### Future (TODOs in Code)
- **Flow Modification**: Edit requests/responses before forwarding (Monaco editor)
- **Graph Visualization**: Replace list with React Flow diagram
- **Scripting**: Automated flow mutations via sandbox

## Troubleshooting

### "Failed to fetch processes"

- Ensure the core is running: `dotnet run` in `socketghost-core/`
- Check that `http://127.0.0.1:9100/processes` returns JSON

### "WebSocket connection error"

- Ensure the core WebSocket is listening on `ws://127.0.0.1:9000`
- Try toggling Demo Mode to test the UI without the core

### Tauri Build Fails

- The app is designed to work in web-only mode via Vite
- If Tauri is unavailable, just use `npm run dev` (no Tauri required)
