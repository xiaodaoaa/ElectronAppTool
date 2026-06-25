# WebSocket 调试工具 — 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 构建一个基于 Electron + React + Ant Design 的桌面 WebSocket 调试工具，包含客户端和服务端两个 Tab 页。

**Architecture:** Electron 主进程管理窗口和 `ws` 服务端，通过 IPC 与渲染进程通信。渲染进程使用 React + Ant Design 构建 UI，客户端 WebSocket 直接使用浏览器原生 API。左右分栏布局，左侧配置/操作区，右侧日志区。

**Tech Stack:** Electron、React 18、TypeScript、Ant Design 5、Vite 5、Node.js `ws`

---

## 文件结构

| 文件 | 职责 |
|------|------|
| `package.json` | 项目配置、依赖、脚本 |
| `vite.config.ts` | Vite 构建配置（React 渲染进程） |
| `tsconfig.json` | TypeScript 编译配置 |
| `index.html` | HTML 入口 |
| `electron/main.js` | Electron 主进程：窗口、IPC、WebSocket 服务端 |
| `electron/preload.js` | 预加载脚本：contextBridge 暴露 API |
| `src/main.tsx` | React 入口 |
| `src/App.tsx` | 顶层布局 + Tab 切换 |
| `src/App.css` | 全局样式 + 布局 + 日志面板样式 |
| `src/types/index.ts` | 公共类型定义 |
| `src/components/LogPanel.tsx` | 共用消息日志组件 |
| `src/components/ClientTab.tsx` | 客户端 Tab |
| `src/components/ServerTab.tsx` | 服务端 Tab |
| `src/hooks/useWebSocket.ts` | 客户端 WebSocket 连接管理 Hook |

---

### Task 1: 项目初始化 — package.json 与配置文件

**Files:**
- Create: `package.json`
- Create: `tsconfig.json`
- Create: `vite.config.ts`
- Create: `index.html`

- [ ] **Step 1: 创建 package.json**

```json
{
  "name": "websocket-tool",
  "version": "1.0.0",
  "description": "WebSocket 调试工具",
  "main": "electron/main.js",
  "scripts": {
    "dev": "concurrently \"vite\" \"wait-on http://localhost:5173 && cross-env VITE_DEV_SERVER_URL=http://localhost:5173 electron .\"",
    "build": "vite build",
    "preview": "vite preview"
  },
  "dependencies": {
    "antd": "^5.22.0",
    "react": "^18.3.0",
    "react-dom": "^18.3.0",
    "ws": "^8.18.0"
  },
  "devDependencies": {
    "@types/react": "^18.3.0",
    "@types/react-dom": "^18.3.0",
    "@types/ws": "^8.5.0",
    "@vitejs/plugin-react": "^4.3.0",
    "concurrently": "^9.1.0",
    "cross-env": "^7.0.3",
    "electron": "^33.2.0",
    "typescript": "^5.6.0",
    "vite": "^6.0.0",
    "wait-on": "^8.0.0"
  }
}
```

- [ ] **Step 2: 创建 tsconfig.json**

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
    "resolveJsonModule": true,
    "isolatedModules": true,
    "noEmit": true,
    "jsx": "react-jsx",
    "strict": true
  },
  "include": ["src"]
}
```

- [ ] **Step 3: 创建 vite.config.ts**

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

- [ ] **Step 4: 创建 index.html**

```html
<!DOCTYPE html>
<html lang="zh-CN">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>WebSocket 调试工具</title>
</head>
<body>
  <div id="root"></div>
  <script type="module" src="/src/main.tsx"></script>
</body>
</html>
```

- [ ] **Step 5: 安装依赖**

Run: `cd "D:/Workspace/ElectronApp/WebsocketTool" && npm install`

Expected: 所有依赖安装成功，无报错。

- [ ] **Step 6: Commit**

```bash
git add package.json tsconfig.json vite.config.ts index.html package-lock.json
git commit -m "chore: init project with Electron + React + Vite + Ant Design"
```

---

### Task 2: 类型定义

**Files:**
- Create: `src/types/index.ts`

- [ ] **Step 1: 创建类型定义文件**

```bash
mkdir -p "D:/Workspace/ElectronApp/WebsocketTool/src/types"
```

- [ ] **Step 2: 写入类型定义**

```typescript
// src/types/index.ts

