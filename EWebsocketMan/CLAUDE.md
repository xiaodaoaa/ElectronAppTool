# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

EWebsocketMan — Electron desktop app replicating WebSocketMan v1.0.9, a WebSocket testing tool with server and client modes.

**Tech stack:** Electron 28 + Vue 3 (Composition API) + Vite 5 + ws (WebSocket library)

## Commands

```bash
npm run dev      # Start Vite dev server + Electron (HMR enabled)
npm run build    # Build for production + electron-builder packaging
npm run preview  # Preview production build
```

## Architecture

### Three-process model

```
Main Process (src/main/main.js)
  ├── WebSocket Server (ws library, manages connectedClients Map)
  ├── WebSocket Client (ws library, single connection)
  ├── IPC handlers (ipcMain.handle)
  └── File operations (config persistence, message logging)

Preload (src/preload/preload.js)
  └── contextBridge exposes electronAPI to renderer

Renderer (src/renderer/)
  ├── App.vue — Tab bar (server/client/about), uses v-show to preserve state
  ├── ServerTab.vue — Server UI
  ├── ClientTab.vue — Client UI
  └── style.css — Pure CSS, no framework
```

### IPC pattern

All communication between renderer and main process goes through `window.electronAPI`:

**Request/Response** (invoke/handle):
- `startServer({ url, options })` / `stopServer()`
- `connectClient({ url, headers })` / `disconnectClient()`
- `sendServerMessage({ clientAddr, message, isHex })`
- `sendClientMessage({ message, isHex })`
- `saveConfig(config)` / `loadConfig()`
- `appendToFile({ filePath, content })`
- `selectSaveFile()` — opens file save dialog

**Event listeners** (on/send): Each returns an unsubscribe function. Components MUST call these in `onUnmounted` to prevent listener leaks.

```js
// Pattern in Vue components:
const unsubscribers = []
function subscribe(channel, cb) {
  const unsub = window.electronAPI[channel](cb)
  unsubscribers.push(unsub)
}
onMounted(() => { subscribe('onServerStarted', handler) })
onUnmounted(() => { unsubscribers.forEach(fn => fn()) })
```

### Key design decisions

1. **Tab state preservation**: Uses `v-show` (not `v-if`) in App.vue so components survive tab switches.

2. **Server options at module level**: `serverOptions` is a module-level variable, not captured in closure, so runtime option changes (echo/sendall) take effect immediately.

3. **Client connection race guard**: `isConnecting` flag prevents double-connect when user rapidly clicks connect/disconnect.

4. **IPv6 address stripping**: Windows returns `::ffff:127.0.0.1`, stripped to `127.0.0.1` for display.

5. **Config persistence**: Saved to `app.getPath('userData')/config.json` (Windows: `%APPDATA%/EWebsocketMan/config.json`). Auto-saved on any config change with 300ms debounce.

6. **Message file logging**: When "保存到文件" is enabled, messages are appended to the specified file via `fs.appendFileSync`.

### Critical gotchas

- **Every function used in template must be in `return` statement** — Vue 3 Composition API requires explicit exposure. Forgetting this causes silent failures (click handlers do nothing, checkboxes don't bind).
- **IPC event listeners accumulate** — without cleanup in `onUnmounted`, each tab switch doubles the listeners.
- **`mainWindow` can be null** — always use `sendToRenderer()` wrapper, never call `mainWindow.webContents.send()` directly.
- **`require('electron')` in main process** — used for `dialog` module in ESM context.
