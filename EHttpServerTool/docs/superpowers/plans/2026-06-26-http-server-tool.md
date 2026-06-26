# HTTP 服务端工具 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build an Electron-based HTTP server debugging tool with custom path configs, multiple HTTP methods, echo mode, custom auto-replies, and full request inspection.

**Architecture:** Electron 33 three-process model. Main process owns the HTTP server (Node.js `http` module), path config management (persisted to `userData/paths.json`), and request logger (in-memory, 1000-entry cap). Renderer is a React 18 + Ant Design 5 UI with left-right split layout — left for path config, right for request log. Communication via IPC (`ipcMain.handle` / `ipcRenderer.invoke`).

**Tech Stack:** Electron 33, React 18, TypeScript 5, Vite 6, Ant Design 5, Node.js `http` module (no Express)

## Global Constraints

- Follow the exact patterns from the sibling EWebsocketTool project (same Electron/Vite/React setup)
- All HTTP server logic runs in main process; renderer communicates only via `window.electronAPI`
- IPC listener pattern: each `on*` handler returns a cleanup function `() => ipcRenderer.removeListener(...)`
- Path config persisted to `app.getPath('userData')/paths.json`
- Request logs kept in memory only, capped at 1000 entries
- HTTP methods supported: GET, POST, PUT, DELETE, PATCH
- Response types: text/plain, application/json, application/xml, text/html
- Path must start with `/`, no empty paths, no duplicate paths
- Port validation: integer 1–65535
- Echo response: JSON with method, path, headers, query, body fields
- 404 for unmatched paths, 405 for method not allowed — both logged

---

### Task 1: Project Scaffolding

**Files:**
- Create: `package.json`
- Create: `vite.config.ts`
- Create: `tsconfig.json`
- Create: `index.html`
- Create: `.gitignore`

- [ ] **Step 1: Create package.json**

```json
{
  "name": "http-server-tool",
  "version": "1.0.0",
  "private": true,
  "author": "HTTP Server Tool",
  "description": "HTTP 服务端调试工具",
  "main": "electron/main.js",
  "build": {
    "appId": "com.http-server-tool.app",
    "productName": "HTTP服务端调试工具",
    "directories": {
      "output": "release"
    },
    "files": [
      "dist/**/*",
      "electron/**/*",
      "package.json"
    ],
    "win": {
      "target": [
        {
          "target": "nsis",
          "arch": ["x64"]
        }
      ]
    },
    "nsis": {
      "oneClick": false,
      "allowToChangeInstallationDirectory": true,
      "shortcutName": "HTTP服务端调试工具"
    }
  },
  "scripts": {
    "dev": "concurrently \"vite\" \"wait-on http://localhost:5173 && cross-env VITE_DEV_SERVER_URL=http://localhost:5173 electron .\"",
    "build": "vite build",
    "preview": "vite preview",
    "pack": "vite build && electron-builder --win -c.electronDist=\"node_modules/electron/dist\" -c.electronVersion=\"33.4.11\""
  },
  "dependencies": {
    "antd": "^5.22.0",
    "react": "^18.3.0",
    "react-dom": "^18.3.0"
  },
  "devDependencies": {
    "@types/react": "^18.3.0",
    "@types/react-dom": "^18.3.0",
    "@vitejs/plugin-react": "^4.3.0",
    "concurrently": "^9.1.0",
    "cross-env": "^7.0.3",
    "electron": "^33.2.0",
    "electron-builder": "^26.15.3",
    "typescript": "^5.6.0",
    "vite": "^6.0.0",
    "wait-on": "^8.0.0"
  }
}
```

- [ ] **Step 2: Create vite.config.ts**

```typescript
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  base: './',
  build: {
    outDir: 'dist',
  },
  server: {
    port: 5173,
  },
})
```

- [ ] **Step 3: Create tsconfig.json**

```json
{
  "compilerOptions": {
    "target": "ES2020",
    "useDefineForClassFields": true,
    "lib": ["ES2020", "DOM", "DOM.Iterable"],
    "module": "ESNext",
    "skipLibCheck": true,
    "moduleResolution": "bundler",
    "allowImportingTsExtensions": true,
    "isolatedModules": true,
    "noEmit": true,
    "jsx": "react-jsx",
    "strict": true,
    "noUnusedLocals": true,
    "noUnusedParameters": true,
    "noFallthroughCasesInSwitch": true
  },
  "include": ["src"]
}
```

- [ ] **Step 4: Create index.html**

```html
<!DOCTYPE html>
<html lang="zh-CN">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>HTTP 服务端调试工具</title>
  </head>
  <body>
    <div id="root"></div>
    <script type="module" src="/src/main.tsx"></script>
  </body>
</html>
```

- [ ] **Step 5: Create .gitignore**

```
node_modules
dist
release
```

- [ ] **Step 6: Install dependencies**

Run: `cd D:\Workspace\ElectronApp\ElectronAppTool\EHttpServerTool && npm install`
Expected: Dependencies installed, node_modules created

- [ ] **Step 7: Commit**

```bash
git add .
git commit -m "chore: scaffold Electron + React + Vite project"
```

---

