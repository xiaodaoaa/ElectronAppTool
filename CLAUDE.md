# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Layout

This is a **monorepo of ten independent desktop tools** — seven Electron apps, two .NET/WPF apps, one C++ Notepad++ plugin. There is **no root `package.json`** — each subdirectory is self-contained with its own dependencies and build tooling. Always `cd` into the relevant subproject before running any command.

| Subproject | Stack | Win7 Compat | Build Approach | Purpose |
|------------|-------|-------------|----------------|---------|
| `EHttpServerTool/` | Electron 33 + React 18 + TS + Ant Design 5 + Vite 6 | ❌ | Standalone `electron/` dir | Mock HTTP server: define path/method responses, echo mode, request logging |
| `EWebsocketTool/` | Electron 22 + React 18 + TS + Ant Design 5 + Vite 6 | ✅ | Standalone `electron/` dir | WebSocket client (browser native) + server (`ws` lib) in one GUI |
| `EWebsocketMan/` | Electron 22 + Vue 3 (Composition API) + Vite 5 | ✅ | `vite-plugin-electron` builds `src/main/` + `src/preload/` | WebSocketMan v1.0.9 replica — WS server/client modes |
| `ERabbitMQTool/` | Electron 22 + React 18 + TS + Ant Design 5 + Vite 6 + `amqplib` | ✅ | Standalone `electron/` dir | RabbitMQ debugging: connection management, producer publish, consumer subscribe (queue/exchange modes), SSL support |
| `ERabbitMQToolPlus/` | Electron 43 + Vue 3 + Element Plus + Pinia + electron-vite + `amqplib` | ❌ | `electron-vite` builds `src/main/` + `src/preload/` + `src/renderer/` → `out/` | RabbitMQ debugging enhanced: singleton services, encrypted config persistence, two-stage typecheck |
| `EActiveMQTool/` | Electron 43 + Vue 3 + Element Plus + Pinia + electron-vite + `@stomp/stompjs` | ❌ | `electron-vite` builds `src/main/` + `src/preload/` + `src/renderer/` → `out/` | ActiveMQ debugging: STOMP over TCP/WebSocket, producer/consumer, JMS selector, TCP socket adapter |
| `EKafkaTool/` | Electron 33 + Vue 3 + Element Plus + Pinia + electron-vite + `kafkajs` | ❌ | `electron-vite` builds `src/main/` + `src/preload/` + `src/renderer/` → `out/` | Kafka teaching tool: connection/topic management, producer/consumer, 6 demo scenarios, message replay |
| `CSNtpd/` | .NET 10 + WPF + xUnit | ❌ | `dotnet` CLI, three-layer solution `NtpTool.slnx` | NTP time sync tool: NTP client (upstream sync) + NTP server (LAN time service), system tray, system time correction |
| `CSSHTunnelProxy/` | .NET 10 + WPF + SSH.NET + xUnit | ❌ | `dotnet` CLI, three-layer solution `SSHTunnelProxy.slnx` | SSH tunnel proxy: SOCKS5/HTTP local proxy forwarded via SSH direct-tcpip, multi-tunnel, auto-reconnect, DPAPI encryption |
| `NStringTool/` | C++17 + CMake + Notepad++ Plugin SDK + Scintilla API | — | CMake + Visual Studio (2015 Win64 / 2022 x64) → `NStringTool.dll` | Notepad++ string escape/unescape plugin (C++/JSON/HTML/XML/URL) |

## Commands (run inside a subproject)