/** 日志条目 */
export interface LogEntry {
  id: string
  timestamp: string
  type: 'send' | 'receive' | 'info' | 'error'
  content: string
}

/** 已连接的客户端信息 */
export interface ConnectedClient {
  clientId: string
  ip: string
  connectTime: string
}

/** Electron 主进程暴露的 API */
export interface ElectronAPI {
  startServer: (port: number) => Promise<{ success: boolean }>
  stopServer: () => Promise<{ success: boolean }>
  sendToClients: (clientIds: string[], message: string) => Promise<{ sent: number; total: number }>
  broadcast: (message: string) => Promise<{ sent: number; total: number }>
  onServerStarted: (callback: (data: { port: number }) => void) => void
  onServerStopped: (callback: () => void) => void
  onServerError: (callback: (data: { message: string }) => void) => void
  onClientConnected: (callback: (data: ConnectedClient) => void) => void
  onClientDisconnected: (callback: (data: { clientId: string }) => void) => void
  onMessageReceived: (callback: (data: { clientId: string; message: string }) => void) => void
  removeAllListeners: (channel: string) => void
}

declare global {
  interface Window {
    electronAPI?: ElectronAPI
  }
}

export {}
```

- [ ] **Step 3: Commit**

```bash
git add src/types/index.ts
git commit -m "feat: add type definitions for LogEntry, ConnectedClient, ElectronAPI"
```

---

### Task 3: Electron 主进程

**Files:**
- Create: `electron/main.js`
- Create: `electron/preload.js`

- [ ] **Step 1: 创建 electron 目录**

```bash
mkdir -p "D:/Workspace/ElectronApp/WebsocketTool/electron"
```

- [ ] **Step 2: 创建 electron/main.js**

```javascript
// electron/main.js
const { app, BrowserWindow, ipcMain } = require('electron')
const path = require('path')
const { WebSocketServer } = require('ws')

let mainWindow = null
let wss = null
const clients = new Map()

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

function setupIPC() {
  ipcMain.handle('start-server', async (_event, port) => {
    return new Promise((resolve, reject) => {
      try {
        wss = new WebSocketServer({ port })
        wss.on('listening', () => {
          sendToRenderer('server-started', { port })
          resolve({ success: true })
        })
        wss.on('error', (err) => {
          sendToRenderer('server-error', { message: err.message })
          reject(err)
        })
        wss.on('connection', (ws, req) => {
          const clientId = `${req.socket.remoteAddress}:${req.socket.remotePort}`
          const clientInfo = {
            ws,
            ip: req.socket.remoteAddress || 'unknown',
            connectTime: new Date().toLocaleTimeString(),
          }
          clients.set(clientId, clientInfo)

          sendToRenderer('client-connected', {
            clientId,
            ip: clientInfo.ip,
            connectTime: clientInfo.connectTime,
          })

          ws.on('message', (data) => {
            sendToRenderer('message-received', {
              clientId,
              message: data.toString(),
            })
          })

          ws.on('close', () => {
            clients.delete(clientId)
            sendToRenderer('client-disconnected', { clientId })
          })

          ws.on('error', (err) => {
            sendToRenderer('server-error', { message: `客户端 ${clientId} 错误: ${err.message}` })
          })
        })
      } catch (err) {
        reject(err)
      }
    })
  })

  ipcMain.handle('stop-server', async () => {
    if (wss) {
      // 关闭所有客户端连接
      for (const [, client] of clients) {
        client.ws.close()
      }
      clients.clear()
      wss.close()
      wss = null
      sendToRenderer('server-stopped')
    }
    return { success: true }
  })

  ipcMain.handle('send-to-clients', async (_event, { clientIds, message }) => {
    let sent = 0
    for (const clientId of clientIds) {
      const client = clients.get(clientId)
      if (client && client.ws.readyState === 1) {
        client.ws.send(message)
        sent++
      }
    }
    return { sent, total: clientIds.length }
  })

  ipcMain.handle('broadcast', async (_event, message) => {
    let sent = 0
    const total = clients.size
    for (const [, client] of clients) {
      if (client.ws.readyState === 1) {
        client.ws.send(message)
        sent++
      }
    }
    return { sent, total }
  })
}