### Task 2: Type Definitions

**Files:**
- Create: `src/types/index.ts`

**Interfaces:**
- Produces: `PathConfig`, `RequestLog`, `ElectronAPI` — used by all renderer components and preload

- [ ] **Step 1: Create src/types/index.ts**

```typescript
/** Path 配置 */
export interface PathConfig {
  id: string
  path: string
  methods: string[]
  echoEnabled: boolean
  responseType: 'text' | 'json' | 'xml' | 'html'
  responseContent: string
}

/** 请求日志 */
export interface RequestLog {
  id: string
  timestamp: number
  clientIp: string
  method: string
  path: string
  headers: Record<string, string>
  query: Record<string, string>
  body: string
}

/** Electron 主进程暴露的 API */
export interface ElectronAPI {
  // Server control
  startServer: (port: number) => Promise<{ success: boolean }>
  stopServer: () => Promise<{ success: boolean }>

  // Path config
  addPath: (config: Omit<PathConfig, 'id'>) => Promise<{ success: boolean; id?: string; error?: string }>
  updatePath: (config: PathConfig) => Promise<{ success: boolean; error?: string }>
  deletePath: (id: string) => Promise<{ success: boolean }>
  listPaths: () => Promise<PathConfig[]>

  // Logs
  getLogs: () => Promise<RequestLog[]>
  clearLogs: () => Promise<{ success: boolean }>

  // Context menu
  showContextMenu: () => Promise<string | null>

  // Event listeners (each returns cleanup function)
  onServerStarted: (callback: (data: { port: number }) => void) => () => void
  onServerStopped: (callback: () => void) => () => void
  onServerError: (callback: (data: { message: string }) => void) => () => void
  onNewRequest: (callback: (data: RequestLog) => void) => () => void

  removeAllListeners: (channel: string) => void
}

declare global {
  interface Window {
    electronAPI?: ElectronAPI
  }
}

export {}
```

- [ ] **Step 2: Commit**

```bash
git add src/types/index.ts
git commit -m "feat: add TypeScript type definitions"
```

---

### Task 3: Main Process — Path Config Module

**Files:**
- Create: `electron/modules/path-config.js`

**Interfaces:**
- Consumes: Electron `app` module for userData path
- Produces: `PathConfigManager` class with `add(config)`, `update(config)`, `remove(id)`, `getAll()`, `getByPath(path)` methods

- [ ] **Step 1: Create electron/modules/path-config.js**

```javascript
// electron/modules/path-config.js
const { app } = require('electron')
const fs = require('fs')
const path = require('path')

let idCounter = 0

class PathConfigManager {
  constructor() {
    this.configs = []
    this.filePath = path.join(app.getPath('userData'), 'paths.json')
    this._load()
  }

  _load() {
    try {
      if (fs.existsSync(this.filePath)) {
        const data = fs.readFileSync(this.filePath, 'utf-8')
        this.configs = JSON.parse(data)
        // Restore idCounter to max existing id
        for (const c of this.configs) {
          const num = parseInt(c.id.replace('path-', ''), 10)
          if (num >= idCounter) idCounter = num
        }
      }
    } catch (_) {
      this.configs = []
    }
  }

  _save() {
    try {
      const dir = path.dirname(this.filePath)
      if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true })
      fs.writeFileSync(this.filePath, JSON.stringify(this.configs, null, 2), 'utf-8')
    } catch (_) {
      // Silently fail — config won't persist but app continues
    }
  }

  _nextId() {
    return `path-${++idCounter}`
  }

  add(config) {
    // Validate path format
    if (!config.path || !config.path.startsWith('/')) {
      return { success: false, error: '路径必须以 / 开头' }
    }
    // Check duplicate
    if (this.configs.some(c => c.path === config.path)) {
      return { success: false, error: '该路径已存在' }
    }
    const id = this._nextId()
    const newConfig = { id, ...config }
    this.configs.push(newConfig)
    this._save()
    return { success: true, id }
  }

  update(config) {
    const index = this.configs.findIndex(c => c.id === config.id)
    if (index === -1) {
      return { success: false, error: '配置不存在' }
    }
    // Check duplicate path (excluding self)
    if (this.configs.some(c => c.path === config.path && c.id !== config.id)) {
      return { success: false, error: '该路径已存在' }
    }
    this.configs[index] = config
    this._save()
    return { success: true }
  }

  remove(id) {
    const index = this.configs.findIndex(c => c.id === id)
    if (index !== -1) {
      this.configs.splice(index, 1)
      this._save()
    }
    return { success: true }
  }

  getAll() {
    return [...this.configs]
  }

  getByPath(reqPath) {
    return this.configs.find(c => c.path === reqPath) || null
  }
}

module.exports = { PathConfigManager }
```

- [ ] **Step 2: Commit**

```bash
git add electron/modules/path-config.js
git commit -m "feat: add path config manager with persistence"
```

---

### Task 4: Main Process — Request Logger Module

**Files:**
- Create: `electron/modules/request-logger.js`

**Interfaces:**
- Consumes: Nothing (standalone module)
- Produces: `RequestLogger` class with `log(entry)`, `getAll()`, `clear()`, `onNewRequest(callback)` methods

