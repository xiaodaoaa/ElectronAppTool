# EActiveMQTool 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 参照 ERabbitMQToolPlus 创建一个 ActiveMQ 调试工具，使用 STOMP 协议连接 ActiveMQ Broker，提供消息发送和消费功能。

**Architecture:** electron-vite 三进程架构（Approach C），Vue 3 + Element Plus + Pinia 渲染进程，@stomp/stompjs 作为 STOMP 客户端，electron-store 持久化配置。

**Tech Stack:** Electron 43, Vue 3, Element Plus, Pinia, TypeScript, @stomp/stompjs, electron-store, dayjs, uuid

---

## 文件结构

```
EActiveMQTool/
├── package.json
├── electron.vite.config.ts
├── tsconfig.json
├── tsconfig.node.json
├── tsconfig.web.json
├── resources/
│   └── icon.png
├── src/
│   ├── shared/
│   │   └── types.ts
│   ├── main/
│   │   ├── index.ts
│   │   ├── ipc/
│   │   │   ├── connection.ts
│   │   │   ├── producer.ts
│   │   │   ├── consumer.ts
│   │   │   └── dialog.ts
│   │   ├── services/
│   │   │   ├── ConnectionManager.ts
│   │   │   ├── ProducerService.ts
│   │   │   └── ConsumerService.ts
│   │   └── utils/
│   │       ├── store.ts
│   │       └── logger.ts
│   ├── preload/
│   │   └── index.ts
│   └── renderer/
│       ├── index.html
│       └── src/
│           ├── main.ts
│           ├── App.vue
│           ├── env.d.ts
│           ├── styles/
│           │   └── global.css
│           ├── views/
│           │   ├── ProducerView.vue
│           │   ├── ConsumerView.vue
│           │   └── SettingsView.vue
│           ├── components/
│           │   ├── ConnectionForm.vue
│           │   ├── LogPanel.vue
│           │   ├── MessageTable.vue
│           │   └── MessageDetail.vue
│           └── stores/
│               ├── connection.ts
│               ├── producer.ts
│               ├── consumer.ts
│               ├── log.ts
│               └── settings.ts
```

---

### Task 1: 项目配置文件

**Files:**
- Create: `EActiveMQTool/package.json`
- Create: `EActiveMQTool/electron.vite.config.ts`
- Create: `EActiveMQTool/tsconfig.json`
- Create: `EActiveMQTool/tsconfig.node.json`
- Create: `EActiveMQTool/tsconfig.web.json`

- [ ] **Step 1: 创建 package.json**

```json
{
    "name": "EActiveMQTool",
    "version": "1.0.0",
    "description": "ActiveMQ 消息发送/消费工具",
    "main": "./out/main/index.js",
    "author": "fengchao12",
    "license": "MIT",
    "type": "module",
    "scripts": {
        "dev": "electron-vite dev",
        "build": "electron-vite build",
        "preview": "electron-vite preview",
        "typecheck:node": "tsc --noEmit -p tsconfig.node.json --composite false",
        "typecheck:web": "vue-tsc --noEmit -p tsconfig.web.json --composite false",
        "typecheck": "npm run typecheck:node && npm run typecheck:web",
        "pack": "electron-vite build && electron-builder --win"
    },
    "dependencies": {
        "@stomp/stompjs": "^7.0.0",
        "dayjs": "^1.11.13",
        "electron-store": "^11.0.2",
        "element-plus": "^2.14.3",
        "pinia": "^4.0.2",
        "uuid": "^14.0.1"
    },
    "build": {
        "appId": "com.eactivemq.tool",
        "productName": "EActiveMQTool",
        "directories": {
            "output": "release"
        },
        "files": [
            "out/**/*"
        ],
        "win": {
            "target": [
                "nsis"
            ],
            "icon": "resources/icon.png"
        },
        "nsis": {
            "oneClick": false,
            "allowToChangeInstallationDirectory": true
        }
    },
    "devDependencies": {
        "@electron-toolkit/tsconfig": "^2.0.0",
        "@electron-toolkit/utils": "^4.0.0",
        "@types/node": "^22.10.0",
        "@types/uuid": "^10.0.0",
        "@vitejs/plugin-vue": "^6.0.8",
        "electron": "^43.1.1",
        "electron-builder": "^25.1.8",
        "electron-vite": "^5.0.0",
        "typescript": "^5.7.2",
        "vite": "^5.4.11",
        "vue": "^3.5.13",
        "vue-tsc": "^3.3.7"
    }
}
```

- [ ] **Step 2: 创建 electron.vite.config.ts**

```typescript
import { resolve } from 'path'
import { defineConfig, externalizeDepsPlugin } from 'electron-vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  main: {
    plugins: [externalizeDepsPlugin()],
    resolve: {
      alias: {
        '@shared': resolve('src/shared')
      }
    }
  },
  preload: {
    plugins: [externalizeDepsPlugin()],
    resolve: {
      alias: {
        '@shared': resolve('src/shared')
      }
    }
  },
  renderer: {
    resolve: {
      alias: {
        '@renderer': resolve('src/renderer/src'),
        '@shared': resolve('src/shared')
      }
    },
    plugins: [vue()]
  }
})
```

- [ ] **Step 3: 创建 tsconfig.json**

```json
{
  "files": [],
  "references": [
    { "path": "./tsconfig.node.json" },
    { "path": "./tsconfig.web.json" }
  ]
}
```

- [ ] **Step 4: 创建 tsconfig.node.json**

```json
{
  "extends": "@electron-toolkit/tsconfig/tsconfig.node.json",
  "include": [
    "electron.vite.config.*",
    "src/main/**/*",
    "src/preload/**/*",
    "src/shared/**/*"
  ],
  "compilerOptions": {
    "composite": true,
    "types": ["electron-vite/node"],
    "baseUrl": ".",
    "paths": {
      "@main/*": ["src/main/*"],
      "@preload/*": ["src/preload/*"],
      "@shared/*": ["src/shared/*"]
    }
  }
}
```

- [ ] **Step 5: 创建 tsconfig.web.json**

```json
{
  "extends": "@electron-toolkit/tsconfig/tsconfig.web.json",
  "include": [
    "src/renderer/src/**/*",
    "src/renderer/src/**/*.vue",
    "src/shared/**/*"
  ],
  "compilerOptions": {
    "composite": true,
    "baseUrl": ".",
    "paths": {
      "@renderer/*": ["src/renderer/src/*"],
      "@shared/*": ["src/shared/*"]
    }
  }
}
```

- [ ] **Step 6: 创建 resources 目录**

```bash
mkdir -p EActiveMQTool/resources
```

- [ ] **Step 7: 安装依赖**

```bash
cd EActiveMQTool && npm install
```

- [ ] **Step 8: Commit**

```bash
git add EActiveMQTool/package.json EActiveMQTool/electron.vite.config.ts EActiveMQTool/tsconfig.json EActiveMQTool/tsconfig.node.json EActiveMQTool/tsconfig.web.json
git commit -m "feat: 初始化 EActiveMQTool 项目配置"
```

---

### Task 2: 共享类型定义

**Files:**
- Create: `EActiveMQTool/src/shared/types.ts`

- [ ] **Step 1: 创建 types.ts**

```typescript
export interface ConnectionConfig {
  host: string
  port: number
  username: string
  password: string
  sslEnabled: boolean
  heartbeatOutgoing: number
  heartbeatIncoming: number
}

export type MessageFormat = 'json' | 'text' | 'xml'

export interface BatchConfig {
  count: number
  intervalMs: number
}

export interface SendParams {
  destination: string
  body: string
  format: MessageFormat
  contentType: string
  persistent: boolean
  priority: number
  expires: number
  headers: Record<string, string>
  batch: BatchConfig
}

export interface SendResult {
  success: boolean
  messageId?: string
  error?: string
}

export interface HistoryItem {
  time: string
  destination: string
  messageSummary: string
}

export interface ConsumedMessage {
  seq: number
  receivedAt: string
  destination: string
  messageId: string
  body: string
  headers: Record<string, string>
  acked: boolean
}

export interface ConsumerConfig {
  destination: string
  ackMode: 'auto' | 'client' | 'client-individual'
  selector: string
  prefetchCount: number
  maxReceive: number
  filterRoutingKey: string
  filterByHeaderKey: string
  filterByHeaderValue: string
}

export interface IpcResult {
  success: boolean
  error?: string
}

export interface ProgressInfo {
  current: number
  total: number
}

export type LogLevel = 'INFO' | 'WARN' | 'ERROR'

export interface LogEntry {
  time: string
  level: LogLevel
  message: string
}

export type ConnectionStatus = 'disconnected' | 'connecting' | 'connected' | 'error'

export interface ConnectionStatusInfo {
  status: ConnectionStatus
  display: string
  sslEnabled: boolean
}
```

- [ ] **Step 2: Commit**

```bash
git add EActiveMQTool/src/shared/types.ts
git commit -m "feat: 添加 ActiveMQ 共享类型定义"
```

---

### Task 3: 主进程工具层

**Files:**
- Create: `EActiveMQTool/src/main/utils/logger.ts`
- Create: `EActiveMQTool/src/main/utils/store.ts`

- [ ] **Step 1: 创建 logger.ts**

```typescript
import type { BrowserWindow } from 'electron'
import type { LogEntry, LogLevel } from '../../shared/types'
import dayjs from 'dayjs'

let mainWindow: BrowserWindow | null = null

export function setMainWindow(win: BrowserWindow): void {
  mainWindow = win
}

export function log(level: LogLevel, message: string): void {
  const entry: LogEntry = {
    time: dayjs().format('YYYY-MM-DD HH:mm:ss.SSS'),
    level,
    message
  }
  if (mainWindow && !mainWindow.isDestroyed()) {
    mainWindow.webContents.send('log:entry', entry)
  }
}

export const logger = {
  info: (msg: string) => log('INFO', msg),
  warn: (msg: string) => log('WARN', msg),
  error: (msg: string) => log('ERROR', msg)
}
```

