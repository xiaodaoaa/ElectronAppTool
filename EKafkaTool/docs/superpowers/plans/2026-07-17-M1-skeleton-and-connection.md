# M1: 骨架与连接管理 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 搭建 electron-vite + Vue3 + TypeScript 工程骨架，实现连接管理（CRUD/测试/持久化）、KafkaClientManager、集群概览页。

**Architecture:** 使用 electron-vite 构建工具，主进程负责 Kafka 连接与 IPC 处理，预加载层通过 contextBridge 暴露白名单 API，渲染进程使用 Vue3 + Element Plus + Pinia 构建 UI。安全基线：nodeIntegration: false、contextIsolation: true、sandbox: true。

**Tech Stack:** Electron 33+, electron-vite, Vue 3, TypeScript, Element Plus, Pinia, KafkaJS 2.x, electron-log, electron-builder

---

## 文件结构规划

| 文件 | 职责 |
|---|---|
| `package.json` | 项目配置、依赖声明、脚本 |
| `electron.vite.config.ts` | electron-vite 构建配置（主进程/预加载/渲染进程） |
| `tsconfig.json` / `tsconfig.node.json` / `tsconfig.web.json` | TypeScript 配置 |
| `src/main/index.ts` | 主进程入口：窗口创建、安全策略、日志初始化、IPC 注册 |
| `src/main/kafka/KafkaClientManager.ts` | Kafka 连接与 Admin 生命周期管理（单例） |
| `src/main/kafka/types.ts` | 主进程 Kafka 相关类型定义 |
| `src/main/ipc/channels.ts` | IPC 通道名常量（主/渲染/preload 共享） |
| `src/main/ipc/registerHandlers.ts` | 统一注册 ipcMain.handle 处理器 |
| `src/main/store/connectionStore.ts` | 连接配置持久化（safeStorage 加密密码） |
| `src/main/util/logger.ts` | electron-log 初始化与封装 |
| `src/preload/index.ts` | contextBridge 暴露 window.kafkaApi |
| `src/renderer/main.ts` | Vue 应用入口 |
| `src/renderer/App.vue` | 根组件：顶栏 + 侧边导航 + 路由视图 |
| `src/renderer/router/index.ts` | Vue Router 路由配置 |
| `src/renderer/stores/connectionStore.ts` | Pinia 连接状态管理 |
| `src/renderer/stores/metadataStore.ts` | Pinia 集群元数据状态管理 |
| `src/renderer/api/kafkaApi.ts` | 对 window.kafkaApi 的 TS 类型封装 |
| `src/renderer/views/ConnectionView.vue` | 连接管理页 |
| `src/renderer/views/ClusterView.vue` | 集群概览页（Broker 列表 + Topic 列表） |
| `src/renderer/components/ConnectionDialog.vue` | 连接新增/编辑弹窗 |
| `src/renderer/styles/` | 全局样式 |
| `resources/` | 应用图标等资源 |
| `docker/docker-compose.yml` | 教学用 KRaft Kafka 环境 |

---

### Task 1: 项目初始化与工程配置

**Files:**
- Create: `package.json`
- Create: `electron.vite.config.ts`
- Create: `tsconfig.json`
- Create: `tsconfig.node.json`
- Create: `tsconfig.web.json`
- Create: `src/main/index.ts`（骨架）
- Create: `src/preload/index.ts`（骨架）
- Create: `src/renderer/main.ts`（骨架）
- Create: `src/renderer/App.vue`（骨架）
- Create: `resources/icon.png`（占位）
- Create: `docker/docker-compose.yml`

- [ ] **Step 1: 初始化 package.json**

```bash
mkdir -p D:\Workspace\ElectronApp\EKafkaTool && cd D:\Workspace\ElectronApp\EKafkaTool && npm init -y
```

- [ ] **Step 2: 安装依赖**

```bash
npm install vue@3 vue-router@4 pinia element-plus @element-plus/icons-vue
npm install -D electron@33 electron-vite electron-builder typescript @vitejs/plugin-vue vite vue-tsc
npm install kafkajs electron-log
```

- [ ] **Step 3: 编写 package.json（含完整脚本与 electron-builder 配置）**

```json
{
  "name": "kafka-teach",
  "version": "1.0.0",
  "description": "Kafka 教学演示工具",
  "main": "./out/main/index.js",
  "scripts": {
    "dev": "electron-vite dev",
    "build": "electron-vite build",
    "preview": "electron-vite preview",
    "package:win": "electron-vite build && electron-builder --win",
    "package:mac": "electron-vite build && electron-builder --mac",
    "package:linux": "electron-vite build && electron-builder --linux",
    "typecheck": "vue-tsc --noEmit",
    "lint": "eslint . --ext .ts,.vue"
  },
  "dependencies": {
    "kafkajs": "^2.2.4",
    "electron-log": "^5.1.7"
  },
  "devDependencies": {
    "electron": "^33.0.0",
    "electron-vite": "^2.3.0",
    "electron-builder": "^25.1.8",
    "typescript": "^5.6.0",
    "vue": "^3.5.0",
    "vue-router": "^4.4.0",
    "pinia": "^2.2.0",
    "element-plus": "^2.8.0",
    "@element-plus/icons-vue": "^2.3.1",
    "@vitejs/plugin-vue": "^5.1.0",
    "vite": "^5.4.0",
    "vue-tsc": "^2.1.0"
  },
  "build": {
    "appId": "com.kafka.teach",
    "productName": "KafkaTeach",
    "directories": { "output": "release" },
    "win": { "target": "nsis" },
    "mac": { "target": "dmg" },
    "linux": { "target": "AppImage" }
  }
}
```

- [ ] **Step 4: 编写 electron.vite.config.ts**

```ts
import { resolve } from 'path'
import { defineConfig, externalizeDepsPlugin } from 'electron-vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  main: {
    plugins: [externalizeDepsPlugin()],
    build: {
      outDir: 'out/main',
      rollupOptions: {
        input: { index: resolve(__dirname, 'src/main/index.ts') }
      }
    }
  },
  preload: {
    plugins: [externalizeDepsPlugin()],
    build: {
      outDir: 'out/preload',
      rollupOptions: {
        input: { index: resolve(__dirname, 'src/preload/index.ts') }
      }
    }
  },
  renderer: {
    root: resolve(__dirname, 'src/renderer'),
    build: {
      outDir: resolve(__dirname, 'out/renderer'),
      rollupOptions: {
        input: { index: resolve(__dirname, 'src/renderer/index.html') }
      }
    },
    plugins: [vue()],
    resolve: {
      alias: {
        '@': resolve(__dirname, 'src/renderer')
      }
    }
  }
})
```

- [ ] **Step 5: 编写 tsconfig.json**

```json
{
  "files": [],
  "references": [
    { "path": "./tsconfig.node.json" },
    { "path": "./tsconfig.web.json" }
  ]
}
```

- [ ] **Step 6: 编写 tsconfig.node.json（主进程 + 预加载）**

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "ESNext",
    "moduleResolution": "bundler",
    "strict": true,
    "esModuleInterop": true,
    "skipLibCheck": true,
    "forceConsistentCasingInFileNames": true,
    "resolveJsonModule": true,
    "isolatedModules": true,
    "outDir": "./out",
    "declaration": true,
    "types": ["node"]
  },
  "include": ["src/main/**/*.ts", "src/preload/**/*.ts", "electron.vite.config.ts"]
}
```

- [ ] **Step 7: 编写 tsconfig.web.json（渲染进程）**

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "ESNext",
    "moduleResolution": "bundler",
    "strict": true,
    "jsx": "preserve",
    "esModuleInterop": true,
    "skipLibCheck": true,
    "forceConsistentCasingInFileNames": true,
    "resolveJsonModule": true,
    "isolatedModules": true,
    "paths": {
      "@/*": ["./src/renderer/*"]
    },
    "types": ["vite/client"]
  },
  "include": ["src/renderer/**/*.ts", "src/renderer/**/*.vue"]
}
```