- [ ] **Step 1: Create electron/modules/request-logger.js**

```javascript
// electron/modules/request-logger.js

const MAX_LOGS = 1000
let logIdCounter = 0

class RequestLogger {
  constructor() {
    this.logs = []
    this.listeners = []
  }

  _nextId() {
    return `log-${++logIdCounter}`
  }

  log(entry) {
    const logEntry = {
      id: this._nextId(),
      timestamp: Date.now(),
      clientIp: entry.clientIp || 'unknown',
      method: entry.method,
      path: entry.path,
      headers: entry.headers || {},
      query: entry.query || {},
      body: entry.body || '',
    }
    this.logs.push(logEntry)
    // Cap at MAX_LOGS
    if (this.logs.length > MAX_LOGS) {
      this.logs = this.logs.slice(this.logs.length - MAX_LOGS)
    }
    // Notify listeners
    for (const listener of this.listeners) {
      try {
        listener(logEntry)
      } catch (_) {
        // Ignore listener errors
      }
    }
    return logEntry
  }

  getAll() {
    return [...this.logs]
  }

  clear() {
    this.logs = []
    return { success: true }
  }

  onNewRequest(callback) {
    this.listeners.push(callback)
    return () => {
      const index = this.listeners.indexOf(callback)
      if (index !== -1) this.listeners.splice(index, 1)
    }
  }
}

module.exports = { RequestLogger }
```

- [ ] **Step 2: Commit**

```bash
git add electron/modules/request-logger.js
git commit -m "feat: add request logger with 1000-entry cap"
```

---

### Task 5: Main Process — HTTP Server Module

**Files:**
- Create: `electron/modules/http-server.js`

**Interfaces:**
- Consumes: `PathConfigManager` (for routing), `RequestLogger` (for logging)
- Produces: `HttpServerManager` class with `start(port, sendToRenderer)`, `stop()` methods

- [ ] **Step 1: Create electron/modules/http-server.js**

```javascript
// electron/modules/http-server.js
const http = require('http')
const { URL } = require('url')

const RESPONSE_CONTENT_TYPES = {
  text: 'text/plain; charset=utf-8',
  json: 'application/json; charset=utf-8',
  xml: 'application/xml; charset=utf-8',
  html: 'text/html; charset=utf-8',
}

// 从 remoteAddress 中提取纯 IPv4 地址
function getIPv4(remoteAddress) {
  if (!remoteAddress) return null
  if (remoteAddress.startsWith('::ffff:')) {
    return remoteAddress.slice(7)
  }
  if (remoteAddress === '::1') {
    return '127.0.0.1'
  }
  if (/^\d+\.\d+\.\d+\.\d+$/.test(remoteAddress)) {
    return remoteAddress
  }
  return null
}

function parseBody(req) {
  return new Promise((resolve) => {
    const chunks = []
    req.on('data', (chunk) => chunks.push(chunk))
    req.on('end', () => resolve(Buffer.concat(chunks).toString('utf-8')))
    req.on('error', () => resolve(''))
  })
}

function parseQuery(urlString) {
  try {
    const url = new URL(urlString, 'http://localhost')
    const query = {}
    for (const [key, value] of url.searchParams) {
      query[key] = value
    }
    return query
  } catch (_) {
    return {}
  }
}

function buildEchoResponse(method, path, headers, query, body) {
  return JSON.stringify({
    echo: true,
    method,
    path,
    headers,
    query,
    body,
  }, null, 2)
}

class HttpServerManager {
  constructor(pathConfigManager, requestLogger) {
    this.pathConfig = pathConfigManager
    this.logger = requestLogger
    this.server = null
    this.sendToRenderer = null
  }

  start(port, sendToRenderer) {
    if (this.server) {
      throw new Error('服务已在运行中')
    }
    this.sendToRenderer = sendToRenderer

    return new Promise((resolve, reject) => {
      this.server = http.createServer(async (req, res) => {
        await this._handleRequest(req, res)
      })

      this.server.on('listening', () => {
        this.sendToRenderer('server-started', { port })
        resolve({ success: true })
      })

      this.server.on('error', (err) => {
        this.server = null
        this.sendToRenderer('server-error', { message: err.message })
        reject(err)
      })

      try {
        this.server.listen(port)
      } catch (err) {
        this.server = null
        reject(err)
      }
    })
  }

  stop() {
    if (this.server) {
      this.server.close()
      this.server = null
      this.sendToRenderer('server-stopped')
    }
    return { success: true }
  }

  async _handleRequest(req, res) {
    const ipv4 = getIPv4(req.socket.remoteAddress)
    const clientIp = ipv4 || 'unknown'
    const method = req.method.toUpperCase()
    const urlObj = new URL(req.url, 'http://localhost')
    const reqPath = urlObj.pathname
    const query = parseQuery(req.url)
    const body = await parseBody(req)
    const headers = { ...req.headers }

    // Log the request
    this.logger.log({ clientIp, method, path: reqPath, headers, query, body })

    // Push log to renderer
    if (this.sendToRenderer) {
      const logs = this.logger.getAll()
      this.sendToRenderer('new-request', logs[logs.length - 1])
    }

    // Find matching path config
    const config = this.pathConfig.getByPath(reqPath)
    if (!config) {
      res.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' })
      res.end('404 Not Found')
      return
    }

    // Check method
    if (!config.methods.includes(method)) {
      res.writeHead(405, { 'Content-Type': 'text/plain; charset=utf-8' })
      res.end('405 Method Not Allowed')
      return
    }

    // Echo mode
    if (config.echoEnabled) {
      const echoBody = buildEchoResponse(method, reqPath, headers, query, body)
      res.writeHead(200, { 'Content-Type': 'application/json; charset=utf-8' })
      res.end(echoBody)
      return
    }

    // Custom response
    const contentType = RESPONSE_CONTENT_TYPES[config.responseType] || 'text/plain; charset=utf-8'
    res.writeHead(200, { 'Content-Type': contentType })
    res.end(config.responseContent || '')
  }
}

module.exports = { HttpServerManager }
```