```bash
# Dependencies
npm install          # Electron projects
dotnet restore       # CSNtpd
# CSSHTunnelProxy: restore happens automatically on build/run (slnx)
# NStringTool: no install step, needs VS + CMake on PATH

# Development — Vite dev server + Electron window
npm run dev          # EHttpServerTool / EWebsocketTool / EWebsocketMan / ERabbitMQTool

# Development — electron-vite three-process HMR
npm run dev          # ERabbitMQToolPlus / EActiveMQTool / EKafkaTool (run inside that dir)

# Development — .NET
dotnet run --project src/NtpTool.App           # CSNtpd
dotnet run --project src/SSHTunnelProxy.App    # CSSHTunnelProxy

# Development — C++ plugin (generates DLL, load into Notepad++ to test)
cmake -B build64 -G "Visual Studio 14 2015 Win64"   # NStringTool, VS2015
cmake -B build -G "Visual Studio 17 2022" -A x64    # NStringTool, VS2022
cmake --build build64 --config Release              # → build64/bin/Release/NStringTool.dll

# Build (Vite → dist/)
npm run build        # all Electron apps

# Build (electron-vite → out/)
npm run build        # ERabbitMQToolPlus / EActiveMQTool / EKafkaTool

# Build (.NET solution)
dotnet build NtpTool.slnx           # CSNtpd (App/Core/Infrastructure/Tests)
dotnet build SSHTunnelProxy.slnx    # CSSHTunnelProxy (App/Core/Tests)

# Preview build
npm run preview      # all Electron apps

# Package Windows NSIS installer → release/
npm run pack         # EHttpServerTool / EWebsocketTool / EWebsocketMan / ERabbitMQTool / ERabbitMQToolPlus / EActiveMQTool
npm run package:win  # EKafkaTool (different script name)

# Package .NET green single-file (self-contained, no runtime needed on target)
dotnet publish src/NtpTool.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true  # CSNtpd
dotnet publish src/SSHTunnelProxy.App/SSHTunnelProxy.App.csproj -c Release -p:Portable=true  # CSSHTunnelProxy (portable, self-contained)

# Typecheck (ERabbitMQToolPlus / EActiveMQTool / EKafkaTool)
npm run typecheck    # ERabbitMQToolPlus/EActiveMQTool: tsc(node) + vue-tsc(web) two-stage; EKafkaTool: vue-tsc single-stage — MUST run after editing

# Lint (EKafkaTool only)
npm run lint         # eslint .ts/.vue

# Tests
dotnet test NtpTool.slnx                          # CSNtpd: xUnit (Core.Tests + Infrastructure.Tests)
dotnet test SSHTunnelProxy.slnx                   # CSSHTunnelProxy: xUnit Unit + Integration (Moq + FluentAssertions)
cmake --build build64 --target ConverterTests --config Debug && ./build64/Debug/ConverterTests.exe  # NStringTool: converter unit tests
# EHttpServerTool: three *.test.js files at project root use Jest globals but NO test runner is configured — must install Jest and add a `test` script first
```

### dev script mechanism

**EHttpServerTool / EWebsocketTool / EWebsocketMan / ERabbitMQTool** use `concurrently` + `wait-on`: Vite starts first (port 5173, `strictPort: true`), then Electron launches only after `http://localhost:5173` is reachable. The main process receives `VITE_DEV_SERVER_URL` env var and loads the dev server — otherwise it loads the built `dist/index.html`.

**ERabbitMQToolPlus** uses `electron-vite dev` which handles all three processes (main/preload/renderer) with HMR in one command. **EActiveMQTool** and **EKafkaTool** use the same approach.

### pack command variations

- **EWebsocketTool**, **EWebsocketMan** and **ERabbitMQTool** (Win7-compatible): `pack` includes `-c.electronDist="node_modules/electron/dist" -c.electronVersion="22.3.27"` to force electron-builder to use the locally-installed Electron 22 binary rather than downloading Electron 33+.
- **EHttpServerTool** (not Win7): `pack` includes `-c.electronDist="node_modules/electron/dist" -c.electronVersion="33.4.11"`.
- **ERabbitMQToolPlus** (not Win7): `pack` runs `electron-vite build && electron-builder --win` — no electronDist override needed (Electron 43).
- **EActiveMQTool** (not Win7): same as ERabbitMQToolPlus — `pack` runs `electron-vite build && electron-builder --win`.
- **EKafkaTool** (not Win7): `package:win` runs `electron-vite build && electron-builder --win` — no electronDist override needed (Electron 33). Note the script name is `package:win`, not `pack`.

## Architecture (shared across the seven Electron apps)

### Three-process model

All Electron apps follow the same split, regardless of frontend framework (React vs Vue):

