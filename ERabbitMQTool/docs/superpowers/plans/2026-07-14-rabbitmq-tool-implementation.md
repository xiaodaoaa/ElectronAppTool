# ERabbitMQTool 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 构建 RabbitMQ 桌面调试工具，支持连接 broker、发送消息（到 exchange/queue）、订阅 queue 消费消息

**Architecture:** Electron 22.3.27 三进程模型（Main/Preload/Renderer），amqplib 运行在主进程，单连接单 channel 共享给生产者和消费者。配置持久化到 userData/config.json（300ms 防抖）。

**Tech Stack:** Electron 22.3.27, React 18, TypeScript, Ant Design 5, Vite 6, amqplib

---

### Task 1: 项目脚手架

**Files:**
- Create: `package.json`
- Create: `vite.config.ts`
- Create: `tsconfig.json`
- Create: `index.html`
- Create: `electron/main.js`（最小骨架）
- Create: `electron/preload.js`（最小骨架）
- Create: `src/main.tsx`（最小入口）
- Create: `src/App.tsx`（最小组件）
- Create: `src/App.css`（空文件占位）
- Create: `src/vite-env.d.ts`

- [ ] **Step 1: 创建 package.json**

```json
{
    "name": "ERabbitMQTool",
    "version": "1.0.0",
    "private": true,
    "author": "RabbitMQ Tool",
    "description": "RabbitMQ 调试工具",
    "main": "electron/main.js",
    "build": {
        "appId": "com.rabbitmq-tool.app",
        "productName": "RabbitMQ调试工具",
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
            "shortcutName": "RabbitMQ调试工具"
        }
    },
    "scripts": {
        "dev": "concurrently \"vite\" \"wait-on http://localhost:5173 && cross-env VITE_DEV_SERVER_URL=http://localhost:5173 electron .\"",
        "build": "vite build",
        "preview": "vite preview",
        "pack": "vite build && electron-builder --win -c.electronDist=\"node_modules/electron/dist\" -c.electronVersion=\"22.3.27\""
    },
    "dependencies": {
        "amqplib": "^0.10.5",
        "antd": "^5.22.0",
        "react": "^18.3.0",
        "react-dom": "^18.3.0"
    },
    "devDependencies": {
        "@types/react": "^18.3.0",
        "@types/react-dom": "^18.3.0",
        "@types/amqplib": "^0.10.5",
        "@vitejs/plugin-react": "^4.3.0",
        "concurrently": "^9.1.0",
        "cross-env": "^7.0.3",
        "electron": "^22.3.27",
        "electron-builder": "^26.15.3",
        "typescript": "^5.6.0",
        "vite": "^6.0.0",
        "wait-on": "^8.0.0"
    }
}
```

- [ ] **Step 2: 创建 vite.config.ts**

```ts
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  base: './',
  build: {
    outDir: 'dist',
    target: 'chrome108',
  },
  server: {
    port: 5173,
    strictPort: true,
  },
})
```

- [ ] **Step 3: 创建 tsconfig.json**

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

- [ ] **Step 4: 创建 index.html**

```html
<!DOCTYPE html>
<html lang="zh-CN">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>RabbitMQ 调试工具</title>
</head>
<body>
  <div id="root"></div>
  <script type="module" src="/src/main.tsx"></script>
</body>
</html>
```

- [ ] **Step 5: 创建 src/vite-env.d.ts**

```ts
/// <reference types="vite/client" />
```

- [ ] **Step 6: 创建最小 electron/main.js**

```js
const { app, BrowserWindow, ipcMain } = require('electron')
const path = require('path')

let mainWindow = null

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
  // 后续任务逐步添加
}

app.whenReady().then(() => {
  setupIPC()
  createWindow()
})

app.on('window-all-closed', () => {
  app.quit()
})

app.on('activate', () => {
  if (BrowserWindow.getAllWindows().length === 0) {
    createWindow()
  }
})
```

- [ ] **Step 7: 创建最小 electron/preload.js**

```js
const { contextBridge, ipcRenderer } = require('electron')

contextBridge.exposeInMainWorld('electronAPI', {
  removeAllListeners: (channel) => ipcRenderer.removeAllListeners(channel),
})
```

- [ ] **Step 8: 创建 src/main.tsx**

```tsx
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

- [ ] **Step 9: 创建 src/App.tsx**

```tsx
const App = () => {
  return <div>ERabbitMQTool</div>
}

export default App
```

- [ ] **Step 10: 创建 src/App.css（空）**

- [ ] **Step 11: 安装依赖并验证**

```bash
cd D:\Workspace\ElectronApp\ElectronAppTool\ERabbitMQTool
npm install
```

Expected: 无报错，node_modules 生成

- [ ] **Step 12: 验证 Vite 构建**

```bash
npm run build
```

Expected: dist/ 目录生成，包含 index.html + 静态资源

---

### Task 2: 共享类型定义

**Files:**
- Create: `src/types/index.ts`

- [ ] **Step 1: 创建类型文件**

```ts
// src/types/index.ts

export interface ConnectionConfig {
  host: string
  port: number
  vhost: string
  username: string
  password: string
}

export interface ServerInfo {
  rabbitmqVersion?: string
  platform?: string
  host: string
  port: number
  vhost: string
}

export interface PublishTarget {
  target: 'exchange' | 'queue'
  exchange?: string
  routingKey?: string
  queue?: string
  message: string
  properties: MessageProperties
}

export interface MessageProperties {
  persistent: boolean
  contentType: string
  priority: number
  messageId?: string
  replyTo?: string
  headers: Record<string, string>
}

export interface ReceivedMessage {
  queue: string
  message: string
  properties: Record<string, unknown>
  consumerTag: string
  timestamp: string
}

export interface LogEntry {
  time: string
  type: 'connect' | 'disconnect' | 'send' | 'receive' | 'subscribe' | 'error'
  detail: string
}

