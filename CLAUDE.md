# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Layout

This is **not** a single project — it is a monorepo of independent Electron desktop tools for network debugging. There is **no root `package.json`**; each subdirectory is a self-contained app with its own `package.json`, dependencies, and `node_modules`. Always `cd` into the relevant subproject before running any command.

| Subproject | Stack | Purpose |
|------------|-------|---------|
| `EHttpServerTool/` | Electron 33 + React 18 + TypeScript + Vite 6 + Ant Design 5 | Mock HTTP server for debugging — define path/method responses, echo mode, request logging |
| `EWebsocketTool/` | Electron 33 + React 18 + TypeScript + Vite 6 + Ant Design 5 + `ws` | WebSocket client + server in one GUI |
| `EWebsocketMan/` | Electron 28 + Vue 3 (Composition API) + Vite 5 + `ws` | Replica of WebSocketMan v1.0.9 (WS server/client modes) |

Each subproject has its own `CLAUDE.md` with deeper detail — read that before working in it.

## Commands (run inside a subproject)

```bash
npm install
npm run dev      # Vite dev server (port 5173, strictPort) + Electron window pointing at it via VITE_DEV_SERVER_URL
npm run build    # Vite build → dist/
npm run preview  # Preview Vite build
npm run pack     # Vite build + electron-builder → Windows NSIS installer (output: release/)
```

`npm run dev` uses `concurrently` + `wait-on`: Vite starts first, then Electron launches only after `http://localhost:5173` is up, with `VITE_DEV_SERVER_URL` injected so the main process loads the dev server instead of the built `dist/index.html`. Vite `strictPort: true` matters here — the Electron launch waits on exactly 5173, so a port shift would hang.

## Architecture (shared across all three apps)

All three apps follow the same Electron three-process model and the same conventions. Understanding this model is more useful than memorizing per-app details.

### Process split

- **Main process** (`electron/main.js` or `src/main/main.js`): owns the BrowserWindow, owns all server-side networking (`http`/`ws`), and registers IPC handlers via `ipcMain.handle()`. The renderer never touches network sockets directly (except EWebsocketTool's *client* mode, which uses the browser's native `WebSocket` in the renderer).
- **Preload** (`electron/preload.js` or `src/preload/preload.js`): `contextBridge.exposeInMainWorld('electronAPI', …)` with `contextIsolation: true`, `nodeIntegration: false`. Every event listener exposed returns an **unsubscribe function**.
- **Renderer** (`src/`): React (TypeScript) or Vue 3. Talks to the main process **exclusively** through `window.electronAPI`. Never imports Node/Electron modules directly.

### IPC pattern

Two flavors, always via `window.electronAPI`:

- **Request/response**: `ipcRenderer.invoke(channel, …args)` ↔ `ipcMain.handle(channel, …)` — for actions (start/stop server, add/list configs).
- **Events**: `ipcRenderer.on(channel, handler)` from main → renderer — for async notifications (server started/stopped, new request, message received). Each exposed listener returns a cleanup function that **must** be called on component teardown (React `useEffect` return / Vue `onUnmounted`) to prevent listener accumulation across tab switches.

Main process must never assume `mainWindow` is alive — use a `sendToRenderer()` wrapper that null-checks `mainWindow && !mainWindow.isDestroyed()`.

### Module structure (main process)

The networking logic is split out of `main.js` into `electron/modules/` (EHttpServerTool) or inline (EWebsocketTool/EWebsocketMan) manager classes. `main.js` is wiring only: instantiate managers, register IPC handlers, forward events to the renderer via `sendToRenderer`. When adding server behavior, edit the manager class, not `main.js`.

### Conventions worth knowing

- **IPv4 normalization**: `getIPv4(remoteAddress)` strips `::ffff:` prefix and maps `::1` → `127.0.0.1` so Windows IPv6-mapped addresses display as plain IPv4. Present in all three apps.
- **Config persistence**: configs (paths, connection settings) are saved to `app.getPath('userData')` as JSON files (e.g. `paths.json`, `config.json`). Auto-save with debounce in EWebsocketMan.
- **Tab state**: tabs use `v-show` (Vue) / conditional rendering that preserves state so connections survive tab switches.

## Testing (EHttpServerTool only)

`EHttpServerTool/` ships three `*.test.js` files at the project root (`http-server.test.js`, `path-config.test.js`, `request-logger.test.js`). They test the `electron/modules/` manager classes with real `http`/`fs` against a temp dir and a real listening port.

These tests use Jest-style globals (`describe`/`it`/`expect`) **but no test runner is configured** — there is no `test` script and Jest is not in `package.json`/`node_modules`. They currently cannot run as-is. To run them, install a runner (e.g. `jest`) and add a `test` script first; do not assume `npm test` works.

The other two subprojects have no tests.

## Notes

- EHttpServerTool and EWebsocketTool are near-identical scaffolding (same Electron/Vite/React/AntD versions, same `dev`/`pack` scripts) — changes to shared tooling (vite config, electron-builder config, dev-launch wiring) often apply to both.
- Docs for design decisions and plans live under each subproject's `docs/superpowers/` and `.superpowers/sdd/` — useful context for past decisions, not part of the build.