app.whenReady().then(() => {
  setupIPC()
  createWindow()
})

app.on('window-all-closed', () => {
  if (wss) {
    for (const [, client] of clients) {
      client.ws.close()
    }
    clients.clear()
    wss.close()
  }
  app.quit()
})

app.on('activate', () => {
  if (BrowserWindow.getAllWindows().length === 0) {
    createWindow()
  }
})
```

- [ ] **Step 3: 创建 electron/preload.js**

```javascript
// electron/preload.js
const { contextBridge, ipcRenderer } = require('electron')

contextBridge.exposeInMainWorld('electronAPI', {
  startServer: (port) => ipcRenderer.invoke('start-server', port),
  stopServer: () => ipcRenderer.invoke('stop-server'),
  sendToClients: (clientIds, message) =>
    ipcRenderer.invoke('send-to-clients', { clientIds, message }),
  broadcast: (message) => ipcRenderer.invoke('broadcast', message),

  onServerStarted: (callback) =>
    ipcRenderer.on('server-started', (_event, data) => callback(data)),
  onServerStopped: (callback) =>
    ipcRenderer.on('server-stopped', () => callback()),
  onServerError: (callback) =>
    ipcRenderer.on('server-error', (_event, data) => callback(data)),
  onClientConnected: (callback) =>
    ipcRenderer.on('client-connected', (_event, data) => callback(data)),
  onClientDisconnected: (callback) =>
    ipcRenderer.on('client-disconnected', (_event, data) => callback(data)),
  onMessageReceived: (callback) =>
    ipcRenderer.on('message-received', (_event, data) => callback(data)),

  removeAllListeners: (channel) => ipcRenderer.removeAllListeners(channel),
})
```

- [ ] **Step 4: Commit**

```bash
git add electron/main.js electron/preload.js
git commit -m "feat: add Electron main process with WebSocket server and IPC"
```

---

### Task 4: React 入口 + App 布局 + 全局样式

**Files:**
- Create: `src/main.tsx`
- Create: `src/App.tsx`
- Create: `src/App.css`

- [ ] **Step 1: 创建 src 目录**

```bash
mkdir -p "D:/Workspace/ElectronApp/WebsocketTool/src/components"
mkdir -p "D:/Workspace/ElectronApp/WebsocketTool/src/hooks"
```

- [ ] **Step 2: 创建 src/main.tsx**

```tsx
// src/main.tsx
import React from 'react'
import ReactDOM from 'react-dom/client'
import App from './App'
import './App.css'

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
)
```

- [ ] **Step 3: 创建 src/App.tsx**

```tsx
// src/App.tsx
import { useState } from 'react'
import { ConfigProvider, Tabs } from 'antd'
import ClientTab from './components/ClientTab'
import ServerTab from './components/ServerTab'

const App = () => {
  const [activeTab, setActiveTab] = useState('client')

  return (
    <ConfigProvider
      theme={{
        token: {
          colorPrimary: '#1677ff',
        },
      }}
    >
      <div className="app-container">
        <Tabs
          activeKey={activeTab}
          onChange={setActiveTab}
          items={[
            { key: 'client', label: '客户端' },
            { key: 'server', label: '服务端' },
          ]}
          className="app-tabs"
        />
        <div className="tab-content">
          {activeTab === 'client' ? <ClientTab /> : <ServerTab />}
        </div>
      </div>
    </ConfigProvider>
  )
}

export default App
```

- [ ] **Step 4: 创建 src/App.css**

```css
/* src/App.css */
* {
  margin: 0;
  padding: 0;
  box-sizing: border-box;
}