export interface ElectronAPI {
  connect: (config: ConnectionConfig) => Promise<{ success: boolean; serverInfo?: ServerInfo }>
  disconnect: () => Promise<{ success: boolean }>
  publish: (target: PublishTarget) => Promise<{ success: boolean }>
  subscribe: (queue: string) => Promise<{ success: boolean; consumerTag?: string }>
  unsubscribe: (consumerTag: string) => Promise<{ success: boolean }>
  saveConfig: (config: ConnectionConfig & { producer?: Record<string, unknown>; consumerQueue?: string }) => Promise<{ success: boolean }>
  loadConfig: () => Promise<{ success: boolean; config?: Record<string, unknown> | null }>

  onConnected: (callback: (data: { serverInfo: ServerInfo }) => void) => () => void
  onDisconnected: (callback: (data: { reason: string }) => void) => () => void
  onConnectionError: (callback: (data: { message: string }) => void) => () => void
  onMessageReceived: (callback: (data: ReceivedMessage) => void) => () => void
  onPublishConfirmed: (callback: (data: { success: boolean; message?: string }) => void) => () => void

  removeAllListeners: (channel: string) => void
}
```

---

### Task 3: Preload — 完整 API

**Files:**
- Modify: `electron/preload.js`

- [ ] **Step 1: 替换 preload.js 内容**

```js
const { contextBridge, ipcRenderer } = require('electron')

contextBridge.exposeInMainWorld('electronAPI', {
  connect: (config) => ipcRenderer.invoke('connect', config),
  disconnect: () => ipcRenderer.invoke('disconnect'),
  publish: (target) => ipcRenderer.invoke('publish', target),
  subscribe: (queue) => ipcRenderer.invoke('subscribe', queue),
  unsubscribe: (consumerTag) => ipcRenderer.invoke('unsubscribe', consumerTag),
  saveConfig: (config) => ipcRenderer.invoke('save-config', config),
  loadConfig: () => ipcRenderer.invoke('load-config'),

  onConnected: (callback) => {
    const handler = (_event, data) => callback(data)
    ipcRenderer.on('connected', handler)
    return () => ipcRenderer.removeListener('connected', handler)
  },
  onDisconnected: (callback) => {
    const handler = (_event, data) => callback(data)
    ipcRenderer.on('disconnected', handler)
    return () => ipcRenderer.removeListener('disconnected', handler)
  },
  onConnectionError: (callback) => {
    const handler = (_event, data) => callback(data)
    ipcRenderer.on('connection-error', handler)
    return () => ipcRenderer.removeListener('connection-error', handler)
  },
  onMessageReceived: (callback) => {
    const handler = (_event, data) => callback(data)
    ipcRenderer.on('message-received', handler)
    return () => ipcRenderer.removeListener('message-received', handler)
  },
  onPublishConfirmed: (callback) => {
    const handler = (_event, data) => callback(data)
    ipcRenderer.on('publish-confirmed', handler)
    return () => ipcRenderer.removeListener('publish-confirmed', handler)
  },

  removeAllListeners: (channel) => ipcRenderer.removeAllListeners(channel),
})
```

---

### Task 4: 主进程 — 连接管理

**Files:**
- Modify: `electron/main.js`

- [ ] **Step 1: 添加 amqplib 引入和状态变量**

在 `const path = require('path')` 之后添加：

```js
const amqplib = require('amqplib')

let connection = null
let channel = null
const consumerTags = new Set()
let isConnecting = false
```

- [ ] **Step 2: 添加 cleanUp 函数**

在 `sendToRenderer` 函数之后添加：

```js
async function cleanUp() {
  for (const tag of consumerTags) {
    try {
      if (channel) await channel.cancel(tag)
    } catch (_) {}
  }
  consumerTags.clear()
  try {
    if (channel) await channel.close()
  } catch (_) {}
  try {
    if (connection) await connection.close()
  } catch (_) {}
  channel = null
  connection = null
  isConnecting = false
}
```

- [ ] **Step 3: 添加 connect/disconnect IPC handlers**

替换 `function setupIPC()` 中的内容：

```js
function setupIPC() {
  ipcMain.handle('connect', async (_event, config) => {
    if (isConnecting) return { success: false }
    if (connection) await cleanUp()
    isConnecting = true

    try {
      const url = `amqp://${config.username}:${config.password}@${config.host}:${config.port}${config.vhost}`
      connection = await amqplib.connect(url)

      connection.on('error', (err) => {
        sendToRenderer('connection-error', { message: err.message })
        cleanUp().then(() => sendToRenderer('disconnected', { reason: err.message }))
      })

      connection.on('close', () => {
        if (connection) {
          cleanUp().then(() => sendToRenderer('disconnected', { reason: '连接已关闭' }))
        }
      })

      channel = await connection.createChannel()

      connection.on('error', () => {}) // 防止重复错误
      channel.on('error', () => {})    // 防止重复错误

      isConnecting = false

      const serverInfo = {
        host: config.host,
        port: config.port,
        vhost: config.vhost,
      }

      sendToRenderer('connected', { serverInfo })
      return { success: true, serverInfo }
    } catch (err) {
      isConnecting = false
      await cleanUp()
      const message = err.message || String(err)
      sendToRenderer('connection-error', { message })
      return { success: false }
    }
  })

  ipcMain.handle('disconnect', async () => {
    sendToRenderer('disconnected', { reason: '主动断开' })
    await cleanUp()
    return { success: true }
  })
}
```

- [ ] **Step 4: window-all-closed 添加清理**

将 `app.on('window-all-closed')` 替换为：

```js
app.on('window-all-closed', async () => {
  await cleanUp()
  app.quit()
})
```

---

### Task 5: 主进程 — 发布与订阅

**Files:**
- Modify: `electron/main.js`

- [ ] **Step 1: 在 setupIPC 中添加 publish handler**

在 `ipcMain.handle('disconnect', ...)` 之后添加：

```js
  ipcMain.handle('publish', async (_event, target) => {
    if (!channel) {
      sendToRenderer('publish-confirmed', { success: false, message: '未连接' })
      return { success: false }
    }

    try {
      const props = {
        persistent: target.properties.persistent,
        contentType: target.properties.contentType || 'text/plain',
        priority: target.properties.priority || 0,
        headers: target.properties.headers || {},
      }
      if (target.properties.messageId) props.messageId = target.properties.messageId
      if (target.properties.replyTo) props.replyTo = target.properties.replyTo

      let exchange = ''
      let routingKey = ''
      let displayTarget = ''

      if (target.target === 'exchange') {
        exchange = target.exchange || ''
        routingKey = target.routingKey || ''
        displayTarget = `exchange ${exchange}`
      } else {
        exchange = ''
        routingKey = target.queue || ''
        displayTarget = `queue ${target.queue}`
      }

      const buf = Buffer.from(target.message || '', 'utf-8')
      channel.publish(exchange, routingKey, buf, props)

      sendToRenderer('publish-confirmed', { success: true })
      const summary = target.message.length > 50 ? target.message.slice(0, 50) + '...' : target.message
      sendToRenderer('log-event', { type: 'send', detail: `[→${displayTarget}] ${summary}` })

      return { success: true }
    } catch (err) {
      sendToRenderer('publish-confirmed', { success: false, message: err.message })
      return { success: false }
    }
  })