- [ ] **Step 8: 编写 src/main/index.ts 骨架（安全基线窗口）**

```ts
import { app, BrowserWindow, shell } from 'electron'
import { join } from 'path'
import { initLogger } from './util/logger'
import { registerAllHandlers } from './ipc/registerHandlers'

const logger = initLogger()

function createWindow(): void {
  const mainWindow = new BrowserWindow({
    width: 1440,
    height: 900,
    title: 'KafkaTeach',
    webPreferences: {
      preload: join(__dirname, '../preload/index.js'),
      nodeIntegration: false,
      contextIsolation: true,
      sandbox: true,
    },
  })

  mainWindow.webContents.setWindowOpenHandler(({ url }) => {
    if (url.startsWith('https:')) shell.openExternal(url)
    return { action: 'deny' }
  })

  if (process.env.ELECTRON_RENDERER_URL) {
    mainWindow.loadURL(process.env.ELECTRON_RENDERER_URL)
  } else {
    mainWindow.loadFile(join(__dirname, '../renderer/index.html'))
  }

  logger.info('主窗口已创建')
}

app.whenReady().then(() => {
  registerAllHandlers()
  createWindow()
})

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit()
})

app.on('activate', () => {
  if (BrowserWindow.getAllWindows().length === 0) createWindow()
})
```

- [ ] **Step 9: 编写 src/preload/index.ts 骨架**

```ts
import { contextBridge } from 'electron'

contextBridge.exposeInMainWorld('kafkaApi', {
  platform: process.platform,
})
```

- [ ] **Step 10: 编写 src/renderer/index.html**

```html
<!DOCTYPE html>
<html lang="zh-CN">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <meta http-equiv="Content-Security-Policy" content="default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'" />
  <title>KafkaTeach</title>
</head>
<body>
  <div id="app"></div>
  <script type="module" src="./main.ts"></script>
</body>
</html>
```

- [ ] **Step 11: 编写 src/renderer/main.ts 骨架**

```ts
import { createApp } from 'vue'
import { createPinia } from 'pinia'
import ElementPlus from 'element-plus'
import 'element-plus/dist/index.css'
import App from './App.vue'
import router from './router'

const app = createApp(App)
app.use(createPinia())
app.use(router)
app.use(ElementPlus)
app.mount('#app')
```

- [ ] **Step 12: 编写 src/renderer/App.vue 骨架**

```vue
<template>
  <div id="app-root">
    <router-view />
  </div>
</template>

<script setup lang="ts">
</script>

<style>
#app-root {
  height: 100vh;
  margin: 0;
  padding: 0;
}
</style>
```

- [ ] **Step 13: 编写 src/renderer/router/index.ts 骨架**

```ts
import { createRouter, createWebHashHistory } from 'vue-router'

const router = createRouter({
  history: createWebHashHistory(),
  routes: [
    { path: '/', redirect: '/connection' },
    { path: '/connection', name: 'connection', component: () => import('@/views/ConnectionView.vue') },
    { path: '/cluster', name: 'cluster', component: () => import('@/views/ClusterView.vue') },
  ],
})

export default router
```

- [ ] **Step 14: 编写 src/renderer/views/ConnectionView.vue 骨架**

```vue
<template>
  <div class="connection-view">
    <h1>连接管理</h1>
    <p>KafkaTeach 启动成功</p>
  </div>
</template>

<script setup lang="ts">
</script>
```

- [ ] **Step 15: 编写 src/renderer/views/ClusterView.vue 骨架**

```vue
<template>
  <div class="cluster-view">
    <h1>集群概览</h1>
    <p>请先连接 Kafka</p>
  </div>
</template>

<script setup lang="ts">
</script>
```

- [ ] **Step 16: 编写 docker/docker-compose.yml**

```yaml
services:
  kafka:
    image: apache/kafka:3.9.0
    container_name: kafka-teach
    ports:
      - "9092:9092"
    environment:
      KAFKA_NODE_ID: 1
      KAFKA_PROCESS_ROLES: broker,controller
      KAFKA_CONTROLLER_QUORUM_VOTERS: 1@kafka:29093
      KAFKA_LISTENERS: PLAINTEXT://0.0.0.0:9092,CONTROLLER://:29093
      KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://localhost:9092
      KAFKA_CONTROLLER_LISTENER_NAMES: CONTROLLER
      KAFKA_LISTENER_SECURITY_PROTOCOL_MAP: CONTROLLER:PLAINTEXT,PLAINTEXT:PLAINTEXT
      KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 1
      KAFKA_AUTO_CREATE_TOPICS_ENABLE: "true"
      CLUSTER_ID: MkU3OEVBNTcwNTJENDM2Qk
    volumes:
      - kafka-data:/var/lib/kafka/data

volumes:
  kafka-data:
```

- [ ] **Step 17: 验证项目可启动**

```bash
npm run dev
```

预期：Electron 窗口打开，显示"连接管理"页面。

- [ ] **Step 18: Commit**

```bash
git add -A && git commit -m "feat: 初始化 electron-vite + Vue3 工程骨架，含安全基线窗口与 docker-compose"
```

---

### Task 2: 主进程日志与类型定义

**Files:**
- Create: `src/main/util/logger.ts`
- Create: `src/main/kafka/types.ts`

- [ ] **Step 1: 编写 src/main/util/logger.ts**

```ts
import log from 'electron-log'

export function initLogger() {
  log.transports.file.level = 'info'
  log.transports.console.level = 'debug'
  log.transports.file.maxSize = 10 * 1024 * 1024
  log.info('===== KafkaTeach 启动 =====')
  return log
}

export { log as logger }
```

- [ ] **Step 2: 编写 src/main/kafka/types.ts**

```ts
export interface ConnectionConfig {
  id: string
  name: string
  brokers: string[]
  clientId: string
  sasl?: {
    mechanism: 'plain' | 'scram-sha-256' | 'scram-sha-512'
    username: string
    password: string
  }
  ssl?: {
    enabled: boolean
    caFile?: string
    rejectUnauthorized?: boolean
  }
}

export interface TestResult {
  success: boolean
  brokerCount?: number
  controllerId?: number
  error?: string
}

export interface ClusterSummary {
  brokers: BrokerInfo[]
  topics: TopicSummary[]
}

export interface BrokerInfo {
  nodeId: number
  host: string
  port: number
  isController: boolean
}

export interface TopicSummary {
  name: string
  partitionCount: number
  replicationFactor: number
  totalMessages: number
}

export interface TopicDetail {
  name: string
  partitions: PartitionDetail[]
}

export interface PartitionDetail {
  partition: number
  leader: number
  replicas: number[]
  isr: number[]
  earliestOffset: string
  latestOffset: string
}

export interface ProduceRequest {
  topic: string
  partition?: number
  key?: string
  value: string
  headers?: Record<string, string>
  acks?: -1 | 0 | 1
}

export interface ProduceResult {
  topic: string
  partition: number
  offset: string
  timestamp: string
  latencyMs: number
}

export interface ConsumerOptions {
  alias: string
  groupId: string
  topics: string[]
  fromBeginning: boolean
  autoCommit: boolean
}

export interface ConsumerInstanceState {
  instanceId: string
  alias: string
  groupId: string
  topics: string[]
  status: 'created' | 'running' | 'paused' | 'stopped' | 'error'
  assignments: Array<{ topic: string; partitions: number[] }>
  consumedCount: number
  lag?: number
  error?: string
}

export interface MessageRecord {
  seq: number
  instanceId: string
  topic: string
  partition: number
  offset: string
  key: string | null
  value: string
  headers?: Record<string, string>
  timestamp: string
  receivedAt: number
}

export interface RebalanceEvent {
  groupId: string
  generation: number
  assignments: Record<string, number[]>
  ts: number
}

export interface ScenarioMeta {
  id: string
  title: string
  description: string
  duration: string
}

export interface ScenarioStep {
  type: 'createTopic' | 'note' | 'createConsumer' | 'produce' | 'sleep'
  [key: string]: unknown
}
```

