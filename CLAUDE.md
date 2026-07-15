# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Layout

This is a **monorepo of five independent Electron desktop tools** for network debugging and education. There is **no root `package.json`** — each subdirectory is a self-contained app with its own `package.json`, dependencies, and `node_modules`. Always `cd` into the relevant subproject before running any command.

| Subproject | Stack | Win7 Compat | Build Approach | Purpose |
|------------|-------|-------------|----------------|---------|
| `EHttpServerTool/` | Electron 33 + React 18 + TS + Ant Design 5 + Vite 6 | ❌ | Standalone `electron/` dir | Mock HTTP server: define path/method responses, echo mode, request logging |
| `EWebsocketTool/` | Electron 22 + React 18 + TS + Ant Design 5 + Vite 6 | ✅ | Standalone `electron/` dir | WebSocket client (browser native) + server (`ws` lib) in one GUI |
| `EWebsocketMan/` | Electron 22 + Vue 3 (Composition API) + Vite 5 | ✅ | `vite-plugin-electron` builds `src/main/` + `src/preload/` | WebSocketMan v1.0.9 replica — WS server/client modes |
| `EPinyinStudy/` | Electron 33 + Vue 3 + Vite 6 | ❌ | Standalone `electron/` dir | First-grade Chinese pinyin education: initials, finals, hanzi, quiz |
| `ERabbitMQTool/` | Electron 22 + React 18 + TS + Ant Design 5 + Vite 6 + `amqplib` | ✅ | Standalone `electron/` dir | RabbitMQ debugging: connection management, producer publish, consumer subscribe (queue/exchange modes), SSL support |

## Commands (run inside a subproject)

```bash
# Dependencies
npm install

# Development — Vite dev server + Electron window
npm run dev          # EHttpServerTool / EWebsocketTool / EWebsocketMan / ERabbitMQTool
npm run electron:dev # EPinyinStudy (uses different script name)

# Build (Vite → dist/)
npm run build

# Preview Vite build
npm run preview      # EHttpServerTool / EWebsocketTool / EWebsocketMan / ERabbitMQTool only

# Package Windows NSIS installer → release/
npm run pack         # EHttpServerTool / EWebsocketTool / EWebsocketMan / ERabbitMQTool
npm run pack         # EPinyinStudy → runs `vite build && electron-builder --win`

# Linux packaging (EPinyinStudy only)
npm run electron:linux
```

### dev script mechanism

All subprojects use `concurrently` + `wait-on`: Vite starts first (port 5173, `strictPort: true`), then Electron launches only after `http://localhost:5173` is reachable. The main process receives `VITE_DEV_SERVER_URL` env var and loads the dev server — otherwise it loads the built `dist/index.html`.

### pack command variations

- **EWebsocketTool**, **EWebsocketMan** and **ERabbitMQTool** (Win7-compatible): `pack` includes `-c.electronDist="node_modules/electron/dist" -c.electronVersion="22.3.27"` to force electron-builder to use the locally-installed Electron 22 binary rather than downloading Electron 33+.
- **EHttpServerTool** and **EPinyinStudy** (not Win7): `pack` does NOT need the electronDist override — electron-builder uses whatever Electron version is in `node_modules`.

## Architecture (shared across all five apps)

### Three-process model

All apps follow the same split, regardless of frontend framework (React vs Vue):

```
Main process (electron/main.js or src/main/main.js)
  ├── Owns BrowserWindow
  ├── Owns all networking (http / ws / WebSocket) — renderer never touches sockets
  ├── Registers ipcMain.handle() for request/response
  └── Sends async events to renderer via webContents.send()

Preload (electron/preload.js or src/preload/preload.js)
  ├── contextBridge.exposeInMainWorld('electronAPI', …)
  ├── contextIsolation: true, nodeIntegration: false
  └── Every event listener returns an unsubscribe function

Renderer (src/ — React TSX or Vue 3)
  └── Talks to main process EXCLUSIVELY through window.electronAPI
      Never imports Node/Electron modules directly
```

### Two build approaches

**Approach A — Standalone** (EHttpServerTool, EWebsocketTool, EPinyinStudy, ERabbitMQTool):
- Main/preload source lives in `electron/` directory at project root
- Vite builds ONLY the renderer (`src/` → `dist/`)
- `"main": "electron/main.js"` in package.json — Electron loads source directly
- electron-builder includes `electron/**/*` in files config

**Approach B — vite-plugin-electron** (EWebsocketMan):
- Main/preload source lives in `src/main/` and `src/preload/`
- Vite builds ALL THREE: renderer → `dist/`, main → `dist-electron/main.js`, preload → `dist-electron/preload.js`
- `"main": "dist-electron/main.js"` in package.json — Electron loads the built artifact
- vite-plugin-electron handles the extra build steps; electron-builder includes `dist-electron/**/*`