- [ ] **Step 2: 创建 store.ts**

```typescript
import Store from 'electron-store'
import { app } from 'electron'
import * as crypto from 'crypto'
import type { ConnectionConfig, SendParams, ConsumerConfig } from '../../shared/types'

const ENCRYPTION_KEY = crypto
  .createHash('sha256')
  .update(app.getName() + ':eactivemq-store-key')
  .digest()

function encrypt(text: string): string {
  const iv = crypto.randomBytes(16)
  const cipher = crypto.createCipheriv('aes-256-cbc', ENCRYPTION_KEY, iv)
  const encrypted = Buffer.concat([cipher.update(text, 'utf8'), cipher.final()])
  return iv.toString('hex') + ':' + encrypted.toString('hex')
}

function decrypt(payload: string): string {
  const [ivHex, dataHex] = payload.split(':')
  const iv = Buffer.from(ivHex, 'hex')
  const data = Buffer.from(dataHex, 'hex')
  const decipher = crypto.createDecipheriv('aes-256-cbc', ENCRYPTION_KEY, iv)
  return decipher.update(data, undefined, 'utf8') + decipher.final('utf8')
}

interface StoreSchema {
  lastConnection?: ConnectionConfig
  lastProducer?: SendParams
  lastConsumer?: ConsumerConfig
  settings?: {
    theme: 'light' | 'dark'
    maxMessageCache: number
    maxDisplayLength: number
  }
}

const store = new Store<StoreSchema>({
  defaults: {
    settings: {
      theme: 'light',
      maxMessageCache: 1000,
      maxDisplayLength: 1000
    }
  }
})

export function saveConnection(config: ConnectionConfig): void {
  const safe = { ...config }
  if (safe.password) {
    safe.password = encrypt(safe.password)
  }
  store.set('lastConnection', safe)
}

export function loadConnection(): ConnectionConfig | undefined {
  const saved = store.get('lastConnection')
  if (!saved) return undefined
  const restored = { ...saved }
  if (restored.password) {
    try {
      restored.password = decrypt(restored.password)
    } catch {
      restored.password = ''
    }
  }
  return restored
}

export function getSettings() {
  return store.get('settings')!
}

export function saveSettings(settings: StoreSchema['settings']): void {
  store.set('settings', settings)
}

export function saveProducerConfig(config: SendParams): void {
  store.set('lastProducer', config)
}

export function loadProducerConfig(): SendParams | undefined {
  return store.get('lastProducer')
}

export function saveConsumerConfig(config: ConsumerConfig): void {
  store.set('lastConsumer', config)
}

export function loadConsumerConfig(): ConsumerConfig | undefined {
  return store.get('lastConsumer')
}
```

- [ ] **Step 3: Commit**

```bash
git add EActiveMQTool/src/main/utils/logger.ts EActiveMQTool/src/main/utils/store.ts
git commit -m "feat: 添加主进程工具层（logger + store）"
```

---

### Task 4: ConnectionManager 服务

**Files:**
- Create: `EActiveMQTool/src/main/services/ConnectionManager.ts`

- [ ] **Step 1: 创建 ConnectionManager.ts**

```typescript
import { Client, StompConfig } from '@stomp/stompjs'
import type { ConnectionConfig, ConnectionStatus, ConnectionStatusInfo } from '../../shared/types'
import { logger } from '../utils/logger'

type StatusListener = (info: ConnectionStatusInfo) => void

class ConnectionManager {
  private client: Client | null = null
  private status: ConnectionStatus = 'disconnected'
  private currentConfig: ConnectionConfig | null = null
  private listeners: Set<StatusListener> = new Set()

  private buildStatusInfo(): ConnectionStatusInfo {
    if (this.status === 'connected' && this.currentConfig) {
      const proto = this.currentConfig.sslEnabled ? 'wss' : 'ws'
      const display = `${proto}://${this.currentConfig.username}@${this.currentConfig.host}:${this.currentConfig.port}`
      return {
        status: this.status,
        display,
        sslEnabled: this.currentConfig.sslEnabled
      }
    }
    return {
      status: this.status,
      display: this.status === 'connecting' ? '连接中...' : '未连接',
      sslEnabled: false
    }
  }

  private notifyListeners(): void {
    const info = this.buildStatusInfo()
    this.listeners.forEach((cb) => cb(info))
  }

  private setStatus(status: ConnectionStatus): void {
    this.status = status
    this.notifyListeners()
  }

  onStatusChange(cb: StatusListener): void {
    this.listeners.add(cb)
  }

  offStatusChange(cb: StatusListener): void {
    this.listeners.delete(cb)
  }

  async connect(config: ConnectionConfig): Promise<{ success: boolean; error?: string }> {
    if (this.client) {
      await this.disconnect()
    }
    this.currentConfig = config
    this.setStatus('connecting')

    return new Promise((resolve) => {
      const proto = config.sslEnabled ? 'wss' : 'ws'
      const brokerURL = `${proto}://${config.host}:${config.port}/ws`

      const stompConfig: StompConfig = {
        brokerURL,
        connectHeaders: {
          login: config.username,
          passcode: config.password
        },
        heartbeatOutgoing: config.heartbeatOutgoing || 10000,
        heartbeatIncoming: config.heartbeatIncoming || 10000,
        reconnectDelay: 0,
        debug: (msg: string) => {
          logger.info('[STOMP] ' + msg)
        },

        onConnect: () => {
          this.setStatus('connected')
          logger.info(`已连接 ${config.host}:${config.port}`)
          resolve({ success: true })
        },

        onDisconnect: () => {
          logger.warn('连接已关闭')
          this.client = null
          this.setStatus('disconnected')
        },

        onStompError: (frame) => {
          const msg = frame.headers['message'] || 'STOMP 错误'
          logger.error('STOMP 错误：' + msg)
          this.client = null
          this.setStatus('error')
          resolve({ success: false, error: this.humanizeError(new Error(msg)) })
        },

        onWebSocketClose: (evt) => {
          if (this.status === 'error') return
          logger.warn('WebSocket 已关闭')
          this.client = null
          this.setStatus('disconnected')
        }
      }

      this.client = new Client(stompConfig)
      this.client.activate()
    })
  }

  async test(config: ConnectionConfig): Promise<{ success: boolean; error?: string }> {
    return new Promise((resolve) => {
      const proto = config.sslEnabled ? 'wss' : 'ws'
      const brokerURL = `${proto}://${config.host}:${config.port}/ws`

      const testClient = new Client({
        brokerURL,
        connectHeaders: {
          login: config.username,
          passcode: config.password
        },
        heartbeatOutgoing: config.heartbeatOutgoing || 10000,
        heartbeatIncoming: config.heartbeatIncoming || 10000,
        reconnectDelay: 0,

        onConnect: () => {
          testClient.deactivate()
          logger.info(`测试连接成功 ${config.host}:${config.port}`)
          resolve({ success: true })
        },

        onStompError: (frame) => {
          const msg = frame.headers['message'] || 'STOMP 错误'
          testClient.deactivate()
          logger.error('测试连接失败：' + msg)
          resolve({ success: false, error: this.humanizeError(new Error(msg), config) })
        },

        onWebSocketClose: () => {
          resolve({ success: false, error: 'WebSocket 连接关闭' })
        }
      })

      testClient.activate()
    })
  }

  async disconnect(): Promise<{ success: boolean; error?: string }> {
    if (!this.client) {
      return { success: true }
    }
    try {
      this.client.deactivate()
    } catch (err: any) {
      logger.warn('关闭连接时出错：' + err.message)
    }
    this.client = null
    this.currentConfig = null
    this.setStatus('disconnected')
    logger.info('已断开连接')
    return { success: true }
  }

  getClient(): Client | null {
    return this.client
  }

  isConnected(): boolean {
    return this.status === 'connected' && this.client !== null
  }

  private humanizeError(err: any, config?: ConnectionConfig | null): string {
    const msg = err?.message || String(err)
    const host = config?.host || this.currentConfig?.host || ''

    if (msg.includes('ECONNREFUSED')) {
      return `无法连接到服务器：${host} 拒绝连接`
    }
    if (msg.includes('ENOTFOUND')) {
      return `主机名无法解析：${host}`
    }
    if (msg.includes('ETIMEDOUT') || msg.includes('timeout')) {
      return '连接超时，请检查网络或主机地址'
    }
    if (msg.includes('401') || msg.includes('Unauthorized') || msg.includes('auth')) {
      return '认证失败：用户名/密码错误或无权限'
    }
    if (msg.includes('SSL') || msg.includes('certificate') || msg.includes('TLS')) {
      return 'SSL/TLS 错误：' + msg
    }
    return msg
  }
}

export const connectionManager = new ConnectionManager()
```

- [ ] **Step 2: Commit**

```bash
git add EActiveMQTool/src/main/services/ConnectionManager.ts
git commit -m "feat: 添加 ConnectionManager 服务（STOMP 连接管理）"
```

---

### Task 5: ProducerService 服务

**Files:**
- Create: `EActiveMQTool/src/main/services/ProducerService.ts`

- [ ] **Step 1: 创建 ProducerService.ts**

```typescript
import { connectionManager } from './ConnectionManager'
import { logger } from '../utils/logger'
import { v4 as uuidv4 } from 'uuid'
import dayjs from 'dayjs'
import type { SendParams, SendResult, HistoryItem, ProgressInfo } from '../../shared/types'

type ProgressListener = (p: ProgressInfo) => void

class ProducerService {
  private history: HistoryItem[] = []
  private progressListeners: Set<ProgressListener> = new Set()

  onProgress(cb: ProgressListener): void {
    this.progressListeners.add(cb)
  }

  offProgress(cb: ProgressListener): void {
    this.progressListeners.delete(cb)
  }

  private emitProgress(p: ProgressInfo): void {
    this.progressListeners.forEach((cb) => cb(p))
  }