- [ ] **Step 3: Commit**

```bash
git add src/main/util/logger.ts src/main/kafka/types.ts && git commit -m "feat: 添加主进程日志封装与 Kafka 类型定义"
```

---

### Task 3: IPC 通道常量与连接配置持久化

**Files:**
- Create: `src/main/ipc/channels.ts`
- Create: `src/main/store/connectionStore.ts`

- [ ] **Step 1: 编写 src/main/ipc/channels.ts**

```ts
export const IPC_CHANNELS = {
  CONN_LIST: 'conn:list',
  CONN_SAVE: 'conn:save',
  CONN_DELETE: 'conn:delete',
  CONN_TEST: 'conn:test',
  CONN_CONNECT: 'conn:connect',
  CONN_DISCONNECT: 'conn:disconnect',

  ADMIN_LIST_TOPICS: 'admin:listTopics',
  ADMIN_TOPIC_DETAIL: 'admin:topicDetail',
  ADMIN_CREATE_TOPIC: 'admin:createTopic',
  ADMIN_DELETE_TOPIC: 'admin:deleteTopic',

  PRODUCER_SEND: 'producer:send',
  PRODUCER_SEND_BATCH: 'producer:sendBatch',
  PRODUCER_STOP_BATCH: 'producer:stopBatch',

  CONSUMER_CREATE: 'consumer:create',
  CONSUMER_START: 'consumer:start',
  CONSUMER_STOP: 'consumer:stop',
  CONSUMER_PAUSE: 'consumer:pause',
  CONSUMER_RESUME: 'consumer:resume',
  CONSUMER_SEEK: 'consumer:seek',
  CONSUMER_COMMIT: 'consumer:commit',
  CONSUMER_LIST_INSTANCES: 'consumer:listInstances',

  SCENARIO_RUN: 'scenario:run',
  SCENARIO_STOP: 'scenario:stop',
  SCENARIO_LIST: 'scenario:list',

  EVENT_CONN_STATUS: 'event:connStatus',
  EVENT_CONSUMER_MESSAGE: 'event:consumerMessage',
  EVENT_CONSUMER_STATE: 'event:consumerState',
  EVENT_REBALANCE: 'event:rebalance',
  EVENT_PRODUCE_ACK: 'event:produceAck',
  EVENT_SCENARIO_STEP: 'event:scenarioStep',
} as const
```

- [ ] **Step 2: 编写 src/main/store/connectionStore.ts**

```ts
import { app } from 'electron'
import { join } from 'path'
import { readFileSync, writeFileSync, existsSync, mkdirSync } from 'fs'
import { safeStorage } from 'electron'
import type { ConnectionConfig } from '../kafka/types'
import { logger } from '../util/logger'

const STORE_FILE = join(app.getPath('userData'), 'connections.json')

function ensureDir(): void {
  const dir = app.getPath('userData')
  if (!existsSync(dir)) mkdirSync(dir, { recursive: true })
}

function encryptPassword(password: string): string {
  if (safeStorage.isEncryptionAvailable()) {
    return safeStorage.encryptString(password).toString('base64')
  }
  return Buffer.from(password).toString('base64')
}

function decryptPassword(encrypted: string): string {
  if (safeStorage.isEncryptionAvailable()) {
    return safeStorage.decryptString(Buffer.from(encrypted, 'base64'))
  }
  return Buffer.from(encrypted, 'base64').toString('utf-8')
}

export function loadConnections(): ConnectionConfig[] {
  try {
    ensureDir()
    if (!existsSync(STORE_FILE)) return []
    const raw = readFileSync(STORE_FILE, 'utf-8')
    const configs: ConnectionConfig[] = JSON.parse(raw)
    for (const c of configs) {
      if (c.sasl?.password) {
        c.sasl.password = decryptPassword(c.sasl.password)
      }
    }
    return configs
  } catch (err) {
    logger.error('加载连接配置失败:', err)
    return []
  }
}

export function saveConnections(configs: ConnectionConfig[]): void {
  ensureDir()
  const toSave = configs.map(c => {
    const copy = { ...c, sasl: c.sasl ? { ...c.sasl } : undefined }
    if (copy.sasl?.password) {
      copy.sasl.password = encryptPassword(copy.sasl.password)
    }
    return copy
  })
  writeFileSync(STORE_FILE, JSON.stringify(toSave, null, 2), 'utf-8')
  logger.info(`已保存 ${configs.length} 条连接配置`)
}
```

- [ ] **Step 3: Commit**

```bash
git add src/main/ipc/channels.ts src/main/store/connectionStore.ts && git commit -m "feat: 添加 IPC 通道常量与连接配置持久化（safeStorage 加密）"
```

---

### Task 4: KafkaClientManager 实现

**Files:**
- Create: `src/main/kafka/KafkaClientManager.ts`

- [ ] **Step 1: 编写 src/main/kafka/KafkaClientManager.ts**

```ts
import { Kafka, Admin, type ITopicMetadata, type BrokerMetadata } from 'kafkajs'
import type { ConnectionConfig, ClusterSummary, BrokerInfo, TopicSummary } from './types'
import { logger } from '../util/logger'

export class KafkaClientManager {
  private kafka: Kafka | null = null
  private admin: Admin | null = null
  private onStatusChange?: (status: string, detail?: string) => void

  setStatusCallback(cb: (status: string, detail?: string) => void): void {
    this.onStatusChange = cb
  }

  async connect(config: ConnectionConfig): Promise<ClusterSummary> {
    await this.disconnect()

    this.kafka = new Kafka({
      clientId: config.clientId,
      brokers: config.brokers,
      ssl: config.ssl?.enabled
        ? { rejectUnauthorized: config.ssl.rejectUnauthorized ?? true }
        : undefined,
      sasl: config.sasl
        ? { mechanism: config.sasl.mechanism, username: config.sasl.username, password: config.sasl.password }
        : undefined,
      retry: { retries: 3 },
    })

    this.admin = this.kafka.admin()
    await this.admin.connect()

    const cluster = await this.admin.describeCluster()
    const brokers: BrokerInfo[] = cluster.brokers.map((b: BrokerMetadata) => ({
      nodeId: b.nodeId,
      host: b.host,
      port: b.port,
      isController: b.nodeId === cluster.controller,
    }))

    const topicMetas = await this.admin.listTopics()
    const topics: TopicSummary[] = []

    for (const topicName of topicMetas) {
      const meta = await this.admin.fetchTopicMetadata({ topics: [topicName] })
      const t = meta.topics[0]
      topics.push({
        name: t.name,
        partitionCount: t.partitions.length,
        replicationFactor: t.partitions[0]?.replicas.length ?? 0,
        totalMessages: 0,
      })
    }

    this.onStatusChange?.('connected')
    logger.info(`已连接 Kafka: ${config.brokers.join(',')}`)

    return { brokers, topics }
  }

  async disconnect(): Promise<void> {
    try {
      if (this.admin) {
        await this.admin.disconnect()
        this.admin = null
      }
    } catch (err) {
      logger.error('断开 Admin 连接失败:', err)
    }
    this.kafka = null
    this.onStatusChange?.('disconnected')
    logger.info('已断开 Kafka 连接')
  }

  getKafka(): Kafka | null {
    return this.kafka
  }

  getAdmin(): Admin | null {
    return this.admin
  }

  isConnected(): boolean {
    return this.admin !== null
  }

  async testConnection(config: ConnectionConfig): Promise<{ success: boolean; brokerCount?: number; controllerId?: number; error?: string }> {
    const testKafka = new Kafka({
      clientId: config.clientId + '-test',
      brokers: config.brokers,
      ssl: config.ssl?.enabled
        ? { rejectUnauthorized: config.ssl.rejectUnauthorized ?? true }
        : undefined,
      sasl: config.sasl
        ? { mechanism: config.sasl.mechanism, username: config.sasl.username, password: config.sasl.password }
        : undefined,
      connectionTimeout: 3000,
      retry: { retries: 0 },
    })
    const testAdmin = testKafka.admin()
    try {
      await testAdmin.connect()
      const cluster = await testAdmin.describeCluster()
      return {
        success: true,
        brokerCount: cluster.brokers.length,
        controllerId: cluster.controller,
      }
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : String(err)
      return { success: false, error: msg }
    } finally {
      await testAdmin.disconnect().catch(() => {})
    }
  }
}

export const kafkaClientManager = new KafkaClientManager()
```