```

- [ ] **Step 2: 在 setupIPC 中添加 subscribe/unsubscribe handler**

在 `ipcMain.handle('publish', ...)` 之后添加：

```js
  ipcMain.handle('subscribe', async (_event, queue) => {
    if (!channel) return { success: false }
    if (!queue || !queue.trim()) return { success: false }

    try {
      const consumeResult = await channel.consume(queue, (msg) => {
        if (!msg) return
        const received = {
          queue,
          message: msg.content.toString('utf-8'),
          properties: {
            contentType: msg.properties.contentType,
            priority: msg.properties.priority,
            messageId: msg.properties.messageId,
            replyTo: msg.properties.replyTo,
            headers: msg.properties.headers,
            deliveryTag: msg.fields.deliveryTag,
          },
          consumerTag: msg.fields.consumerTag,
          timestamp: new Date().toISOString(),
        }
        channel.ack(msg)
        sendToRenderer('message-received', received)
        const summary = received.message.length > 50 ? received.message.slice(0, 50) + '...' : received.message
        sendToRenderer('log-event', { type: 'receive', detail: `[←queue ${queue}] ${summary}` })
      }, { noAck: false })

      consumerTags.add(consumeResult.consumerTag)
      sendToRenderer('log-event', { type: 'subscribe', detail: `queue=${queue} consumerTag=${consumeResult.consumerTag}` })

      return { success: true, consumerTag: consumeResult.consumerTag }
    } catch (err) {
      sendToRenderer('log-event', { type: 'error', detail: `订阅失败: ${err.message}` })
      return { success: false }
    }
  })

  ipcMain.handle('unsubscribe', async (_event, consumerTag) => {
    if (!channel) return { success: false }

    try {
      await channel.cancel(consumerTag)
      consumerTags.delete(consumerTag)
      sendToRenderer('log-event', { type: 'subscribe', detail: `已取消 consumerTag=${consumerTag}` })
      return { success: true }
    } catch (err) {
      return { success: false }
    }
  })
```

---

### Task 6: 主进程 — 配置持久化

**Files:**
- Modify: `electron/main.js`

- [ ] **Step 1: 在文件顶部添加 fs/path 引入**

在 `const path = require('path')` 之后添加：

```js
const fs = require('fs')
```

- [ ] **Step 2: 在 setupIPC 中添加 save-config/load-config handler**

在 `ipcMain.handle('unsubscribe', ...)` 之后添加：

```js
  ipcMain.handle('save-config', async (_event, config) => {
    try {
      const configPath = path.join(app.getPath('userData'), 'config.json')
      fs.writeFileSync(configPath, JSON.stringify(config, null, 2), 'utf-8')
      return { success: true }
    } catch (err) {
      return { success: false }
    }
  })

  ipcMain.handle('load-config', async () => {
    try {
      const configPath = path.join(app.getPath('userData'), 'config.json')
      if (!fs.existsSync(configPath)) {
        return { success: true, config: null }
      }
      const data = fs.readFileSync(configPath, 'utf-8')
      return { success: true, config: JSON.parse(data) }
    } catch (err) {
      return { success: true, config: null }
    }
  })
```

---

### Task 7: 渲染进程 — useRabbitMQ Hook

**Files:**
- Create: `src/hooks/useRabbitMQ.ts`
- Create: `src/hooks/useConfig.ts`

- [ ] **Step 1: 创建 useRabbitMQ hook**

```ts
// src/hooks/useRabbitMQ.ts
import { useEffect, useRef, useCallback } from 'react'
import type { ConnectionConfig, ServerInfo, ReceivedMessage, LogEntry, PublishTarget } from '../types'

interface UseRabbitMQCallbacks {
  onConnected?: (info: ServerInfo) => void
  onDisconnected?: (reason: string) => void
  onConnectionError?: (message: string) => void
  onMessageReceived?: (msg: ReceivedMessage) => void
  onPublishConfirmed?: (result: { success: boolean; message?: string }) => void
  onLogEvent?: (entry: LogEntry) => void
}