```
Main process (electron/main.js or src/main/main.js or src/main/index.ts)
  ├── Owns BrowserWindow
  ├── Owns all networking (http / ws / WebSocket / Kafka / STOMP) — renderer never touches sockets
  ├── Registers ipcMain.handle() for request/response
  └── Sends async events to renderer via webContents.send()

Preload (electron/preload.js or src/preload/preload.js or src/preload/index.ts)
  ├── contextBridge.exposeInMainWorld('electronAPI'/'api'/'kafkaApi', …)
  ├── contextIsolation: true, nodeIntegration: false
  └── Every event listener returns an unsubscribe function

Renderer (src/ — React TSX or Vue 3)
  └── Talks to main process EXCLUSIVELY through window.electronAPI (first four apps),
      window.api (ERabbitMQToolPlus/EActiveMQTool), or window.kafkaApi (EKafkaTool)
      Never imports Node/Electron modules directly
```

### Three build approaches

**Approach A — Standalone** (EHttpServerTool, EWebsocketTool, ERabbitMQTool):
- Main/preload source lives in `electron/` directory at project root
- Vite builds ONLY the renderer (`src/` → `dist/`)
- `"main": "electron/main.js"` in package.json — Electron loads source directly
- electron-builder includes `electron/**/*` in files config

**Approach B — vite-plugin-electron** (EWebsocketMan):
- Main/preload source lives in `src/main/` and `src/preload/`
- Vite builds ALL THREE: renderer → `dist/`, main → `dist-electron/main.js`, preload → `dist-electron/preload.js`
- `"main": "dist-electron/main.js"` in package.json — Electron loads the built artifact
- vite-plugin-electron handles the extra build steps; electron-builder includes `dist-electron/**/*`

**Approach C — electron-vite** (ERabbitMQToolPlus, EActiveMQTool, EKafkaTool):
- Main/preload/renderer source lives in `src/main/`, `src/preload/`, `src/renderer/`
- electron-vite builds ALL THREE: main → `out/main/`, preload → `out/preload/`, renderer → `out/renderer/`
- `"main": "./out/main/index.js"` in package.json (EKafkaTool uses `./out/main/index.mjs`) — Electron loads built artifacts
- electron-builder includes `out/**/*` in files config
- Shared types: ERabbitMQToolPlus/EActiveMQTool in `src/shared/`, referenced by all three processes via `@shared/*` alias; EKafkaTool in `src/main/kafka/types.ts`, referenced via relative paths (no `src/shared/` dir)
- **sandbox: false** — preload needs to require non-Electron modules (shared/types, kafkajs types); not available in sandbox mode

### IPC pattern

Every app uses the same two flavors, through `window.electronAPI` (first four apps), `window.api` (ERabbitMQToolPlus/EActiveMQTool), or `window.kafkaApi` (EKafkaTool):

**Request/response** — `ipcRenderer.invoke(channel, …args)` ↔ `ipcMain.handle(channel, handler)`:
- Used for actions: start/stop server, add/config, save/load, send message, file operations

**Events** — `ipcRenderer.on(channel, handler)` (main → renderer):
- Used for async notifications: server started/stopped, client connected/disconnected, message received, errors
- **Critical cleanup**: Every listener registration returns an unsubscribe function that MUST be called on component teardown (React `useEffect` return / Vue `onUnmounted`). Without this, listeners accumulate across tab switches causing duplicate handling and memory leaks.

**Safety guard**: Main process never assumes `mainWindow` is alive. Every app has a `sendToRenderer()` wrapper that null-checks `mainWindow && !mainWindow.isDestroyed()` before calling `webContents.send()`.

### Module structure (main process)

- **EHttpServerTool**: `electron/modules/` with separate files for HTTP server, path config, request logging, and file logging — `main.js` wires them together. When adding server behavior, edit the manager classes in `electron/modules/`, not `main.js`.
- **EWebsocketTool** / **EWebsocketMan**: Server/client logic is inline in `main.js` since the WS state is simpler
- **ERabbitMQTool**: RabbitMQ connection/channel/consumer logic is inline in `electron/main.js` (amqplib operations + IPC registration)
- **ERabbitMQToolPlus**: Business logic in `src/main/services/` as singletons (`ConnectionManager`, `ProducerService`, `ConsumerService`); `src/main/ipc/` only forwards args; `src/main/utils/` has ssl/store/logger