- [ ] **Step 2: Commit**

```bash
git add src/main/kafka/KafkaClientManager.ts && git commit -m "feat: 实现 KafkaClientManager 单例（连接/断开/测试/集群概要）"
```

---

### Task 5: IPC Handler 注册（连接管理部分）

**Files:**
- Create: `src/main/ipc/registerHandlers.ts`
- Modify: `src/main/index.ts`（已导入 registerAllHandlers，无需修改）

- [ ] **Step 1: 编写 src/main/ipc/registerHandlers.ts**

```ts
import { ipcMain, BrowserWindow } from 'electron'
import { IPC_CHANNELS } from './channels'
import { kafkaClientManager } from '../kafka/KafkaClientManager'
import { loadConnections, saveConnections } from '../store/connectionStore'
import type { ConnectionConfig } from '../kafka/types'
import { logger } from '../util/logger'

function getMainWindow(): BrowserWindow | null {
  const wins = BrowserWindow.getAllWindows()
  return wins.length > 0 ? wins[0] : null
}

function pushToRenderer(channel: string, payload: unknown): void {
  const win = getMainWindow()
  if (win && !win.isDestroyed()) {
    win.webContents.send(channel, payload)
  }
}

export function registerAllHandlers(): void {
  kafkaClientManager.setStatusCallback((status, detail) => {
    pushToRenderer(IPC_CHANNELS.EVENT_CONN_STATUS, { status, detail })
  })

  ipcMain.handle(IPC_CHANNELS.CONN_LIST, async () => {
    return loadConnections()
  })

  ipcMain.handle(IPC_CHANNELS.CONN_SAVE, async (_event, config: ConnectionConfig) => {
    const configs = loadConnections()
    const idx = configs.findIndex(c => c.id === config.id)
    if (idx >= 0) {
      configs[idx] = config
    } else {
      configs.push(config)
    }
    saveConnections(configs)
    logger.info(`连接配置已保存: ${config.name}`)
  })

  ipcMain.handle(IPC_CHANNELS.CONN_DELETE, async (_event, id: string) => {
    const configs = loadConnections().filter(c => c.id !== id)
    saveConnections(configs)
    logger.info(`连接配置已删除: ${id}`)
  })

  ipcMain.handle(IPC_CHANNELS.CONN_TEST, async (_event, config: ConnectionConfig) => {
    return kafkaClientManager.testConnection(config)
  })

  ipcMain.handle(IPC_CHANNELS.CONN_CONNECT, async (_event, id: string) => {
    const configs = loadConnections()
    const config = configs.find(c => c.id === id)
    if (!config) throw new Error(`连接配置不存在: ${id}`)
    return kafkaClientManager.connect(config)
  })

  ipcMain.handle(IPC_CHANNELS.CONN_DISCONNECT, async () => {
    await kafkaClientManager.disconnect()
  })

  ipcMain.handle(IPC_CHANNELS.ADMIN_LIST_TOPICS, async () => {
    const admin = kafkaClientManager.getAdmin()
    if (!admin) throw new Error('未连接 Kafka')
    const topicNames = await admin.listTopics()
    const topics = []
    for (const name of topicNames) {
      const meta = await admin.fetchTopicMetadata({ topics: [name] })
      const t = meta.topics[0]
      topics.push({
        name: t.name,
        partitionCount: t.partitions.length,
        replicationFactor: t.partitions[0]?.replicas.length ?? 0,
        totalMessages: 0,
      })
    }
    return topics
  })

  ipcMain.handle(IPC_CHANNELS.ADMIN_TOPIC_DETAIL, async (_event, topic: string) => {
    const admin = kafkaClientManager.getAdmin()
    if (!admin) throw new Error('未连接 Kafka')
    const meta = await admin.fetchTopicMetadata({ topics: [topic] })
    const t = meta.topics[0]
    if (!t) throw new Error(`Topic 不存在: ${topic}`)

    const offsets = await admin.fetchTopicOffsets(topic)
    return {
      name: t.name,
      partitions: t.partitions.map(p => {
        const offsetInfo = offsets.find(o => o.partition === p.partition)
        return {
          partition: p.partition,
          leader: p.leader,
          replicas: p.replicas,
          isr: p.isr,
          earliestOffset: offsetInfo?.low ?? '0',
          latestOffset: offsetInfo?.high ?? '0',
        }
      }),
    }
  })

  ipcMain.handle(IPC_CHANNELS.ADMIN_CREATE_TOPIC, async (_event, args: { name: string; numPartitions: number; replicationFactor: number }) => {
    const admin = kafkaClientManager.getAdmin()
    if (!admin) throw new Error('未连接 Kafka')
    await admin.createTopics({
      topics: [{ topic: args.name, numPartitions: args.numPartitions, replicationFactor: args.replicationFactor }],
    })
    logger.info(`Topic 已创建: ${args.name}`)
  })

  ipcMain.handle(IPC_CHANNELS.ADMIN_DELETE_TOPIC, async (_event, name: string) => {
    const admin = kafkaClientManager.getAdmin()
    if (!admin) throw new Error('未连接 Kafka')
    await admin.deleteTopics({ topics: [name] })
    logger.info(`Topic 已删除: ${name}`)
  })

  logger.info('IPC Handlers 已注册')
}
```

- [ ] **Step 2: Commit**

```bash
git add src/main/ipc/registerHandlers.ts && git commit -m "feat: 注册连接管理与集群元数据 IPC handlers"
```

---

### Task 6: Preload 安全暴露 API

**Files:**
- Modify: `src/preload/index.ts`

- [ ] **Step 1: 重写 src/preload/index.ts**