- [ ] **Step 2: Commit**

```bash
git add electron/modules/http-server.js
git commit -m "feat: add HTTP server module with routing, echo, and logging"
```

---

### Task 6: Main Process — Preload & Entry Point

**Files:**
- Create: `electron/preload.js`
- Create: `electron/main.js`

**Interfaces:**
- Consumes: `PathConfigManager`, `RequestLogger`, `HttpServerManager`
- Produces: `window.electronAPI` in renderer

- [ ] **Step 1: Create electron/preload.js**

```javascript
// electron/preload.js
const { contextBridge, ipcRenderer } = require('electron')

contextBridge.exposeInMainWorld('electronAPI', {
  // Server control
  startServer: (port) => ipcRenderer.invoke('start-server', port),
  stopServer: () => ipcRenderer.invoke('stop-server'),

  // Path config
  addPath: (config) => ipcRenderer.invoke('add-path', config),
  updatePath: (config) => ipcRenderer.invoke('update-path', config),
  deletePath: (id) => ipcRenderer.invoke('delete-path', id),
  listPaths: () => ipcRenderer.invoke('list-paths'),

  // Logs
  getLogs: () => ipcRenderer.invoke('get-logs'),
  clearLogs: () => ipcRenderer.invoke('clear-logs'),

  // Context menu
  showContextMenu: () => ipcRenderer.invoke('show-context-menu'),

  // Event listeners
  onServerStarted: (callback) => {
    const handler = (_event, data) => callback(data)
    ipcRenderer.on('server-started', handler)
    return () => ipcRenderer.removeListener('server-started', handler)
  },
  onServerStopped: (callback) => {
    const handler = () => callback()
    ipcRenderer.on('server-stopped', handler)
    return () => ipcRenderer.removeListener('server-stopped', handler)
  },
  onServerError: (callback) => {
    const handler = (_event, data) => callback(data)
    ipcRenderer.on('server-error', handler)
    return () => ipcRenderer.removeListener('server-error', handler)
  },
  onNewRequest: (callback) => {
    const handler = (_event, data) => callback(data)
    ipcRenderer.on('new-request', handler)
    return () => ipcRenderer.removeListener('new-request', handler)
  },

  removeAllListeners: (channel) => ipcRenderer.removeAllListeners(channel),
})
```

- [ ] **Step 2: Create electron/main.js**

```javascript
// electron/main.js
const { app, BrowserWindow, ipcMain, Menu } = require('electron')
const path = require('path')
const { PathConfigManager } = require('./modules/path-config')
const { RequestLogger } = require('./modules/request-logger')
const { HttpServerManager } = require('./modules/http-server')

let mainWindow = null
let pathConfigManager = null
let requestLogger = null
let httpServerManager = null

function createWindow() {
  mainWindow = new BrowserWindow({
    width: 1200,
    height: 800,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
    },
  })

  const devUrl = process.env.VITE_DEV_SERVER_URL
  if (devUrl) {
    mainWindow.loadURL(devUrl)
  } else {
    mainWindow.loadFile(path.join(__dirname, '..', 'dist', 'index.html'))
  }
}

function sendToRenderer(channel, data) {
  if (mainWindow && !mainWindow.isDestroyed()) {
    mainWindow.webContents.send(channel, data)
  }
}

function setupModules() {
  pathConfigManager = new PathConfigManager()
  requestLogger = new RequestLogger()
  httpServerManager = new HttpServerManager(pathConfigManager, requestLogger)
}

function setupIPC() {
  // Server control
  ipcMain.handle('start-server', async (_event, port) => {
    return httpServerManager.start(port, sendToRenderer)
  })

  ipcMain.handle('stop-server', async () => {
    return httpServerManager.stop()
  })

  // Path config
  ipcMain.handle('add-path', async (_event, config) => {
    return pathConfigManager.add(config)
  })

  ipcMain.handle('update-path', async (_event, config) => {
    return pathConfigManager.update(config)
  })

  ipcMain.handle('delete-path', async (_event, id) => {
    return pathConfigManager.remove(id)
  })

  ipcMain.handle('list-paths', async () => {
    return pathConfigManager.getAll()
  })

  // Logs
  ipcMain.handle('get-logs', async () => {
    return requestLogger.getAll()
  })

  ipcMain.handle('clear-logs', async () => {
    return requestLogger.clear()
  })

  // Context menu
  ipcMain.handle('show-context-menu', async () => {
    return new Promise((resolve) => {
      const menu = Menu.buildFromTemplate([
        {
          label: '清空',
          click: () => resolve('clear'),
        },
      ])
      menu.popup({
        callback: () => resolve(null),
      })
    })
  })
}

app.whenReady().then(() => {
  setupModules()
  setupIPC()
  createWindow()
})

app.on('window-all-closed', () => {
  if (httpServerManager) {
    httpServerManager.stop()
  }
  app.quit()
})

app.on('activate', () => {
  if (BrowserWindow.getAllWindows().length === 0) {
    createWindow()
  }
})
```