### Windows 7 compatibility

Three apps (EWebsocketTool, EWebsocketMan, ERabbitMQTool) run on Windows 7 x64 via Electron 22.3.27 — the last Electron version with Win7 support (Electron 23+ dropped it). The downgrade affects only:
- `electron` devDependency version
- `pack` script electronDist override
- Build target may need adjustment (`vite.config.ts` `build.target` for EWebsocketTool is `chrome108`)

ERabbitMQToolPlus uses Electron 43 and does NOT support Win7. EActiveMQTool also uses Electron 43 and does NOT support Win7. EKafkaTool uses Electron 33 and does NOT support Win7.

### IPv4 normalization

All three WS/HTTP apps strip the IPv6-mapped prefix from `remoteAddress`:
- `::ffff:127.0.0.1` → `127.0.0.1`
- `::1` → `127.0.0.1`
- Pure IPv6 addresses are filtered/nullified

### Config persistence

Configs (server settings, connection URLs) are saved to `app.getPath('userData')` as JSON files. EWebsocketMan auto-saves with 300ms debounce. EHttpServerTool uses a PathConfigManager class. ERabbitMQTool saves connection config (host/port/vhost/credentials/SSL options) to `config.json` in `userData`. ERabbitMQToolPlus/EActiveMQTool use `electron-store` (`src/main/utils/store.ts`) with AES-256-CBC encrypted passwords; configs saved on successful operations (connect/send/start), renderer pulls via `loadProducer`/`loadConsumer` on mount (not push, due to timing issues). EKafkaTool uses a plain JSON file (`connections.json`) with `safeStorage`-encrypted passwords (base64 fallback when `safeStorage.isEncryptionAvailable()` is false) — **does NOT use electron-store**; connection configs saved on save/delete, renderer pulls via `loadConfigs` on mount.

## Subproject specifics

### EHttpServerTool

The only Electron app with tests — three `*.test.js` files at project root (`http-server.test.js`, `path-config.test.js`, `request-logger.test.js`). They use Jest globals (`describe`/`it`/`expect`) against real `http`/`fs` resources (temp dir, real port) but **no test runner is configured** — must install Jest and add a `test` script.

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

### ERabbitMQToolPlus

Enhanced RabbitMQ debugging tool built on `amqplib` with electron-vite three-process architecture. See `ERabbitMQToolPlus/AGENTS.md` for detailed agent guidance. Key points:

- **Singleton services** in `src/main/services/`: `ConnectionManager` (amqp/amqps connections, SSL optional, auto-cleanup consumers on disconnect), `ProducerService` (publish to queue/exchange), `ConsumerService` (queue consume + exchange subscribe with configurable binding key for direct/topic)
- **IPC**: `src/main/ipc/` only forwards args to services; channel names must match exactly between preload `invoke('xxx:yyy')` and main `ipcMain.handle('xxx:yyy')`
- **`window.api`** (not `window.electronAPI`): preload exports `api` const + `contextBridge.exposeInMainWorld('api', api)`; renderer types derived via `import type { api } from '../../preload'` in `src/renderer/src/env.d.ts`
- **amqplib `noAck` semantics**: `noAck: true` = server auto-ack. In ConsumerService, `noAck` value equals `config.autoAck` (NOT `!autoAck`)
- **Config persistence**: `electron-store` with AES-256-CBC encrypted passwords; saved on successful operations, renderer pulls on mount
- **Path aliases**: `@shared/*` → `src/shared/*` (all three processes), `@renderer/*` → `src/renderer/src/*` (renderer only)
- **Renderer**: Vue 3 + Element Plus + Pinia; views (`ProducerView`/`ConsumerView`/`SettingsView`), components (`ConnectionForm`/`LogPanel`/`MessageDetail`/`MessageTable`), stores (connection/producer/consumer/log/settings)
- **Gotcha**: Electron binary may not be downloaded (`node_modules/electron/dist/electron.exe` missing) — run `node node_modules/electron/install.js` manually