export function useRabbitMQ(callbacks: UseRabbitMQCallbacks) {
  const unsubsRef = useRef<(() => void)[]>([])

  useEffect(() => {
    const api = (window as any).electronAPI
    if (!api) return

    const unsubs: (() => void)[] = [
      api.onConnected((data: { serverInfo: ServerInfo }) => {
        callbacks.onConnected?.(data.serverInfo)
      }),
      api.onDisconnected((data: { reason: string }) => {
        callbacks.onDisconnected?.(data.reason)
      }),
      api.onConnectionError((data: { message: string }) => {
        callbacks.onConnectionError?.(data.message)
      }),
      api.onMessageReceived((msg: ReceivedMessage) => {
        callbacks.onMessageReceived?.(msg)
      }),
      api.onPublishConfirmed((result: { success: boolean; message?: string }) => {
        callbacks.onPublishConfirmed?.(result)
      }),
    ]

    unsubsRef.current = unsubs

    return () => {
      unsubs.forEach((fn) => fn())
      unsubsRef.current = []
    }
  }, [])

  const connect = useCallback(async (config: ConnectionConfig) => {
    const api = (window as any).electronAPI
    return api.connect(config)
  }, [])

  const disconnect = useCallback(async () => {
    const api = (window as any).electronAPI
    return api.disconnect()
  }, [])

  const publish = useCallback(async (target: PublishTarget) => {
    const api = (window as any).electronAPI
    return api.publish(target)
  }, [])

  const subscribe = useCallback(async (queue: string) => {
    const api = (window as any).electronAPI
    return api.subscribe(queue)
  }, [])

  const unsubscribe = useCallback(async (consumerTag: string) => {
    const api = (window as any).electronAPI
    return api.unsubscribe(consumerTag)
  }, [])

  return { connect, disconnect, publish, subscribe, unsubscribe }
}
```

- [ ] **Step 2: 创建 useConfig hook**

```ts
// src/hooks/useConfig.ts
import { useState, useCallback, useRef, useEffect } from 'react'
import type { ConnectionConfig } from '../types'

const DEFAULT_CONFIG: ConnectionConfig = {
  host: '127.0.0.1',
  port: 5672,
  vhost: '/',
  username: 'guest',
  password: 'guest',
}

export function useConfig() {
  const [config, setConfig] = useState<ConnectionConfig>(DEFAULT_CONFIG)
  const [loaded, setLoaded] = useState(false)
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  useEffect(() => {
    const api = (window as any).electronAPI
    api.loadConfig().then((result: any) => {
      if (result.success && result.config) {
        setConfig((prev) => ({ ...prev, ...result.config }))
      }
      setLoaded(true)
    })
  }, [])

  const saveConfig = useCallback((newConfig: ConnectionConfig) => {
    if (debounceRef.current) clearTimeout(debounceRef.current)
    debounceRef.current = setTimeout(() => {
      const api = (window as any).electronAPI
      api.saveConfig(newConfig)
    }, 300)
  }, [])

  const updateConfig = useCallback((partial: Partial<ConnectionConfig>) => {
    setConfig((prev) => {
      const next = { ...prev, ...partial }
      saveConfig(next)
      return next
    })
  }, [saveConfig])

  return { config, updateConfig, loaded }
}
```

---

### Task 8: 渲染进程 — 类型声明增强 + App Shell + ConnectionPanel

**Files:**
- Modify: `src/App.tsx`
- Modify: `src/App.css`
- Create: `src/components/ConnectionPanel.tsx`

- [ ] **Step 1: 创建 ConnectionPanel 组件**

```tsx
// src/components/ConnectionPanel.tsx
import { Input, Button, Tag, Space, Form } from 'antd'
import type { ConnectionConfig } from '../types'

interface ConnectionPanelProps {
  config: ConnectionConfig
  onConfigChange: (partial: Partial<ConnectionConfig>) => void
  connected: boolean
  connecting: boolean
  onConnect: () => void
  onDisconnect: () => void
  serverLabel: string
}

const formItemLayout = {
  labelCol: { style: { width: 50 } },
  wrapperCol: { style: { flex: 1 } },
}

const ConnectionPanel: React.FC<ConnectionPanelProps> = ({
  config, onConfigChange, connected, connecting, onConnect, onDisconnect, serverLabel,
}) => {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap', padding: '8px 0' }}>
      <Space size={4} wrap>
        <Form.Item label="Host" {...formItemLayout} style={{ marginBottom: 0 }}>
          <Input
            size="small"
            style={{ width: 120 }}
            value={config.host}
            onChange={(e) => onConfigChange({ host: e.target.value })}
            disabled={connected || connecting}
          />
        </Form.Item>
        <Form.Item label="Port" {...formItemLayout} style={{ marginBottom: 0 }}>
          <Input
            size="small"
            style={{ width: 80 }}
            value={config.port}
            type="number"
            onChange={(e) => onConfigChange({ port: Number(e.target.value) })}
            disabled={connected || connecting}
          />
        </Form.Item>
        <Form.Item label="VHost" {...formItemLayout} style={{ marginBottom: 0 }}>
          <Input
            size="small"
            style={{ width: 100 }}
            value={config.vhost}
            onChange={(e) => onConfigChange({ vhost: e.target.value })}
            disabled={connected || connecting}
          />
        </Form.Item>
        <Form.Item label="用户" {...formItemLayout} style={{ marginBottom: 0 }}>
          <Input
            size="small"
            style={{ width: 90 }}
            value={config.username}
            onChange={(e) => onConfigChange({ username: e.target.value })}
            disabled={connected || connecting}
          />
        </Form.Item>
        <Input.Password
          size="small"
          style={{ width: 90 }}
          value={config.password}
          onChange={(e) => onConfigChange({ password: e.target.value })}
          disabled={connected || connecting}
          placeholder="密码"
        />
      </Space>

      <Space size={4}>
        {!connected ? (
          <Button type="primary" size="small" loading={connecting} onClick={onConnect} disabled={connecting}>
            连接
          </Button>
        ) : (
          <Button danger size="small" onClick={onDisconnect}>
            断开
          </Button>
        )}
        {connecting && <Tag color="processing">连接中</Tag>}
        {connected && <Tag color="success">已连接 {serverLabel}</Tag>}
        {!connected && !connecting && <Tag>未连接</Tag>}
      </Space>
    </div>
  )
}

export default ConnectionPanel
```

- [ ] **Step 2: 更新 src/App.tsx**

```tsx
import { useState, useCallback, useEffect } from 'react'
import { ConfigProvider, Tabs } from 'antd'
import type { TabsProps } from 'antd'
import ConnectionPanel from './components/ConnectionPanel'
import ProducerTab from './components/ProducerTab'
import ConsumerTab from './components/ConsumerTab'
import LogPanel from './components/LogPanel'
import { useRabbitMQ } from './hooks/useRabbitMQ'
import { useConfig } from './hooks/useConfig'
import type { ServerInfo, ReceivedMessage, LogEntry } from './types'