.app-container {
  height: 100vh;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.app-tabs {
  padding: 0 16px;
  flex-shrink: 0;
}

.app-tabs .ant-tabs-nav {
  margin-bottom: 0;
}

.tab-content {
  flex: 1;
  overflow: hidden;
  padding: 16px;
}

/* 左右分栏布局 */
.tab-layout {
  display: flex;
  height: 100%;
  gap: 16px;
}

.tab-left {
  width: 360px;
  flex-shrink: 0;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.tab-right {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  min-width: 0;
}

/* 日志面板 */
.log-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
  border: 1px solid #d9d9d9;
  border-radius: 8px;
  overflow: hidden;
}

.log-panel-header {
  padding: 8px 12px;
  font-weight: 500;
  border-bottom: 1px solid #d9d9d9;
  background: #fafafa;
  flex-shrink: 0;
}

.log-panel-content {
  flex: 1;
  overflow-y: auto;
  padding: 8px 12px;
  font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
  font-size: 13px;
  line-height: 1.7;
  background: #fff;
}

.log-entry {
  word-break: break-all;
}

.log-time {
  color: #999;
  margin-right: 6px;
}

.log-direction {
  margin-right: 6px;
  font-weight: 500;
}

.log-send .log-direction {
  color: #1677ff;
}

.log-receive .log-direction {
  color: #52c41a;
}

.log-info .log-direction {
  color: #999;
}

.log-error .log-direction {
  color: #ff4d4f;
}

.log-message {
  color: #333;
}

.log-empty {
  color: #999;
  text-align: center;
  padding: 40px 0;
}
```

- [ ] **Step 5: 创建占位组件（确保编译通过）**

```tsx
// src/components/ClientTab.tsx (临时占位)
const ClientTab = () => <div>客户端</div>
export default ClientTab
```

```tsx
// src/components/ServerTab.tsx (临时占位)
const ServerTab = () => <div>服务端</div>
export default ServerTab
```

- [ ] **Step 6: 验证 Vite 构建正常**

Run: `cd "D:/Workspace/ElectronApp/WebsocketTool" && npx vite build`

Expected: 构建成功，`dist/` 目录生成。

- [ ] **Step 7: Commit**

```bash
git add src/main.tsx src/App.tsx src/App.css src/components/ClientTab.tsx src/components/ServerTab.tsx
git commit -m "feat: add React entry, App layout with tabs, global styles"
```

---

### Task 5: LogPanel 共用组件

**Files:**
- Create: `src/components/LogPanel.tsx`

- [ ] **Step 1: 实现 LogPanel**

```tsx
// src/components/LogPanel.tsx
import { useEffect, useRef } from 'react'
import { LogEntry } from '../types'

interface LogPanelProps {
  logs: LogEntry[]
}

const DIRECTION_LABELS: Record<string, string> = {
  send: '发送',
  receive: '接收',
  info: '信息',
  error: '错误',
}

const LogPanel = ({ logs }: LogPanelProps) => {
  const containerRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (containerRef.current) {
      containerRef.current.scrollTop = containerRef.current.scrollHeight
    }
  }, [logs])

  return (
    <div className="log-panel">
      <div className="log-panel-header">消息日志</div>
      <div className="log-panel-content" ref={containerRef}>
        {logs.map((log) => (
          <div key={log.id} className={`log-entry log-${log.type}`}>
            <span className="log-time">[{log.timestamp}]</span>
            <span className="log-direction">
              [{DIRECTION_LABELS[log.type] || log.type}]
            </span>
            <span className="log-message">{log.content}</span>
          </div>
        ))}
        {logs.length === 0 && (
          <div className="log-empty">暂无日志</div>
        )}
      </div>
    </div>
  )
}

export default LogPanel
```

- [ ] **Step 2: 验证构建**

Run: `cd "D:/Workspace/ElectronApp/WebsocketTool" && npx vite build`

Expected: 构建成功。

- [ ] **Step 3: Commit**

```bash
git add src/components/LogPanel.tsx
git commit -m "feat: add LogPanel shared component with auto-scroll"
```

---

### Task 6: useWebSocket Hook

**Files:**
- Create: `src/hooks/useWebSocket.ts`

- [ ] **Step 1: 实现 useWebSocket Hook**

```tsx
// src/hooks/useWebSocket.ts
import { useRef, useState, useCallback } from 'react'
import { LogEntry } from '../types'