### EActiveMQTool

ActiveMQ debugging tool built on `@stomp/stompjs` with electron-vite three-process architecture. Mirrors ERabbitMQToolPlus design. Key points:

- **STOMP connection**: `ConnectionManager` singleton supports **TCP native** (via `net.Socket` adapter in `src/main/utils/tcp-socket.ts`) and **WebSocket** (ws/wss). TCP adapter wraps `net.Socket` to match `IStompSocket` interface.
- **Connection form**: has a TCP toggle; when enabled, SSL option is hidden and port defaults to 61613 (STOMP TCP port). When disabled, uses WebSocket with port 61614.
- **Producer**: `ProducerService` publishes to `/queue/xxx` or `/topic/xxx` destinations; supports `persistent`, `priority`, `expires` STOMP headers; batch sending with progress
- **Consumer**: `ConsumerService` subscribes with auto/client/client-individual ACK modes; supports JMS `selector` header; `prefetchCount` for client-individual mode; message filtering (routingKey wildcard + header key-value match)
- **ACK via `messageId`**: `client.ack(messageId, subscriptionId)` requires two args — `messageId` (string) and `subscriptionId` (from `StompSubscription.id`). NOT `deliveryTag` like RabbitMQ.
- **Config persistence**: `electron-store` with AES-256-CBC encrypted passwords; same pattern as ERabbitMQToolPlus
- **Renderer**: Vue 3 + Element Plus + Pinia; same component structure as ERabbitMQToolPlus (ConnectionForm/LogPanel/MessageTable/MessageDetail, ProducerView/ConsumerView/SettingsView)

### EKafkaTool

Kafka teaching/demonstration tool built on `kafkajs` with electron-vite three-process architecture. Designed for learning Kafka concepts via 6 guided demo scenarios. See `EKafkaTool/AGENTS.md` for detailed agent guidance. Key points:

- **Connection**: `KafkaClientManager` singleton (`src/main/kafka/KafkaClientManager.ts`) manages the `Kafka` + `Admin` instances; supports SASL (plain/scram-sha-256/scram-sha-512) and SSL; `testConnection` spins up a throwaway admin; `setStatusCallback` pushes status changes via `event:connStatus`
- **Topic admin**: listTopics/topicDetail/createTopic/deleteTopic via the `Admin` instance; topicDetail fetches partition metadata + earliest/latest offsets
- **Producer**: `ProducerService` (`src/main/kafka/ProducerService.ts`) — single producer lazily connected; `send` for one-shot, `sendBatch` for periodic sends with `AbortController` cancellation; message value template supports `{{seq}}`/`{{ts}}`/`{{rand}}` placeholders; key strategies: fixed/random/round-robin; acks pushed via `event:produceAck`
- **Consumer**: `ConsumerService` (`src/main/kafka/ConsumerService.ts`) — multi-instance `Map<id, InstanceEntry>`; each instance has its own `MessageBuffer`; supports pause/resume/seek/commit; `GROUP_JOIN` event pushes rebalance info via `event:rebalance`; state changes pushed via `event:consumerState`
- **MessageBuffer** (`src/main/util/messageBuffer.ts`): batches consumer messages, flushes on `maxSize=100` or `intervalMs=200ms`, pushes via `event:consumerMessage` — avoids per-message IPC overhead
- **Demo scenarios**: `DemoScenarioService` (`src/main/kafka/DemoScenarioService.ts`) + `src/main/data/scenarios.ts` defines 6 scenarios (S1-S6): first message, partitions & key, consumer group load balancing, rebalance, message replay, ordering & acks; `produce` with `count=0` means continuous send (999999 msgs); progress pushed via `event:scenarioStep`
- **`window.kafkaApi`** (not `window.api`): preload does `contextBridge.exposeInMainWorld('kafkaApi', api)` + `export type KafkaApi = typeof api`; renderer types are hand-written in `src/renderer/api/kafkaApi.ts` (`KafkaApi` interface + `declare global { interface Window { kafkaApi } }`) — **must be kept in sync manually** with preload
- **IPC channels**: centralized in `src/main/ipc/channels.ts` (`IPC_CHANNELS` const); `src/main/ipc/registerHandlers.ts` registers all handlers via `wrapHandler` (error logging + rethrow); `pushToRenderer` wrapper null-checks `win && !win.isDestroyed()`
- **Config persistence**: plain JSON file (`connections.json` in `app.getPath('userData')`) with `safeStorage`-encrypted passwords (base64 fallback) — **NOT electron-store**; saved on save/delete, renderer pulls via `loadConfigs` on mount
- **Path alias**: `@/*` → `src/renderer/*` (renderer only); main/preload use relative paths; **no `@shared/*` alias** (shared types in `src/main/kafka/types.ts`)
- **typecheck**: single-stage `vue-tsc --noEmit` (web side only) — differs from ERabbitMQToolPlus/EActiveMQTool's two-stage `tsc + vue-tsc`
- **package script**: `npm run package:win` (not `pack`) for Windows NSIS installer
- **`package.json` `main`**: `./out/main/index.mjs` (note `.mjs` extension, because `"type": "module"`); preload path is `../preload/index.mjs`
- **Gotcha**: `ProducerService.send` reads offset from `result[0].baseOffset` — kafkajs types don't export this, so it's cast via `as Record<string, unknown>`
- **Local Kafka**: `docker/docker-compose.yml` runs single-node Kafka 3.9.0 (KRaft mode, port 9092); connect with brokers `localhost:9092`

