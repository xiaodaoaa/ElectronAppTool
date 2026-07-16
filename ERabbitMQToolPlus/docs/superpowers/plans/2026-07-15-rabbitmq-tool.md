# Electron RabbitMQ 工具 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现一个基于 Electron + Vue 3 的 RabbitMQ 可视化工具 MVP，支持 SSL 连接、生产者发送、消费者消费、基础日志与设置。

**Architecture:** electron-vite 三进程架构（main/preload/renderer）。主进程承载 ConnectionManager 单例 + Producer/ConsumerService + IPC handler；preload 用 contextBridge 暴露最小 API 面；渲染进程 Vue 3 + Element Plus + Pinia，通过 `window.api.*` 调用主进程。单连接多 channel 模式。

**Tech Stack:** Electron（最新稳定版）、electron-vite、electron-builder、Vue 3、Element Plus、Pinia、amqplib、electron-store、dayjs、uuid。

**参考设计文档:** `docs/superpowers/specs/2026-07-15-rabbitmq-tool-design.md`

---

## 文件结构总览

**主进程 (`src/main/`)**
- `index.ts` — 入口：创建窗口、注册所有 IPC、初始化 store 加载
- `ipc/connection.ts` — 连接 IPC handler（connect/test/disconnect/onStatusChange）
- `ipc/producer.ts` — 生产者 IPC handler（send/onProgress/getHistory）
- `ipc/consumer.ts` — 消费者 IPC handler（start/pause/resume/stop/pull/ack/nack/onMessage）
- `services/ConnectionManager.ts` — 单例：管连接 + 状态 + 事件
- `services/ProducerService.ts` — 声明交换机 + publish + 批量 + 历史
- `services/ConsumerService.ts` — 声明队列 + consume + ack/nack + 过滤 + 拉模式
- `utils/ssl.ts` — SSL 配置构造
- `utils/store.ts` — electron-store 封装（密码加密）
- `utils/logger.ts` — 主进程日志收集器（推送给渲染进程）
- `types.ts` — 共享类型定义（ConnectionConfig/SendParams/ConsumedMessage 等）

**Preload (`src/preload/`)**
- `index.ts` — contextBridge 暴露 window.api

**渲染进程 (`src/renderer/`)**
- `index.html`、`src/main.ts`、`src/App.vue`
- `components/ConnectionForm.vue` — 连接配置表单（含 SSL）
- `components/MessageTable.vue` — 消费者消息列表
- `components/MessageDetail.vue` — 消息详情弹窗
- `components/LogPanel.vue` — 日志面板
- `views/ProducerView.vue` — 生产者 Tab
- `views/ConsumerView.vue` — 消费者 Tab
- `views/SettingsView.vue` — 设置页
- `stores/connection.ts`、`stores/producer.ts`、`stores/consumer.ts`、`stores/log.ts`、`stores/settings.ts`
- `utils/format.ts` — 时间/消息体格式化
- `types.ts` — 渲染进程类型（与主进程共享部分）

---

## Task 1: 项目脚手架初始化

**Files:**
- Create: `package.json`
- Create: `electron.vite.config.ts`
- Create: `tsconfig.json`、`tsconfig.node.json`、`tsconfig.web.json`
- Create: `src/main/index.ts`（最小入口）
- Create: `src/preload/index.ts`（最小 preload）
- Create: `src/renderer/index.html`
- Create: `src/renderer/src/main.ts`
- Create: `src/renderer/src/App.vue`
- Create: `.gitignore`

- [ ] **Step 1: 初始化 electron-vite 项目骨架**

运行（在项目根目录）：
```bash
npm create @quick-start/electron@latest . -- --template vue-ts
```
若交互式提示，选择 Vue + TypeScript 模板。如果该命令因目录非空报错，先临时移动 `1.md` 和 `docs/` 到上级，初始化后再移回。

预期：生成 electron-vite + Vue 3 + TS 的标准骨架。

- [ ] **Step 2: 安装业务依赖**

```bash
npm install amqplib electron-store dayjs uuid pinia element-plus
npm install -D @types/amqplib
```

预期：依赖写入 package.json。

- [ ] **Step 3: 配置 tsconfig 路径别名（可选但推荐）**

在 `tsconfig.web.json` 的 `compilerOptions` 中添加：
```json
{
  "compilerOptions": {
    "baseUrl": ".",
    "paths": {
      "@renderer/*": ["src/renderer/src/*"],
      "@shared/*": ["src/shared/*"]
    }
  }
}
```
在 `tsconfig.node.json` 同样添加指向 `src/main/*`、`src/preload/*`、`src/shared/*` 的别名。

- [ ] **Step 4: 创建共享类型目录**

Create: `src/shared/types.ts`
```ts
export interface ConnectionConfig {
  host: string;
  port: number;
  vhost: string;
  username: string;
  password: string;
  timeout: number;
  sslEnabled: boolean;
  caPath?: string;
  certPath?: string;
  keyPath?: string;
  passphrase?: string;
  rejectUnauthorized: boolean;
}

export type ExchangeType = 'direct' | 'topic' | 'fanout' | 'headers';
export type MessageFormat = 'json' | 'text' | 'xml';

export interface MessageProperties {
  contentType?: string;
  contentEncoding?: string;
  deliveryMode?: 1 | 2;
  priority?: number;
  expiration?: string;
  headers?: Record<string, any>;
}

export interface BatchConfig {
  count: number;
  intervalMs: number;
}

export interface SendParams {
  exchange: string;
  exchangeType?: ExchangeType;
  routingKey: string;
  autoDeclare: boolean;
  durable: boolean;
  message: string;
  format: MessageFormat;
  properties: MessageProperties;
  batch: BatchConfig;
}

export interface SendResult {
  success: boolean;
  messageId?: string;
  error?: string;
}

export interface HistoryItem {
  time: string;
  exchange: string;
  routingKey: string;
  messageSummary: string;
}

export interface ConsumedMessage {
  seq: number;
  receivedAt: string;
  exchange: string;
  routingKey: string;
  messageId?: string;
  deliveryTag: number;
  content: string;
  properties: MessageProperties;
}

export interface ConsumerConfig {
  queue: string;
  autoDeclareQueue: boolean;
  durable: boolean;
  exclusive: boolean;
  autoDelete: boolean;
  exchange: string;
  bindingKey: string;
  mode: 'push' | 'pull';
  prefetch: number;
  autoAck: boolean;
  filterByRoutingKey?: string;
  filterByHeaderKey?: string;
  filterByHeaderValue?: string;
  maxReceive: number;
}

export interface IpcResult {
  success: boolean;
  error?: string;
}

export interface ProgressInfo {
  current: number;
  total: number;
}

export type LogLevel = 'INFO' | 'WARN' | 'ERROR';

export interface LogEntry {
  time: string;
  level: LogLevel;
  message: string;
}

export type ConnectionStatus = 'disconnected' | 'connecting' | 'connected' | 'error';

export interface ConnectionStatusInfo {
  status: ConnectionStatus;
  display: string;
  sslEnabled: boolean;
  rejectUnauthorized: boolean;
}
```

- [ ] **Step 5: 验证骨架可启动**

```bash
npm run dev
```
预期：Electron 窗口打开，显示 Vue 默认欢迎页。关闭窗口结束。

- [ ] **Step 6: 提交**

```bash
git add -A
git commit -m "chore: 初始化 electron-vite + Vue 3 脚手架"
```

---

## Task 2: 主进程类型与 IPC 基础设施

**Files:**
- Create: `src/main/types.ts`（主进程专用类型，如内部消息缓存）
- Modify: `src/preload/index.ts`

**说明:** 本任务建立 preload 暴露的 API 契约骨架（先空实现），后续任务填充各模块时逐步完善。这样渲染进程可以早一点对接 API 形状。

- [ ] **Step 1: 定义 preload API 接口类型**