const WS_URL_KEY = 'ws-client-url'

export const useWebSocket = (addLog: (entry: LogEntry) => void) => {
  const wsRef = useRef<WebSocket | null>(null)
  const [connected, setConnected] = useState(false)
  const [connecting, setConnecting] = useState(false)
  const [url, setUrl] = useState(() => {
    return localStorage.getItem(WS_URL_KEY) || 'ws://localhost:8080'
  })

  const connect = useCallback(() => {
    if (!url.startsWith('ws://') && !url.startsWith('wss://')) {
      addLog({
        id: Date.now().toString(),
        timestamp: new Date().toLocaleTimeString(),
        type: 'error',
        content: 'URL 格式不正确，需以 ws:// 或 wss:// 开头',
      })
      return
    }

    setConnecting(true)
    localStorage.setItem(WS_URL_KEY, url)

    try {
      const ws = new WebSocket(url)
      wsRef.current = ws

      ws.onopen = () => {
        setConnected(true)
        setConnecting(false)
        addLog({
          id: Date.now().toString(),
          timestamp: new Date().toLocaleTimeString(),
          type: 'info',
          content: `已连接到 ${url}`,
        })
      }

      ws.onmessage = (event) => {
        addLog({
          id: Date.now().toString(),
          timestamp: new Date().toLocaleTimeString(),
          type: 'receive',
          content: event.data,
        })
      }

      ws.onclose = (event) => {
        setConnected(false)
        setConnecting(false)
        addLog({
          id: Date.now().toString(),
          timestamp: new Date().toLocaleTimeString(),
          type: 'info',
          content: `连接已关闭: code=${event.code}${event.reason ? ', reason=' + event.reason : ''}`,
        })
      }

      ws.onerror = () => {
        addLog({
          id: Date.now().toString(),
          timestamp: new Date().toLocaleTimeString(),
          type: 'error',
          content: '连接发生错误',
        })
      }
    } catch (err: any) {
      setConnecting(false)
      addLog({
        id: Date.now().toString(),
        timestamp: new Date().toLocaleTimeString(),
        type: 'error',
        content: `连接失败: ${err.message}`,
      })
    }
  }, [url, addLog])

  const disconnect = useCallback(() => {
    if (wsRef.current) {
      wsRef.current.close(1000, '主动断开')
      wsRef.current = null
    }
  }, [])

  const send = useCallback(
    (message: string): boolean => {
      if (wsRef.current && wsRef.current.readyState === WebSocket.OPEN) {
        wsRef.current.send(message)
        addLog({
          id: Date.now().toString(),
          timestamp: new Date().toLocaleTimeString(),
          type: 'send',
          content: message,
        })
        return true
      }
      addLog({
        id: Date.now().toString(),
        timestamp: new Date().toLocaleTimeString(),
        type: 'error',
        content: '未连接，无法发送消息',
      })
      return false
    },
    [addLog]
  )

  return { url, setUrl, connected, connecting, connect, disconnect, send }
}
```

- [ ] **Step 2: 验证构建**

Run: `cd "D:/Workspace/ElectronApp/WebsocketTool" && npx vite build`

Expected: 构建成功。

- [ ] **Step 3: Commit**

```bash
git add src/hooks/useWebSocket.ts
git commit -m "feat: add useWebSocket hook for client WebSocket management"
```

---

### Task 7: ClientTab 组件

**Files:**
- Modify: `src/components/ClientTab.tsx` (替换占位组件)

- [ ] **Step 1: 实现 ClientTab**

```tsx
// src/components/ClientTab.tsx
import { useState, useCallback } from 'react'
import { Input, Button, Space, Card, Tag } from 'antd'
import LogPanel from './LogPanel'
import { useWebSocket } from '../hooks/useWebSocket'
import { LogEntry } from '../types'