### CSNtpd

NTP time sync tool built on .NET 10 + WPF, independent of the Electron architecture. Three-layer + dependency injection:

- **NtpTool.App** (WPF startup layer): MVVM (`MainViewModel`/`SettingsViewModel` + `ObservableObject`/`RelayCommand` base classes), `CompositionRoot` configures `Microsoft.Extensions.DependencyInjection`, `Services/` has `SettingsService` + `TrayIcon` (system tray via `NotifyIcon`). References Core + Infrastructure.
- **NtpTool.Core** (core business layer, no UI dependency): `Ntp/` has `NtpPacket` (RFC 4330 packet), `NtpPacketCodec` (encode/decode), `NtpQueryClient` (UDP query), `TimeCalculator` (offset/delay calc); `Services/` has interfaces + implementations (`NtpClientService`/`NtpServerService`/`SyncScheduler`/`NetworkAccessController`/`ConfigValidator`); `Models/` has `AppSettings`/`NtpClientOptions`/`NtpServerOptions`/`NtpSyncResult`; `Logging/` has `IAppLogger`/`LogLevel` abstraction.
- **NtpTool.Infrastructure** (infrastructure layer): `Config/` has `JsonConfigurationRepository` (JSON config read/write, implements Core's `IConfigurationRepository`); `Logging/` has `FileLogger` (file logging + rolling + retention, implements Core's `IAppLogger`); `Windows/` has `WindowsSystemTimeService` (calls Win32 `SetSystemTime` API, implements Core's `ISystemTimeService`).
- **tests/**: xUnit — `NtpTool.Core.Tests` (NtpPacketCodec/TimeCalculator/ConfigValidator/NetworkAccessController/SyncScheduler/NtpServerIntegrationTests), `NtpTool.Infrastructure.Tests` (FileLogger/JsonConfigurationRepository).

Key design:
- **Client + server in one**: syncs time from upstream NTP servers (UDP 123) AND serves as NTP server for LAN
- **System time correction**: via `WindowsSystemTimeService` calling Win32 `SetSystemTime` API, requires admin; large offset (over `maxAllowedOffsetMs`) won't auto-modify, needs manual confirmation
- **Single instance**: mutex lock ensures only one instance runs; repeat launch activates existing window
- **System tray**: closing main window minimizes to tray; tray menu has sync/start-stop server/settings/exit
- **Config**: `ntp-tool-config.json` (program directory), graphical settings UI (Windows 11 style, client/server/log pages)
- **Rate limit & whitelist**: server supports per-IP rate limit, CIDR whitelist, request logging
- **`.gitignore`**: excludes `bin/`, `obj/`, `logs/`, `*.log`, local `ntp-tool-config.json`

### CSSHTunnelProxy

SSH tunnel proxy tool built on .NET 10 + WPF + SSH.NET, independent of the Electron architecture. Three-layer + dependency injection:

- **SSHTunnelProxy.Core** (`net10.0`, no UI dependency): `Models/` (SshServerProfile/AppSettings/ProxyType/AuthMethod/TunnelState/ConnectionLog), `Services/` (TunnelManager multi-tunnel lifecycle, ConfigService, LogService SQLite), `Proxy/` (Socks5ProxyServer/HttpProxyServer protocol parsing + local listen, Socks5Protocol/HttpParser/ProxyCredentialValidator), `Tunnel/` (SshTunnelTransport SSH session + reconnect monitor, SshDirectTcpipChannel, StreamRelay, TrafficCounter), `Security/` (DpapiProtector DPAPI, HostKeyVerifier TOFU), `Utils/` (AccentColorProvider, StreamRelay).
- **SSHTunnelProxy.App** (`net10.0-windows`, WPF): `Views/` (MainWindow/ConfigDialog), `ViewModels/` (Main/TunnelItem/Config/Log/Settings — CommunityToolkit.Mvvm source generators), `Framework/` (TrayIconController/SerilogLoggerProvider/DispatcherUI/StartupRegistrar/RangeObservableCollection), `Resources/` (Win11 Fluent control styles). References Core.
- **SSHTunnelProxy.Tests**: xUnit + Moq + FluentAssertions — `Unit/` (Socks5Protocol/HttpParser/DpapiProtector/TrafficCounter/StreamRelay/ConfigService/AccentColorProvider), `Integration/` (Socks5EndToEnd/HttpProxyEndToEnd via FakeSshTunnelTransport), `Helpers/` (FakeSshTunnelTransport/LocalFixture/CollectingSink).

Key design:
- **Custom protocol parsing (not ForwardedPortDynamic)**: builds SOCKS5/HTTP protocol layer + direct-tcpip Channel forwarding so both protocols share one `SshTunnelTransport`, enabling per-connection traffic/target stats and future rule-based routing + local auth
- **direct-tcpip workaround**: SSH.NET 2026 made low-level Channel API internal, so `SshDirectTcpipChannel` uses public `ForwardedPortLocal` (bind temp local port `boundPort=0`, connect via `TcpClient` for bidirectional `Stream`); each proxy connection = one temp `ForwardedPortLocal`, stopped + removed after use
- **Reconnect**: `SshTunnelTransport.MonitorLoopAsync` checks `client.IsConnected` every 2s, fires `ConnectionLost`; `TunnelManager.ReconnectLoopAsync` uses exponential backoff 5→10→20→40→60 (cap, `MaxReconnectAttempts=-1` = infinite)
- **Portable data**: all runtime data in program directory (`AppPaths.Root = AppContext.BaseDirectory`, NOT `%APPDATA%`) — `profiles.json`/`settings.json`/`logs.db` (SQLite)/`known_hosts.json`/`logs/app-.log`
- **Sensitive data**: `DpapiProtector` (`ProtectedData`, CurrentUser scope + additional entropy) encrypts passwords, private key passphrases, proxy passwords, embedded private key content; host keys via TOFU (`HostKeyVerifier`)
- **HTTP proxy CONNECT only**: first phase supports CONNECT tunnel mode (covers HTTPS), plain GET/POST forwarding out of scope
- **UI**: Win11 Fluent, light single theme; accent color read at runtime and injected via `AccentColorProvider`; sidebar nav (tunnels/logs/settings)

Gotchas (from `CSSHTunnelProxy/CLAUDE.md`):
- **Restart must not call `_manager.RestartTunnelAsync` directly**: it creates a new transport but doesn't wire its `StateChanged` to the ViewModel → status dot/text won't update after reconnect. Use the stop-old-context → `StartTunnelAsync` → `AttachEvents` flow. First successful connect also needs manual `State = Connected` (transport's `StateChanged(Connected)` fires inside `ConnectAsync` before `AttachEvents` subscribes → event lost).
- **Tray menu built once at construction**: tunnel items must be rebuilt in the menu `Opened` event via `RebuildTunnelItems`, else stuck at stale startup list. Menu structure fixed: `[Show main window, (tunnel items...), Separator, Exit]`.
- **Settings take effect immediately**: `MainWindow` re-reads `settings.json` on `Closing`/`StateChanged` (CloseToTray/MinimizeToTray), no cache.
- **`MainWindow.IsQuitting` static flag**: distinguishes real exit from minimize-to-tray. Tray "Exit" must set `IsQuitting = true` before `Application.Shutdown()`, else close-intercept turns exit into hide.
- **UI thread marshalling**: non-UI-thread callbacks (e.g. transport `StateChanged`) must go through `DispatcherUI.Run` before touching bound properties.
- **`SshServerProfile.Id` immutable**: `TunnelManager` dictionary key depends on it; `TunnelItemViewModel.ApplyProfile` copies field-by-field but preserves `Id`.
- **`.gitignore`**: excludes `bin/`, `obj/`, `Debug/`, `Release/`, `*.log`, `logs/`, `*.user`, `.vs/`, `.idea/`, `*.nupkg`

### NStringTool

Notepad++ string escape/unescape plugin built on C++17 + CMake, independent of the Electron/.NET architecture.

- **Plugin entry**: `src/PluginEntry.cpp` — DLL exports + menu registration (8 operations under "Plugins → NStringTool" menu)
- **Editor ops**: `src/EditorOps.cpp/.h` — Scintilla API operations (read/write selection or whole document, single Ctrl+Z undo per conversion)
- **Converters**: `src/converters/` — four format converters, each a pair of `.cpp/.h`:
  - `CppConverter` — C/C++ escape/unescape (`\n \t \" \\ \xNN \uNNNN`)
  - `JsonConverter` — JSON escape/unescape (`\" \\ \n \t \uXXXX`, RFC 8259)
  - `HtmlConverter` — HTML/XML entity escape/unescape (`&lt; &gt; &amp; &#60; &#x3C;`)
  - `UrlConverter` — URL encode/decode (`%20 %2F`, RFC 3986)
  - `Converters.h` — converter interface
- **SDK**: `sdk/` — Notepad++ Plugin SDK headers (`PluginInterface.h`, `Notepad_plus_msgs.h`, `Scintilla.h`, `Sci_Position.h`)
- **Tests**: `tests/` — converter core unit tests (`test_main.cpp`, `test_converters.cpp`, `test_macros.h`), built as `ConverterTests` target
- **Build**: CMake + Visual Studio. VS 2015 default generator is Win32 — MUST use `Win64` variant (`-G "Visual Studio 14 2015 Win64"`) or `-A x64` for VS 2022, else the Win32 DLL won't load into 64-bit Notepad++.
- **Artifact**: `build64/bin/Release/NStringTool.dll` (x64, PE32+) — committed to repo (`.gitignore` excludes `*.dll` but this one is force-added). Copy to `<Notepad++>\plugins\NStringTool\NStringTool.dll` (must be in a subfolder named after the plugin, Notepad++ 7.6+ requirement), restart Notepad++.
- **`.gitignore`**: excludes `build/`, `build-*/`, `build*/`, `bin/`, `*.dll` (except the committed artifact), `*.obj`, `*.lib`, `*.pdb`, `CMakeCache.txt`, `CMakeFiles/`

## Common gotchas

- **Electron binary not downloaded**: `node_modules/electron/dist/electron.exe` missing — run `node node_modules/electron/install.js` manually. Symptom: `npm run dev` reports `Electron uninstall` error.
- **EKafkaTool script name**: `npm run package:win` (not `pack`).
- **EKafkaTool `main` extension**: `./out/main/index.mjs` (`.mjs` because `"type": "module"`).
- **NStringTool platform**: must build x64 (`Win64`/`-A x64`), Win32 DLL won't load into 64-bit Notepad++.
- **EKafkaTool renderer API types**: hand-written in `src/renderer/api/kafkaApi.ts`, must be kept in sync manually with preload.
- For deeper per-project guidance, see `ERabbitMQToolPlus/AGENTS.md` and `EKafkaTool/AGENTS.md`.