const App: React.FC = () => {
  const { config, updateConfig, loaded } = useConfig()
  const [connected, setConnected] = useState(false)
  const [connecting, setConnecting] = useState(false)
  const [serverInfo, setServerInfo] = useState<ServerInfo | null>(null)
  const [logs, setLogs] = useState<LogEntry[]>([])
  const [messages, setMessages] = useState<ReceivedMessage[]>([])

  const addLog = useCallback((entry: LogEntry) => {
    setLogs((prev) => [...prev, entry])
  }, [])

  const onConnected = useCallback((info: ServerInfo) => {
    setConnected(true)
    setConnecting(false)
    setServerInfo(info)
    addLog({ time: new Date().toLocaleTimeString(), type: 'connect', detail: `已连接到 amqp://${info.host}:${info.port}${info.vhost}` })
  }, [addLog])

  const onDisconnected = useCallback((reason: string) => {
    setConnected(false)
    setConnecting(false)
    setServerInfo(null)
    addLog({ time: new Date().toLocaleTimeString(), type: 'disconnect', detail: reason })
  }, [addLog])

  const onConnectionError = useCallback((message: string) => {
    setConnecting(false)
    addLog({ time: new Date().toLocaleTimeString(), type: 'error', detail: `连接失败: ${message}` })
  }, [addLog])

  const onMessageReceived = useCallback((msg: ReceivedMessage) => {
    setMessages((prev) => [...prev, msg])
  }, [])

  const onLogEvent = useCallback((entry: LogEntry) => {
    addLog(entry)
  }, [addLog])

  const { connect, disconnect, publish, subscribe, unsubscribe } = useRabbitMQ({
    onConnected, onDisconnected, onConnectionError, onMessageReceived, onLogEvent,
  })

  const handleConnect = useCallback(async () => {
    setConnecting(true)
    await connect(config)
  }, [connect, config])

  const handleDisconnect = useCallback(async () => {
    await disconnect()
  }, [disconnect])

  const serverLabel = serverInfo ? `${serverInfo.host}:${serverInfo.port}` : ''

  const tabItems: TabsProps['items'] = [
    {
      key: 'producer',
      label: '生产者',
      children: (
        <ProducerTab
          connected={connected}
          onPublish={publish}
        />
      ),
    },
    {
      key: 'consumer',
      label: '消费者',
      children: (
        <ConsumerTab
          connected={connected}
          messages={messages}
          onSubscribe={subscribe}
          onUnsubscribe={unsubscribe}
        />
      ),
    },
  ]

  return (
    <ConfigProvider
      theme={{
        token: { colorPrimary: '#1677ff' },
      }}
    >
      <div className="app-container">
        <div style={{ padding: '0 16px', borderBottom: '1px solid #d9d9d9' }}>
          <Tabs items={tabItems} className="app-tabs" />
          {loaded && (
            <ConnectionPanel
              config={config}
              onConfigChange={updateConfig}
              connected={connected}
              connecting={connecting}
              onConnect={handleConnect}
              onDisconnect={handleDisconnect}
              serverLabel={serverLabel}
            />
          )}
        </div>
        <div className="tab-content">
          <div className="tab-layout">
            <div className="tab-left">
              {/* 子 Tab 的内容在此渲染 */}
              <Tabs items={tabItems} tabBarStyle={{ display: 'none' }} />
            </div>
            <div className="tab-right">
              <LogPanel logs={logs} />
            </div>
          </div>
        </div>
      </div>
    </ConfigProvider>
  )
}

export default App
```

- [ ] **Step 3: 替换 src/App.css**

```css
.app-container {
  box-sizing: border-box;
}

.app-container *,
.app-container *::before,
.app-container *::after {
  box-sizing: border-box;
}