const ClientTab = () => {
  const [logs, setLogs] = useState<LogEntry[]>([])
  const [message, setMessage] = useState('')

  const addLog = useCallback((entry: LogEntry) => {
    setLogs((prev) => [...prev, entry])
  }, [])

  const { url, setUrl, connected, connecting, connect, disconnect, send } =
    useWebSocket(addLog)

  const handleSend = () => {
    const trimmed = message.trim()
    if (!trimmed) return
    const ok = send(trimmed)
    if (ok) setMessage('')
  }

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      handleSend()
    }
  }

  return (
    <div className="tab-layout">
      <div className="tab-left">
        <Card title="连接配置" size="small">
          <Space direction="vertical" style={{ width: '100%' }}>
            <Input
              placeholder="ws://localhost:8080"
              value={url}
              onChange={(e) => setUrl(e.target.value)}
              disabled={connected || connecting}
            />
            <Space>
              {!connected ? (
                <Button type="primary" onClick={connect} loading={connecting}>
                  连接
                </Button>
              ) : (
                <Button danger onClick={disconnect}>
                  断开
                </Button>
              )}
              <Tag color={connected ? 'green' : connecting ? 'processing' : 'default'}>
                {connected ? '已连接' : connecting ? '连接中...' : '已断开'}
              </Tag>
            </Space>
          </Space>
        </Card>

        <Card title="发送消息" size="small">
          <Space direction="vertical" style={{ width: '100%' }}>
            <Input.TextArea
              rows={3}
              value={message}
              onChange={(e) => setMessage(e.target.value)}
              onKeyDown={handleKeyDown}
              placeholder="输入消息内容，Enter 发送"
              disabled={!connected}
            />
            <Button
              type="primary"
              onClick={handleSend}
              disabled={!connected || !message.trim()}
              block
            >
              发送
            </Button>
          </Space>
        </Card>
      </div>

      <div className="tab-right">
        <LogPanel logs={logs} />
      </div>
    </div>
  )
}

export default ClientTab
```

- [ ] **Step 2: 验证构建**

Run: `cd "D:/Workspace/ElectronApp/WebsocketTool" && npx vite build`

Expected: 构建成功，无 TypeScript 错误。

- [ ] **Step 3: Commit**

```bash
git add src/components/ClientTab.tsx
git commit -m "feat: implement ClientTab with connect/disconnect/send/log"
```

---

### Task 8: ServerTab 组件

**Files:**
- Modify: `src/components/ServerTab.tsx` (替换占位组件)

- [ ] **Step 1: 实现 ServerTab**

```tsx
// src/components/ServerTab.tsx
import { useState, useEffect, useCallback } from 'react'
import { Input, Button, Space, Card, Tag, Table, InputNumber } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import LogPanel from './LogPanel'
import { LogEntry, ConnectedClient } from '../types'

const PORT_KEY = 'ws-server-port'