  async send(params: SendParams): Promise<SendResult> {
    if (!connectionManager.isConnected()) {
      return { success: false, error: '请先建立连接' }
    }
    const client = connectionManager.getClient()!

    if (params.format === 'json') {
      try {
        JSON.parse(params.body)
      } catch {
        return { success: false, error: '消息体不是合法 JSON' }
      }
    }

    const headers: Record<string, string> = {
      'content-type': params.contentType || 'text/plain',
      ...params.headers
    }

    if (params.persistent) {
      headers['persistent'] = 'true'
    }
    if (params.priority > 0) {
      headers['priority'] = String(params.priority)
    }
    if (params.expires > 0) {
      headers['expires'] = String(params.expires)
    }

    const total = Math.max(1, params.batch.count)
    let lastMessageId = ''

    try {
      for (let i = 0; i < total; i++) {
        const messageId = uuidv4()
        lastMessageId = messageId
        client.publish({
          destination: params.destination,
          body: params.body,
          headers: { ...headers, 'message-id': messageId }
        })
        this.emitProgress({ current: i + 1, total })
        if (i < total - 1 && params.batch.intervalMs > 0) {
          await new Promise((r) => setTimeout(r, params.batch.intervalMs))
        }
      }

      const historyItem: HistoryItem = {
        time: dayjs().format('YYYY-MM-DD HH:mm:ss'),
        destination: params.destination,
        messageSummary: params.body.slice(0, 50)
      }
      this.history.unshift(historyItem)
      if (this.history.length > 10) {
        this.history = this.history.slice(0, 10)
      }

      logger.info(`发送成功：${params.destination}，共 ${total} 条`)
      return { success: true, messageId: lastMessageId }
    } catch (err: any) {
      const msg = err?.message || String(err)
      logger.error('发送失败：' + msg)
      return { success: false, error: msg }
    }
  }

  getHistory(): HistoryItem[] {
    return this.history
  }

  clearHistoryOnDisconnect(): void {
    this.history = []
  }
}

export const producerService = new ProducerService()
```

- [ ] **Step 2: Commit**

```bash
git add EActiveMQTool/src/main/services/ProducerService.ts
git commit -m "feat: 添加 ProducerService 服务（STOMP 消息发送）"
```

---

### Task 6: ConsumerService 服务

**Files:**
- Create: `EActiveMQTool/src/main/services/ConsumerService.ts`

- [ ] **Step 1: 创建 ConsumerService.ts**

```typescript
import { StompSubscription } from '@stomp/stompjs'
import { connectionManager } from './ConnectionManager'
import { logger } from '../utils/logger'
import dayjs from 'dayjs'
import type { ConsumerConfig, ConsumedMessage, IpcResult } from '../../shared/types'

type MessageListener = (msgs: ConsumedMessage[]) => void
type StopListener = () => void

const BATCH_INTERVAL_MS = 100
const BATCH_MAX_SIZE = 200

class ConsumerService {
  private subscription: StompSubscription | null = null
  private seq = 0
  private receivedCount = 0
  private config: ConsumerConfig | null = null
  private messageListeners: Set<MessageListener> = new Set()
  private stopListeners: Set<StopListener> = new Set()
  private paused = false
  private batchBuffer: ConsumedMessage[] = []
  private batchTimer: ReturnType<typeof setInterval> | null = null

  onMessage(cb: MessageListener): void {
    this.messageListeners.add(cb)
  }

  offMessage(cb: MessageListener): void {
    this.messageListeners.delete(cb)
  }

  onStop(cb: StopListener): void {
    this.stopListeners.add(cb)
  }

  offStop(cb: StopListener): void {
    this.stopListeners.delete(cb)
  }

  private emitMessage(msgs: ConsumedMessage[]): void {
    this.messageListeners.forEach((cb) => cb(msgs))
  }

  private emitStop(): void {
    this.stopListeners.forEach((cb) => cb())
  }

  private startBatchTimer(): void {
    if (this.batchTimer) return
    this.batchTimer = setInterval(() => {
      this.flushBatch()
    }, BATCH_INTERVAL_MS)
  }

  private stopBatchTimer(): void {
    if (this.batchTimer) {
      clearInterval(this.batchTimer)
      this.batchTimer = null
    }
  }

  private flushBatch(): void {
    if (this.batchBuffer.length === 0) return
    const batch = this.batchBuffer.splice(0, this.batchBuffer.length)
    this.emitMessage(batch)
  }

  async start(config: ConsumerConfig): Promise<IpcResult> {
    if (!connectionManager.isConnected()) {
      return { success: false, error: '请先建立连接' }
    }
    if (this.subscription) {
      return { success: false, error: '已有消费进行中，请先停止' }
    }

    const client = connectionManager.getClient()!

    this.config = config
    this.seq = 0
    this.receivedCount = 0
    this.paused = false
    this.batchBuffer = []
    this.startBatchTimer()

    const subscribeHeaders: Record<string, string> = {
      ack: config.ackMode || 'auto'
    }

    if (config.selector) {
      subscribeHeaders['selector'] = config.selector
    }

    if (config.ackMode === 'client-individual' && config.prefetchCount > 0) {
      subscribeHeaders['activemq.prefetchSize'] = String(config.prefetchCount)
    }

    try {
      this.subscription = client.subscribe(
        config.destination,
        (message) => this.handleMessage(message),
        subscribeHeaders
      )
      logger.info(`开始消费：${config.destination}（ack=${config.ackMode}）`)
      return { success: true }
    } catch (err: any) {
      const msg = err?.message || String(err)
      logger.error('启动消费失败：' + msg)
      this.stopBatchTimer()
      return { success: false, error: msg }
    }
  }

  private handleMessage(message: { body: string; headers: Record<string, string>; ack: () => void; nack: () => void }): void {
    if (this.paused) {
      return
    }

    const config = this.config!
    const headers = message.headers || {}

    const matched = this.matchFilter(headers, config)
    if (!matched) {
      if (config.ackMode !== 'auto') {
        try {
          message.ack()
        } catch {
          // ignore
        }
      }
      return
    }

    const consumed = this.buildConsumedMessage(message)

    this.batchBuffer.push(consumed)
    if (this.batchBuffer.length >= BATCH_MAX_SIZE) {
      this.flushBatch()
    }

    logger.info(`收到消息 #${consumed.seq}：${config.destination}，内容：${consumed.body.slice(0, 100)}`)

    if (config.maxReceive > 0 && this.receivedCount >= config.maxReceive) {
      logger.info(`达到最大接收数 ${config.maxReceive}，自动停止`)
      this.stop()
      this.emitStop()
    }
  }

  private buildConsumedMessage(message: { body: string; headers: Record<string, string>; ack: () => void; nack: () => void }): ConsumedMessage {
    const seq = ++this.seq
    this.receivedCount++

    return {
      seq,
      receivedAt: dayjs().format('YYYY-MM-DD HH:mm:ss.SSS'),
      destination: message.headers['destination'] || this.config?.destination || '',
      messageId: message.headers['message-id'] || '',
      body: message.body,
      headers: message.headers,
      acked: this.config?.ackMode === 'auto'
    }
  }

  private matchFilter(headers: Record<string, string>, config: ConsumerConfig): boolean {
    if (config.filterRoutingKey) {
      const destination = headers['destination'] || ''
      if (!this.routingKeyMatch(destination, config.filterRoutingKey)) {
        return false
      }
    }
    if (config.filterByHeaderKey && config.filterByHeaderValue !== undefined) {
      if (headers[config.filterByHeaderKey] !== config.filterByHeaderValue) {
        return false
      }
    }
    return true
  }

  private routingKeyMatch(actual: string, pattern: string): boolean {
    if (!pattern.includes('*') && !pattern.includes('#')) {
      return actual === pattern
    }
    const patternParts = pattern.split('.')
    const actualParts = actual.split('.')
    return this.amqpMatch(actualParts, 0, patternParts, 0)
  }

  private amqpMatch(actual: string[], ai: number, pattern: string[], pi: number): boolean {
    if (pi >= pattern.length) {
      return ai >= actual.length
    }
    if (pattern[pi] === '#') {
      if (pi === pattern.length - 1) return true
      for (let i = ai; i <= actual.length; i++) {
        if (this.amqpMatch(actual, i, pattern, pi + 1)) return true
      }
      return false
    }
    if (ai >= actual.length) return false
    if (pattern[pi] === '*' || pattern[pi] === actual[ai]) {
      return this.amqpMatch(actual, ai + 1, pattern, pi + 1)
    }
    return false
  }

  async pause(): Promise<IpcResult> {
    if (!this.subscription) {
      return { success: false, error: '无活跃消费者' }
    }
    try {
      this.subscription.unsubscribe()
      this.subscription = null
      this.paused = true
      logger.info('消费已暂停')
      return { success: true }
    } catch (err: any) {
      return { success: false, error: err?.message || String(err) }
    }
  }

  async resume(): Promise<IpcResult> {
    if (!this.config) {
      return { success: false, error: '无活跃消费者' }
    }
    if (!this.paused) {
      return { success: false, error: '消费者未暂停' }
    }

    const client = connectionManager.getClient()!
    const config = this.config

    const subscribeHeaders: Record<string, string> = {
      ack: config.ackMode || 'auto'
    }
    if (config.selector) {
      subscribeHeaders['selector'] = config.selector
    }

    try {
      this.subscription = client.subscribe(
        config.destination,
        (message) => this.handleMessage(message),
        subscribeHeaders
      )
      this.paused = false
      logger.info('消费已恢复')
      return { success: true }
    } catch (err: any) {
      return { success: false, error: err?.message || String(err) }
    }
  }

  async stop(): Promise<IpcResult> {
    this.stopBatchTimer()
    this.flushBatch()
    if (this.subscription) {
      try {
        this.subscription.unsubscribe()
      } catch {
        // ignore
      }
      this.subscription = null
    }
    this.paused = false
    logger.info('消费已停止')
    return { success: true }
  }

  async ack(messageId: string): Promise<IpcResult> {
    if (!connectionManager.isConnected()) {
      return { success: false, error: '未连接' }
    }
    const client = connectionManager.getClient()!
    try {
      client.ack({ 'message-id': messageId })
      return { success: true }
    } catch (err: any) {
      return { success: false, error: err?.message || String(err) }
    }
  }