.app-container {
  height: 100vh;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.app-tabs {
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

.tab-layout {
  display: flex;
  height: 100%;
  gap: 16px;
}

.tab-left {
  width: 380px;
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

---

### Task 9: 渲染进程 — ProducerTab + LogPanel

**Files:**
- Create: `src/components/ProducerTab.tsx`
- Create: `src/components/LogPanel.tsx`

- [ ] **Step 1: 创建 ProducerTab**

```tsx
// src/components/ProducerTab.tsx
import { useState, useCallback } from 'react'
import { Radio, Input, Button, Switch, Select, Space, Divider, Typography } from 'antd'
import { MinusCircleOutlined, PlusOutlined } from '@ant-design/icons'
import type { PublishTarget, MessageProperties } from '../types'

const { TextArea } = Input
const { Text } = Typography

interface ProducerTabProps {
  connected: boolean
  onPublish: (target: PublishTarget) => Promise<{ success: boolean }>
}

const defaultProperties: MessageProperties = {
  persistent: true,
  contentType: 'text/plain',
  priority: 0,
  headers: {},
}

const ProducerTab: React.FC<ProducerTabProps> = ({ connected, onPublish }) => {
  const [targetMode, setTargetMode] = useState<'exchange' | 'queue'>('exchange')
  const [exchange, setExchange] = useState('')
  const [routingKey, setRoutingKey] = useState('')
  const [queue, setQueue] = useState('')
  const [message, setMessage] = useState('')
  const [properties, setProperties] = useState<MessageProperties>(defaultProperties)
  const [headers, setHeaders] = useState<{ key: string; value: string }[]>([])
  const [sending, setSending] = useState(false)

  const handleSend = useCallback(async (msg?: string) => {
    const content = msg ?? message
    if (!content || !content.trim()) return
    setSending(true)

    const headersObj: Record<string, string> = {}
    headers.forEach((h) => {
      if (h.key) headersObj[h.key] = h.value
    })

    const target: PublishTarget = {
      target: targetMode,
      exchange: targetMode === 'exchange' ? exchange : undefined,
      routingKey: targetMode === 'exchange' ? routingKey : undefined,
      queue: targetMode === 'queue' ? queue : undefined,
      message: content,
      properties: { ...properties, headers: headersObj },
    }

    await onPublish(target)
    setSending(false)
  }, [message, targetMode, exchange, routingKey, queue, properties, headers, onPublish])

  const handleKeyDown = useCallback((e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      handleSend()
    }
  }, [handleSend])

  const addHeader = useCallback(() => {
    setHeaders((prev) => [...prev, { key: '', value: '' }])
  }, [])

  const removeHeader = useCallback((index: number) => {
    setHeaders((prev) => prev.filter((_, i) => i !== index))
  }, [])

  const updateHeader = useCallback((index: number, field: 'key' | 'value', val: string) => {
    setHeaders((prev) => prev.map((h, i) => i === index ? { ...h, [field]: val } : h))
  }, [])

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
      <div>
        <Text strong>发送目标</Text>
        <Radio.Group
          value={targetMode}
          onChange={(e) => setTargetMode(e.target.value)}
          style={{ marginLeft: 12 }}
        >
          <Radio value="exchange">Exchange</Radio>
          <Radio value="queue">Queue</Radio>
        </Radio.Group>
      </div>

      {targetMode === 'exchange' ? (
        <Space direction="vertical" style={{ width: '100%' }}>
          <Input
            placeholder="Exchange 名"
            value={exchange}
            onChange={(e) => setExchange(e.target.value)}
            disabled={!connected}
          />
          <Input
            placeholder="Routing Key"
            value={routingKey}
            onChange={(e) => setRoutingKey(e.target.value)}
            disabled={!connected}
          />
        </Space>
      ) : (
        <Input
          placeholder="Queue 名"
          value={queue}
          onChange={(e) => setQueue(e.target.value)}
          disabled={!connected}
        />
      )}

      <Divider style={{ margin: '4px 0' }} />

      <div>
        <Text strong>消息属性</Text>
        <Space direction="vertical" style={{ width: '100%', marginTop: 8 }}>
          <Space>
            <Text type="secondary">Persistent:</Text>
            <Switch
              size="small"
              checked={properties.persistent}
              onChange={(v) => setProperties((p) => ({ ...p, persistent: v }))}
            />
          </Space>
          <Space>
            <Text type="secondary">Content-Type:</Text>
            <Select
              size="small"
              style={{ width: 140 }}
              value={properties.contentType}
              onChange={(v) => setProperties((p) => ({ ...p, contentType: v }))}
              options={[
                { value: 'text/plain', label: 'text/plain' },
                { value: 'application/json', label: 'application/json' },
                { value: 'text/xml', label: 'text/xml' },
                { value: 'application/octet-stream', label: 'application/octet-stream' },
              ]}
            />
          </Space>
          <Space>
            <Text type="secondary">Priority:</Text>
            <Input
              size="small"
              style={{ width: 60 }}
              type="number"
              min={0}
              max={9}
              value={properties.priority}
              onChange={(e) => setProperties((p) => ({ ...p, priority: Math.min(9, Math.max(0, Number(e.target.value))) }))}
            />
          </Space>
          <Space>
            <Text type="secondary">Message ID:</Text>
            <Input
              size="small"
              style={{ width: 180 }}
              value={properties.messageId || ''}
              onChange={(e) => setProperties((p) => ({ ...p, messageId: e.target.value || undefined }))}
            />
          </Space>
          <Space>
            <Text type="secondary">Reply-To:</Text>
            <Input
              size="small"
              style={{ width: 180 }}
              value={properties.replyTo || ''}
              onChange={(e) => setProperties((p) => ({ ...p, replyTo: e.target.value || undefined }))}
            />
          </Space>
        </Space>
      </div>

      <div>
        <Space>
          <Text strong>Headers</Text>
          <Button type="link" icon={<PlusOutlined />} size="small" onClick={addHeader} />
        </Space>
        {headers.map((h, i) => (
          <Space key={i} style={{ display: 'flex', marginTop: 4 }}>
            <Input
              size="small"
              placeholder="Key"
              style={{ width: 120 }}
              value={h.key}
              onChange={(e) => updateHeader(i, 'key', e.target.value)}
            />
            <Input
              size="small"
              placeholder="Value"
              style={{ width: 120 }}
              value={h.value}
              onChange={(e) => updateHeader(i, 'value', e.target.value)}
            />
            <Button
              type="link"
              danger
              icon={<MinusCircleOutlined />}
              size="small"
              onClick={() => removeHeader(i)}
            />
          </Space>
        ))}
      </div>

      <Divider style={{ margin: '4px 0' }} />

      <TextArea
        rows={4}
        placeholder="消息内容 (Enter 发送，Shift+Enter 换行)"
        value={message}
        onChange={(e) => setMessage(e.target.value)}
        onKeyDown={handleKeyDown}
        disabled={!connected}
      />

      <Button
        type="primary"
        onClick={() => handleSend()}
        loading={sending}
        disabled={!connected || !message.trim()}
      >
        发送
      </Button>
    </div>
  )
}

export default ProducerTab
```

- [ ] **Step 2: 创建 LogPanel**

```tsx
// src/components/LogPanel.tsx
import { useEffect, useRef } from 'react'
import type { LogEntry } from '../types'

interface LogPanelProps {
  logs: LogEntry[]
}

const typeLabel: Record<LogEntry['type'], string> = {
  connect: '[连接]',
  disconnect: '[断开]',
  send: '[发送]',
  receive: '[接收]',
  subscribe: '[订阅]',
  error: '[错误]',
}

const LogPanel: React.FC<LogPanelProps> = ({ logs }) => {
  const endRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    endRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [logs])

  return (
    <div className="log-panel">
      <div className="log-panel-header">消息日志</div>
      <div className="log-panel-content">
        {logs.length === 0 ? (
          <div className="log-empty">暂无日志</div>
        ) : (
          logs.map((entry, i) => (
            <div key={i} className={`log-entry log-${entry.type}`}>
              <span className="log-time">{entry.time}</span>
              <span className="log-direction">{typeLabel[entry.type]}</span>
              <span className="log-message">{entry.detail}</span>
            </div>
          ))
        )}
        <div ref={endRef} />
      </div>
    </div>
  )
}

export default LogPanel
```

---

### Task 10: 渲染进程 — ConsumerTab

**Files:**
- Create: `src/components/ConsumerTab.tsx`

- [ ] **Step 1: 创建 ConsumerTab**

```tsx
// src/components/ConsumerTab.tsx
import { useState, useCallback } from 'react'
import { Input, Button, Tag, Typography } from 'antd'
import type { ReceivedMessage } from '../types'

const { Text } = Typography

interface ConsumerTabProps {
  connected: boolean
  messages: ReceivedMessage[]
  onSubscribe: (queue: string) => Promise<{ success: boolean; consumerTag?: string }>
  onUnsubscribe: (consumerTag: string) => Promise<{ success: boolean }>
}

const ConsumerTab: React.FC<ConsumerTabProps> = ({ connected, messages, onSubscribe, onUnsubscribe }) => {
  const [queue, setQueue] = useState('')
  const [consumerTag, setConsumerTag] = useState<string | null>(null)
  const [subscribing, setSubscribing] = useState(false)

  const handleSubscribe = useCallback(async () => {
    if (!queue.trim()) return
    setSubscribing(true)
    const result = await onSubscribe(queue.trim())
    if (result.success && result.consumerTag) {
      setConsumerTag(result.consumerTag)
    }
    setSubscribing(false)
  }, [queue, onSubscribe])

  const handleUnsubscribe = useCallback(async () => {
    if (!consumerTag) return
    await onUnsubscribe(consumerTag)
    setConsumerTag(null)
  }, [consumerTag, onUnsubscribe])

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12, height: '100%' }}>
      <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
        <Input
          placeholder="Queue 名"
          value={queue}
          onChange={(e) => setQueue(e.target.value)}
          disabled={!connected || !!consumerTag}
          style={{ flex: 1 }}
        />
        {!consumerTag ? (
          <Button
            type="primary"
            onClick={handleSubscribe}
            loading={subscribing}
            disabled={!connected || !queue.trim()}
          >
            订阅
          </Button>
        ) : (
          <Button danger onClick={handleUnsubscribe}>
            取消订阅
          </Button>
        )}
      </div>

      {consumerTag && (
        <Tag color="blue">已订阅: {consumerTag}</Tag>
      )}

      <div style={{ flex: 1, overflow: 'auto', border: '1px solid #d9d9d9', borderRadius: 8, padding: 8 }}>
        {messages.length === 0 ? (
          <div style={{ color: '#999', textAlign: 'center', padding: 40 }}>暂无消息</div>
        ) : (
          messages.map((msg, i) => (
            <div
              key={i}
              style={{
                padding: '6px 8px',
                borderBottom: '1px solid #f0f0f0',
                fontFamily: 'Consolas, Monaco, monospace',
                fontSize: 13,
              }}
            >
              <div>
                <Text type="secondary" style={{ fontSize: 12 }}>[{msg.timestamp}]</Text>
                <Tag style={{ marginLeft: 6 }} color="green">{msg.queue}</Tag>
              </div>
              <div style={{ marginTop: 2 }}>{msg.message}</div>
              <details style={{ marginTop: 4 }}>
                <summary style={{ fontSize: 12, color: '#999', cursor: 'pointer' }}>Properties</summary>
                <pre style={{ fontSize: 11, margin: 4, whiteSpace: 'pre-wrap' }}>
                  {JSON.stringify(msg.properties, null, 2)}
                </pre>
              </details>
            </div>
          ))
        )}
      </div>
    </div>
  )
}

export default ConsumerTab
```

---

### Task 11: App.tsx 重构 — 正确的左右分栏布局

**Files:**
- Modify: `src/App.tsx`

当前 App.tsx 的左右布局在 Task 8 中是一个占位方案。现在需要改为正确的方案：左侧根据 Tab 切换显示 ProducerTab 或 ConsumerTab 的内容，右侧固定显示 LogPanel。

- [ ] **Step 1: 重构 App.tsx**

```tsx
import { useState, useCallback, useEffect } from 'react'
import { ConfigProvider, Tabs } from 'antd'
import type { TabsProps } from 'antd'
import ConnectionPanel from './components/ConnectionPanel'
import ProducerTab from './components/ProducerTab'
import ConsumerTab from './components/ConsumerTab'
import LogPanel from './components/LogPanel'
import { useRabbitMQ } from './hooks/useRabbitMQ'
import { useConfig } from './hooks/useConfig'
import type { ServerInfo, ReceivedMessage, LogEntry, PublishTarget } from './types'

const App: React.FC = () => {
  const { config, updateConfig, loaded } = useConfig()
  const [connected, setConnected] = useState(false)
  const [connecting, setConnecting] = useState(false)
  const [serverInfo, setServerInfo] = useState<ServerInfo | null>(null)
  const [logs, setLogs] = useState<LogEntry[]>([])
  const [messages, setMessages] = useState<ReceivedMessage[]>([])
  const [activeTab, setActiveTab] = useState('producer')

  const addLog = useCallback((entry: LogEntry) => {
    setLogs((prev) => [...prev, entry])
  }, [])

  const onConnected = useCallback((info: ServerInfo) => {
    setConnected(true)
    setConnecting(false)
    setServerInfo(info)
    addLog({ time: new Date().toLocaleTimeString(), type: 'connect', detail: `已连接到 amqp://${info.host}:${info.port}${info.vhost}` })
  }, [addLog])

  const onDisconnected = useCallback((reason: string) => {
    setConnected(false)
    setConnecting(false)
    setServerInfo(null)
    setMessages([])
    addLog({ time: new Date().toLocaleTimeString(), type: 'disconnect', detail: reason })
  }, [addLog])

  const onConnectionError = useCallback((message: string) => {
    setConnecting(false)
    addLog({ time: new Date().toLocaleTimeString(), type: 'error', detail: `连接失败: ${message}` })
  }, [addLog])

  const onMessageReceived = useCallback((msg: ReceivedMessage) => {
    setMessages((prev) => [...prev, msg])
  }, [])

  const onPublishConfirmed = useCallback((result: { success: boolean; message?: string }) => {
    if (!result.success) {
      addLog({ time: new Date().toLocaleTimeString(), type: 'error', detail: `发送失败: ${result.message || '未知错误'}` })
    }
  }, [addLog])

  const onLogEvent = useCallback((entry: LogEntry) => {
    addLog(entry)
  }, [addLog])

  const { connect, disconnect, publish, subscribe, unsubscribe } = useRabbitMQ({
    onConnected, onDisconnected, onConnectionError, onMessageReceived, onPublishConfirmed, onLogEvent,
  })

  const handleConnect = useCallback(async () => {
    setConnecting(true)
    const result = await connect(config)
    if (!result.success) setConnecting(false)
  }, [connect, config])

  const handleDisconnect = useCallback(async () => {
    await disconnect()
  }, [disconnect])

  const serverLabel = serverInfo ? `${serverInfo.host}:${serverInfo.port}` : ''

  return (
    <ConfigProvider theme={{ token: { colorPrimary: '#1677ff' } }}>
      <div className="app-container">
        <div style={{ borderBottom: '1px solid #d9d9d9', paddingBottom: 4 }}>
          <Tabs
            activeKey={activeTab}
            onChange={setActiveTab}
            items={[
              { key: 'producer', label: '生产者' },
              { key: 'consumer', label: '消费者' },
            ]}
            className="app-tabs"
          />
          {loaded && (
            <ConnectionPanel
              config={config}
              onConfigChange={updateConfig}
              connected={connected}
              connecting={connecting}
              onConnect={handleConnect}
              onDisconnect={handleDisconnect}
              serverLabel={serverLabel}
            />
          )}
        </div>
        <div className="tab-content">
          <div className="tab-layout">
            <div className="tab-left">
              {activeTab === 'producer' ? (
                <ProducerTab connected={connected} onPublish={publish} />
              ) : (
                <ConsumerTab
                  connected={connected}
                  messages={messages}
                  onSubscribe={subscribe}
                  onUnsubscribe={unsubscribe}
                />
              )}
            </div>
            <div className="tab-right">
              <LogPanel logs={logs} />
            </div>
          </div>
        </div>
      </div>
    </ConfigProvider>
  )
}