```ts
import { contextBridge, ipcRenderer } from 'electron'
import { IPC_CHANNELS } from '../main/ipc/channels'
import type {
  ConnectionConfig, TestResult, ClusterSummary, TopicSummary,
  TopicDetail, ProduceRequest, ProduceResult, ConsumerOptions,
  ConsumerInstanceState, MessageRecord, RebalanceEvent, ScenarioMeta,
} from '../main/kafka/types'

const api = {
  connList: (): Promise<ConnectionConfig[]> =>
    ipcRenderer.invoke(IPC_CHANNELS.CONN_LIST),

  connSave: (config: ConnectionConfig): Promise<void> =>
    ipcRenderer.invoke(IPC_CHANNELS.CONN_SAVE, config),

  connDelete: (id: string): Promise<void> =>
    ipcRenderer.invoke(IPC_CHANNELS.CONN_DELETE, id),

  connTest: (config: ConnectionConfig): Promise<TestResult> =>
    ipcRenderer.invoke(IPC_CHANNELS.CONN_TEST, config),

  connConnect: (id: string): Promise<ClusterSummary> =>
    ipcRenderer.invoke(IPC_CHANNELS.CONN_CONNECT, id),

  connDisconnect: (): Promise<void> =>
    ipcRenderer.invoke(IPC_CHANNELS.CONN_DISCONNECT),

  listTopics: (): Promise<TopicSummary[]> =>
    ipcRenderer.invoke(IPC_CHANNELS.ADMIN_LIST_TOPICS),

  topicDetail: (topic: string): Promise<TopicDetail> =>
    ipcRenderer.invoke(IPC_CHANNELS.ADMIN_TOPIC_DETAIL, topic),

  createTopic: (args: { name: string; numPartitions: number; replicationFactor: number }): Promise<void> =>
    ipcRenderer.invoke(IPC_CHANNELS.ADMIN_CREATE_TOPIC, args),

  deleteTopic: (name: string): Promise<void> =>
    ipcRenderer.invoke(IPC_CHANNELS.ADMIN_DELETE_TOPIC, name),

  produce: (req: ProduceRequest): Promise<ProduceResult> =>
    ipcRenderer.invoke(IPC_CHANNELS.PRODUCER_SEND, req),

  produceBatch: (args: { req: ProduceRequest; count: number; intervalMs: number; keyStrategy: string }): Promise<string> =>
    ipcRenderer.invoke(IPC_CHANNELS.PRODUCER_SEND_BATCH, args),

  stopBatch: (taskId: string): Promise<void> =>
    ipcRenderer.invoke(IPC_CHANNELS.PRODUCER_STOP_BATCH, taskId),

  createConsumer: (opts: ConsumerOptions): Promise<string> =>
    ipcRenderer.invoke(IPC_CHANNELS.CONSUMER_CREATE, opts),

  startConsumer: (id: string): Promise<void> =>
    ipcRenderer.invoke(IPC_CHANNELS.CONSUMER_START, id),

  stopConsumer: (id: string): Promise<void> =>
    ipcRenderer.invoke(IPC_CHANNELS.CONSUMER_STOP, id),

  pauseConsumer: (id: string): Promise<void> =>
    ipcRenderer.invoke(IPC_CHANNELS.CONSUMER_PAUSE, id),

  resumeConsumer: (id: string): Promise<void> =>
    ipcRenderer.invoke(IPC_CHANNELS.CONSUMER_RESUME, id),

  seekConsumer: (args: { instanceId: string; topic: string; partition: number; offset: string }): Promise<void> =>
    ipcRenderer.invoke(IPC_CHANNELS.CONSUMER_SEEK, args),

  commitConsumer: (id: string): Promise<void> =>
    ipcRenderer.invoke(IPC_CHANNELS.CONSUMER_COMMIT, id),

  listConsumerInstances: (): Promise<ConsumerInstanceState[]> =>
    ipcRenderer.invoke(IPC_CHANNELS.CONSUMER_LIST_INSTANCES),

  runScenario: (scenarioId: string): Promise<string> =>
    ipcRenderer.invoke(IPC_CHANNELS.SCENARIO_RUN, scenarioId),

  stopScenario: (runId: string): Promise<void> =>
    ipcRenderer.invoke(IPC_CHANNELS.SCENARIO_STOP, runId),

  listScenarios: (): Promise<ScenarioMeta[]> =>
    ipcRenderer.invoke(IPC_CHANNELS.SCENARIO_LIST),

  onConnStatus: (cb: (data: { status: string; detail?: string }) => void) => {
    const handler = (_event: Electron.IpcRendererEvent, data: { status: string; detail?: string }) => cb(data)
    ipcRenderer.on(IPC_CHANNELS.EVENT_CONN_STATUS, handler)
    return () => ipcRenderer.removeListener(IPC_CHANNELS.EVENT_CONN_STATUS, handler)
  },

  onConsumerMessage: (cb: (batch: MessageRecord[]) => void) => {
    const handler = (_event: Electron.IpcRendererEvent, batch: MessageRecord[]) => cb(batch)
    ipcRenderer.on(IPC_CHANNELS.EVENT_CONSUMER_MESSAGE, handler)
    return () => ipcRenderer.removeListener(IPC_CHANNELS.EVENT_CONSUMER_MESSAGE, handler)
  },

  onConsumerState: (cb: (state: ConsumerInstanceState) => void) => {
    const handler = (_event: Electron.IpcRendererEvent, state: ConsumerInstanceState) => cb(state)
    ipcRenderer.on(IPC_CHANNELS.EVENT_CONSUMER_STATE, handler)
    return () => ipcRenderer.removeListener(IPC_CHANNELS.EVENT_CONSUMER_STATE, handler)
  },

  onRebalance: (cb: (e: RebalanceEvent) => void) => {
    const handler = (_event: Electron.IpcRendererEvent, e: RebalanceEvent) => cb(e)
    ipcRenderer.on(IPC_CHANNELS.EVENT_REBALANCE, handler)
    return () => ipcRenderer.removeListener(IPC_CHANNELS.EVENT_REBALANCE, handler)
  },

  onProduceAck: (cb: (result: ProduceResult) => void) => {
    const handler = (_event: Electron.IpcRendererEvent, result: ProduceResult) => cb(result)
    ipcRenderer.on(IPC_CHANNELS.EVENT_PRODUCE_ACK, handler)
    return () => ipcRenderer.removeListener(IPC_CHANNELS.EVENT_PRODUCE_ACK, handler)
  },

  onScenarioStep: (cb: (data: { runId: string; stepIndex: number; message: string }) => void) => {
    const handler = (_event: Electron.IpcRendererEvent, data: { runId: string; stepIndex: number; message: string }) => cb(data)
    ipcRenderer.on(IPC_CHANNELS.EVENT_SCENARIO_STEP, handler)
    return () => ipcRenderer.removeListener(IPC_CHANNELS.EVENT_SCENARIO_STEP, handler)
  },
}

contextBridge.exposeInMainWorld('kafkaApi', api)

export type KafkaApi = typeof api
```

- [ ] **Step 2: Commit**

```bash
git add src/preload/index.ts && git commit -m "feat: 实现 preload contextBridge 白名单 API 暴露"
```

---

### Task 7: 渲染进程 API 封装与 Pinia Store

**Files:**
- Create: `src/renderer/api/kafkaApi.ts`
- Create: `src/renderer/stores/connectionStore.ts`
- Create: `src/renderer/stores/metadataStore.ts`

- [ ] **Step 1: 编写 src/renderer/api/kafkaApi.ts**

```ts
import type {
  ConnectionConfig, TestResult, ClusterSummary, TopicSummary,
  TopicDetail, ProduceRequest, ProduceResult, ConsumerOptions,
  ConsumerInstanceState, MessageRecord, RebalanceEvent, ScenarioMeta,
} from '../../main/kafka/types'

interface KafkaApi {
  connList(): Promise<ConnectionConfig[]>
  connSave(config: ConnectionConfig): Promise<void>
  connDelete(id: string): Promise<void>
  connTest(config: ConnectionConfig): Promise<TestResult>
  connConnect(id: string): Promise<ClusterSummary>
  connDisconnect(): Promise<void>
  listTopics(): Promise<TopicSummary[]>
  topicDetail(topic: string): Promise<TopicDetail>
  createTopic(args: { name: string; numPartitions: number; replicationFactor: number }): Promise<void>
  deleteTopic(name: string): Promise<void>
  produce(req: ProduceRequest): Promise<ProduceResult>
  produceBatch(args: { req: ProduceRequest; count: number; intervalMs: number; keyStrategy: string }): Promise<string>
  stopBatch(taskId: string): Promise<void>
  createConsumer(opts: ConsumerOptions): Promise<string>
  startConsumer(id: string): Promise<void>
  stopConsumer(id: string): Promise<void>
  pauseConsumer(id: string): Promise<void>
  resumeConsumer(id: string): Promise<void>
  seekConsumer(args: { instanceId: string; topic: string; partition: number; offset: string }): Promise<void>
  commitConsumer(id: string): Promise<void>
  listConsumerInstances(): Promise<ConsumerInstanceState[]>
  runScenario(scenarioId: string): Promise<string>
  stopScenario(runId: string): Promise<void>
  listScenarios(): Promise<ScenarioMeta[]>
  onConnStatus(cb: (data: { status: string; detail?: string }) => void): () => void
  onConsumerMessage(cb: (batch: MessageRecord[]) => void): () => void
  onConsumerState(cb: (state: ConsumerInstanceState) => void): () => void
  onRebalance(cb: (e: RebalanceEvent) => void): () => void
  onProduceAck(cb: (result: ProduceResult) => void): () => void
  onScenarioStep(cb: (data: { runId: string; stepIndex: number; message: string }) => void): () => void
}

declare global {
  interface Window {
    kafkaApi: KafkaApi
  }
}

export function getKafkaApi(): KafkaApi {
  if (!window.kafkaApi) {
    throw new Error('kafkaApi 不可用，请确保 preload 脚本已加载')
  }
  return window.kafkaApi
}
```