- [ ] **Step 3: Commit**

```bash
git add electron/preload.js electron/main.js
git commit -m "feat: add Electron main process with IPC and preload"
```

---

### Task 7: Renderer — PathList Component

**Files:**
- Create: `src/components/PathList.tsx`

**Interfaces:**
- Consumes: `PathConfig[]` (via props), `onSelect(id)`, `onAdd()`, `selectedId`
- Produces: Clickable list of paths with add button

- [ ] **Step 1: Create src/components/PathList.tsx**

```tsx
import React from 'react'
import { List, Button, Typography } from 'antd'
import { PlusOutlined } from '@ant-design/icons'
import type { PathConfig } from '../types'

const { Text } = Typography

interface PathListProps {
  paths: PathConfig[]
  selectedId: string | null
  onSelect: (id: string) => void
  onAdd: () => void
}

const PathList: React.FC<PathListProps> = ({ paths, selectedId, onSelect, onAdd }) => {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      <div style={{ padding: '8px 12px', borderBottom: '1px solid #f0f0f0' }}>
        <Button type="primary" icon={<PlusOutlined />} onClick={onAdd} block>
          添加 Path
        </Button>
      </div>
      <div style={{ flex: 1, overflow: 'auto' }}>
        <List
          dataSource={paths}
          locale={{ emptyText: '暂无配置，请添加 Path' }}
          renderItem={(item) => (
            <List.Item
              onClick={() => onSelect(item.id)}
              style={{
                cursor: 'pointer',
                padding: '8px 12px',
                backgroundColor: selectedId === item.id ? '#e6f4ff' : undefined,
                borderLeft: selectedId === item.id ? '3px solid #1677ff' : '3px solid transparent',
              }}
            >
              <Text strong={selectedId === item.id}>{item.path}</Text>
              <Text type="secondary" style={{ fontSize: 12 }}>
                {item.methods.join(', ')}
              </Text>
            </List.Item>
          )}
        />
      </div>
    </div>
  )
}

export default PathList
```

- [ ] **Step 2: Commit**

```bash
git add src/components/PathList.tsx
git commit -m "feat: add PathList component"
```

---

### Task 8: Renderer — PathDetail Component

**Files:**
- Create: `src/components/PathDetail.tsx`

**Interfaces:**
- Consumes: `PathConfig | null` (selected config), `onSave(config)`, `onDelete(id)`
- Produces: Form for editing path, methods, echo toggle, response type, response content

- [ ] **Step 1: Create src/components/PathDetail.tsx**

```tsx
import React, { useEffect, useState } from 'react'
import { Form, Input, Checkbox, Switch, Select, Button, Space, Typography, message } from 'antd'
import { DeleteOutlined, SaveOutlined } from '@ant-design/icons'
import type { PathConfig } from '../types'

const { TextArea } = Input
const { Title } = Typography

const ALL_METHODS = ['GET', 'POST', 'PUT', 'DELETE', 'PATCH']

const RESPONSE_TYPE_OPTIONS = [
  { label: 'Text', value: 'text' },
  { label: 'JSON', value: 'json' },
  { label: 'XML', value: 'xml' },
  { label: 'HTML', value: 'html' },
]

interface PathDetailProps {
  config: PathConfig | null
  onSave: (config: PathConfig) => void
  onDelete: (id: string) => void
}

const PathDetail: React.FC<PathDetailProps> = ({ config, onSave, onDelete }) => {
  const [form] = Form.useForm()
  const [isEditing, setIsEditing] = useState(false)

  useEffect(() => {
    if (config) {
      form.setFieldsValue({
        path: config.path,
        methods: config.methods,
        echoEnabled: config.echoEnabled,
        responseType: config.responseType,
        responseContent: config.responseContent,
      })
      setIsEditing(false)
    }
  }, [config, form])

  if (!config) {
    return (
      <div style={{ padding: 24, textAlign: 'center', color: '#999' }}>
        请选择或添加一个 Path
      </div>
    )
  }

  const handleSave = () => {
    form.validateFields().then((values) => {
      if (!values.path.startsWith('/')) {
        message.error('路径必须以 / 开头')
        return
      }
      onSave({
        id: config.id,
        path: values.path,
        methods: values.methods,
        echoEnabled: values.echoEnabled,
        responseType: values.responseType,
        responseContent: values.responseContent,
      })
    })
  }

  const handleDelete = () => {
    onDelete(config.id)
  }

  return (
    <div style={{ padding: 16 }}>
      <Title level={5}>Path 配置</Title>
      <Form form={form} layout="vertical" onValuesChange={() => setIsEditing(true)}>
        <Form.Item
          name="path"
          label="路径"
          rules={[
            { required: true, message: '请输入路径' },
            { pattern: /^\//, message: '路径必须以 / 开头' },
          ]}
        >
          <Input placeholder="/api/users" />
        </Form.Item>

        <Form.Item
          name="methods"
          label="请求方法"
          rules={[{ required: true, message: '请选择至少一个方法', type: 'array', min: 1 }]}
        >
          <Checkbox.Group options={ALL_METHODS} />
        </Form.Item>

        <Form.Item name="echoEnabled" label="Echo 模式" valuePropName="checked">
          <Switch checkedChildren="开" unCheckedChildren="关" />
        </Form.Item>

        <Form.Item name="responseType" label="回复类型">
          <Select options={RESPONSE_TYPE_OPTIONS} />
        </Form.Item>

        <Form.Item name="responseContent" label="回复内容">
          <TextArea rows={6} placeholder="输入自定义回复内容..." />
        </Form.Item>

        <Form.Item>
          <Space>
            <Button
              type="primary"
              icon={<SaveOutlined />}
              onClick={handleSave}
              disabled={!isEditing}
            >
              保存
            </Button>
            <Button
              danger
              icon={<DeleteOutlined />}
              onClick={handleDelete}
            >
              删除
            </Button>
          </Space>
        </Form.Item>
      </Form>
    </div>
  )
}

export default PathDetail
```