  async nack(messageId: string): Promise<IpcResult> {
    if (!connectionManager.isConnected()) {
      return { success: false, error: '未连接' }
    }
    const client = connectionManager.getClient()!
    try {
      client.nack({ 'message-id': messageId })
      return { success: true }
    } catch (err: any) {
      return { success: false, error: err?.message || String(err) }
    }
  }

  clearOnDisconnect(): void {
    this.stopBatchTimer()
    this.flushBatch()
    if (this.subscription) {
      try {
        this.subscription.unsubscribe()
      } catch {
        // ignore
      }
      this.subscription = null
    }
    this.config = null
    this.paused = false
  }
}

export const consumerService = new ConsumerService()
```

- [ ] **Step 2: Commit**

```bash
git add EActiveMQTool/src/main/services/ConsumerService.ts
git commit -m "feat: 添加 ConsumerService 服务（STOMP 消息消费）"
```

---

### Task 7: IPC 注册层

**Files:**
- Create: `EActiveMQTool/src/main/ipc/connection.ts`
- Create: `EActiveMQTool/src/main/ipc/producer.ts`
- Create: `EActiveMQTool/src/main/ipc/consumer.ts`
- Create: `EActiveMQTool/src/main/ipc/dialog.ts`

- [ ] **Step 1: 创建 connection.ts**

```typescript
import { ipcMain } from 'electron'
import { connectionManager } from '../services/ConnectionManager'
import {
  loadConnection, saveConnection,
  loadProducerConfig, saveProducerConfig,
  loadConsumerConfig, saveConsumerConfig
} from '../utils/store'
import { logger } from '../utils/logger'
import type { ConnectionConfig, ConnectionStatusInfo, SendParams, ConsumerConfig } from '../../shared/types'

export function registerConnectionIpc(
  mainWindow: { webContents: { send: (ch: string, ...args: any[]) => void } }
): void {
  connectionManager.onStatusChange((info: ConnectionStatusInfo) => {
    mainWindow.webContents.send('connection:statusChanged', info)
  })

  ipcMain.handle('connection:connect', async (_evt, config: ConnectionConfig) => {
    const result = await connectionManager.connect(config)
    if (result.success) {
      saveConnection(config)
    }
    return result
  })

  ipcMain.handle('connection:test', async (_evt, config: ConnectionConfig) => {
    return await connectionManager.test(config)
  })

  ipcMain.handle('connection:disconnect', async () => {
    return await connectionManager.disconnect()
  })

  ipcMain.handle('config:saveProducer', async (_evt, config: SendParams) => {
    saveProducerConfig(config)
    return { success: true }
  })

  ipcMain.handle('config:saveConsumer', async (_evt, config: ConsumerConfig) => {
    saveConsumerConfig(config)
    return { success: true }
  })

  ipcMain.handle('config:loadProducer', async () => {
    return loadProducerConfig()
  })

  ipcMain.handle('config:loadConsumer', async () => {
    return loadConsumerConfig()
  })

  logger.info('连接 IPC 已注册')
}

export function getLastSavedConnection(): ConnectionConfig | undefined {
  return loadConnection()
}

export function getLastProducerConfig(): SendParams | undefined {
  return loadProducerConfig()
}

export function getLastConsumerConfig(): ConsumerConfig | undefined {
  return loadConsumerConfig()
}
```

- [ ] **Step 2: 创建 producer.ts**

```typescript
import { ipcMain } from 'electron'
import { producerService } from '../services/ProducerService'
import { logger } from '../utils/logger'
import type { ProgressInfo } from '../../shared/types'

export function registerProducerIpc(
  mainWindow: { webContents: { send: (ch: string, ...args: any[]) => void } }
): void {
  producerService.onProgress((p: ProgressInfo) => {
    mainWindow.webContents.send('producer:progress', p)
  })

  ipcMain.handle('producer:send', async (_evt, params) => {
    return await producerService.send(params)
  })

  ipcMain.handle('producer:getHistory', async () => {
    return producerService.getHistory()
  })

  logger.info('生产者 IPC 已注册')
}
```

- [ ] **Step 3: 创建 consumer.ts**

```typescript
import { ipcMain } from 'electron'
import { consumerService } from '../services/ConsumerService'
import { logger } from '../utils/logger'
import type { ConsumedMessage } from '../../shared/types'

export function registerConsumerIpc(
  mainWindow: { webContents: { send: (ch: string, ...args: any[]) => void } }
): void {
  consumerService.onMessage((msgs: ConsumedMessage[]) => {
    mainWindow.webContents.send('consumer:message', msgs)
  })

  consumerService.onStop(() => {
    mainWindow.webContents.send('consumer:stopped')
  })

  ipcMain.handle('consumer:start', async (_evt, config) => {
    return await consumerService.start(config)
  })
  ipcMain.handle('consumer:pause', async () => {
    return await consumerService.pause()
  })
  ipcMain.handle('consumer:resume', async () => {
    return await consumerService.resume()
  })
  ipcMain.handle('consumer:stop', async () => {
    return await consumerService.stop()
  })
  ipcMain.handle('consumer:ack', async (_evt, messageId: string) => {
    return await consumerService.ack(messageId)
  })
  ipcMain.handle('consumer:nack', async (_evt, messageId: string) => {
    return await consumerService.nack(messageId)
  })

  logger.info('消费者 IPC 已注册')
}
```

- [ ] **Step 4: 创建 dialog.ts**

```typescript
import { ipcMain, dialog } from 'electron'
import { logger } from '../utils/logger'

export function registerDialogIpc(): void {
  ipcMain.handle('dialog:selectFile', async () => {
    const r = await dialog.showOpenDialog({ properties: ['openFile'] })
    if (r.canceled) return null
    return r.filePaths[0]
  })
  logger.info('Dialog IPC 已注册')
}
```

- [ ] **Step 5: Commit**

```bash
git add EActiveMQTool/src/main/ipc/connection.ts EActiveMQTool/src/main/ipc/producer.ts EActiveMQTool/src/main/ipc/consumer.ts EActiveMQTool/src/main/ipc/dialog.ts
git commit -m "feat: 添加 IPC 注册层"
```

---

### Task 8: 主进程入口

**Files:**
- Create: `EActiveMQTool/src/main/index.ts`

- [ ] **Step 1: 创建 index.ts**

```typescript
import { app, shell, BrowserWindow } from 'electron'
import { join } from 'path'
import { electronApp, optimizer, is } from '@electron-toolkit/utils'
import { setMainWindow } from './utils/logger'
import { registerConnectionIpc, getLastSavedConnection, getLastProducerConfig, getLastConsumerConfig } from './ipc/connection'
import { registerProducerIpc } from './ipc/producer'
import { registerConsumerIpc } from './ipc/consumer'
import { registerDialogIpc } from './ipc/dialog'
import { producerService } from './services/ProducerService'
import { consumerService } from './services/ConsumerService'
import { connectionManager } from './services/ConnectionManager'

connectionManager.onStatusChange((info) => {
  if (info.status === 'disconnected' || info.status === 'error') {
    producerService.clearHistoryOnDisconnect()
    consumerService.clearOnDisconnect()
  }
})

function createWindow(): void {
  const mainWindow = new BrowserWindow({
    width: 900,
    height: 670,
    show: false,
    autoHideMenuBar: true,
    webPreferences: {
      preload: join(__dirname, '../preload/index.mjs'),
      sandbox: false
    }
  })

  mainWindow.on('ready-to-show', () => {
    mainWindow.show()
  })

  mainWindow.webContents.setWindowOpenHandler((details) => {
    shell.openExternal(details.url)
    return { action: 'deny' }
  })

  setMainWindow(mainWindow)

  registerConnectionIpc(mainWindow)

  registerProducerIpc(mainWindow)

  registerConsumerIpc(mainWindow)

  registerDialogIpc()

  mainWindow.webContents.on('did-finish-load', () => {
    const saved = getLastSavedConnection()
    if (saved) {
      mainWindow.webContents.send('connection:lastConfigLoaded', saved)
    }
    const producer = getLastProducerConfig()
    if (producer) {
      mainWindow.webContents.send('config:producerLoaded', producer)
    }
    const consumer = getLastConsumerConfig()
    if (consumer) {
      mainWindow.webContents.send('config:consumerLoaded', consumer)
    }
  })

  if (is.dev && process.env['ELECTRON_RENDERER_URL']) {
    mainWindow.loadURL(process.env['ELECTRON_RENDERER_URL'])
  } else {
    mainWindow.loadFile(join(__dirname, '../renderer/index.html'))
  }
}

app.whenReady().then(() => {
  electronApp.setAppUserModelId('com.fengchao12.eactivemqtool')

  app.on('browser-window-created', (_, window) => {
    optimizer.watchWindowShortcuts(window)
  })

  createWindow()

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow()
  })
})

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit()
  }
})
```

- [ ] **Step 2: Commit**

```bash
git add EActiveMQTool/src/main/index.ts
git commit -m "feat: 添加主进程入口"
```

---

### Task 9: Preload 脚本

**Files:**
- Create: `EActiveMQTool/src/preload/index.ts`

- [ ] **Step 1: 创建 preload/index.ts**

```typescript
import { contextBridge, ipcRenderer } from 'electron'
import type {
  ConnectionConfig, SendParams, SendResult, HistoryItem,
  ConsumedMessage, ConsumerConfig, IpcResult, ProgressInfo,
  ConnectionStatusInfo, LogEntry
} from '../shared/types'