- [ ] **Step 2: 编写 src/renderer/stores/connectionStore.ts**

```ts
import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { getKafkaApi } from '@/api/kafkaApi'
import type { ConnectionConfig, ClusterSummary } from '../../main/kafka/types'

export const useConnectionStore = defineStore('connection', () => {
  const configs = ref<ConnectionConfig[]>([])
  const activeId = ref<string | null>(null)
  const status = ref<'disconnected' | 'connecting' | 'connected' | 'error'>('disconnected')
  const cluster = ref<ClusterSummary | null>(null)
  const error = ref<string | null>(null)

  const isConnected = computed(() => status.value === 'connected')
  const activeConfig = computed(() => configs.value.find(c => c.id === activeId.value) ?? null)

  async function loadConfigs() {
    configs.value = await getKafkaApi().connList()
  }

  async function saveConfig(config: ConnectionConfig) {
    await getKafkaApi().connSave(config)
    await loadConfigs()
  }

  async function deleteConfig(id: string) {
    await getKafkaApi().connDelete(id)
    if (activeId.value === id) {
      activeId.value = null
      status.value = 'disconnected'
      cluster.value = null
    }
    await loadConfigs()
  }

  async function testConnection(config: ConnectionConfig) {
    return getKafkaApi().connTest(config)
  }

  async function connect(id: string) {
    status.value = 'connecting'
    error.value = null
    try {
      cluster.value = await getKafkaApi().connConnect(id)
      activeId.value = id
      status.value = 'connected'
    } catch (err: unknown) {
      status.value = 'error'
      error.value = err instanceof Error ? err.message : String(err)
      throw err
    }
  }

  async function disconnect() {
    await getKafkaApi().connDisconnect()
    activeId.value = null
    status.value = 'disconnected'
    cluster.value = null
  }

  function setupStatusListener() {
    getKafkaApi().onConnStatus((data) => {
      if (data.status === 'disconnected' || data.status === 'error') {
        status.value = data.status
        if (data.detail) error.value = data.detail
      }
    })
  }

  return {
    configs, activeId, status, cluster, error,
    isConnected, activeConfig,
    loadConfigs, saveConfig, deleteConfig, testConnection, connect, disconnect, setupStatusListener,
  }
})
```

- [ ] **Step 3: 编写 src/renderer/stores/metadataStore.ts**

```ts
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { getKafkaApi } from '@/api/kafkaApi'
import type { TopicSummary, TopicDetail } from '../../main/kafka/types'

export const useMetadataStore = defineStore('metadata', () => {
  const topics = ref<TopicSummary[]>([])
  const topicDetails = ref<Map<string, TopicDetail>>(new Map())
  const loading = ref(false)
  let pollTimer: ReturnType<typeof setInterval> | null = null

  async function refreshTopics() {
    loading.value = true
    try {
      topics.value = await getKafkaApi().listTopics()
    } finally {
      loading.value = false
    }
  }

  async function fetchTopicDetail(topic: string) {
    const detail = await getKafkaApi().topicDetail(topic)
    topicDetails.value.set(topic, detail)
    return detail
  }

  function startPolling() {
    stopPolling()
    refreshTopics()
    pollTimer = setInterval(refreshTopics, 30000)
  }

  function stopPolling() {
    if (pollTimer) {
      clearInterval(pollTimer)
      pollTimer = null
    }
  }

  return {
    topics, topicDetails, loading,
    refreshTopics, fetchTopicDetail, startPolling, stopPolling,
  }
})
```

- [ ] **Step 4: Commit**

```bash
git add src/renderer/api/kafkaApi.ts src/renderer/stores/connectionStore.ts src/renderer/stores/metadataStore.ts && git commit -m "feat: 添加渲染进程 API 封装与 Pinia Store（连接/元数据）"
```

---

### Task 8: 连接管理页面 UI

**Files:**
- Modify: `src/renderer/App.vue`
- Modify: `src/renderer/views/ConnectionView.vue`
- Create: `src/renderer/components/ConnectionDialog.vue`

- [ ] **Step 1: 重写 src/renderer/App.vue（三栏布局）**

```vue
<template>
  <el-container id="app-root">
    <el-header class="app-header">
      <div class="header-left">
        <span class="app-title">KafkaTeach</span>
      </div>
      <div class="header-right">
        <el-select
          v-model="selectedConnId"
          placeholder="选择连接"
          :disabled="!connectionStore.configs.length"
          @change="onConnSelect"
          style="width: 260px"
        >
          <el-option
            v-for="c in connectionStore.configs"
            :key="c.id"
            :label="c.name"
            :value="c.id"
          />
        </el-select>
        <el-tag
          :type="statusTagType"
          size="small"
          style="margin-left: 8px"
        >
          {{ statusText }}
        </el-tag>
      </div>
    </el-header>
    <el-container>
      <el-aside width="180px" class="app-aside">
        <el-menu
          :default-active="activeMenu"
          router
          @select="onMenuSelect"
        >
          <el-menu-item index="/connection">
            <el-icon><Link /></el-icon>
            <span>连接管理</span>
          </el-menu-item>
          <el-menu-item index="/cluster" :disabled="!connectionStore.isConnected">
            <el-icon><DataBoard /></el-icon>
            <span>集群概览</span>
          </el-menu-item>
          <el-menu-item index="/producer" :disabled="!connectionStore.isConnected">
            <el-icon><Upload /></el-icon>
            <span>生产者</span>
          </el-menu-item>
          <el-menu-item index="/consumer" :disabled="!connectionStore.isConnected">
            <el-icon><Download /></el-icon>
            <span>消费者</span>
          </el-menu-item>
          <el-menu-item index="/visual" :disabled="!connectionStore.isConnected">
            <el-icon><PieChart /></el-icon>
            <span>可视化</span>
          </el-menu-item>
          <el-menu-item index="/scenario" :disabled="!connectionStore.isConnected">
            <el-icon><VideoPlay /></el-icon>
            <span>演示场景</span>
          </el-menu-item>
        </el-menu>
      </el-aside>
      <el-main class="app-main">
        <router-view />
      </el-main>
    </el-container>
  </el-container>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { Link, DataBoard, Upload, Download, PieChart, VideoPlay } from '@element-plus/icons-vue'
import { useConnectionStore } from '@/stores/connectionStore'

const route = useRoute()
const connectionStore = useConnectionStore()

const selectedConnId = ref<string | null>(null)
const activeMenu = ref(route.path)

const statusTagType = computed(() => {
  const map: Record<string, string> = {
    connected: 'success',
    connecting: 'warning',
    error: 'danger',
    disconnected: 'info',
  }
  return map[connectionStore.status] ?? 'info'
})

const statusText = computed(() => {
  const map: Record<string, string> = {
    connected: '已连接',
    connecting: '连接中...',
    error: '连接失败',
    disconnected: '未连接',
  }
  return map[connectionStore.status] ?? '未连接'
})

function onMenuSelect(index: string) {
  activeMenu.value = index
}

async function onConnSelect(id: string) {
  try {
    await connectionStore.connect(id)
  } catch {
    selectedConnId.value = null
  }
}

onMounted(async () => {
  connectionStore.setupStatusListener()
  await connectionStore.loadConfigs()
})
</script>

<style>
html, body, #app {
  margin: 0;
  padding: 0;
  height: 100%;
}
#app-root {
  height: 100vh;
}
.app-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  border-bottom: 1px solid var(--el-border-color-light);
  background: var(--el-bg-color);
}
.header-left .app-title {
  font-size: 18px;
  font-weight: 600;
  color: var(--el-color-primary);
}
.header-right {
  display: flex;
  align-items: center;
}
.app-aside {
  border-right: 1px solid var(--el-border-color-light);
}
.app-main {
  background: var(--el-bg-color-page);
  padding: 16px;
  overflow-y: auto;
}
</style>
```