- [ ] **Step 2: Commit**

```bash
git add src/components/PathDetail.tsx
git commit -m "feat: add PathDetail component with form"
```

---

### Task 9: Renderer — RequestLog Component

**Files:**
- Create: `src/components/RequestLog.tsx`

**Interfaces:**
- Consumes: `RequestLog[]` (via props)
- Produces: Scrollable log list with expandable detail view, auto-scroll, clear button

- [ ] **Step 1: Create src/components/RequestLog.tsx**

```tsx
import React, { useRef, useEffect, useState, useCallback } from 'react'
import { Button, Checkbox, Collapse, Tag, Typography, Empty } from 'antd'
import { ClearOutlined } from '@ant-design/icons'
import type { RequestLog } from '../types'

const { Text } = Typography

interface RequestLogProps {
  logs: RequestLog[]
  onClear: () => void
}

const METHOD_COLORS: Record<string, string> = {
  GET: 'green',
  POST: 'blue',
  PUT: 'orange',
  DELETE: 'red',
  PATCH: 'purple',
}

function formatTime(timestamp: number): string {
  const d = new Date(timestamp)
  return d.toLocaleTimeString('zh-CN', { hour12: false })
}

const RequestLogPanel: React.FC<RequestLogProps> = ({ logs, onClear }) => {
  const scrollRef = useRef<HTMLDivElement>(null)
  const [autoScroll, setAutoScroll] = useState(true)

  const scrollToBottom = useCallback(() => {
    if (scrollRef.current && autoScroll) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight
    }
  }, [autoScroll])

  useEffect(() => {
    scrollToBottom()
  }, [logs, scrollToBottom])

  const handleContextMenu = async (e: React.MouseEvent) => {
    e.preventDefault()
    if (window.electronAPI) {
      const result = await window.electronAPI.showContextMenu()
      if (result === 'clear') {
        onClear()
      }
    }
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      <div
        style={{
          padding: '8px 12px',
          borderBottom: '1px solid #f0f0f0',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
        }}
      >
        <Text strong>请求日志 ({logs.length})</Text>
        <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
          <Checkbox checked={autoScroll} onChange={(e) => setAutoScroll(e.target.checked)}>
            自动滚动
          </Checkbox>
          <Button size="small" icon={<ClearOutlined />} onClick={onClear}>
            清空
          </Button>
        </div>
      </div>
      <div
        ref={scrollRef}
        onContextMenu={handleContextMenu}
        style={{ flex: 1, overflow: 'auto', padding: '4px 0' }}
      >
        {logs.length === 0 ? (
          <Empty description="暂无请求" style={{ marginTop: 40 }} />
        ) : (
          <Collapse
            ghost
            items={logs.map((log) => ({
              key: log.id,
              label: (
                <div style={{ display: 'flex', gap: 8, alignItems: 'center', fontFamily: 'monospace' }}>
                  <Text type="secondary" style={{ fontSize: 12, minWidth: 70 }}>
                    {formatTime(log.timestamp)}
                  </Text>
                  <Tag color={METHOD_COLORS[log.method] || 'default'} style={{ margin: 0 }}>
                    {log.method}
                  </Tag>
                  <Text style={{ fontSize: 13 }}>{log.path}</Text>
                </div>
              ),
              children: (
                <div style={{ fontFamily: 'monospace', fontSize: 12, lineHeight: 1.8 }}>
                  <div><Text strong>Client:</Text> {log.clientIp}</div>
                  <div style={{ marginTop: 4 }}>
                    <Text strong>Headers:</Text>
                    {Object.keys(log.headers).length === 0 ? (
                      <div style={{ color: '#999', paddingLeft: 12 }}>无</div>
                    ) : (
                      Object.entries(log.headers).map(([k, v]) => (
                        <div key={k} style={{ paddingLeft: 12 }}>
                          <Text type="secondary">{k}:</Text> {v}
                        </div>
                      ))
                    )}
                  </div>
                  <div style={{ marginTop: 4 }}>
                    <Text strong>Query:</Text>
                    {Object.keys(log.query).length === 0 ? (
                      <div style={{ color: '#999', paddingLeft: 12 }}>无</div>
                    ) : (
                      Object.entries(log.query).map(([k, v]) => (
                        <div key={k} style={{ paddingLeft: 12 }}>
                          {k}={v}
                        </div>
                      ))
                    )}
                  </div>
                  <div style={{ marginTop: 4 }}>
                    <Text strong>Body:</Text>
                    <pre style={{
                      paddingLeft: 12,
                      margin: 0,
                      whiteSpace: 'pre-wrap',
                      wordBreak: 'break-all',
                      color: log.body ? undefined : '#999',
                    }}>
                      {log.body || '无'}
                    </pre>
                  </div>
                </div>
              ),
            }))}
          />
        )}
      </div>
    </div>
  )
}

export default RequestLogPanel
```