Create: `src/preload/index.ts`
```ts
import { contextBridge, ipcRenderer } from 'electron'
import type {
  ConnectionConfig, SendParams, SendResult, HistoryItem,
  ConsumedMessage, ConsumerConfig, IpcResult, ProgressInfo,
  ConnectionStatusInfo, LogEntry
} from '../shared/types'

const api = {
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
    pull: (count: number): Promise<ConsumedMessage[]> =>
      ipcRenderer.invoke('consumer:pull', count),
    ack: (deliveryTag: number): Promise<IpcResult> =>
      ipcRenderer.invoke('consumer:ack', deliveryTag),
    nack: (deliveryTag: number, requeue: boolean): Promise<IpcResult> =>
      ipcRenderer.invoke('consumer:nack', deliveryTag, requeue),
    onMessage: (cb: (msg: ConsumedMessage) => void): void => {
      const handler = (_: unknown, msg: ConsumedMessage) => cb(msg)
      ipcRenderer.on('consumer:message', handler)
    }
  },
  log: {
    onLog: (cb: (entry: LogEntry) => void): void => {
      const handler = (_: unknown, entry: LogEntry) => cb(entry)
      ipcRenderer.on('log:entry', handler)
    }
  }
}

contextBridge.exposeInMainWorld('api', api)
```

- [ ] **Step 2: 声明 window.api 全局类型**

Create: `src/renderer/src/env.d.ts`（若已存在则追加）
```ts
import type { api } from '../../preload'

declare global {
  interface Window {
    api: typeof api
  }
}
```

- [ ] **Step 3: 验证类型检查通过**

```bash
npm run typecheck
```
预期：无类型错误（此时 IPC handler 尚未注册，运行时调用会失败，但类型应通过）。

- [ ] **Step 4: 提交**

```bash
git add -A
git commit -m "feat: 定义 preload API 契约与共享类型"
```

---

## Task 3: 主进程日志收集器

**Files:**
- Create: `src/main/utils/logger.ts`
- Modify: `src/main/index.ts`（注册 logger 的 webContents 引用）

**说明:** 主进程各 service 产生日志后调用 logger，logger 通过 webContents.send 推送给渲染进程。logger 需要持有当前 BrowserWindow 的 webContents 引用。

- [ ] **Step 1: 编写 logger 模块**

Create: `src/main/utils/logger.ts`
```ts
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

- [ ] **Step 2: 在主进程入口设置 mainWindow 引用**

Modify: `src/main/index.ts`，在 `createWindow()` 创建窗口后调用 `setMainWindow(mainWindow)`：
```ts
import { setMainWindow } from './utils/logger'

// 在 createWindow 中，mainWindow = new BrowserWindow(...) 之后：
setMainWindow(mainWindow)
```

- [ ] **Step 3: 验证 typecheck**

```bash
npm run typecheck
```
预期：通过。

- [ ] **Step 4: 提交**

```bash
git add -A
git commit -m "feat: 主进程日志收集器，推送至渲染进程"
```

---

## Task 4: electron-store 封装（密码加密）

**Files:**
- Create: `src/main/utils/store.ts`

- [ ] **Step 1: 编写 store 封装**

Create: `src/main/utils/store.ts`
```ts
import Store from 'electron-store'
import { app } from 'electron'
import * as crypto from 'crypto'
import type { ConnectionConfig } from '../../shared/types'

