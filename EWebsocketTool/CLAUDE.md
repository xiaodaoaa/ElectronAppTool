# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A WebSocket debugging tool (WebSocket调试工具) built as an Electron app with React + TypeScript frontend. It provides both a **WebSocket client** (connect to a remote WS server) and a **WebSocket server** (accept client connections, send/broadcast messages) in one GUI.

## Architecture

### Process Split (Electron)

- **Main process** (`electron/main.js`): Manages the BrowserWindow, owns the WebSocketServer (`ws` library), and handles all WS server-side logic. Uses `ipcMain.handle()` to expose RPC-style handlers.
- **Preload** (`electron/preload.js`): Exposes a typed `electronAPI` object to the renderer via `contextBridge.exposeInMainWorld`. Each IPC listener returns a cleanup function (unsubscribe).
- **Renderer** (`src/`): React app built with Vite, communicates with the main process exclusively through `window.electronAPI`.

### IPC Channel Map

| Channel | Direction | Purpose |
|---------|-----------|---------|
| `start-server` | renderer → main | Start WS server on given port |
| `stop-server` | renderer → main | Stop WS server |
| `send-to-clients` | renderer → main | Send to selected client IDs |
| `broadcast` | renderer → main | Send to all clients |
| `server-started` | main → renderer | Server is listening |
| `server-stopped` | main → renderer | Server stopped |
| `server-error` | main → renderer | Error occurred |
| `client-connected` | main → renderer | New client connected |
| `client-disconnected` | main → renderer | Client disconnected |
| `message-received` | main → renderer | Message from a client |

### Source Structure

```
src/
├── main.tsx                  # React entry point
├── App.tsx                   # Root component with ConfigProvider + Tabs (Client | Server)
├── App.css                   # Layout styles (flexbox, log panel, colors)
├── types/index.ts            # TypeScript interfaces: LogEntry, ConnectedClient, ElectronAPI
├── hooks/useWebSocket.ts     # Hook wrapping native WebSocket for client mode
└── components/
    ├── ClientTab.tsx         # WebSocket client UI: connect/disconnect, send messages
    ├── ServerTab.tsx         # WebSocket server UI: start/stop, client list, send/broadcast
    └── LogPanel.tsx          # Shared log display with auto-scroll
```

### Key Data Types

- **LogEntry**: `{ id, timestamp, type: 'send'|'receive'|'info'|'error', content }` — rendered by `LogPanel`
- **ConnectedClient**: `{ clientId, ip, connectTime }` — shown in `ServerTab` client table
- **ElectronAPI**: Full interface of IPC methods exposed from preload, available as `window.electronAPI`

### Key Patterns

- **Client mode** (`useWebSocket` hook): Uses the browser's native `WebSocket` API directly in the renderer. No IPC needed.
- **Server mode** (`ServerTab`): All WS operations happen in the main process. The renderer triggers actions via `ipcRenderer.invoke()` and receives events via `ipcRenderer.on()` listeners.
- **Log collection**: Each tab owns its `logs` state array. New entries are appended via an `addLog` callback. `LogPanel` auto-scrolls if the user is already at the bottom.
- **IPv4 extraction**: `getIPv4()` in `electron/main.js` normalizes `remoteAddress` — strips `::ffff:` prefix, maps `::1` to `127.0.0.1`, filters pure IPv6 to `null`.
- **Cleanup**: IPC listeners return unsubscribe functions called on effect teardown. WS server disconnects all clients and clears the `clients` Map on stop/unexpected error.

## Commands

```bash
# Development (Vite dev server + Electron window)
npm run dev

# Build renderer only (Vite output to dist/)
npm run build

# Preview Vite build
npm run preview

# Package for Windows (NSIS installer)
npm run pack
```