- [ ] **Step 2: Commit**

```bash
git add src/components/RequestLog.tsx
git commit -m "feat: add RequestLog component with expandable detail"
```

---

### Task 10: Renderer — App, Entry Point, Styles

**Files:**
- Create: `src/main.tsx`
- Create: `src/App.tsx`
- Create: `src/App.css`

**Interfaces:**
- Consumes: `PathList`, `PathDetail`, `RequestLogPanel` components
- Consumes: `window.electronAPI` for all IPC communication

- [ ] **Step 1: Create src/main.tsx**

```tsx
import React from 'react'
import ReactDOM from 'react-dom/client'
import App from './App'
import './App.css'

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
)
```

- [ ] **Step 2: Create src/App.tsx**

```tsx
import React, { useState, useEffect, useCallback } from 'react'
import { ConfigProvider, InputNumber, Button, Space, Tag, message } from 'antd'
import { PlayCircleOutlined, StopOutlined } from '@ant-design/icons'
import PathList from './components/PathList'
import PathDetail from './components/PathDetail'
import RequestLogPanel from './components/RequestLog'
import type { PathConfig, RequestLog } from './types'

const EMPTY_CONFIG: Omit<PathConfig, 'id'> = {
  path: '',
  methods: ['GET'],
  echoEnabled: false,
  responseType: 'json',
  responseContent: '',
}

const App: React.FC = () => {
  const [port, setPort] = useState<number>(8080)
  const [running, setRunning] = useState(false)
  const [paths, setPaths] = useState<PathConfig[]>([])
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [logs, setLogs] = useState<RequestLog[]>([])

  // Load paths on mount
  useEffect(() => {
    const loadPaths = async () => {
      if (window.electronAPI) {
        const list = await window.electronAPI.listPaths()
        setPaths(list)
      }
    }
    loadPaths()
  }, [])

  // Event listeners
  useEffect(() => {
    if (!window.electronAPI) return
    const cleanups: (() => void)[] = []

    cleanups.push(window.electronAPI.onServerStarted(() => {
      setRunning(true)
      message.success('服务器已启动')
    }))

    cleanups.push(window.electronAPI.onServerStopped(() => {
      setRunning(false)
      message.info('服务器已停止')
    }))

    cleanups.push(window.electronAPI.onServerError((data) => {
      message.error(data.message)
    }))

    cleanups.push(window.electronAPI.onNewRequest((log) => {
      setLogs((prev) => [...prev, log])
    }))

    return () => {
      for (const cleanup of cleanups) cleanup()
    }
  }, [])

  const handleStart = useCallback(async () => {
    if (!window.electronAPI || !port) return
    try {
      await window.electronAPI.startServer(port)
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : '启动失败'
      message.error(msg)
    }
  }, [port])

  const handleStop = useCallback(async () => {
    if (!window.electronAPI) return
    await window.electronAPI.stopServer()
  }, [])

  const handleAddPath = useCallback(async () => {
    if (!window.electronAPI) return
    const result = await window.electronAPI.addPath(EMPTY_CONFIG)
    if (result.success && result.id) {
      const list = await window.electronAPI.listPaths()
      setPaths(list)
      setSelectedId(result.id)
    } else if (result.error) {
      message.error(result.error)
    }
  }, [])

  const handleSavePath = useCallback(async (config: PathConfig) => {
    if (!window.electronAPI) return
    const result = await window.electronAPI.updatePath(config)
    if (result.success) {
      const list = await window.electronAPI.listPaths()
      setPaths(list)
      message.success('保存成功')
    } else if (result.error) {
      message.error(result.error)
    }
  }, [])

  const handleDeletePath = useCallback(async (id: string) => {
    if (!window.electronAPI) return
    await window.electronAPI.deletePath(id)
    const list = await window.electronAPI.listPaths()
    setPaths(list)
    if (selectedId === id) setSelectedId(null)
    message.success('已删除')
  }, [selectedId])

  const handleClearLogs = useCallback(async () => {
    if (!window.electronAPI) return
    await window.electronAPI.clearLogs()
    setLogs([])
  }, [])

  const selectedConfig = paths.find((p) => p.id === selectedId) || null

  return (
    <ConfigProvider>
      <div className="app-container">
        {/* Top toolbar */}
        <div className="toolbar">
          <Space>
            <span>端口:</span>
            <InputNumber
              min={1}
              max={65535}
              value={port}
              onChange={(v) => setPort(v || 8080)}
              disabled={running}
              style={{ width: 100 }}
            />
            {running ? (
              <Button danger icon={<StopOutlined />} onClick={handleStop}>
                停止
              </Button>
            ) : (
              <Button type="primary" icon={<PlayCircleOutlined />} onClick={handleStart}>
                启动
              </Button>
            )}
            <Tag color={running ? 'green' : 'default'}>
              {running ? '运行中' : '已停止'}
            </Tag>
          </Space>
        </div>

        {/* Main content */}
        <div className="main-content">
          {/* Left panel: path list + detail */}
          <div className="left-panel">
            <div className="path-list-area">
              <PathList
                paths={paths}
                selectedId={selectedId}
                onSelect={setSelectedId}
                onAdd={handleAddPath}
              />
            </div>
            <div className="path-detail-area">
              <PathDetail
                config={selectedConfig}
                onSave={handleSavePath}
                onDelete={handleDeletePath}
              />
            </div>
          </div>

          {/* Right panel: request log */}
          <div className="right-panel">
            <RequestLogPanel logs={logs} onClear={handleClearLogs} />
          </div>
        </div>
      </div>
    </ConfigProvider>
  )
}

export default App
```