export const api = {
  connection: {
    connect: (config: ConnectionConfig): Promise<IpcResult> =>
      ipcRenderer.invoke('connection:connect', config),
    test: (config: ConnectionConfig): Promise<IpcResult> =>
      ipcRenderer.invoke('connection:test', config),
    disconnect: (): Promise<IpcResult> =>
      ipcRenderer.invoke('connection:disconnect'),
    onStatusChange: (cb: (info: ConnectionStatusInfo) => void): void => {
      const handler = (_: unknown, info: ConnectionStatusInfo) => cb(info)
      ipcRenderer.on('connection:statusChanged', handler)
    },
    onLastConfigLoaded: (cb: (config: ConnectionConfig) => void): void => {
      const handler = (_: unknown, config: ConnectionConfig) => cb(config)
      ipcRenderer.on('connection:lastConfigLoaded', handler)
    }
  },
  producer: {
    send: (params: SendParams): Promise<SendResult> =>
      ipcRenderer.invoke('producer:send', params),
    getHistory: (): Promise<HistoryItem[]> =>
      ipcRenderer.invoke('producer:getHistory'),
    onProgress: (cb: (p: ProgressInfo) => void): void => {
      const handler = (_: unknown, p: ProgressInfo) => cb(p)
      ipcRenderer.on('producer:progress', handler)
    }
  },
  consumer: {
    start: (config: ConsumerConfig): Promise<IpcResult> =>
      ipcRenderer.invoke('consumer:start', config),
    pause: (): Promise<IpcResult> => ipcRenderer.invoke('consumer:pause'),
    resume: (): Promise<IpcResult> => ipcRenderer.invoke('consumer:resume'),
    stop: (): Promise<IpcResult> => ipcRenderer.invoke('consumer:stop'),
    ack: (messageId: string): Promise<IpcResult> =>
      ipcRenderer.invoke('consumer:ack', messageId),
    nack: (messageId: string): Promise<IpcResult> =>
      ipcRenderer.invoke('consumer:nack', messageId),
    onMessage: (cb: (msgs: ConsumedMessage[]) => void): void => {
      const handler = (_: unknown, msgs: ConsumedMessage[]) => cb(msgs)
      ipcRenderer.on('consumer:message', handler)
    },
    onStop: (cb: () => void): void => {
      const handler = () => cb()
      ipcRenderer.on('consumer:stopped', handler)
    }
  },
  log: {
    onLog: (cb: (entry: LogEntry) => void): void => {
      const handler = (_: unknown, entry: LogEntry) => cb(entry)
      ipcRenderer.on('log:entry', handler)
    }
  },
  dialog: {
    selectFile: (): Promise<string | null> => ipcRenderer.invoke('dialog:selectFile')
  },
  config: {
    saveProducer: (config: SendParams): Promise<{ success: boolean }> =>
      ipcRenderer.invoke('config:saveProducer', config),
    saveConsumer: (config: ConsumerConfig): Promise<{ success: boolean }> =>
      ipcRenderer.invoke('config:saveConsumer', config),
    loadProducer: (): Promise<SendParams | null> =>
      ipcRenderer.invoke('config:loadProducer'),
    loadConsumer: (): Promise<ConsumerConfig | null> =>
      ipcRenderer.invoke('config:loadConsumer'),
    onProducerLoaded: (cb: (config: SendParams) => void): void => {
      const handler = (_: unknown, config: SendParams) => cb(config)
      ipcRenderer.on('config:producerLoaded', handler)
    },
    onConsumerLoaded: (cb: (config: ConsumerConfig) => void): void => {
      const handler = (_: unknown, config: ConsumerConfig) => cb(config)
      ipcRenderer.on('config:consumerLoaded', handler)
    }
  }
}

if (process.contextIsolated) {
  try {
    contextBridge.exposeInMainWorld('api', api)
  } catch (error) {
    console.error(error)
  }
} else {
  // @ts-ignore
  window.api = api
}
```

- [ ] **Step 2: Commit**

```bash
git add EActiveMQTool/src/preload/index.ts
git commit -m "feat: 添加 preload 脚本"
```

---

### Task 10: 渲染进程基础文件

**Files:**
- Create: `EActiveMQTool/src/renderer/index.html`
- Create: `EActiveMQTool/src/renderer/src/main.ts`
- Create: `EActiveMQTool/src/renderer/src/env.d.ts`
- Create: `EActiveMQTool/src/renderer/src/styles/global.css`

- [ ] **Step 1: 创建 index.html**

```html
<!doctype html>
<html lang="zh-CN">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>EActiveMQTool</title>
  </head>
  <body>
    <div id="app"></div>
    <script type="module" src="./src/main.ts"></script>
  </body>
</html>
```

- [ ] **Step 2: 创建 main.ts**

```typescript
import { createApp } from 'vue'
import { createPinia } from 'pinia'
import ElementPlus from 'element-plus'
import 'element-plus/dist/index.css'
import 'element-plus/theme-chalk/dark/css-vars.css'
import './styles/global.css'
import App from './App.vue'
import { useConnectionStore } from './stores/connection'
import { useProducerStore } from './stores/producer'
import { useConsumerStore } from './stores/consumer'
import { useLogStore } from './stores/log'

const app = createApp(App)
app.use(createPinia())
app.use(ElementPlus)

const connectionStore = useConnectionStore()
const producerStore = useProducerStore()
const consumerStore = useConsumerStore()
const logStore = useLogStore()
connectionStore.bindIpc()
producerStore.bindIpc()
consumerStore.bindIpc()
logStore.bindIpc()

app.mount('#app')
```

- [ ] **Step 3: 创建 env.d.ts**

```typescript
/// <reference types="vite/client" />

import type { api } from '../../preload'

declare global {
  interface Window {
    api: typeof api
  }
}

declare module '*.vue' {
  import type { DefineComponent } from 'vue'
  const component: DefineComponent<{}, {}, any>
  export default component
}
```

- [ ] **Step 4: 创建 global.css**

```css
html, body, #app {
  margin: 0;
  padding: 0;
  height: 100%;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
}

html.dark {
  color-scheme: dark;
}
```

- [ ] **Step 5: Commit**

```bash
git add EActiveMQTool/src/renderer/index.html EActiveMQTool/src/renderer/src/main.ts EActiveMQTool/src/renderer/src/env.d.ts EActiveMQTool/src/renderer/src/styles/global.css
git commit -m "feat: 添加渲染进程基础文件"
```

---

### Task 11: Pinia Stores

**Files:**
- Create: `EActiveMQTool/src/renderer/src/stores/connection.ts`
- Create: `EActiveMQTool/src/renderer/src/stores/producer.ts`
- Create: `EActiveMQTool/src/renderer/src/stores/consumer.ts`
- Create: `EActiveMQTool/src/renderer/src/stores/log.ts`
- Create: `EActiveMQTool/src/renderer/src/stores/settings.ts`

- [ ] **Step 1: 创建 connection.ts**

```typescript
import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { ConnectionConfig, ConnectionStatusInfo } from '../../../shared/types'

export const useConnectionStore = defineStore('connection', () => {
  const status = ref<ConnectionStatusInfo>({
    status: 'disconnected',
    display: '未连接',
    sslEnabled: false
  })
  const lastConfig = ref<ConnectionConfig | null>(null)
  const connecting = ref(false)

  async function connect(config: ConnectionConfig) {
    connecting.value = true
    try {
      const r = await window.api.connection.connect(config)
      return r
    } finally {
      connecting.value = false
    }
  }

  async function test(config: ConnectionConfig) {
    return await window.api.connection.test(config)
  }

  async function disconnect() {
    return await window.api.connection.disconnect()
  }

  function bindIpc() {
    window.api.connection.onStatusChange((info) => {
      status.value = info
    })
    window.api.connection.onLastConfigLoaded?.((config) => {
      lastConfig.value = config
    })
  }

  return { status, lastConfig, connecting, connect, test, disconnect, bindIpc }
})
```

- [ ] **Step 2: 创建 producer.ts**

```typescript
import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { SendParams, SendResult, HistoryItem, ProgressInfo } from '../../../shared/types'

export const useProducerStore = defineStore('producer', () => {
  const history = ref<HistoryItem[]>([])
  const progress = ref<ProgressInfo | null>(null)
  const sending = ref(false)

  async function send(params: SendParams): Promise<SendResult> {
    sending.value = true
    try {
      const r = await window.api.producer.send(params)
      return r
    } finally {
      sending.value = false
    }
  }

  async function loadHistory() {
    history.value = await window.api.producer.getHistory()
  }

  function bindIpc() {
    window.api.producer.onProgress((p) => {
      progress.value = p
    })
  }

  return { history, progress, sending, send, loadHistory, bindIpc }
})
```

- [ ] **Step 3: 创建 consumer.ts**

```typescript
import { defineStore } from 'pinia'
import { ref, shallowRef } from 'vue'
import type { ConsumedMessage, ConsumerConfig, IpcResult } from '../../../shared/types'

export const useConsumerStore = defineStore('consumer', () => {
  const messages = shallowRef<ConsumedMessage[]>([])
  const running = ref(false)
  const paused = ref(false)
  const maxCache = ref(1000)
  const ackMode = ref<'auto' | 'client' | 'client-individual'>('auto')

  async function start(config: ConsumerConfig): Promise<IpcResult> {
    const r = await window.api.consumer.start(config)
    if (r.success) {
      running.value = true
      paused.value = false
      ackMode.value = config.ackMode
    }
    return r
  }

  async function pause() {
    const r = await window.api.consumer.pause()
    if (r.success) paused.value = true
    return r
  }

  async function resume() {
    const r = await window.api.consumer.resume()
    if (r.success) paused.value = false
    return r
  }

  async function stop() {
    const r = await window.api.consumer.stop()
    if (r.success) {
      running.value = false
      paused.value = false
      messages.value = []
    }
    return r
  }

  async function ack(messageId: string) {
    const r = await window.api.consumer.ack(messageId)
    if (r.success) {
      messages.value = messages.value.filter((m) => m.messageId !== messageId)
    }
    return r
  }

  async function nack(messageId: string) {
    const r = await window.api.consumer.nack(messageId)
    if (r.success) {
      messages.value = messages.value.filter((m) => m.messageId !== messageId)
    }
    return r
  }

  let rafId: number | null = null
  let pendingBatch: ConsumedMessage[] = []

  function flushPending() {
    if (pendingBatch.length === 0) return
    const frozen = pendingBatch.map((m) => Object.freeze(m))
    pendingBatch = []
    const current = messages.value
    const merged = current.concat(frozen)
    if (merged.length > maxCache.value) {
      messages.value = merged.slice(merged.length - maxCache.value)
    } else {
      messages.value = merged
    }
  }

  function bindIpc() {
    window.api.consumer.onMessage((msgs) => {
      pendingBatch = pendingBatch.concat(msgs)
      if (rafId === null) {
        rafId = requestAnimationFrame(() => {
          rafId = null
          flushPending()
        })
      }
    })
    window.api.consumer.onStop(() => {
      if (rafId !== null) {
        cancelAnimationFrame(rafId)
        rafId = null
      }
      flushPending()
      running.value = false
      paused.value = false
    })
  }

  return { messages, running, paused, maxCache, ackMode, start, pause, resume, stop, ack, nack, bindIpc }
})
```

- [ ] **Step 4: 创建 log.ts**

```typescript
import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { LogEntry } from '../../../shared/types'