- [ ] **Step 2: 重写 src/renderer/views/ConnectionView.vue**

```vue
<template>
  <div class="connection-view">
    <div class="view-header">
      <h2>连接管理</h2>
      <el-button type="primary" @click="openDialog()">
        <el-icon><Plus /></el-icon> 新增连接
      </el-button>
    </div>

    <el-table :data="connectionStore.configs" style="width: 100%" v-loading="loading">
      <el-table-column prop="name" label="名称" min-width="150" />
      <el-table-column prop="brokers" label="Bootstrap Servers" min-width="200">
        <template #default="{ row }">
          {{ row.brokers.join(', ') }}
        </template>
      </el-table-column>
      <el-table-column prop="clientId" label="Client ID" min-width="120" />
      <el-table-column label="SASL" width="80">
        <template #default="{ row }">
          {{ row.sasl ? row.sasl.mechanism : '-' }}
        </template>
      </el-table-column>
      <el-table-column label="SSL" width="60">
        <template #default="{ row }">
          {{ row.ssl?.enabled ? '是' : '否' }}
        </template>
      </el-table-column>
      <el-table-column label="操作" width="260" fixed="right">
        <template #default="{ row }">
          <el-button size="small" @click="handleTest(row)">测试</el-button>
          <el-button size="small" type="primary" @click="handleConnect(row)">连接</el-button>
          <el-button size="small" @click="openDialog(row)">编辑</el-button>
          <el-button size="small" type="danger" @click="handleDelete(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-empty v-if="!connectionStore.configs.length" description="暂无连接配置，请新增" />

    <ConnectionDialog
      v-model:visible="dialogVisible"
      :edit-config="editingConfig"
      @saved="onSaved"
    />
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus } from '@element-plus/icons-vue'
import { useConnectionStore } from '@/stores/connectionStore'
import type { ConnectionConfig } from '../../main/kafka/types'
import ConnectionDialog from '@/components/ConnectionDialog.vue'

const connectionStore = useConnectionStore()
const dialogVisible = ref(false)
const editingConfig = ref<ConnectionConfig | null>(null)
const loading = ref(false)

function openDialog(config?: ConnectionConfig) {
  editingConfig.value = config ?? null
  dialogVisible.value = true
}

function onSaved() {
  dialogVisible.value = false
  editingConfig.value = null
}

async function handleTest(config: ConnectionConfig) {
  loading.value = true
  try {
    const result = await connectionStore.testConnection(config)
    if (result.success) {
      ElMessage.success(`连接成功！Broker 数: ${result.brokerCount}, Controller ID: ${result.controllerId}`)
    } else {
      ElMessage.error(`连接失败: ${result.error}`)
    }
  } finally {
    loading.value = false
  }
}

async function handleConnect(config: ConnectionConfig) {
  try {
    await connectionStore.connect(config.id)
    ElMessage.success(`已连接到 ${config.name}`)
  } catch (err: unknown) {
    ElMessage.error(`连接失败: ${err instanceof Error ? err.message : String(err)}`)
  }
}

async function handleDelete(config: ConnectionConfig) {
  try {
    await ElMessageBox.confirm(`确定删除连接 "${config.name}"？`, '确认删除', {
      type: 'warning',
      confirmButtonText: '删除',
      cancelButtonText: '取消',
    })
    await connectionStore.deleteConfig(config.id)
    ElMessage.success('已删除')
  } catch {
    // 取消
  }
}
</script>

<style scoped>
.connection-view {
  max-width: 1000px;
}
.view-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}
.view-header h2 {
  margin: 0;
}
</style>
```

- [ ] **Step 3: 编写 src/renderer/components/ConnectionDialog.vue**

```vue
<template>
  <el-dialog
    :model-value="visible"
    :title="isEdit ? '编辑连接' : '新增连接'"
    width="560px"
    @update:model-value="$emit('update:visible', $event)"
    @close="resetForm"
  >
    <el-form
      ref="formRef"
      :model="form"
      :rules="rules"
      label-width="130px"
    >
      <el-form-item label="名称" prop="name">
        <el-input v-model="form.name" placeholder="如：本地教学环境" />
      </el-form-item>
      <el-form-item label="Bootstrap Servers" prop="brokers">
        <el-input v-model="brokersStr" placeholder="localhost:9092,host2:9092" />
      </el-form-item>
      <el-form-item label="Client ID" prop="clientId">
        <el-input v-model="form.clientId" placeholder="kafka-teach" />
      </el-form-item>
      <el-form-item label="SASL 机制">
        <el-select v-model="form.saslMechanism" placeholder="无" clearable style="width: 100%">
          <el-option label="无" value="" />
          <el-option label="PLAIN" value="plain" />
          <el-option label="SCRAM-SHA-256" value="scram-sha-256" />
          <el-option label="SCRAM-SHA-512" value="scram-sha-512" />
        </el-select>
      </el-form-item>
      <template v-if="form.saslMechanism">
        <el-form-item label="用户名">
          <el-input v-model="form.saslUsername" />
        </el-form-item>
        <el-form-item label="密码">
          <el-input v-model="form.saslPassword" type="password" show-password />
        </el-form-item>
      </template>
      <el-form-item label="SSL">
        <el-switch v-model="form.sslEnabled" />
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button @click="$emit('update:visible', false)">取消</el-button>
      <el-button type="primary" @click="handleSave" :loading="saving">保存</el-button>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch } from 'vue'
import { ElMessage } from 'element-plus'
import type { FormInstance, FormRules } from 'element-plus'
import { useConnectionStore } from '@/stores/connectionStore'
import type { ConnectionConfig } from '../../main/kafka/types'

const props = defineProps<{
  visible: boolean
  editConfig: ConnectionConfig | null
}>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  saved: []
}>()

const connectionStore = useConnectionStore()
const formRef = ref<FormInstance>()
const saving = ref(false)

const isEdit = computed(() => !!props.editConfig)

const form = reactive({
  name: '',
  brokersStr: '',
  clientId: 'kafka-teach',
  saslMechanism: '' as string,
  saslUsername: '',
  saslPassword: '',
  sslEnabled: false,
})

const rules: FormRules = {
  name: [{ required: true, message: '请输入名称', trigger: 'blur' }],
  brokersStr: [{ required: true, message: '请输入 Bootstrap Servers', trigger: 'blur' }],
  clientId: [{ required: true, message: '请输入 Client ID', trigger: 'blur' }],
}

watch(() => props.visible, (val) => {
  if (val) {
    if (props.editConfig) {
      form.name = props.editConfig.name
      form.brokersStr = props.editConfig.brokers.join(',')
      form.clientId = props.editConfig.clientId
      form.saslMechanism = props.editConfig.sasl?.mechanism ?? ''
      form.saslUsername = props.editConfig.sasl?.username ?? ''
      form.saslPassword = props.editConfig.sasl?.password ?? ''
      form.sslEnabled = props.editConfig.ssl?.enabled ?? false
    } else {
      resetForm()
    }
  }
})

function resetForm() {
  form.name = ''
  form.brokersStr = ''
  form.clientId = 'kafka-teach'
  form.saslMechanism = ''
  form.saslUsername = ''
  form.saslPassword = ''
  form.sslEnabled = false
  formRef.value?.resetFields()
}

async function handleSave() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return

  saving.value = true
  try {
    const brokers = form.brokersStr.split(',').map(s => s.trim()).filter(Boolean)
    const config: ConnectionConfig = {
      id: props.editConfig?.id ?? crypto.randomUUID(),
      name: form.name,
      brokers,
      clientId: form.clientId,
      sasl: form.saslMechanism
        ? {
            mechanism: form.saslMechanism as 'plain' | 'scram-sha-256' | 'scram-sha-512',
            username: form.saslUsername,
            password: form.saslPassword,
          }
        : undefined,
      ssl: form.sslEnabled ? { enabled: true } : undefined,
    }
    await connectionStore.saveConfig(config)
    ElMessage.success(isEdit.value ? '已更新' : '已创建')
    emit('saved')
  } catch (err: unknown) {
    ElMessage.error(`保存失败: ${err instanceof Error ? err.message : String(err)}`)
  } finally {
    saving.value = false
  }
}
</script>
```