export default App
```

---

### Task 12: 构建验证

**Files:**
- Verify: 无文件更改

- [ ] **Step 1: 清理并构建**

```bash
cd D:\Workspace\ElectronApp\ElectronAppTool\ERabbitMQTool
Remove-Item -Recurse -Force dist -ErrorAction SilentlyContinue
npm run build
```

Expected: dist/ 目录包含 index.html + 静态资源，无 TypeScript/构建错误

- [ ] **Step 2: 验证 dev 模式启动**

```bash
npm run dev
```

Expected: Vite dev server 启动在 5173 端口, Electron 窗口弹出加载 http://localhost:5173

按 Ctrl+C 停止

- [ ] **Step 3: 验证打包**

```bash
npm run pack
```

Expected: release/ 目录下生成 NSIS 安装包

---

### 实现顺序与依赖关系

```
Task 1 (脚手架) → Task 2 (类型) → Task 3 (preload)
                                     ↓
                    ┌────────────────┴────────────────┐
                    ↓                                  ↓
              Task 4 (主进程-连接)              Task 7 (useRabbitMQ hook)
              Task 5 (主进程-发布订阅)           Task 7 (useConfig hook)
              Task 6 (主进程-持久化)                 ↓
                                         ┌──────────┴──────────┐
                                         ↓                     ↓
                                   Task 8 (App+Connection)  Task 9 (Producer+Log)
                                         ↓                     ↓
                                   ┌─────┴─────────────────────┘
                                   ↓
                              Task 10 (ConsumerTab)
                                   ↓
                              Task 11 (App 重构)
                                   ↓
                              Task 12 (构建验证)