export const useLogStore = defineStore('log', () => {
  const entries = ref<LogEntry[]>([])
  const visible = ref(false)

  function add(entry: LogEntry) {
    entries.value.push(entry)
    if (entries.value.length > 500) {
      entries.value = entries.value.slice(entries.value.length - 500)
    }
  }

  function toggle() {
    visible.value = !visible.value
  }

  function clear() {
    entries.value = []
  }

  function bindIpc() {
    window.api.log.onLog((entry) => add(entry))
  }

  return { entries, visible, add, toggle, clear, bindIpc }
})
```

- [ ] **Step 5: 创建 settings.ts**

```typescript
import { defineStore } from 'pinia'
import { ref } from 'vue'

export const useSettingsStore = defineStore('settings', () => {
  const theme = ref<'light' | 'dark'>('light')
  const maxMessageCache = ref(1000)
  const maxDisplayLength = ref(1000)

  function setTheme(t: 'light' | 'dark') {
    theme.value = t
    document.documentElement.classList.toggle('dark', t === 'dark')
  }

  return { theme, maxMessageCache, maxDisplayLength, setTheme }
})
```

- [ ] **Step 6: Commit**

```bash
git add EActiveMQTool/src/renderer/src/stores/connection.ts EActiveMQTool/src/renderer/src/stores/producer.ts EActiveMQTool/src/renderer/src/stores/consumer.ts EActiveMQTool/src/renderer/src/stores/log.ts EActiveMQTool/src/renderer/src/stores/settings.ts
git commit -m "feat: 添加 Pinia stores"
```

---

### Task 12: App.vue 根组件

**Files:**
- Create: `EActiveMQTool/src/renderer/src/App.vue`

- [ ] **Step 1: 创建 App.vue**

```vue
<template>
  <el-container class="app-container">
    <el-header class="app-header">
      <div class="app-title">EActiveMQ Tool</div>
      <div class="header-status">
        <el-tag :type="statusTagType" size="small">{{ connectionStore.status.display }}</el-tag>
        <el-tag v-if="connectionStore.status.sslEnabled" type="success" size="small">SSL</el-tag>
      </div>
      <div class="header-actions">
        <el-button size="small" @click="logStore.toggle()">日志</el-button>
        <el-button size="small" @click="showSettings = true">设置</el-button>
      </div>
    </el-header>

    <el-main class="app-main">
      <ConnectionForm />
      <el-tabs v-model="activeTab">
        <el-tab-pane label="生产者" name="producer" lazy="false">
          <ProducerView v-show="activeTab === 'producer'" />
        </el-tab-pane>
        <el-tab-pane label="消费者" name="consumer" lazy="false">
          <ConsumerView v-show="activeTab === 'consumer'" />
        </el-tab-pane>
      </el-tabs>
    </el-main>

    <el-footer class="app-footer">
      <span>{{ connectionStore.status.display }}</span>
      <span class="footer-counts">
        发送：{{ producerStore.history.length }} | 接收：{{ consumerStore.messages.length }}
      </span>
    </el-footer>

    <LogPanel v-if="logStore.visible" class="app-log-panel" />
    <SettingsView v-model:visible="showSettings" />
  </el-container>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useConnectionStore } from './stores/connection'
import { useProducerStore } from './stores/producer'
import { useConsumerStore } from './stores/consumer'
import { useLogStore } from './stores/log'
import ProducerView from './views/ProducerView.vue'
import ConsumerView from './views/ConsumerView.vue'
import LogPanel from './components/LogPanel.vue'
import SettingsView from './views/SettingsView.vue'
import ConnectionForm from './components/ConnectionForm.vue'

const connectionStore = useConnectionStore()
const producerStore = useProducerStore()
const consumerStore = useConsumerStore()
const logStore = useLogStore()

const activeTab = ref<'producer' | 'consumer'>('producer')
const showSettings = ref(false)

const statusTagType = computed(() => {
  switch (connectionStore.status.status) {
    case 'connected': return 'success'
    case 'connecting': return 'warning'
    case 'error': return 'danger'
    default: return 'info'
  }
})
</script>

<style scoped>
.app-container { height: 100vh; display: flex; flex-direction: column; }
.app-header { display: flex; align-items: center; gap: 16px; border-bottom: 1px solid var(--el-border-color); }
.app-title { font-weight: bold; font-size: 16px; }
.header-status { display: flex; gap: 6px; align-items: center; }
.header-actions { margin-left: auto; display: flex; gap: 8px; }
.app-main { flex: 1; overflow: auto; }
.app-footer { display: flex; justify-content: space-between; align-items: center; border-top: 1px solid var(--el-border-color); font-size: 12px; height: 32px; }
.app-log-panel { border-top: 1px solid var(--el-border-color); }
</style>
```

- [ ] **Step 2: Commit**

```bash
git add EActiveMQTool/src/renderer/src/App.vue
git commit -m "feat: 添加 App.vue 根组件"
```

---

### Task 13: Vue 组件

**Files:**
- Create: `EActiveMQTool/src/renderer/src/components/ConnectionForm.vue`
- Create: `EActiveMQTool/src/renderer/src/components/LogPanel.vue`
- Create: `EActiveMQTool/src/renderer/src/components/MessageTable.vue`
- Create: `EActiveMQTool/src/renderer/src/components/MessageDetail.vue`

- [ ] **Step 1: 创建 ConnectionForm.vue**

```vue
<template>
  <el-card class="connection-form" shadow="never">
    <el-form :model="form" label-width="100px" size="small" inline>
      <el-form-item label="主机">
        <el-input v-model="form.host" placeholder="localhost" style="width: 160px" />
      </el-form-item>
      <el-form-item label="端口">
        <el-input-number v-model="form.port" :min="1" :max="65535" controls-position="right" style="width: 110px" />
      </el-form-item>
      <el-form-item label="用户名">
        <el-input v-model="form.username" style="width: 120px" />
      </el-form-item>
      <el-form-item label="密码">
        <el-input v-model="form.password" type="password" show-password style="width: 140px" />
      </el-form-item>
      <el-form-item label="SSL">
        <el-switch v-model="form.sslEnabled" />
      </el-form-item>
    </el-form>

    <el-collapse-transition>
      <div v-if="form.sslEnabled" class="ssl-config">
        <el-form :model="form" label-width="120px" size="small" inline>
          <el-form-item label="心跳发送(ms)">
            <el-input-number v-model="form.heartbeatOutgoing" :min="0" :step="1000" controls-position="right" style="width: 140px" />
          </el-form-item>
          <el-form-item label="心跳接收(ms)">
            <el-input-number v-model="form.heartbeatIncoming" :min="0" :step="1000" controls-position="right" style="width: 140px" />
          </el-form-item>
        </el-form>
      </div>
    </el-collapse-transition>

    <div class="form-actions">
      <el-button
        type="primary"
        :loading="connectionStore.connecting"
        :disabled="connectionStore.connecting || connectionStore.status.status === 'connected'"
        @click="onConnect"
      >连接</el-button>
      <el-button
        :loading="testing"
        :disabled="connectionStore.connecting"
        @click="onTest"
      >测试连接</el-button>
      <el-button
        :loading="disconnecting"
        :disabled="connectionStore.status.status !== 'connected'"
        @click="onDisconnect"
      >断开</el-button>
    </div>
  </el-card>
</template>

<script setup lang="ts">
import { reactive, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { useConnectionStore } from '../stores/connection'
import type { ConnectionConfig } from '../../../shared/types'

const connectionStore = useConnectionStore()

const testing = ref(false)
const disconnecting = ref(false)

const form = reactive<ConnectionConfig>({
  host: 'localhost',
  port: 61614,
  username: 'admin',
  password: 'admin',
  sslEnabled: false,
  heartbeatOutgoing: 10000,
  heartbeatIncoming: 10000
})

watch(
  () => connectionStore.lastConfig,
  (cfg) => {
    if (cfg) Object.assign(form, cfg)
  },
  { immediate: true }
)

watch(
  () => form.sslEnabled,
  (ssl, old) => {
    if (old === undefined) return
    const defaultPort = ssl ? 61617 : 61614
    const otherDefault = ssl ? 61614 : 61617
    if (form.port === otherDefault) {
      form.port = defaultPort
    }
  }
)

async function onConnect(): Promise<void> {
  const r = await connectionStore.connect({ ...form })
  if (r.success) ElMessage.success('连接成功')
  else ElMessage.error(r.error || '连接失败')
}

async function onTest(): Promise<void> {
  testing.value = true
  try {
    const r = await connectionStore.test({ ...form })
    if (r.success) ElMessage.success('测试连接成功')
    else ElMessage.error(r.error || '测试失败')
  } finally {
    testing.value = false
  }
}

async function onDisconnect(): Promise<void> {
  disconnecting.value = true
  try {
    await connectionStore.disconnect()
  } finally {
    disconnecting.value = false
  }
}
</script>

<style scoped>
.connection-form {
  margin-bottom: 12px;
}
.ssl-config {
  padding: 8px 0;
  border-top: 1px dashed var(--el-border-color);
  margin-top: 8px;
}
.form-actions {
  margin-top: 8px;
}
</style>
```

- [ ] **Step 2: 创建 LogPanel.vue**

```vue
<template>
  <div class="log-panel">
    <div class="log-header">
      <span>日志</span>
      <div>
        <el-button size="small" @click="logStore.clear()">清空</el-button>
        <el-button size="small" @click="logStore.toggle()">收起</el-button>
      </div>
    </div>
    <div class="log-list" ref="logListRef">
      <div
        v-for="(entry, i) in logStore.entries"
        :key="i"
        :class="['log-entry', `level-${entry.level.toLowerCase()}`]"
      >
        <span class="log-time">{{ entry.time }}</span>
        <span class="log-level">{{ entry.level }}</span>
        <span class="log-msg">{{ entry.message }}</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, nextTick } from 'vue'