const ENCRYPTION_KEY = crypto
  .createHash('sha256')
  .update(app.getName() + ':erabbitmq-store-key')
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
  if (safe.passphrase) {
    safe.passphrase = encrypt(safe.passphrase)
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
  if (restored.passphrase) {
    try {
      restored.passphrase = decrypt(restored.passphrase)
    } catch {
      restored.passphrase = ''
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
```

- [ ] **Step 2: 验证 typecheck**

```bash
npm run typecheck
```
预期：通过。

- [ ] **Step 3: 提交**

```bash
git add -A
git commit -m "feat: electron-store 封装，密码加密存储"
```

---

## Task 5: SSL 配置构造

**Files:**
- Create: `src/main/utils/ssl.ts`

- [ ] **Step 1: 编写 SSL 构造逻辑**

Create: `src/main/utils/ssl.ts`
```ts
import * as fs from 'fs'
import * as path from 'path'
import type { ConnectionConfig } from '../../shared/types'
import { logger } from './logger'

export interface AmqpConnectOptions {
  protocol: 'amqp' | 'amqps'
  hostname: string
  port: number
  username: string
  password: string
  vhost: string
  timeout?: number
  tls?: {
    rejectUnauthorized?: boolean
    ca?: Buffer[]
    cert?: Buffer
    key?: Buffer
    passphrase?: string
  }
}

export function validateConnectionConfig(config: ConnectionConfig): string | null {
  if (config.sslEnabled && !config.caPath && config.rejectUnauthorized) {
    return '请上传 CA 证书或关闭证书认证'
  }
  if (config.certPath && !config.keyPath) {
    return '上传客户端证书时需同时填写私钥'
  }
  if (config.sslEnabled && config.rejectUnauthorized && config.caPath) {
    if (!fs.existsSync(config.caPath)) {
      return `CA 证书路径无效：文件不存在 (${config.caPath})`
    }
  }
  if (config.certPath && !fs.existsSync(config.certPath)) {
    return `客户端证书路径无效：文件不存在 (${config.certPath})`
  }
  if (config.keyPath && !fs.existsSync(config.keyPath)) {
    return `客户端私钥路径无效：文件不存在 (${config.keyPath})`
  }
  return null
}

export function buildConnectOptions(config: ConnectionConfig): AmqpConnectOptions {
  const options: AmqpConnectOptions = {
    protocol: config.sslEnabled ? 'amqps' : 'amqp',
    hostname: config.host,
    port: config.port,
    username: config.username,
    password: config.password,
    vhost: config.vhost || '/',
    timeout: config.timeout || 5000
  }

  if (config.sslEnabled) {
    const tls: AmqpConnectOptions['tls'] = {}
    if (!config.rejectUnauthorized) {
      tls.rejectUnauthorized = false
      logger.warn('SSL 连接已关闭证书认证，存在中间人风险')
    } else {
      tls.rejectUnauthorized = true
      if (config.caPath) {
        tls.ca = [fs.readFileSync(path.resolve(config.caPath))]
      }
      if (config.certPath && config.keyPath) {
        tls.cert = fs.readFileSync(path.resolve(config.certPath))
        tls.key = fs.readFileSync(path.resolve(config.keyPath))
      }
      if (config.passphrase) {
        tls.passphrase = config.passphrase
      }
    }
    options.tls = tls
  }

  return options
}
```

- [ ] **Step 2: 验证 typecheck**

```bash
npm run typecheck
```
预期：通过。

- [ ] **Step 3: 提交**

```bash
git add -A
git commit -m "feat: SSL 配置构造与连接校验"
```

---

## Task 6: ConnectionManager 单例

**Files:**
- Create: `src/main/services/ConnectionManager.ts`

**说明:** ConnectionManager 是核心单例，管连接实例、状态、事件。Producer/ConsumerService 依赖它取连接。状态变化通过回调通知（IPC 层订阅后转发渲染进程）。

- [ ] **Step 1: 编写 ConnectionManager**

Create: `src/main/services/ConnectionManager.ts`
```ts
import * as amqp from 'amqplib'
import type { ConnectionConfig, ConnectionStatus, ConnectionStatusInfo } from '../../shared/types'
import { buildConnectOptions, validateConnectionConfig } from '../utils/ssl'
import { logger } from '../utils/logger'

type StatusListener = (info: ConnectionStatusInfo) => void

class ConnectionManager {
  private connection: amqp.Connection | null = null
  private status: ConnectionStatus = 'disconnected'
  private currentConfig: ConnectionConfig | null = null
  private listeners: Set<StatusListener> = new Set()

  private buildStatusInfo(): ConnectionStatusInfo {
    if (this.status === 'connected' && this.currentConfig) {
      const proto = this.currentConfig.sslEnabled ? 'amqps' : 'amqp'
      const display = `${proto}://${this.currentConfig.username}@${this.currentConfig.host}:${this.currentConfig.port}/${this.currentConfig.vhost || '/'}`
      return {
        status: this.status,
        display,
        sslEnabled: this.currentConfig.sslEnabled,
        rejectUnauthorized: this.currentConfig.rejectUnauthorized
      }
    }
    return {
      status: this.status,
      display: this.status === 'connecting' ? '连接中...' : '未连接',
      sslEnabled: false,
      rejectUnauthorized: false
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
    const validationError = validateConnectionConfig(config)
    if (validationError) {
      return { success: false, error: validationError }
    }
    if (this.connection) {
      await this.disconnect()
    }
    this.currentConfig = config
    this.setStatus('connecting')
    try {
      const options = buildConnectOptions(config)
      this.connection = await amqp.connect(options as any)
      this.connection.on('close', () => {
        logger.warn('连接已关闭')
        this.connection = null
        this.setStatus('disconnected')
      })
      this.connection.on('error', (err: Error) => {
        logger.error('连接错误：' + err.message)
        this.connection = null
        this.setStatus('error')
      })
      this.setStatus('connected')
      logger.info(`已连接 ${config.host}:${config.port}`)
      return { success: true }
    } catch (err: any) {
      this.connection = null
      this.setStatus('error')
      const msg = this.humanizeError(err)
      logger.error('连接失败：' + msg)
      return { success: false, error: msg }
    }
  }

  async test(config: ConnectionConfig): Promise<{ success: boolean; error?: string }> {
    const validationError = validateConnectionConfig(config)
    if (validationError) {
      return { success: false, error: validationError }
    }
    try {
      const options = buildConnectOptions(config)
      const conn = await amqp.connect(options as any)
      await conn.close()
      logger.info(`测试连接成功 ${config.host}:${config.port}`)
      return { success: true }
    } catch (err: any) {
      const msg = this.humanizeError(err)
      logger.error('测试连接失败：' + msg)
      return { success: false, error: msg }
    }
  }

  async disconnect(): Promise<{ success: boolean; error?: string }> {
    if (!this.connection) {
      return { success: true }
    }
    try {
      await this.connection.close()
    } catch (err: any) {
      logger.warn('关闭连接时出错：' + err.message)
    }
    this.connection = null
    this.currentConfig = null
    this.setStatus('disconnected')
    logger.info('已断开连接')
    return { success: true }
  }

  getConnection(): amqp.Connection | null {
    return this.connection
  }

  isConnected(): boolean {
    return this.status === 'connected' && this.connection !== null
  }

  private humanizeError(err: any): string {
    const code = err?.code || ''
    const msg = err?.message || String(err)
    if (code === 'ECONNREFUSED' || msg.includes('ECONNREFUSED')) {
      return `无法连接到服务器：${this.currentConfig?.host || ''} 拒绝连接`
    }
    if (code === 'ENOTFOUND' || msg.includes('ENOTFOUND')) {
      return `主机名无法解析：${this.currentConfig?.host || ''}`
    }
    if (code === 'ETIMEDOUT' || msg.includes('ETIMEDOUT')) {
      return '连接超时，请检查网络或主机地址'
    }
    if (msg.includes('ACCESS_REFUSED') || msg.includes('Handshake')) {
      return '认证失败：用户名/密码错误或无权限'
    }
    if (msg.includes('SSL') || msg.includes('certificate') || msg.includes('CERT')) {
      return 'SSL/证书错误：' + msg
    }
    return msg
  }
}

export const connectionManager = new ConnectionManager()
```

- [ ] **Step 2: 验证 typecheck**

```bash
npm run typecheck
```
预期：通过。

- [ ] **Step 3: 提交**

```bash
git add -A
git commit -m "feat: ConnectionManager 单例，连接管理 + 状态事件"
```

---

## Task 7: 连接 IPC handler 与启动加载

**Files:**
- Create: `src/main/ipc/connection.ts`
- Modify: `src/main/index.ts`

- [ ] **Step 1: 编写连接 IPC handler**

Create: `src/main/ipc/connection.ts`
```ts
import { ipcMain } from 'electron'
import { connectionManager } from '../services/ConnectionManager'
import { loadConnection, saveConnection } from '../utils/store'
import { logger } from '../utils/logger'
import type { ConnectionConfig, ConnectionStatusInfo } from '../../shared/types'

export function registerConnectionIpc(mainWindow: { webContents: { send: (ch: string, ...args: any[]) => void } }): void {
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

  logger.info('连接 IPC 已注册')
}

export function getLastSavedConnection(): ConnectionConfig | undefined {
  return loadConnection()
}
```

- [ ] **Step 2: 在主进程入口注册 IPC 并推送初始状态**

Modify: `src/main/index.ts`，在 `createWindow()` 内 `setMainWindow(mainWindow)` 之后、窗口加载前：
```ts
import { registerConnectionIpc } from './ipc/connection'

// 在 createWindow 中：
registerConnectionIpc(mainWindow)

// 窗口加载完成后推送初始状态
mainWindow.webContents.on('did-finish-load', () => {
  const saved = getLastSavedConnection()
  if (saved) {
    mainWindow.webContents.send('connection:lastConfigLoaded', saved)
  }
})
```
并在文件顶部 import `getLastSavedConnection`。

- [ ] **Step 3: 在 preload 补充 lastConfigLoaded 监听**

Modify: `src/preload/index.ts`，在 `connection` 对象内追加：
```ts
    onLastConfigLoaded: (cb: (config: ConnectionConfig) => void): void => {
      const handler = (_: unknown, config: ConnectionConfig) => cb(config)
      ipcRenderer.on('connection:lastConfigLoaded', handler)
    }
```

- [ ] **Step 4: 验证 typecheck**

```bash
npm run typecheck
```
预期：通过。

- [ ] **Step 5: 提交**

```bash
git add -A
git commit -m "feat: 连接 IPC handler 与启动加载上次配置"
```

---

## Task 8: ProducerService

**Files:**
- Create: `src/main/services/ProducerService.ts`

- [ ] **Step 1: 编写 ProducerService**

Create: `src/main/services/ProducerService.ts`
```ts
import * as amqp from 'amqplib'
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
    const conn = connectionManager.getConnection()!
    let channel: amqp.Channel | null = null
    try {
      channel = await conn.createChannel()

      if (params.autoDeclare && params.exchange) {
        await channel.assertExchange(params.exchange, params.exchangeType || 'direct', {
          durable: params.durable
        })
      } else if (params.exchange) {
        try {
          await channel.checkExchange(params.exchange)
        } catch {
          return {
            success: false,
            error: `交换机不存在：${params.exchange}，请检查名称或开启自动声明`
          }
        }
      }

      if (params.format === 'json') {
        try {
          JSON.parse(params.message)
        } catch {
          return { success: false, error: '消息体不是合法 JSON' }
        }
      }

      const content = Buffer.from(params.message, 'utf8')
      const options: amqp.Options.Publish = {
        contentType: params.properties.contentType,
        contentEncoding: params.properties.contentEncoding,
        deliveryMode: params.properties.deliveryMode,
        priority: params.properties.priority,
        expiration: params.properties.expiration,
        headers: params.properties.headers,
        messageId: uuidv4()
      }

      const total = Math.max(1, params.batch.count)
      for (let i = 0; i < total; i++) {
        channel.publish(params.exchange || '', params.routingKey, content, options)
        this.emitProgress({ current: i + 1, total })
        if (i < total - 1 && params.batch.intervalMs > 0) {
          await new Promise((r) => setTimeout(r, params.batch.intervalMs))
        }
      }

      const historyItem: HistoryItem = {
        time: dayjs().format('YYYY-MM-DD HH:mm:ss'),
        exchange: params.exchange || '(默认)',
        routingKey: params.routingKey,
        messageSummary: params.message.slice(0, 50)
      }
      this.history.unshift(historyItem)
      if (this.history.length > 10) {
        this.history = this.history.slice(0, 10)
      }

      logger.info(`发送成功：${params.exchange || '(默认)'} / ${params.routingKey}，共 ${total} 条`)
      return { success: true, messageId: options.messageId }
    } catch (err: any) {
      const msg = err?.message || String(err)
      logger.error('发送失败：' + msg)
      return { success: false, error: msg }
    } finally {
      if (channel) {
        try {
          await channel.close()
        } catch {
          // ignore
        }
      }
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

- [ ] **Step 2: 验证 typecheck**

```bash
npm run typecheck
```
预期：通过。

- [ ] **Step 3: 提交**

```bash
git add -A
git commit -m "feat: ProducerService，发送/批量/历史"
```

---

## Task 9: 生产者 IPC handler

**Files:**
- Create: `src/main/ipc/producer.ts`
- Modify: `src/main/index.ts`

- [ ] **Step 1: 编写生产者 IPC handler**

Create: `src/main/ipc/producer.ts`
```ts
import { ipcMain } from 'electron'
import { producerService } from '../services/ProducerService'
import { logger } from '../utils/logger'
import type { ProgressInfo } from '../../shared/types'

export function registerProducerIpc(mainWindow: { webContents: { send: (ch: string, ...args: any[]) => void } }): void {
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

- [ ] **Step 2: 在主进程入口注册**

Modify: `src/main/index.ts`，在 `registerConnectionIpc(mainWindow)` 之后：
```ts
import { registerProducerIpc } from './ipc/producer'
// ...
registerProducerIpc(mainWindow)
```

- [ ] **Step 3: 验证 typecheck**

```bash
npm run typecheck
```
预期：通过。

- [ ] **Step 4: 提交**

```bash
git add -A
git commit -m "feat: 生产者 IPC handler"
```

---

## Task 10: ConsumerService

**Files:**
- Create: `src/main/services/ConsumerService.ts`

**说明:** 这是最复杂的服务。管 channel、consumerTag、消息缓存、过滤、ack/nack、拉模式、最大接收数自动停止。消息推送给渲染进程通过回调（IPC 层订阅）。

- [ ] **Step 1: 编写 ConsumerService**

Create: `src/main/services/ConsumerService.ts`
```ts
import * as amqp from 'amqplib'
import { connectionManager } from './ConnectionManager'
import { logger } from '../utils/logger'
import dayjs from 'dayjs'
import type { ConsumerConfig, ConsumedMessage, IpcResult } from '../../shared/types'

type MessageListener = (msg: ConsumedMessage) => void
type StopListener = () => void

class ConsumerService {
  private channel: amqp.Channel | null = null
  private consumerTag: string | null = null
  private seq = 0
  private receivedCount = 0
  private config: ConsumerConfig | null = null
  private pendingMessages: Map<number, amqp.Message> = new Map()
  private messageListeners: Set<MessageListener> = new Set()
  private stopListeners: Set<StopListener> = new Set()
  private paused = false

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

  private emitMessage(msg: ConsumedMessage): void {
    this.messageListeners.forEach((cb) => cb(msg))
  }

  private emitStop(): void {
    this.stopListeners.forEach((cb) => cb())
  }

  async start(config: ConsumerConfig): Promise<IpcResult> {
    if (!connectionManager.isConnected()) {
      return { success: false, error: '请先建立连接' }
    }
    if (this.channel) {
      return { success: false, error: '已有消费进行中，请先停止' }
    }
    const conn = connectionManager.getConnection()!
    try {
      this.channel = await conn.createChannel()
      this.config = config
      this.seq = 0
      this.receivedCount = 0
      this.paused = false
      this.pendingMessages.clear()

      let queueName = config.queue
      if (config.autoDeclareQueue) {
        const q = await this.channel.assertQueue(config.queue, {
          durable: config.durable,
          exclusive: config.exclusive,
          autoDelete: config.autoDelete
        })
        queueName = q.queue
        if (config.exchange) {
          await this.channel.bindQueue(queueName, config.exchange, config.bindingKey || '')
        }
      } else {
        try {
          await this.channel.checkQueue(config.queue)
        } catch {
          return {
            success: false,
            error: `队列不存在：${config.queue}，请检查名称或开启自动声明`
          }
        }
      }

      await this.channel.prefetch(config.prefetch || 0)

      if (config.mode === 'push') {
        const consumeResult = await this.channel.consume(
          queueName,
          (msg) => this.handleMessage(msg),
          { noAck: !config.autoAck }
        )
        this.consumerTag = consumeResult.consumerTag
        logger.info(`开始消费队列：${queueName}（推模式）`)
      }

      return { success: true }
    } catch (err: any) {
      const msg = err?.message || String(err)
      logger.error('启动消费失败：' + msg)
      await this.cleanupChannel()
      return { success: false, error: msg }
    }
  }

  private async handleMessage(msg: amqp.Message | null): Promise<void> {
    if (!msg) {
      logger.warn('消费者被服务器取消')
      return
    }
    if (this.paused) {
      // 暂停期间不应收到消息（已 cancel），兜底处理
      return
    }
    const config = this.config!
    const routingKey = msg.fields.routingKey
    const headers = msg.properties.headers || {}

    const matched = this.matchFilter(routingKey, headers, config)
    if (!matched) {
      if (!config.autoAck && this.channel) {
        try {
          this.channel.ack(msg)
        } catch (e) {
          logger.warn('过滤消息 ack 失败：' + (e as Error).message)
        }
      }
      return
    }

    this.seq++
    this.receivedCount++
    const content = msg.content.toString('utf8')
    const consumed: ConsumedMessage = {
      seq: this.seq,
      receivedAt: dayjs().format('YYYY-MM-DD HH:mm:ss.SSS'),
      exchange: msg.fields.exchange || '',
      routingKey,
      messageId: msg.properties.messageId,
      deliveryTag: msg.fields.deliveryTag,
      content,
      properties: {
        contentType: msg.properties.contentType,
        contentEncoding: msg.properties.contentEncoding,
        deliveryMode: msg.properties.deliveryMode as 1 | 2 | undefined,
        priority: msg.properties.priority,
        expiration: msg.properties.expiration,
        headers: msg.properties.headers
      }
    }

    if (!config.autoAck) {
      this.pendingMessages.set(msg.fields.deliveryTag, msg)
    }

    this.emitMessage(consumed)
    logger.info(`收到消息 #${consumed.seq}：${routingKey}`)

    if (config.maxReceive > 0 && this.receivedCount >= config.maxReceive) {
      logger.info(`达到最大接收数 ${config.maxReceive}，自动停止`)
      await this.stop()
      this.emitStop()
    }
  }

  private matchFilter(routingKey: string, headers: Record<string, any>, config: ConsumerConfig): boolean {
    if (config.filterByRoutingKey) {
      if (!this.routingKeyMatch(routingKey, config.filterByRoutingKey)) {
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
    if (!this.channel || !this.consumerTag) {
      return { success: false, error: '无活跃的推模式消费者' }
    }
    try {
      await this.channel.cancel(this.consumerTag)
      this.consumerTag = null
      this.paused = true
      logger.info('消费已暂停')
      return { success: true }
    } catch (err: any) {
      return { success: false, error: err?.message || String(err) }
    }
  }

  async resume(): Promise<IpcResult> {
    if (!this.channel || !this.config) {
      return { success: false, error: '无活跃消费者' }
    }
    if (!this.paused) {
      return { success: false, error: '消费者未暂停' }
    }
    try {
      const config = this.config
      const consumeResult = await this.channel.consume(
        config.queue,
        (msg) => this.handleMessage(msg),
        { noAck: !config.autoAck }
      )
      this.consumerTag = consumeResult.consumerTag
      this.paused = false
      logger.info('消费已恢复')
      return { success: true }
    } catch (err: any) {
      return { success: false, error: err?.message || String(err) }
    }
  }

  async stop(): Promise<IpcResult> {
    await this.cleanupChannel()
    this.pendingMessages.clear()
    this.paused = false
    logger.info('消费已停止，清空未确认列表')
    return { success: true }
  }

  private async cleanupChannel(): Promise<void> {
    if (this.consumerTag && this.channel) {
      try {
        await this.channel.cancel(this.consumerTag)
      } catch {
        // ignore
      }
      this.consumerTag = null
    }
    if (this.channel) {
      try {
        await this.channel.close()
      } catch {
        // ignore
      }
      this.channel = null
    }
  }

  async pull(count: number): Promise<ConsumedMessage[]> {
    if (!this.channel || !this.config) {
      return []
    }
    if (this.config.mode !== 'pull') {
      return []
    }
    const results: ConsumedMessage[] = []
    for (let i = 0; i < count; i++) {
      const msg = await this.channel.get(this.config.queue, { noAck: !this.config.autoAck })
      if (!msg) break
      this.seq++
      this.receivedCount++
      if (!this.config.autoAck) {
        this.pendingMessages.set(msg.fields.deliveryTag, msg)
      }
      results.push({
        seq: this.seq,
        receivedAt: dayjs().format('YYYY-MM-DD HH:mm:ss.SSS'),
        exchange: msg.fields.exchange || '',
        routingKey: msg.fields.routingKey,
        messageId: msg.properties.messageId,
        deliveryTag: msg.fields.deliveryTag,
        content: msg.content.toString('utf8'),
        properties: {
          contentType: msg.properties.contentType,
          contentEncoding: msg.properties.contentEncoding,
          deliveryMode: msg.properties.deliveryMode as 1 | 2 | undefined,
          priority: msg.properties.priority,
          expiration: msg.properties.expiration,
          headers: msg.properties.headers
        }
      })
    }
    if (results.length > 0) {
      results.forEach((m) => this.emitMessage(m))
      logger.info(`拉取 ${results.length} 条消息`)
    }
    if (this.config.maxReceive > 0 && this.receivedCount >= this.config.maxReceive) {
      await this.stop()
      this.emitStop()
    }
    return results
  }

  async ack(deliveryTag: number): Promise<IpcResult> {
    if (!this.channel) {
      return { success: false, error: '无活跃 channel' }
    }
    const msg = this.pendingMessages.get(deliveryTag)
    if (!msg) {
      return { success: false, error: '消息不在未确认列表中' }
    }
    try {
      this.channel.ack(msg)
      this.pendingMessages.delete(deliveryTag)
      return { success: true }
    } catch (err: any) {
      return { success: false, error: err?.message || String(err) }
    }
  }

  async nack(deliveryTag: number, requeue: boolean): Promise<IpcResult> {
    if (!this.channel) {
      return { success: false, error: '无活跃 channel' }
    }
    const msg = this.pendingMessages.get(deliveryTag)
    if (!msg) {
      return { success: false, error: '消息不在未确认列表中' }
    }
    try {
      this.channel.nack(msg, false, requeue)
      this.pendingMessages.delete(deliveryTag)
      logger.info(`消息 #${deliveryTag} 已 nack，requeue=${requeue}`)
      return { success: true }
    } catch (err: any) {
      return { success: false, error: err?.message || String(err) }
    }
  }

  clearOnDisconnect(): void {
    this.cleanupChannel()
    this.pendingMessages.clear()
    this.config = null
    this.paused = false
  }
}

export const consumerService = new ConsumerService()
```

- [ ] **Step 2: 验证 typecheck**

```bash
npm run typecheck
```
预期：通过。

- [ ] **Step 3: 提交**

```bash
git add -A
git commit -m "feat: ConsumerService，消费/过滤/ack/nack/拉模式"
```

---

## Task 11: 消费者 IPC handler

**Files:**
- Create: `src/main/ipc/consumer.ts`
- Modify: `src/main/index.ts`

- [ ] **Step 1: 编写消费者 IPC handler**

Create: `src/main/ipc/consumer.ts`
```ts
import { ipcMain } from 'electron'
import { consumerService } from '../services/ConsumerService'
import { logger } from '../utils/logger'
import type { ConsumedMessage } from '../../shared/types'

export function registerConsumerIpc(mainWindow: { webContents: { send: (ch: string, ...args: any[]) => void } }): void {
  consumerService.onMessage((msg: ConsumedMessage) => {
    mainWindow.webContents.send('consumer:message', msg)
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
  ipcMain.handle('consumer:pull', async (_evt, count: number) => {
    return await consumerService.pull(count)
  })
  ipcMain.handle('consumer:ack', async (_evt, deliveryTag: number) => {
    return await consumerService.ack(deliveryTag)
  })
  ipcMain.handle('consumer:nack', async (_evt, deliveryTag: number, requeue: boolean) => {
    return await consumerService.nack(deliveryTag, requeue)
  })

  logger.info('消费者 IPC 已注册')
}
```

- [ ] **Step 2: 在主进程入口注册**

Modify: `src/main/index.ts`，在 `registerProducerIpc(mainWindow)` 之后：
```ts
import { registerConsumerIpc } from './ipc/consumer'
// ...
registerConsumerIpc(mainWindow)
```

- [ ] **Step 3: 在 ConnectionManager 断开时清理 producer/consumer 状态**

Modify: `src/main/index.ts`，在 `registerConnectionIpc` 调用前，给 connectionManager 加一个状态监听，当 disconnected 时清理：
```ts
import { producerService } from './services/ProducerService'
import { consumerService } from './services/ConsumerService'
import { connectionManager } from './services/ConnectionManager'

connectionManager.onStatusChange((info) => {
  if (info.status === 'disconnected' || info.status === 'error') {
    producerService.clearHistoryOnDisconnect()
    consumerService.clearOnDisconnect()
  }
})
```

- [ ] **Step 4: 验证 typecheck**

```bash
npm run typecheck
```
预期：通过。

- [ ] **Step 4: 提交**

```bash
git add -A
git commit -m "feat: 消费者 IPC handler 与断连清理"
```

---

## Task 12: 渲染进程 Pinia stores 基础

**Files:**
- Create: `src/renderer/src/stores/connection.ts`
- Create: `src/renderer/src/stores/producer.ts`
- Create: `src/renderer/src/stores/consumer.ts`
- Create: `src/renderer/src/stores/log.ts`
- Create: `src/renderer/src/stores/settings.ts`
- Modify: `src/renderer/src/main.ts`（注册 Pinia）

- [ ] **Step 1: connection store**

Create: `src/renderer/src/stores/connection.ts`
```ts
import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { ConnectionConfig, ConnectionStatusInfo } from '../../../shared/types'

export const useConnectionStore = defineStore('connection', () => {
  const status = ref<ConnectionStatusInfo>({
    status: 'disconnected',
    display: '未连接',
    sslEnabled: false,
    rejectUnauthorized: false
  })
  const lastConfig = ref<ConnectionConfig | null>(null)
  const connecting = ref(false)

  async function connect(config: ConnectionConfig) {
    connecting.value = true
    const r = await window.api.connection.connect(config)
    connecting.value = false
    return r
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

- [ ] **Step 2: producer store**

Create: `src/renderer/src/stores/producer.ts`
```ts
import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { SendParams, SendResult, HistoryItem, ProgressInfo } from '../../../shared/types'

export const useProducerStore = defineStore('producer', () => {
  const history = ref<HistoryItem[]>([])
  const progress = ref<ProgressInfo | null>(null)
  const sending = ref(false)

  async function send(params: SendParams): Promise<SendResult> {
    sending.value = true
    const r = await window.api.producer.send(params)
    sending.value = false
    return r
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

- [ ] **Step 3: consumer store**

Create: `src/renderer/src/stores/consumer.ts`
```ts
import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { ConsumedMessage, ConsumerConfig, IpcResult } from '../../../shared/types'

export const useConsumerStore = defineStore('consumer', () => {
  const messages = ref<ConsumedMessage[]>([])
  const running = ref(false)
  const paused = ref(false)
  const maxCache = ref(1000)

  async function start(config: ConsumerConfig): Promise<IpcResult> {
    const r = await window.api.consumer.start(config)
    if (r.success) {
      running.value = true
      paused.value = false
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

  async function pull(count: number) {
    return await window.api.consumer.pull(count)
  }

  async function ack(deliveryTag: number) {
    const r = await window.api.consumer.ack(deliveryTag)
    if (r.success) {
      messages.value = messages.value.filter((m) => m.deliveryTag !== deliveryTag)
    }
    return r
  }

  async function nack(deliveryTag: number, requeue: boolean) {
    const r = await window.api.consumer.nack(deliveryTag, requeue)
    if (r.success) {
      messages.value = messages.value.filter((m) => m.deliveryTag !== deliveryTag)
    }
    return r
  }

  function bindIpc() {
    window.api.consumer.onMessage((msg) => {
      messages.value.push(msg)
      if (messages.value.length > maxCache.value) {
        messages.value = messages.value.slice(messages.value.length - maxCache.value)
      }
    })
  }

  return { messages, running, paused, maxCache, start, pause, resume, stop, pull, ack, nack, bindIpc }
})
```

- [ ] **Step 4: log store**

Create: `src/renderer/src/stores/log.ts`
```ts
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

- [ ] **Step 5: settings store**

Create: `src/renderer/src/stores/settings.ts`
```ts
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

- [ ] **Step 6: 在 main.ts 注册 Pinia 并绑定 IPC**

Modify: `src/renderer/src/main.ts`：
```ts
import { createApp } from 'vue'
import { createPinia } from 'pinia'
import ElementPlus from 'element-plus'
import 'element-plus/dist/index.css'
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

- [ ] **Step 7: 验证 typecheck**

```bash
npm run typecheck
```
预期：通过。

- [ ] **Step 8: 提交**

```bash
git add -A
git commit -m "feat: 渲染进程 Pinia stores 与 IPC 绑定"
```

---

## Task 13: 根布局 App.vue

**Files:**
- Create: `src/renderer/src/App.vue`

**说明:** 顶部导航（应用名 + 连接状态 + 设置入口 + 日志开关）、Tab 容器（生产者/消费者）、底部状态栏（连接信息 + 累计计数）、底部日志面板（可折叠）。

- [ ] **Step 1: 编写 App.vue**

Create: `src/renderer/src/App.vue`
```vue
<template>
  <el-container class="app-container">
    <el-header class="app-header">
      <div class="app-title">ERabbitMQ Tool</div>
      <div class="header-status">
        <el-tag :type="statusTagType" size="small">{{ connectionStore.status.display }}</el-tag>
        <el-tag v-if="connectionStore.status.sslEnabled" type="success" size="small">SSL</el-tag>
        <el-tag v-if="connectionStore.status.sslEnabled && !connectionStore.status.rejectUnauthorized" type="warning" size="small">证书认证已关闭</el-tag>
      </div>
      <div class="header-actions">
        <el-button size="small" @click="logStore.toggle()">日志</el-button>
        <el-button size="small" @click="showSettings = true">设置</el-button>
      </div>
    </el-header>

    <el-main class="app-main">
      <el-tabs v-model="activeTab">
        <el-tab-pane label="生产者" name="producer">
          <ProducerView v-if="activeTab === 'producer'" />
        </el-tab-pane>
        <el-tab-pane label="消费者" name="consumer">
          <ConsumerView v-if="activeTab === 'consumer'" />
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

- [ ] **Step 2: 验证 typecheck**

```bash
npm run typecheck
```
预期：通过（ProducerView/ConsumerView/LogPanel/SettingsView 尚未创建，下一步创建；若 typecheck 报错找不到组件，先创建空占位文件）。

- [ ] **Step 3: 提交**

```bash
git add -A
git commit -m "feat: 根布局 App.vue（导航/Tab/状态栏/日志入口）"
```

---

## Task 14: ConnectionForm 组件

**Files:**
- Create: `src/renderer/src/components/ConnectionForm.vue`

**说明:** 连接配置表单，含 SSL 配置与证书路径选择。供生产者/消费者 Tab 顶部共用。提供「连接」「测试连接」「断开」按钮。

- [ ] **Step 1: 编写 ConnectionForm**

Create: `src/renderer/src/components/ConnectionForm.vue`
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
      <el-form-item label="虚拟主机">
        <el-input v-model="form.vhost" style="width: 100px" />
      </el-form-item>
      <el-form-item label="用户名">
        <el-input v-model="form.username" style="width: 120px" />
      </el-form-item>
      <el-form-item label="密码">
        <el-input v-model="form.password" type="password" show-password style="width: 140px" />
      </el-form-item>
      <el-form-item label="超时(ms)">
        <el-input-number v-model="form.timeout" :min="1000" :step="1000" controls-position="right" style="width: 120px" />
      </el-form-item>
      <el-form-item label="SSL">
        <el-switch v-model="form.sslEnabled" />
      </el-form-item>
    </el-form>

    <el-collapse-transition>
      <div v-if="form.sslEnabled" class="ssl-config">
        <el-form :model="form" label-width="120px" size="small" inline>
          <el-form-item label="验证证书">
            <el-switch v-model="form.rejectUnauthorized" />
          </el-form-item>
          <el-form-item label="CA 证书">
            <el-input v-model="form.caPath" placeholder="文件路径" style="width: 240px">
              <template #append>
                <el-button @click="selectFile('caPath')">选择</el-button>
              </template>
            </el-input>
          </el-form-item>
          <el-form-item label="客户端证书">
            <el-input v-model="form.certPath" placeholder="文件路径" style="width: 240px">
              <template #append>
                <el-button @click="selectFile('certPath')">选择</el-button>
              </template>
            </el-input>
          </el-form-item>
          <el-form-item label="客户端私钥">
            <el-input v-model="form.keyPath" placeholder="文件路径" style="width: 240px">
              <template #append>
                <el-button @click="selectFile('keyPath')">选择</el-button>
              </template>
            </el-input>
          </el-form-item>
          <el-form-item label="私钥密码">
            <el-input v-model="form.passphrase" type="password" show-password style="width: 160px" />
          </el-form-item>
        </el-form>
      </div>
    </el-collapse-transition>

    <div class="form-actions">
      <el-button type="primary" :loading="connectionStore.connecting" @click="onConnect">连接</el-button>
      <el-button @click="onTest">测试连接</el-button>
      <el-button :disabled="!connectionStore.status.status" @click="onDisconnect">断开</el-button>
    </div>
  </el-card>
</template>

<script setup lang="ts">
import { reactive, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { useConnectionStore } from '../stores/connection'
import type { ConnectionConfig } from '../../../shared/types'

const connectionStore = useConnectionStore()

const form = reactive<ConnectionConfig>({
  host: 'localhost',
  port: 5672,
  vhost: '/',
  username: 'guest',
  password: 'guest',
  timeout: 5000,
  sslEnabled: false,
  rejectUnauthorized: true,
  caPath: '',
  certPath: '',
  keyPath: '',
  passphrase: ''
})

watch(() => connectionStore.lastConfig, (cfg) => {
  if (cfg) Object.assign(form, cfg)
}, { immediate: true })

watch(() => form.sslEnabled, (ssl) => {
  if (ssl) form.port = 5671
  else form.port = 5672
})

async function selectFile(field: 'caPath' | 'certPath' | 'keyPath') {
  const input = document.createElement('input')
  input.type = 'file'
  input.onchange = () => {
    const file = input.files?.[0]
    if (file) {
      form[field] = (file as any).path || file.name
    }
  }
  input.click()
}

async function onConnect() {
  const r = await connectionStore.connect({ ...form })
  if (r.success) ElMessage.success('连接成功')
  else ElMessage.error(r.error || '连接失败')
}

async function onTest() {
  const r = await connectionStore.test({ ...form })
  if (r.success) ElMessage.success('测试连接成功')
  else ElMessage.error(r.error || '测试失败')
}

async function onDisconnect() {
  await connectionStore.disconnect()
}
</script>

<style scoped>
.connection-form { margin-bottom: 12px; }
.ssl-config { padding: 8px 0; border-top: 1px dashed var(--el-border-color); margin-top: 8px; }
.form-actions { margin-top: 8px; }
</style>
```

**注意:** 文件路径选择使用隐藏 input。Electron 渲染进程中 `file.path` 在新版 Electron 已移除，需通过 preload 暴露 `dialog.showOpenDialog`。若 `file.path` 为 undefined，在 preload 追加：
```ts
// 在 preload api 对象内追加
dialog: {
  selectFile: () => ipcRenderer.invoke('dialog:selectFile')
}
```
并在主进程 `src/main/ipc/dialog.ts`（新建）注册：
```ts
import { ipcMain, dialog } from 'electron'
export function registerDialogIpc() {
  ipcMain.handle('dialog:selectFile', async () => {
    const r = await dialog.showOpenDialog({ properties: ['openFile'] })
    return r.canceled ? null : r.filePaths[0]
  })
}
```
在 `src/main/index.ts` 调用 `registerDialogIpc()`。然后 ConnectionForm 的 `selectFile` 改为 `form[field] = await window.api.dialog.selectFile()`。**以 dialog IPC 方式为准**，忽略 `file.path` 写法。

- [ ] **Step 2: 创建 dialog IPC（按上面注意项）**

Create: `src/main/ipc/dialog.ts`
```ts
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

Modify `src/preload/index.ts`，在 api 对象追加：
```ts
  dialog: {
    selectFile: (): Promise<string | null> => ipcRenderer.invoke('dialog:selectFile')
  }
```

Modify `src/main/index.ts`，注册：
```ts
import { registerDialogIpc } from './ipc/dialog'
registerDialogIpc()
```

Modify ConnectionForm 的 `selectFile`：
```ts
async function selectFile(field: 'caPath' | 'certPath' | 'keyPath') {
  const p = await window.api.dialog.selectFile()
  if (p) form[field] = p
}
```

- [ ] **Step 3: 验证 typecheck**

```bash
npm run typecheck
```
预期：通过。

- [ ] **Step 4: 提交**

```bash
git add -A
git commit -m "feat: ConnectionForm 组件 + 文件选择 dialog IPC"
```

---

## Task 15: ProducerView 生产者页

**Files:**
- Create: `src/renderer/src/views/ProducerView.vue`

- [ ] **Step 1: 编写 ProducerView**

Create: `src/renderer/src/views/ProducerView.vue`
```vue
<template>
  <div class="producer-view">
    <ConnectionForm />

    <el-card shadow="never" class="producer-card">
      <el-form :model="form" label-width="100px" size="small">
        <el-divider content-position="left">目标交换机</el-divider>
        <el-form-item label="交换机名称">
          <el-input v-model="form.exchange" placeholder="空=默认交换机" style="width: 240px" />
        </el-form-item>
        <el-form-item label="交换机类型">
          <el-select v-model="form.exchangeType" style="width: 140px">
            <el-option label="Direct" value="direct" />
            <el-option label="Topic" value="topic" />
            <el-option label="Fanout" value="fanout" />
            <el-option label="Headers" value="headers" />
          </el-select>
        </el-form-item>
        <el-form-item label="路由键">
          <el-input v-model="form.routingKey" :disabled="form.exchangeType === 'fanout'" style="width: 240px" />
        </el-form-item>
        <el-form-item label="自动声明">
          <el-switch v-model="form.autoDeclare" />
        </el-form-item>
        <el-form-item label="持久化">
          <el-switch v-model="form.durable" />
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
            v-model="form.message"
            type="textarea"
            :rows="8"
            placeholder="输入消息内容"
          />
        </el-form-item>

        <el-divider content-position="left">消息属性</el-divider>
        <el-form-item label="contentType">
          <el-input v-model="form.properties.contentType" style="width: 200px" />
        </el-form-item>
        <el-form-item label="deliveryMode">
          <el-radio-group v-model="form.properties.deliveryMode">
            <el-radio :label="1">非持久化</el-radio>
            <el-radio :label="2">持久化</el-radio>
          </el-radio-group>
        </el-form-item>
        <el-form-item label="priority">
          <el-input-number v-model="form.properties.priority" :min="0" :max="9" controls-position="right" style="width: 100px" />
        </el-form-item>
        <el-form-item label="expiration">
          <el-input v-model="form.properties.expiration" placeholder="如 60000" style="width: 160px" />
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
        <el-button @click="form.message = ''">清空消息</el-button>
        <span v-if="producerStore.progress" class="progress-text">
          {{ producerStore.progress.current }}/{{ producerStore.progress.total }}
        </span>
      </div>

      <el-collapse class="history-collapse">
        <el-collapse-item title="历史记录（最近10条）">
          <el-table :data="producerStore.history" size="small" @row-click="onHistoryClick">
            <el-table-column prop="time" label="时间" width="180" />
            <el-table-column prop="exchange" label="交换机" width="120" />
            <el-table-column prop="routingKey" label="路由键" width="140" />
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
import ConnectionForm from '../components/ConnectionForm.vue'
import { useProducerStore } from '../stores/producer'
import { useConnectionStore } from '../stores/connection'
import type { SendParams } from '../../../shared/types'

const producerStore = useProducerStore()
const connectionStore = useConnectionStore()

const form = reactive<SendParams>({
  exchange: '',
  exchangeType: 'direct',
  routingKey: '',
  autoDeclare: false,
  durable: true,
  message: '',
  format: 'json',
  properties: {
    contentType: 'application/json',
    deliveryMode: 2,
    priority: 0
  },
  batch: { count: 1, intervalMs: 100 }
})

onMounted(() => {
  producerStore.loadHistory()
})

async function onSend() {
  if (!connectionStore.status.status || connectionStore.status.status !== 'connected') {
    ElMessage.warning('请先建立连接')
    return
  }
  const r = await producerStore.send({ ...form, properties: { ...form.properties }, batch: { ...form.batch } })
  if (r.success) {
    ElMessage.success(`发送成功，消息ID：${r.messageId}`)
    await producerStore.loadHistory()
  } else {
    ElMessage.error(r.error || '发送失败')
  }
}

function onHistoryClick(row: any) {
  form.message = row.messageSummary
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

- [ ] **Step 2: 验证 typecheck**

```bash
npm run typecheck
```
预期：通过。

- [ ] **Step 3: 提交**

```bash
git add -A
git commit -m "feat: ProducerView 生产者页"
```

---

## Task 16: MessageTable 与 MessageDetail 组件

**Files:**
- Create: `src/renderer/src/components/MessageTable.vue`
- Create: `src/renderer/src/components/MessageDetail.vue`

- [ ] **Step 1: 编写 MessageTable**

Create: `src/renderer/src/components/MessageTable.vue`
```vue
<template>
  <el-table :data="displayMessages" size="small" height="100%" border>
    <el-table-column prop="seq" label="序号" width="70" />
    <el-table-column prop="receivedAt" label="接收时间" width="180" />
    <el-table-column prop="exchange" label="交换机" width="120" />
    <el-table-column prop="routingKey" label="路由键" width="140" />
    <el-table-column prop="messageId" label="消息ID" width="160" show-overflow-tooltip />
    <el-table-column prop="deliveryTag" label="deliveryTag" width="100" />
    <el-table-column label="消息体" show-overflow-tooltip>
      <template #default="{ row }">
        {{ truncateContent(row.content) }}
      </template>
    </el-table-column>
    <el-table-column label="操作" width="240" fixed="right">
      <template #default="{ row }">
        <el-button size="small" link @click="onView(row)">详情</el-button>
        <el-button size="small" link type="success" @click="onAck(row)">确认</el-button>
        <el-button size="small" link type="danger" @click="onNack(row)">拒绝</el-button>
      </template>
    </el-table-column>
  </el-table>

  <MessageDetail v-model:visible="detailVisible" :message="detailMessage" />
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import MessageDetail from './MessageDetail.vue'
import { useConsumerStore } from '../stores/consumer'
import { useSettingsStore } from '../stores/settings'
import type { ConsumedMessage } from '../../../shared/types'

const consumerStore = useConsumerStore()
const settingsStore = useSettingsStore()

const detailVisible = ref(false)
const detailMessage = ref<ConsumedMessage | null>(null)

const displayMessages = computed(() => consumerStore.messages)

function truncateContent(content: string): string {
  const max = settingsStore.maxDisplayLength
  return content.length > max ? content.slice(0, max) + '...' : content
}

function onView(row: ConsumedMessage) {
  detailMessage.value = row
  detailVisible.value = true
}

async function onAck(row: ConsumedMessage) {
  const r = await consumerStore.ack(row.deliveryTag)
  if (r.success) ElMessage.success('已确认')
  else ElMessage.error(r.error || '确认失败')
}

async function onNack(row: ConsumedMessage) {
  try {
    const action = await ElMessageBox.confirm('选择处理方式', '拒绝消息', {
      distinguishCancelAndClose: true,
      confirmButtonText: '重新入队',
      cancelButtonText: '丢弃（进死信）',
      type: 'warning'
    })
    const requeue = action === 'confirm'
    const r = await consumerStore.nack(row.deliveryTag, requeue)
    if (r.success) ElMessage.success(requeue ? '已重新入队' : '已丢弃')
    else ElMessage.error(r.error || '拒绝失败')
  } catch {
    // 用户关闭弹窗
  }
}
</script>
```

- [ ] **Step 2: 编写 MessageDetail**

Create: `src/renderer/src/components/MessageDetail.vue`
```vue
<template>
  <el-dialog :model-value="visible" @update:model-value="$emit('update:visible', $event)" title="消息详情" width="700px">
    <div v-if="message">
      <el-descriptions :column="2" border size="small">
        <el-descriptions-item label="序号">{{ message.seq }}</el-descriptions-item>
        <el-descriptions-item label="接收时间">{{ message.receivedAt }}</el-descriptions-item>
        <el-descriptions-item label="交换机">{{ message.exchange }}</el-descriptions-item>
        <el-descriptions-item label="路由键">{{ message.routingKey }}</el-descriptions-item>
        <el-descriptions-item label="消息ID">{{ message.messageId || '-' }}</el-descriptions-item>
        <el-descriptions-item label="deliveryTag">{{ message.deliveryTag }}</el-descriptions-item>
        <el-descriptions-item label="contentType">{{ message.properties.contentType || '-' }}</el-descriptions-item>
        <el-descriptions-item label="deliveryMode">{{ message.properties.deliveryMode === 2 ? '持久化' : '非持久化' }}</el-descriptions-item>
        <el-descriptions-item label="priority">{{ message.properties.priority ?? '-' }}</el-descriptions-item>
        <el-descriptions-item label="expiration">{{ message.properties.expiration || '-' }}</el-descriptions-item>
        <el-descriptions-item label="headers" :span="2">{{ message.properties.headers }}</el-descriptions-item>
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

const props = defineProps<{ message: ConsumedMessage | null }>()
defineProps<{ visible: boolean }>()
defineEmits<{ 'update:visible': [boolean] }>()

const compressed = ref(true)

const displayContent = computed(() => {
  if (!props.message) return ''
  const raw = props.message.content
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

- [ ] **Step 3: 验证 typecheck**

```bash
npm run typecheck
```
预期：通过。

- [ ] **Step 4: 提交**

```bash
git add -A
git commit -m "feat: MessageTable 与 MessageDetail 组件"
```

---

## Task 17: ConsumerView 消费者页

**Files:**
- Create: `src/renderer/src/views/ConsumerView.vue`

- [ ] **Step 1: 编写 ConsumerView**

Create: `src/renderer/src/views/ConsumerView.vue`
```vue
<template>
  <div class="consumer-view">
    <ConnectionForm />

    <el-card shadow="never" class="consumer-card">
      <el-form :model="form" label-width="100px" size="small">
        <el-divider content-position="left">队列配置</el-divider>
        <el-form-item label="队列名称">
          <el-input v-model="form.queue" placeholder="如 order.create 或 order.*" style="width: 240px" />
        </el-form-item>
        <el-form-item label="自动声明">
          <el-switch v-model="form.autoDeclareQueue" />
        </el-form-item>
        <el-form-item label="持久化">
          <el-switch v-model="form.durable" />
        </el-form-item>
        <el-form-item label="排他">
          <el-switch v-model="form.exclusive" />
        </el-form-item>
        <el-form-item label="自动删除">
          <el-switch v-model="form.autoDelete" />
        </el-form-item>
        <el-form-item label="绑定交换机">
          <el-input v-model="form.exchange" placeholder="空=默认交换机" style="width: 200px" />
        </el-form-item>
        <el-form-item label="绑定路由键">
          <el-input v-model="form.bindingKey" placeholder="Topic 支持 # / *" style="width: 200px" />
        </el-form-item>

        <el-divider content-position="left">消费配置</el-divider>
        <el-form-item label="消费模式">
          <el-radio-group v-model="form.mode">
            <el-radio label="push">推模式（实时）</el-radio>
            <el-radio label="pull">拉模式（手动）</el-radio>
          </el-radio-group>
        </el-form-item>
        <el-form-item label="预取QoS">
          <el-input-number v-model="form.prefetch" :min="0" controls-position="right" style="width: 120px" />
        </el-form-item>
        <el-form-item label="自动确认">
          <el-switch v-model="form.autoAck" />
          <span class="hint">关闭可避免消息丢失</span>
        </el-form-item>
        <el-form-item label="路由键过滤">
          <el-input v-model="form.filterByRoutingKey" placeholder="如 order.create" style="width: 200px" />
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
        <el-button v-if="form.mode === 'pull' && consumerStore.running" @click="onPull">拉取10条</el-button>
        <el-button v-if="consumerStore.running" type="danger" @click="onStop">停止</el-button>
      </div>
    </el-card>

    <div class="message-list">
      <MessageTable />
    </div>
  </div>
</template>

<script setup lang="ts">
import { reactive } from 'vue'
import { ElMessage } from 'element-plus'
import ConnectionForm from '../components/ConnectionForm.vue'
import MessageTable from '../components/MessageTable.vue'
import { useConsumerStore } from '../stores/consumer'
import { useConnectionStore } from '../stores/connection'
import type { ConsumerConfig } from '../../../shared/types'

const consumerStore = useConsumerStore()
const connectionStore = useConnectionStore()

const form = reactive<ConsumerConfig>({
  queue: '',
  autoDeclareQueue: false,
  durable: true,
  exclusive: false,
  autoDelete: false,
  exchange: '',
  bindingKey: '',
  mode: 'push',
  prefetch: 0,
  autoAck: false,
  filterByRoutingKey: '',
  filterByHeaderKey: '',
  filterByHeaderValue: '',
  maxReceive: 0
})

async function onStart() {
  if (connectionStore.status.status !== 'connected') {
    ElMessage.warning('请先建立连接')
    return
  }
  const r = await consumerStore.start({ ...form })
  if (!r.success) ElMessage.error(r.error || '启动失败')
  else ElMessage.success('已开始消费')
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

async function onPull() {
  await consumerStore.pull(10)
}
</script>

<style scoped>
.consumer-view { padding: 8px; display: flex; flex-direction: column; height: 100%; }
.consumer-card { margin-bottom: 8px; }
.consumer-actions { margin: 8px 0; }
.hint { color: var(--el-text-color-secondary); font-size: 12px; margin-left: 8px; }
.message-list { flex: 1; min-height: 200px; overflow: hidden; }
</style>
```

- [ ] **Step 2: 验证 typecheck**

```bash
npm run typecheck
```
预期：通过。

- [ ] **Step 3: 提交**

```bash
git add -A
git commit -m "feat: ConsumerView 消费者页"
```

---

## Task 18: LogPanel 与 SettingsView

**Files:**
- Create: `src/renderer/src/components/LogPanel.vue`
- Create: `src/renderer/src/views/SettingsView.vue`

- [ ] **Step 1: 编写 LogPanel**

Create: `src/renderer/src/components/LogPanel.vue`
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

- [ ] **Step 2: 编写 SettingsView**

Create: `src/renderer/src/views/SettingsView.vue`
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

- [ ] **Step 3: 验证 typecheck**

```bash
npm run typecheck
```
预期：通过。

- [ ] **Step 4: 提交**

```bash
git add -A
git commit -m "feat: LogPanel 日志面板与 SettingsView 设置页"
```

---

## Task 19: 深色主题样式与全局样式

**Files:**
- Modify: `src/renderer/src/main.ts`（引入 dark 主题 css）
- Create: `src/renderer/src/styles/global.css`

- [ ] **Step 1: 引入 Element Plus 深色主题**

Modify: `src/renderer/src/main.ts`，在 `import 'element-plus/dist/index.css'` 后追加：
```ts
import 'element-plus/theme-chalk/dark/css-vars.css'
```

- [ ] **Step 2: 全局样式**

Create: `src/renderer/src/styles/global.css`
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

在 `main.ts` 追加：
```ts
import './styles/global.css'
```

- [ ] **Step 3: 验证 typecheck**

```bash
npm run typecheck
```
预期：通过。

- [ ] **Step 4: 提交**

```bash
git add -A
git commit -m "feat: 深色主题与全局样式"
```

---

## Task 20: 端到端启动验证与打包配置

**Files:**
- Modify: `package.json`（确认 build 配置）
- Modify: `electron-builder.yml` 或 package.json 的 build 字段

- [ ] **Step 1: 确认 electron-builder 配置**

检查 `package.json` 的 `build` 字段（electron-vite 模板通常已生成），确保：
```json
"build": {
  "appId": "com.erabbitmq.tool",
  "productName": "ERabbitMQTool",
  "directories": { "output": "release" },
  "files": ["dist/**/*"],
  "win": {
    "target": ["nsis"],
    "icon": "resources/icon.png"
  }
}
```

- [ ] **Step 2: 启动 dev 验证整体可运行**

```bash
npm run dev
```
预期：窗口打开，显示连接表单 + 生产者/消费者 Tab + 状态栏。无控制台报错。关闭窗口结束。

- [ ] **Step 3: 打包验证（可选，耗时较长）**

```bash
npm run build:win
```
预期：在 `release/` 生成安装包。若仅验证构建链路，可只跑 `npm run build`（不打包）确认 vite 构建通过。

- [ ] **Step 4: 提交**

```bash
git add -A
git commit -m "chore: 打包配置与端到端启动验证"
```

---

## 自检结果

**Spec 覆盖检查：**
- §3 连接管理（含 SSL/校验/持久化/状态栏）→ Task 4-7
- §4 生产者 Tab（交换机/消息/属性/批量/历史/校验）→ Task 8-9, 15
- §5 消费者 Tab（队列/消费/过滤/ack/nack/拉模式/详情/最大接收数）→ Task 10-11, 16-17
- §6.1 布局 → Task 13
- §6.2 日志 → Task 3, 18
- §6.3 设置 → Task 18
- §6.4 错误处理 → Task 5（校验）, 6（humanizeError）, 各 service try/catch
- §6.5 性能/内存 → Task 12 consumer store FIFO, settings store
- §6.6 安全性 → Task 4 密码加密, preload contextBridge（无 nodeIntegration）

**类型一致性检查：** 共享类型 `ConnectionConfig`/`SendParams`/`ConsumerConfig`/`ConsumedMessage` 在 Task 1 定义，后续 task 引用一致；`IpcResult`/`SendResult`/`ProgressInfo`/`LogEntry`/`ConnectionStatusInfo` 命名一致。

**占位符扫描：** 无 TBD/TODO；所有代码步骤均含完整代码。