### IPC pattern

Every app uses the same two flavors, always through `window.electronAPI`:

**Request/response** — `ipcRenderer.invoke(channel, …args)` ↔ `ipcMain.handle(channel, handler)`:
- Used for actions: start/stop server, add/config, save/load, send message, file operations

**Events** — `ipcRenderer.on(channel, handler)` (main → renderer):
- Used for async notifications: server started/stopped, client connected/disconnected, message received, errors
- **Critical cleanup**: Every listener registration returns an unsubscribe function that MUST be called on component teardown (React `useEffect` return / Vue `onUnmounted`). Without this, listeners accumulate across tab switches causing duplicate handling and memory leaks.

**Safety guard**: Main process never assumes `mainWindow` is alive. Every app has a `sendToRenderer()` wrapper that null-checks `mainWindow && !mainWindow.isDestroyed()` before calling `webContents.send()`.

### Module structure (main process)

Three of five apps keep networking logic separate from the Electron wiring:

- **EHttpServerTool**: `electron/modules/` with separate files for HTTP server, path config, request logging, and file logging — `main.js` wires them together
- **EWebsocketTool** / **EWebsocketMan**: Server/client logic is inline in `main.js` since the WS state is simpler
- **ERabbitMQTool**: RabbitMQ connection/channel/consumer logic is inline in `main.js` (amqplib operations + IPC registration)
- **EPinyinStudy**: Minimal main process (no networking) — just window creation and platform detection

When adding server behavior to EHttpServerTool, edit the manager classes in `electron/modules/`, not `main.js`.

### Windows 7 compatibility

Three apps (EWebsocketTool, EWebsocketMan, ERabbitMQTool) run on Windows 7 x64 via Electron 22.3.27 — the last Electron version with Win7 support (Electron 23+ dropped it). The downgrade affects only:
- `electron` devDependency version
- `pack` script electronDist override
- Build target may need adjustment (`vite.config.ts` `build.target` for EWebsocketTool and ERabbitMQTool is `chrome108`)

### IPv4 normalization

All three WS/HTTP apps strip the IPv6-mapped prefix from `remoteAddress`:
- `::ffff:127.0.0.1` → `127.0.0.1`
- `::1` → `127.0.0.1`
- Pure IPv6 addresses are filtered/nullified

### Config persistence

Configs (server settings, connection URLs) are saved to `app.getPath('userData')` as JSON files. EWebsocketMan auto-saves with 300ms debounce. EHttpServerTool uses a PathConfigManager class. ERabbitMQTool saves connection config (host/port/vhost/credentials/SSL options) to `config.json`.

## Subproject specifics

### EHttpServerTool

The only app with tests — three `*.test.js` files at project root (`http-server.test.js`, `path-config.test.js`, `request-logger.test.js`). They use Jest globals (`describe`/`it`/`expect`) against real `http`/`fs` resources (temp dir, real port) but **no test runner is configured** — must install Jest and add a `test` script.

### EPinyinStudy

- No backend, no API calls — all pinyin/hanzi data is bundled at build time in `src/data/pinyin.js`
- Pronunciation uses Web Speech API (`SpeechSynthesisUtterance`, `lang: 'zh-CN'`), not audio files
- No Vue Router — tab navigation via `currentTab` ref and `v-if` in `App.vue`
- No state management (Vuex/Pinia) — all state is local `ref`s, data flows via props/$emit
- Has Linux packaging support (`electron:linux` script, AppImage target)
- Uses `base: './'` in Vite config for Electron `file://` path resolution

### ERabbitMQTool

RabbitMQ debugging tool built on `amqplib`. Main process manages connection/channel/consumerTags lifecycle inline in `electron/main.js` (no separate modules dir).

- **Connection**: supports `amqp`/`amqps` protocols; SSL is opt-in via `sslEnabled` + `sslValidateServerCert` (controls `rejectUnauthorized`); connection config persisted to `config.json` in `userData`
- **Producer** (`publish` IPC): publishes to a queue (asserts durable queue first) or exchange; supports `persistent`, `contentType`, `priority`, `headers`, `messageId`, `replyTo` properties
- **Consumer** (`subscribe` IPC): two modes —
  - Queue mode: `assertQueue` + `consume` with `noAck: true`; warns if existing consumers detected (round-robin risk)
  - Exchange mode: creates exclusive temporary queue, binds to exchange, then consumes
- **Cleanup**: `cleanUp()` cancels all consumer tags, closes channel and connection; called on disconnect, connection error/close, and app quit
- **Events sent to renderer**: `connected`, `disconnected`, `connection-error`, `message-received`, `publish-confirmed`, `log-event`
- Renderer components: `ConnectionPanel`, `ProducerTab`, `ConsumerTab`, `LogPanel` (React + Ant Design)