import { useLogStore } from '../stores/log'

const logStore = useLogStore()
const logListRef = ref<HTMLElement | null>(null)

watch(() => logStore.entries.length, async () => {
  await nextTick()
  if (logListRef.value) {
    logListRef.value.scrollTop = logListRef.value.scrollHeight
  }
})
</script>

<style scoped>
.log-panel { height: 200px; display: flex; flex-direction: column; background: var(--el-bg-color); }
.log-header { display: flex; justify-content: space-between; align-items: center; padding: 4px 8px; border-bottom: 1px solid var(--el-border-color); }
.log-list { flex: 1; overflow-y: auto; padding: 4px 8px; font-family: monospace; font-size: 12px; }
.log-entry { padding: 2px 0; }
.log-time { color: var(--el-text-color-secondary); margin-right: 8px; }
.log-level { margin-right: 8px; font-weight: bold; }
.level-info .log-level { color: var(--el-color-info); }
.level-warn .log-level { color: var(--el-color-warning); }
.level-error .log-level { color: var(--el-color-danger); }
</style>
```

- [ ] **Step 3: 创建 MessageTable.vue**

```vue
<template>
  <div ref="containerRef" class="message-table-container">
    <ElTableV2
      v-if="tableSize.width > 0 && tableSize.height > 0"
      :columns="columns"
      :data="consumerStore.messages"
      :width="tableSize.width"
      :height="tableSize.height"
      :row-height="36"
      fixed
    />
  </div>

  <MessageDetail v-model:visible="detailVisible" :message="detailMessage" />
</template>

<script setup lang="ts">
import { ref, computed, h, onMounted, onUnmounted } from "vue";
import { ElTableV2 } from "element-plus";
import { ElButton, ElMessage } from "element-plus";
import MessageDetail from "./MessageDetail.vue";
import { useConsumerStore } from "../stores/consumer";
import { useSettingsStore } from "../stores/settings";
import type { ConsumedMessage } from "../../../shared/types";
import type { Column } from "element-plus";
import type { CellRendererParams } from "element-plus/es/components/table-v2/src/types";
import { FixedDir } from "element-plus/es/components/table-v2/src/constants";

const consumerStore = useConsumerStore();
const settingsStore = useSettingsStore();
const isAutoAck = computed(() => consumerStore.ackMode === 'auto');

const detailVisible = ref(false);
const detailMessage = ref<ConsumedMessage | null>(null);

const containerRef = ref<HTMLElement | null>(null);
const tableSize = ref({ width: 0, height: 0 });

let resizeObserver: ResizeObserver | null = null;

onMounted(() => {
  if (containerRef.value) {
    tableSize.value = {
      width: containerRef.value.clientWidth,
      height: containerRef.value.clientHeight,
    };
    resizeObserver = new ResizeObserver((entries) => {
      const entry = entries[0];
      if (entry) {
        tableSize.value = {
          width: entry.contentRect.width,
          height: entry.contentRect.height,
        };
      }
    });
    resizeObserver.observe(containerRef.value);
  }
});

onUnmounted(() => {
  resizeObserver?.disconnect();
});

function truncateContent(content: string): string {
  const max = settingsStore.maxDisplayLength;
  return content.length > max ? content.slice(0, max) + "..." : content;
}

function onView(row: ConsumedMessage) {
  detailMessage.value = row;
  detailVisible.value = true;
}

async function onAck(row: ConsumedMessage) {
  const r = await consumerStore.ack(row.messageId);
  if (r.success) ElMessage.success("已确认");
  else ElMessage.error(r.error || "确认失败");
}

async function onNack(row: ConsumedMessage) {
  const r = await consumerStore.nack(row.messageId);
  if (r.success) ElMessage.success("已拒绝");
  else ElMessage.error(r.error || "拒绝失败");
}

const columns: Column<ConsumedMessage>[] = [
  {
    key: "seq",
    dataKey: "seq",
    title: "序号",
    width: 70,
    align: "center",
  },
  {
    key: "receivedAt",
    dataKey: "receivedAt",
    title: "接收时间",
    width: 180,
  },
  {
    key: "destination",
    dataKey: "destination",
    title: "目标",
    width: 200,
  },
  {
    key: "messageId",
    dataKey: "messageId",
    title: "消息ID",
    width: 200,
  },
  {
    key: "body",
    dataKey: "body",
    title: "消息体",
    width: 600,
    flexGrow: 1,
    minWidth: 120,
    cellRenderer: ({ cellData }: CellRendererParams<ConsumedMessage>) => {
      const text = String(cellData ?? "");
      return h(
        "span",
        {
          title: text,
          style: {
            display: "block",
            overflow: "hidden",
            textOverflow: "ellipsis",
            whiteSpace: "nowrap",
          },
        },
        truncateContent(text),
      );
    },
  },
  {
    key: "actions",
    dataKey: "messageId",
    title: "操作",
    width: 200,
    fixed: FixedDir.RIGHT,
    cellRenderer: ({ rowData }: CellRendererParams<ConsumedMessage>) => {
      const children = [
        h(
          ElButton,
          {
            size: "small",
            link: true,
            onClick: () => onView(rowData as ConsumedMessage),
          },
          () => "详情",
        ),
      ];
      if (!isAutoAck.value) {
        children.push(
          h(
            ElButton,
            {
              size: "small",
              link: true,
              type: "success",
              onClick: () => onAck(rowData as ConsumedMessage),
            },
            () => "确认",
          ),
          h(
            ElButton,
            {
              size: "small",
              link: true,
              type: "danger",
              onClick: () => onNack(rowData as ConsumedMessage),
            },
            () => "拒绝",
          ),
        );
      }
      return h("div", { style: { display: "flex", gap: "4px" } }, children);
    },
  },
];
</script>

<style scoped>
.message-table-container {
  height: 100%;
  width: 100%;
  min-height: 200px;
}
</style>
```

- [ ] **Step 4: 创建 MessageDetail.vue**

```vue
<template>
  <el-dialog :model-value="visible" @update:model-value="$emit('update:visible', $event)" title="消息详情" width="700px">
    <div v-if="message">
      <el-descriptions :column="2" border size="small">
        <el-descriptions-item label="序号">{{ message.seq }}</el-descriptions-item>
        <el-descriptions-item label="接收时间">{{ message.receivedAt }}</el-descriptions-item>
        <el-descriptions-item label="目标">{{ message.destination }}</el-descriptions-item>
        <el-descriptions-item label="消息ID">{{ message.messageId || '-' }}</el-descriptions-item>
        <el-descriptions-item label="已确认">{{ message.acked ? '是' : '否' }}</el-descriptions-item>
        <el-descriptions-item label="Headers" :span="2">{{ message.headers }}</el-descriptions-item>
      </el-descriptions>

      <div class="detail-body">
        <div class="body-header">
          <span>消息体</span>
          <el-button size="small" @click="compressed = !compressed">{{ compressed ? '格式化' : '压缩' }}</el-button>
        </div>
        <pre class="body-content">{{ displayContent }}</pre>
      </div>
    </div>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import type { ConsumedMessage } from '../../../shared/types'

const props = defineProps<{ message: ConsumedMessage | null; visible: boolean }>()
defineEmits<{ 'update:visible': [boolean] }>()

const compressed = ref(true)

const displayContent = computed(() => {
  if (!props.message) return ''
  const raw = props.message.body
  if (compressed.value) return raw
  try {
    return JSON.stringify(JSON.parse(raw), null, 2)
  } catch {
    return raw
  }
})
</script>

