# Logger System Design

**Date**: 2026-07-06
**Topic**: Add structured logging system to EHttpServerTool

## Goal

Add a production-grade logging system that outputs to console, Chrome DevTools (renderer), and disk files across all Electron main-process modules.

## Architecture

```
┌─────────────────────────────────────────────────┐
│                    Main Process                  │
│  ┌───────────────────────────────────────────┐  │
│  │          logger.js (pino-based)           │  │
│  │  ┌─────────┐  ┌──────────┐  ┌──────────┐ │  │
│  │  │Console  │  │  File    │  │EventHub  │ │  │
│  │  │Output   │  │  Roll    │  │(emit)    │ │  │
│  │  └─────────┘  └──────────┘  └────┬─────┘ │  │
│  └───────────────────────────────────┬───────┘  │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐      │
│  │http-server│ │path-config│ │req-logger│      │
│  └──────────┘  └──────────┘  └──────────┘      │
└──────────────────────┬──────────────────────────┘
                       │ sendToRenderer('dev-log', data)
┌──────────────────────┴──────────────────────────┐
│                   Preload                        │
│  exposeInMainWorld('electronAPI').onDevLog()    │
└──────────────────────┬──────────────────────────┘
                       │ callback → console[level]()
┌──────────────────────┴──────────────────────────┐
│                  Renderer (React)                │
│        Chrome DevTools Console output            │
└─────────────────────────────────────────────────┘
```

## Module 1: `electron/modules/logger.js`

### Library
- **pino** — lightweight, fast, structured logging for Node.js
- Install as dependency: `npm install pino`

### Levels
Six levels: `TRACE` (100), `DEBUG` (200), `INFO` (300), `WARN` (400), `ERROR` (500), `FATAL` (600).

### Factory API

```js
const { createLogger } = require('./logger')
const logger = createLogger({ name: 'http-server' })
logger.trace('Detailed trace message')
logger.debug('Debug info')
logger.info('Normal operation')
logger.warn('Warning condition')
logger.error('Error occurred')
logger.fatal('Fatal, shutting down')
```

### Console Output
- Format: `[LEVEL] [YYYY-MM-DD HH:mm:ss.SSS] [module] message`
- ANSI colors in development (non-TTY disabled in production)
- Destination: `process.stdout` for all levels
- Error/Fatal also go to `process.stderr`

### File Output
- Location: `<userData>/logs/app.log`
- Rotation: by size, max 5MB per file
- Retention: 3 backup files (total max ~20MB)
- Filename pattern: `app.log` → `app-<timestamp>-<seq>.log`
- Using `pino-file-transport` or `pino-roll`

### Log Event Hub
- Internal `EventEmitter` fires on every log call
- Emits `{ level, message, timestamp, module }`
- Used by `main.js` to forward to DevTools

### Error Handling
- File write failures are caught and logged to console (cascading fail-safe)
- If file transport fails, console output continues uninterrupted

## Module 2: DevTools Forwarding

### Flow
1. `logger.js` emits `log-event` on every call
2. `main.js` subscribes, calls `sendToRenderer('dev-log', { level, message, timestamp })`
3. `preload.js` exposes `onDevLog(callback)` → stores callback, calls `ipcRenderer.on('dev-log', handler)`
4. Renderer calls `window.electronAPI.onDevLog()` in `useEffect`, receives data
5. Renderer calls `console[level](formattedMessage)` to output to DevTools

### Why console() instead of UI?
- DevTools already has search/filter/export for console output
- No additional UI complexity in renderer
- Developers can filter by level, module, keyword

## Module 3: Integration

### `main.js`
- Create root logger, wire event hub to `sendToRenderer`
- Add debug logging to each IPC handler entry/exit
- Pass sub-loggers to module constructors

### `http-server.js`
- `trace`: request received (method, path, clientIp)
- `debug`: path config matched, echo mode, custom response
- `warn`: 404 (path not found), 405 (method not allowed)
- `error`: server listen error, handler exception
- `info`: server started/stopped

### `path-config.js`
- `trace`: config loaded from disk, save started
- `debug`: config added/updated/deleted
- `error`: filesystem error, config parse error

### `request-logger.js`
- Minimal changes; already logs requests. Add info-level entry for each log operation.

## File Rotation

```
<userData>/logs/
  app.log              ← current (max 5MB)
  app-20260706123045-1.log  ← backup 1 (5MB)
  app-20260706145522-2.log  ← backup 2 (5MB)
  app-20260706172010-3.log  ← backup 3 (5MB)
```

When `app.log` exceeds 5MB:
1. Rename to `app-<timestamp>-<seq>.log` (seq increments)
2. Create new `app.log`
3. If backups > 3, delete oldest

## Files to Modify

| File | Change |
|------|--------|
| `package.json` | Add `pino` dependency |
| `electron/modules/logger.js` | **New** — pino factory + event hub |
| `electron/main.js` | Import logger, wire DevTools forwarding, log IPC handlers |
| `electron/modules/http-server.js` | Add log calls to request handling |
| `electron/modules/path-config.js` | Add log calls to CRUD operations |
| `electron/preload.js` | Add `onDevLog` exposure |
| `src/types/index.ts` | Add `onDevLog` to ElectronAPI interface |

## Files NOT Modified
- `electron/modules/request-logger.js` — minimal changes only (info-level event)
- `src/App.tsx`, `src/components/*` — DevTools forwarding uses `console()`, no UI change needed

## Dependencies
- `pino` — the only new dependency (~100KB, fast, no extra peer deps)