- [ ] **Step 3: Create src/App.css**

```css
* {
  margin: 0;
  padding: 0;
  box-sizing: border-box;
}

html, body, #root {
  height: 100%;
  width: 100%;
  overflow: hidden;
}

.app-container {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: #fff;
}

.toolbar {
  padding: 12px 16px;
  border-bottom: 1px solid #f0f0f0;
  background: #fafafa;
  flex-shrink: 0;
}

.main-content {
  display: flex;
  flex: 1;
  overflow: hidden;
}

.left-panel {
  width: 420px;
  min-width: 320px;
  border-right: 1px solid #f0f0f0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.path-list-area {
  flex-shrink: 0;
  max-height: 40%;
  overflow: auto;
  border-bottom: 1px solid #f0f0f0;
}

.path-detail-area {
  flex: 1;
  overflow: auto;
}

.right-panel {
  flex: 1;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}
```

- [ ] **Step 4: Verify the app launches**

Run: `npm run dev`
Expected: Electron window opens with the toolbar, left panel (path list + detail), and right panel (request log).

- [ ] **Step 5: Commit**

```bash
git add src/main.tsx src/App.tsx src/App.css
git commit -m "feat: add App component with full layout and IPC wiring"
```

---

### Task 11: Manual Integration Test

**Files:** (no new files — testing existing code)

- [ ] **Step 1: Start the app**

Run: `npm run dev`

- [ ] **Step 2: Test server start/stop**

1. Enter port `8080`
2. Click "启动" — should show "运行中" tag and success message
3. Click "停止" — should show "已停止" tag and info message

- [ ] **Step 3: Test path configuration**

1. Click "添加 Path" — a new empty entry appears
2. Fill in path: `/api/test`, select methods: GET, POST
3. Set response type: JSON, response content: `{"message": "hello"}`
4. Click "保存" — should show success message
5. Verify config persists: restart the app, the path should still be there

- [ ] **Step 4: Test HTTP requests**

1. Start the server on port 8080
2. Add path `/api/test` with GET method, JSON response `{"status": "ok"}`
3. Open browser or use curl: `curl http://localhost:8080/api/test`
4. Verify response: `{"status": "ok"}` with Content-Type `application/json`
5. Check request log in the app — should show the request with full details

- [ ] **Step 5: Test echo mode**

1. Enable echo mode for `/api/test`
2. Send request: `curl -X POST -H "Content-Type: application/json" -d '{"name":"test"}' http://localhost:8080/api/test?foo=bar`
3. Verify response is JSON with echo: true, method, path, headers, query, body
4. Disable echo mode, verify custom response returns

- [ ] **Step 6: Test error handling**

1. Request unconfigured path: `curl http://localhost:8080/unknown` → 404
2. Request with wrong method: `curl -X DELETE http://localhost:8080/api/test` → 405
3. Both should appear in the request log

- [ ] **Step 7: Test log features**

1. Verify auto-scroll works when new requests arrive
2. Click a log entry to expand — verify headers, query, body are shown
3. Click "清空" — logs should clear
4. Right-click in log area → "清空" should also work

- [ ] **Step 8: Final commit (if any fixes needed)**

```bash
git add -A
git commit -m "fix: integration test fixes"
```