```

### 自检清单

**1. Spec 覆盖检查：**
- 连接区（Host/Port/VHost/用户名/密码）→ Task 8 ConnectionPanel + Task 7 useConfig
- 连接/断开/状态指示 → Task 4 main.js + Task 8 App.tsx
- 生产者 Exchange/Queue 模式切换 → Task 9 ProducerTab
- 消息属性（persistent, content-type, priority, message-id, reply-to, headers）→ Task 9 ProducerTab
- 消息体 + Enter 发送 → Task 9 ProducerTab handleKeyDown
- 消费者订阅/取消 → Task 10 ConsumerTab
- 消息列表展示 + properties 折叠 → Task 10 ConsumerTab
- 消息日志 → Task 9 LogPanel
- 配置持久化 → Task 6 main.js + Task 7 useConfig（300ms 防抖）
- 单连接模型 → Task 4 main.js 全局 connection/channel 变量
- 防连竞态 isConnecting → Task 4 main.js + Task 8 App.tsx
- sendToRenderer 空值守卫 → Task 1 main.js sendToRenderer 函数
- window-all-closed 清理 → Task 4 main.js
- preload 每个 on* 返回 unsubscribe → Task 2 preload.js
- useEffect 清理监听器 → Task 7 useRabbitMQ

**2. Placeholder 扫描：** 无占位符/TODO

**3. 类型一致性检查：**
- `PublishTarget.target` 类型为 `'exchange' | 'queue'` — ProducerTab 和 main.js 一致
- `MessageProperties.headers` 类型为 `Record<string, string>` — ProducerTab 和 types 一致
- `ReceivedMessage` 包含 queue/message/properties/consumerTag/timestamp — types 和 main.js 一致
- `ConnectionConfig` 包含 host/port/vhost/username/password — 各处一致
- `ElectronAPI` 的 `subscribe` 参数为 `queue: string` — preload 和 types 一致

---

## 执行交接

计划完整，保存至 `docs/superpowers/plans/2026-07-14-rabbitmq-tool-implementation.md`。两种执行方式：

**1. Subagent-Driven（推荐）** — 每个 Task 派发独立子代理，Task 间审查，快速迭代

**2. Inline Execution** — 当前会话中按序执行，批量执行 + 检查点审查

选择哪种方式？