- [ ] **Step 4: Commit**

```bash
git add src/renderer/App.vue src/renderer/views/ConnectionView.vue src/renderer/components/ConnectionDialog.vue && git commit -m "feat: 实现连接管理页面 UI（三栏布局 + 连接 CRUD + 测试）"
```

---

### Task 9: 集群概览页面 UI

**Files:**
- Modify: `src/renderer/views/ClusterView.vue`

- [ ] **Step 1: 重写 src/renderer/views/ClusterView.vue**

```vue
<template>
  <div class="cluster-view">
    <div class="view-header">
      <h2>集群概览</h2>
      <el-button @click="refresh" :loading="metadataStore.loading">
        <el-icon><Refresh /></el-icon> 刷新
      </el-button>
    </div>

    <el-card v-if="connectionStore.cluster" class="section-card">
      <template #header>Broker 列表</template>
      <el-table :data="connectionStore.cluster.brokers" style="width: 100%" size="small">
        <el-table-column prop="nodeId" label="Node ID" width="100" />
        <el-table-column label="地址" min-width="200">
          <template #default="{ row }">{{ row.host }}:{{ row.port }}</template>
        </el-table-column>
        <el-table-column label="Controller" width="100">
          <template #default="{ row }">
            <el-tag v-if="row.isController" type="success" size="small">Controller</el-tag>
            <span v-else>-</span>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <el-card class="section-card" style="margin-top: 16px">
      <template #header>
        <span>Topic 列表（{{ metadataStore.topics.length }}）</span>
      </template>
      <el-table
        :data="metadataStore.topics"
        style="width: 100%"
        size="small"
        @row-click="onTopicClick"
        highlight-current-row
      >
        <el-table-column prop="name" label="Topic 名称" min-width="200" />
        <el-table-column prop="partitionCount" label="分区数" width="100" />
        <el-table-column prop="replicationFactor" label="副本因子" width="100" />
        <el-table-column prop="totalMessages" label="消息总量" width="120" />
      </el-table>
    </el-card>

    <el-card v-if="selectedTopic" class="section-card" style="margin-top: 16px">
      <template #header>
        <span>分区详情：{{ selectedTopic }}</span>
      </template>
      <el-table :data="topicPartitions" style="width: 100%" size="small" v-loading="detailLoading">
        <el-table-column prop="partition" label="Partition" width="100" />
        <el-table-column prop="leader" label="Leader" width="80" />
        <el-table-column label="Replicas" min-width="150">
          <template #default="{ row }">[{{ row.replicas.join(', ') }}]</template>
        </el-table-column>
        <el-table-column label="ISR" min-width="150">
          <template #default="{ row }">[{{ row.isr.join(', ') }}]</template>
        </el-table-column>
        <el-table-column prop="earliestOffset" label="Earliest Offset" width="140" />
        <el-table-column prop="latestOffset" label="Latest Offset" width="140" />
      </el-table>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { Refresh } from '@element-plus/icons-vue'
import { useConnectionStore } from '@/stores/connectionStore'
import { useMetadataStore } from '@/stores/metadataStore'
import type { PartitionDetail } from '../../main/kafka/types'

const connectionStore = useConnectionStore()
const metadataStore = useMetadataStore()

const selectedTopic = ref<string | null>(null)
const topicPartitions = ref<PartitionDetail[]>([])
const detailLoading = ref(false)

async function refresh() {
  await metadataStore.refreshTopics()
}

async function onTopicClick(row: { name: string }) {
  selectedTopic.value = row.name
  detailLoading.value = true
  try {
    const detail = await metadataStore.fetchTopicDetail(row.name)
    topicPartitions.value = detail.partitions
  } finally {
    detailLoading.value = false
  }
}

onMounted(() => {
  metadataStore.startPolling()
})

onUnmounted(() => {
  metadataStore.stopPolling()
})
</script>

<style scoped>
.cluster-view {
  max-width: 1000px;
}
.view-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}
.view-header h2 {
  margin: 0;
}
.section-card {
  margin-bottom: 0;
}
</style>
```

- [ ] **Step 2: Commit**

```bash
git add src/renderer/views/ClusterView.vue && git commit -m "feat: 实现集群概览页（Broker 列表 + Topic 列表 + 分区详情）"
```

---

### Task 10: 占位页面与路由完善

**Files:**
- Create: `src/renderer/views/ProducerView.vue`
- Create: `src/renderer/views/ConsumerView.vue`
- Create: `src/renderer/views/VisualView.vue`
- Create: `src/renderer/views/ScenarioView.vue`
- Modify: `src/renderer/router/index.ts`

- [ ] **Step 1: 创建占位页面**

`src/renderer/views/ProducerView.vue`:
```vue
<template>
  <div class="producer-view">
    <h2>生产者</h2>
    <p>功能开发中...</p>
  </div>
</template>
```

`src/renderer/views/ConsumerView.vue`:
```vue
<template>
  <div class="consumer-view">
    <h2>消费者</h2>
    <p>功能开发中...</p>
  </div>
</template>
```

`src/renderer/views/VisualView.vue`:
```vue
<template>
  <div class="visual-view">
    <h2>可视化</h2>
    <p>功能开发中...</p>
  </div>
</template>
```

`src/renderer/views/ScenarioView.vue`:
```vue
<template>
  <div class="scenario-view">
    <h2>演示场景</h2>
    <p>功能开发中...</p>
  </div>
</template>
```

- [ ] **Step 2: 更新路由配置**

修改 `src/renderer/router/index.ts`:
```ts
import { createRouter, createWebHashHistory } from 'vue-router'

const router = createRouter({
  history: createWebHashHistory(),
  routes: [
    { path: '/', redirect: '/connection' },
    { path: '/connection', name: 'connection', component: () => import('@/views/ConnectionView.vue') },
    { path: '/cluster', name: 'cluster', component: () => import('@/views/ClusterView.vue') },
    { path: '/producer', name: 'producer', component: () => import('@/views/ProducerView.vue') },
    { path: '/consumer', name: 'consumer', component: () => import('@/views/ConsumerView.vue') },
    { path: '/visual', name: 'visual', component: () => import('@/views/VisualView.vue') },
    { path: '/scenario', name: 'scenario', component: () => import('@/views/ScenarioView.vue') },
  ],
})

export default router
```

- [ ] **Step 3: Commit**

```bash
git add src/renderer/views/ProducerView.vue src/renderer/views/ConsumerView.vue src/renderer/views/VisualView.vue src/renderer/views/ScenarioView.vue src/renderer/router/index.ts && git commit -m "feat: 添加占位页面与完整路由配置"
```

---

### Task 11: 类型检查与验证

- [ ] **Step 1: 运行类型检查**

```bash
npm run typecheck
```

预期：无类型错误。

- [ ] **Step 2: 运行开发模式验证**

```bash
npm run dev
```

预期：Electron 窗口打开，显示三栏布局，可新增/编辑/删除连接，可测试连接（需本地 Kafka 运行）。

- [ ] **Step 3: Commit（如有修改）**

```bash
git add -A && git commit -m "fix: 类型检查与验证修复"
```

---

## M1 完成检查清单

- [ ] 项目可 `npm run dev` 启动
- [ ] 安全基线：nodeIntegration: false, contextIsolation: true, sandbox: true
- [ ] 连接 CRUD 功能正常，密码 safeStorage 加密持久化
- [ ] 连接测试功能正常（3s 超时）
- [ ] 连接/断开状态指示正确
- [ ] 集群概览页显示 Broker 列表与 Topic 列表
- [ ] Topic 分区详情可展开查看
- [ ] 30s 轮询刷新，离开页面停止
- [ ] `npm run typecheck` 无错误