<style scoped>
.detail-body { margin-top: 12px; }
.body-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 4px; }
.body-content { background: var(--el-fill-color-light); padding: 8px; max-height: 300px; overflow: auto; white-space: pre-wrap; word-break: break-all; }
</style>
```

- [ ] **Step 5: Commit**

```bash
git add EActiveMQTool/src/renderer/src/components/ConnectionForm.vue EActiveMQTool/src/renderer/src/components/LogPanel.vue EActiveMQTool/src/renderer/src/components/MessageTable.vue EActiveMQTool/src/renderer/src/components/MessageDetail.vue
git commit -m "feat: 添加 Vue 组件"
```

---

### Task 14: Vue 视图

**Files:**
- Create: `EActiveMQTool/src/renderer/src/views/ProducerView.vue`
- Create: `EActiveMQTool/src/renderer/src/views/ConsumerView.vue`
- Create: `EActiveMQTool/src/renderer/src/views/SettingsView.vue`

- [ ] **Step 1: 创建 ProducerView.vue**

```vue
<template>
  <div class="producer-view">
    <el-card shadow="never" class="producer-card">
      <el-form :model="form" label-width="100px" size="small">
        <el-divider content-position="left">目标地址</el-divider>
        <el-form-item label="Destination">
          <el-input v-model="form.destination" placeholder="/queue/test 或 /topic/test" style="width: 320px" />
        </el-form-item>

        <el-divider content-position="left">消息内容</el-divider>
        <el-form-item label="消息格式">
          <el-select v-model="form.format" style="width: 120px">
            <el-option label="JSON" value="json" />
            <el-option label="纯文本" value="text" />
            <el-option label="XML" value="xml" />
          </el-select>
        </el-form-item>
        <el-form-item label="消息体">
          <el-input
            v-model="form.body"
            type="textarea"
            :rows="8"
            placeholder="输入消息内容"
          />
        </el-form-item>

        <el-divider content-position="left">消息属性</el-divider>
        <el-form-item label="contentType">
          <el-input v-model="form.contentType" style="width: 200px" />
        </el-form-item>
        <el-form-item label="持久化">
          <el-switch v-model="form.persistent" />
        </el-form-item>
        <el-form-item label="priority">
          <el-input-number v-model="form.priority" :min="0" :max="9" controls-position="right" style="width: 100px" />
        </el-form-item>
        <el-form-item label="expires(ms)">
          <el-input-number v-model="form.expires" :min="0" :step="1000" controls-position="right" style="width: 140px" />
        </el-form-item>

        <el-divider content-position="left">批量发送</el-divider>
        <el-form-item label="发送次数">
          <el-input-number v-model="form.batch.count" :min="1" :max="10000" controls-position="right" style="width: 140px" />
        </el-form-item>
        <el-form-item label="间隔(ms)">
          <el-input-number v-model="form.batch.intervalMs" :min="0" :step="100" controls-position="right" style="width: 140px" />
        </el-form-item>
      </el-form>

      <div class="producer-actions">
        <el-button type="primary" :loading="producerStore.sending" @click="onSend">发送消息</el-button>
        <el-button @click="form.body = ''">清空消息</el-button>
        <span v-if="producerStore.progress" class="progress-text">
          {{ producerStore.progress.current }}/{{ producerStore.progress.total }}
        </span>
      </div>

      <el-collapse class="history-collapse">
        <el-collapse-item title="历史记录（最近10条）">
          <el-table :data="producerStore.history" size="small" @row-click="onHistoryClick">
            <el-table-column prop="time" label="时间" width="180" />
            <el-table-column prop="destination" label="目标" width="240" />
            <el-table-column prop="messageSummary" label="消息摘要" />
          </el-table>
        </el-collapse-item>
      </el-collapse>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { reactive, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { useProducerStore } from '../stores/producer'
import { useConnectionStore } from '../stores/connection'
import type { SendParams } from '../../../shared/types'

const producerStore = useProducerStore()
const connectionStore = useConnectionStore()

const form = reactive<SendParams>({
  destination: '',
  body: '',
  format: 'json',
  contentType: 'application/json',
  persistent: true,
  priority: 0,
  expires: 0,
  headers: {},
  batch: { count: 1, intervalMs: 100 }
})

onMounted(async () => {
  producerStore.loadHistory()
  const cfg = await window.api.config.loadProducer()
  if (cfg) {
    Object.assign(form, cfg)
    if (cfg.batch) Object.assign(form.batch, cfg.batch)
  }
})

async function onSend() {
  if (!connectionStore.status.status || connectionStore.status.status !== 'connected') {
    ElMessage.warning('请先建立连接')
    return
  }
  const params = { ...form, headers: { ...form.headers }, batch: { ...form.batch } }
  const r = await producerStore.send(params)
  if (r.success) {
    ElMessage.success(`发送成功，消息ID：${r.messageId}`)
    await producerStore.loadHistory()
    window.api.config.saveProducer(params)
  } else {
    ElMessage.error(r.error || '发送失败')
  }
}

function onHistoryClick(row: any) {
  form.body = row.messageSummary
}
</script>

<style scoped>
.producer-view { padding: 8px; }
.producer-card { margin-top: 12px; }
.producer-actions { margin: 12px 0; display: flex; align-items: center; gap: 12px; }
.progress-text { color: var(--el-color-primary); }
.history-collapse { margin-top: 12px; }
</style>
```

- [ ] **Step 2: 创建 ConsumerView.vue**

```vue
<template>
  <div class="consumer-view">
    <el-card shadow="never" class="consumer-card">
      <el-form :model="form" label-width="100px" size="small" :disabled="consumerStore.running">
        <el-divider content-position="left">消费配置</el-divider>
        <el-form-item label="Destination">
          <el-input v-model="form.destination" placeholder="/queue/test 或 /topic/test" style="width: 320px" />
        </el-form-item>
        <el-form-item label="ACK 模式">
          <el-select v-model="form.ackMode" style="width: 200px">
            <el-option label="auto（自动确认）" value="auto" />
            <el-option label="client（客户端确认）" value="client" />
            <el-option label="client-individual（单条确认）" value="client-individual" />
          </el-select>
        </el-form-item>
        <el-form-item v-if="form.ackMode === 'client-individual'" label="预取数">
          <el-input-number v-model="form.prefetchCount" :min="1" controls-position="right" style="width: 120px" />
        </el-form-item>
        <el-form-item label="Selector">
          <el-input v-model="form.selector" placeholder="JMS selector，如 color='red'" style="width: 320px" />
        </el-form-item>
        <el-form-item label="路由键过滤">
          <el-input v-model="form.filterRoutingKey" placeholder="如 order.create" style="width: 200px" />
        </el-form-item>
        <el-form-item label="Header过滤键">
          <el-input v-model="form.filterByHeaderKey" style="width: 160px" />
        </el-form-item>
        <el-form-item label="Header过滤值">
          <el-input v-model="form.filterByHeaderValue" style="width: 160px" />
        </el-form-item>
        <el-form-item label="最大接收数">
          <el-input-number v-model="form.maxReceive" :min="0" controls-position="right" style="width: 120px" />
          <span class="hint">0=无限制</span>
        </el-form-item>
      </el-form>

      <div class="consumer-actions">
        <el-button v-if="!consumerStore.running" type="primary" @click="onStart">开始消费</el-button>
        <el-button v-if="consumerStore.running && !consumerStore.paused" @click="onPause">暂停</el-button>
        <el-button v-if="consumerStore.paused" type="primary" @click="onResume">恢复</el-button>
        <el-button v-if="consumerStore.running" type="danger" @click="onStop">停止</el-button>
      </div>
    </el-card>

    <div class="message-list">
      <MessageTable />
    </div>
  </div>
</template>

<script setup lang="ts">
import { reactive, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import MessageTable from '../components/MessageTable.vue'
import { useConsumerStore } from '../stores/consumer'
import { useConnectionStore } from '../stores/connection'
import type { ConsumerConfig } from '../../../shared/types'

const consumerStore = useConsumerStore()
const connectionStore = useConnectionStore()

const form = reactive<ConsumerConfig>({
  destination: '',
  ackMode: 'auto',
  selector: '',
  prefetchCount: 1,
  maxReceive: 0,
  filterRoutingKey: '',
  filterByHeaderKey: '',
  filterByHeaderValue: ''
})

async function onStart() {
  if (connectionStore.status.status !== 'connected') {
    ElMessage.warning('请先建立连接')
    return
  }
  const cfg = { ...form }
  const r = await consumerStore.start(cfg)
  if (!r.success) ElMessage.error(r.error || '启动失败')
  else {
    ElMessage.success('已开始消费')
    window.api.config.saveConsumer(cfg)
  }
}

async function onPause() {
  const r = await consumerStore.pause()
  if (!r.success) ElMessage.error(r.error || '暂停失败')
}

async function onResume() {
  const r = await consumerStore.resume()
  if (!r.success) ElMessage.error(r.error || '恢复失败')
}

async function onStop() {
  await consumerStore.stop()
  ElMessage.info('已停止消费')
}

onMounted(async () => {
  const cfg = await window.api.config.loadConsumer()
  if (cfg) {
    Object.assign(form, cfg)
  }
})
</script>

<style scoped>
.consumer-view { padding: 8px; display: flex; flex-direction: column; height: 100%; }
.consumer-card { margin-bottom: 8px; }
.consumer-actions { margin: 8px 0; }
.hint { color: var(--el-text-color-secondary); font-size: 12px; margin-left: 8px; }
.message-list { flex: 1; min-height: 200px; overflow: hidden; }
</style>
```

- [ ] **Step 3: 创建 SettingsView.vue**

```vue
<template>
  <el-dialog :model-value="visible" @update:model-value="$emit('update:visible', $event)" title="设置" width="480px">
    <el-form label-width="160px" size="small">
      <el-form-item label="主题">
        <el-radio-group :model-value="settingsStore.theme" @update:model-value="settingsStore.setTheme($event)">
          <el-radio label="light">浅色</el-radio>
          <el-radio label="dark">深色</el-radio>
        </el-radio-group>
      </el-form-item>
      <el-form-item label="最大消息缓存数">
        <el-input-number v-model="settingsStore.maxMessageCache" :min="100" :step="100" controls-position="right" />
      </el-form-item>
      <el-form-item label="消息体最大显示长度">
        <el-input-number v-model="settingsStore.maxDisplayLength" :min="100" :step="100" controls-position="right" />
      </el-form-item>
    </el-form>
  </el-dialog>
</template>

<script setup lang="ts">
import { useSettingsStore } from '../stores/settings'

defineProps<{ visible: boolean }>()
defineEmits<{ 'update:visible': [boolean] }>()

const settingsStore = useSettingsStore()
</script>
```

- [ ] **Step 4: Commit**

```bash
git add EActiveMQTool/src/renderer/src/views/ProducerView.vue EActiveMQTool/src/renderer/src/views/ConsumerView.vue EActiveMQTool/src/renderer/src/views/SettingsView.vue
git commit -m "feat: 添加 Vue 视图（Producer/Consumer/Settings）"
```

---

### Task 15: 验证构建

- [ ] **Step 1: 运行 typecheck**

```bash
cd EActiveMQTool && npm run typecheck
```

Expected: 无类型错误。

- [ ] **Step 2: 运行 build**

```bash
cd EActiveMQTool && npm run build
```

Expected: 构建成功，`out/` 目录生成。

- [ ] **Step 3: 修复任何构建错误**

如果有类型或构建错误，修复后重新运行验证。

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "fix: 修复构建和类型检查错误"
```