const ServerTab = () => {
  const [logs, setLogs] = useState<LogEntry[]>([])
  const [message, setMessage] = useState('')
  const [running, setRunning] = useState(false)
  const [port, setPort] = useState<number>(() => {
    const saved = localStorage.getItem(PORT_KEY)
    return saved ? parseInt(saved, 10) : 8080
  })
  const [clients, setClients] = useState<ConnectedClient[]>([])
  const [selectedRowKeys, setSelectedRowKeys] = useState<React.Key[]>([])

  const addLog = useCallback((entry: LogEntry) => {
    setLogs((prev) => [...prev, entry])
  }, [])

  // 设置 IPC 监听器
  useEffect(() => {
    const api = window.electronAPI
    if (!api) return

    api.onServerStarted((data) => {
      setRunning(true)
      addLog({
        id: Date.now().toString(),
        timestamp: new Date().toLocaleTimeString(),
        type: 'info',
        content: `服务已启动，监听端口: ${data.port}`,
      })
    })

    api.onServerStopped(() => {
      setRunning(false)
      setClients([])
      setSelectedRowKeys([])
      addLog({
        id: Date.now().toString(),
        timestamp: new Date().toLocaleTimeString(),
        type: 'info',
        content: '服务已停止',
      })
    })

    api.onServerError((data) => {
      addLog({
        id: Date.now().toString(),
        timestamp: new Date().toLocaleTimeString(),
        type: 'error',
        content: `服务错误: ${data.message}`,
      })
    })

    api.onClientConnected((data) => {
      setClients((prev) => [...prev, data])
      addLog({
        id: Date.now().toString(),
        timestamp: new Date().toLocaleTimeString(),
        type: 'info',
        content: `客户端已连接: ${data.clientId}`,
      })
    })

    api.onClientDisconnected((data) => {
      setClients((prev) => prev.filter((c) => c.clientId !== data.clientId))
      setSelectedRowKeys((prev) => prev.filter((k) => k !== data.clientId))
      addLog({
        id: Date.now().toString(),
        timestamp: new Date().toLocaleTimeString(),
        type: 'info',
        content: `客户端已断开: ${data.clientId}`,
      })
    })

    api.onMessageReceived((data) => {
      addLog({
        id: Date.now().toString(),
        timestamp: new Date().toLocaleTimeString(),
        type: 'receive',
        content: `[${data.clientId}] ${data.message}`,
      })
    })

    return () => {
      api.removeAllListeners('server-started')
      api.removeAllListeners('server-stopped')
      api.removeAllListeners('server-error')
      api.removeAllListeners('client-connected')
      api.removeAllListeners('client-disconnected')
      api.removeAllListeners('message-received')
    }
  }, [addLog])

  const handleStart = async () => {
    localStorage.setItem(PORT_KEY, port.toString())
    const api = window.electronAPI
    if (!api) return
    try {
      await api.startServer(port)
    } catch (err: any) {
      addLog({
        id: Date.now().toString(),
        timestamp: new Date().toLocaleTimeString(),
        type: 'error',
        content: `启动失败: ${err.message}`,
      })
    }
  }

  const handleStop = async () => {
    await window.electronAPI?.stopServer()
  }

  const handleSend = async () => {
    const trimmed = message.trim()
    if (!trimmed || selectedRowKeys.length === 0) return
    const api = window.electronAPI
    if (!api) return
    const result = await api.sendToClients(selectedRowKeys as string[], trimmed)
    addLog({
      id: Date.now().toString(),
      timestamp: new Date().toLocaleTimeString(),
      type: 'send',
      content: `[→ 选中 ${result.sent}/${result.total}] ${trimmed}`,
    })
    setMessage('')
  }

  const handleBroadcast = async () => {
    const trimmed = message.trim()
    if (!trimmed) return
    const api = window.electronAPI
    if (!api) return
    const result = await api.broadcast(trimmed)
    addLog({
      id: Date.now().toString(),
      timestamp: new Date().toLocaleTimeString(),
      type: 'send',
      content: `[广播 → ${result.sent}/${result.total}] ${trimmed}`,
    })
    setMessage('')
  }

  const columns: ColumnsType<ConnectedClient> = [
    { title: '客户端 ID', dataIndex: 'clientId', key: 'clientId', ellipsis: true },
    { title: 'IP 地址', dataIndex: 'ip', key: 'ip', width: 130 },
    { title: '连接时间', dataIndex: 'connectTime', key: 'connectTime', width: 90 },
  ]

  return (
    <div className="tab-layout">
      <div className="tab-left">
        <Card title="服务配置" size="small">
          <Space direction="vertical" style={{ width: '100%' }}>
            <Space>
              <span>端口:</span>
              <InputNumber
                min={1}
                max={65535}
                value={port}
                onChange={(v) => setPort(v || 8080)}
                disabled={running}
                style={{ width: 120 }}
              />
            </Space>
            <Space>
              {!running ? (
                <Button type="primary" onClick={handleStart}>
                  启动
                </Button>
              ) : (
                <Button danger onClick={handleStop}>
                  停止
                </Button>
              )}
              <Tag color={running ? 'green' : 'default'}>
                {running ? '运行中' : '已停止'}
              </Tag>
            </Space>
          </Space>
        </Card>

        <Card title="已连接客户端" size="small">
          <Table
            dataSource={clients}
            columns={columns}
            rowKey="clientId"
            size="small"
            rowSelection={{
              selectedRowKeys,
              onChange: (keys) => setSelectedRowKeys(keys),
              selections: [
                {
                  key: 'all',
                  text: '全选',
                  onSelect: () =>
                    setSelectedRowKeys(clients.map((c) => c.clientId)),
                },
                {
                  key: 'none',
                  text: '取消全选',
                  onSelect: () => setSelectedRowKeys([]),
                },
              ],
            }}
            locale={{ emptyText: '暂无连接' }}
            pagination={false}
            scroll={{ y: 200 }}
          />
        </Card>

        <Card title="发送消息" size="small">
          <Space direction="vertical" style={{ width: '100%' }}>
            <Input.TextArea
              rows={3}
              value={message}
              onChange={(e) => setMessage(e.target.value)}
              placeholder="输入消息内容"
              disabled={!running}
            />
            <Space>
              <Button
                type="primary"
                onClick={handleSend}
                disabled={!running || selectedRowKeys.length === 0 || !message.trim()}
              >
                发送
              </Button>
              <Button
                onClick={handleBroadcast}
                disabled={!running || clients.length === 0 || !message.trim()}
              >
                广播
              </Button>
            </Space>
          </Space>
        </Card>
      </div>

      <div className="tab-right">
        <LogPanel logs={logs} />
      </div>
    </div>
  )
}

export default ServerTab
```

- [ ] **Step 2: 验证构建**

Run: `cd "D:/Workspace/ElectronApp/WebsocketTool" && npx vite build`

Expected: 构建成功，无 TypeScript 错误。

- [ ] **Step 3: Commit**

```bash
git add src/components/ServerTab.tsx
git commit -m "feat: implement ServerTab with start/stop, client list, send/broadcast"
```

---

### Task 9: 集成验证

- [ ] **Step 1: 验证 Vite 生产构建**

Run: `cd "D:/Workspace/ElectronApp/WebsocketTool" && npx vite build`

Expected: 构建成功，`dist/` 目录包含 `index.html` 和静态资源。

- [ ] **Step 2: 验证 Electron 启动**

Run: `cd "D:/Workspace/ElectronApp/WebsocketTool" && npx electron .`

Expected: Electron 窗口打开，显示 WebSocket 调试工具界面。由于 `dist/` 目录已有构建产物，应直接加载。

- [ ] **Step 3: 手动功能验证清单**

- [ ] 客户端 Tab：输入 URL → 点击连接 → 状态显示"已连接"
- [ ] 客户端 Tab：输入消息 → 点击发送 → 日志显示发送记录
- [ ] 客户端 Tab：断开连接 → 日志显示断开
- [ ] 服务端 Tab：输入端口 → 点击启动 → 状态显示"运行中"
- [ ] 服务端 Tab：客户端连接 → 列表显示客户端 → 日志显示连接
- [ ] 服务端 Tab：选中客户端 → 发送消息 → 日志显示发送
- [ ] 服务端 Tab：点击广播 → 日志显示广播
- [ ] 服务端 Tab：客户端断开 → 列表移除 → 日志显示断开
- [ ] 持久化：关闭重开后 URL 和端口号保留

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "chore: final integration verification"
```

---

## 实现顺序

```
Task 1 (项目初始化)
  └─> Task 2 (类型定义)
        └─> Task 3 (Electron 主进程)
              └─> Task 4 (React 入口 + 布局)
                    ├─> Task 5 (LogPanel)
                    ├─> Task 6 (useWebSocket Hook)
                    │     └─> Task 7 (ClientTab)
                    └─> Task 8 (ServerTab)
                          └─> Task 9 (集成验证)
```

Task 5 和 Task 6 可以并行进行，Task 7 依赖 Task 6，Task 8 不依赖 Task 6/7。