# EActiveMQTool 设计文档

## 概述

EActiveMQTool 是一个基于 Electron 的 ActiveMQ 调试桌面工具，参照 ERabbitMQToolPlus 架构，使用 STOMP 协议连接 ActiveMQ Broker，提供消息发送（生产者）和消息消费（消费者）功能。

## 技术选型

| 层面 | 技术 |
|------|------|
| 桌面框架 | Electron 43 |
| 前端框架 | Vue 3 (Composition API) |
| UI 组件库 | Element Plus |
| 状态管理 | Pinia |
| 构建工具 | electron-vite (三进程构建) |
| 语言 | TypeScript |
| STOMP 客户端 | @stomp/stompjs |
| 持久化 | electron-store (AES-256-CBC 加密密码) |
| 打包 | electron-builder (NSIS) |

## 架构

采用 electron-vite 三进程架构（Approach C），与 ERabbitMQToolPlus 完全一致：

```
Main process (src/main/)
  ├── BrowserWindow (sandbox: false)
  ├── Services: ConnectionManager, ProducerService, ConsumerService (单例)
  ├── IPC: 只做参数转发，不包含业务逻辑
  └── Utils: store (持久化), logger (日志推送)

Preload (src/preload/)
  └── contextBridge.exposeInMainWorld('api', api)

Renderer (src/renderer/)
  ├── Vue 3 + Element Plus + Pinia
  ├── Views: ProducerView, ConsumerView, SettingsView
  ├── Components: ConnectionForm, LogPanel, MessageTable, MessageDetail
  └── Stores: connection, producer, consumer, log, settings
```

## 目录结构

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

## 核心差异：STOMP vs AMQP

| 维度 | RabbitMQ (AMQP 0-9-1) | ActiveMQ (STOMP) |
|------|----------------------|-------------------|
| 客户端库 | amqplib | @stomp/stompjs |
| 连接方式 | amqp.connect(url) | StompJs.Client (WebSocket) |
| 通道模型 | Channel 是核心概念 | 无 Channel，直接 subscribe/publish |
| 目标地址 | exchange + routingKey | destination（/queue/xxx 或 /topic/xxx） |
| 消息确认 | channel.ack(msg) | message.ack() / client.ack({id}) |
| 预取控制 | channel.prefetch(n) | ack: 'client-individual' + prefetch-count header |
| 持久化 | deliveryMode: 2 | persistent: true header |
| 消息选择器 | 不支持原生 selector | 支持 JMS selector（selector header） |
| SSL | amqps:// | wss:// |

## 服务层设计

### ConnectionManager

使用 @stomp/stompjs 的 Client 类管理 STOMP 连接。

**状态机**：`disconnected → connecting → connected → error`

**连接配置**：
- host, port（默认 61614，ActiveMQ WebSocket 端口）
- username, password
- SSL 开关（wss://）
- heartbeat（默认 outgoing: 10000, incoming: 10000）
- reconnectDelay（断线重连延迟，默认 5000ms）

**方法**：
- `connect(config)` — 创建 Client，连接，成功后保存配置
- `test(config)` — 创建临时连接测试连通性，测试后断开
- `disconnect()` — 断开连接，清理状态
- `getClient()` — 获取当前 STOMP Client 实例
- `isConnected()` — 是否已连接

**事件**：
- 监听 Client 的 `onConnect`、`onDisconnect`、`onStompError`、`onWebSocketClose`
- 状态变更时通知所有监听器（`onStatusChange`）

### ProducerService

通过 STOMP Client 发送消息。

**发送参数**：
- destination（/queue/xxx 或 /topic/xxx）
- body（消息体，支持 JSON/文本/XML）
- headers（content-type、persistent、priority、expires、correlation-id、自定义 headers）
- 批量发送配置（count、intervalMs）

**方法**：
- `send(params)` — 单条发送
- `getHistory()` — 获取最近 10 条发送记录
- `clearHistoryOnDisconnect()` — 断开时清空历史

**批量发送**：循环调用 `client.publish()`，支持间隔，通过 `onProgress` 推送进度。

### ConsumerService

通过 STOMP Client 订阅消息。

**消费配置**：
- destination（/queue/xxx 或 /topic/xxx）
- ack 模式（auto / client / client-individual）
- selector（JMS 选择器，可选）
- prefetch-count（仅 client-individual 模式有效）
- maxReceive（最大接收数，达到后自动停止）
- 消息过滤（routingKey 通配符 + header 键值匹配）

**方法**：
- `start(config)` — 开始订阅
- `pause()` — 暂停接收（暂存消息但不推送）
- `resume()` — 恢复接收
- `stop()` — 取消订阅
- `ack(messageId)` — 确认消息
- `nack(messageId)` — 拒绝消息
- `clearOnDisconnect()` — 断开时清理

**批量推送**：100ms 定时器 + 200 条上限，批量推送消息到渲染进程。

## IPC 通道

### 请求/响应 (invoke ↔ handle)

| 域 | 通道 | 说明 |
|----|------|------|
| connection | `connection:connect` | 连接 ActiveMQ |
| connection | `connection:test` | 测试连接 |
| connection | `connection:disconnect` | 断开连接 |
| producer | `producer:send` | 发送消息 |
| producer | `producer:getHistory` | 获取发送历史 |
| consumer | `consumer:start` | 开始消费 |
| consumer | `consumer:pause` | 暂停消费 |
| consumer | `consumer:resume` | 恢复消费 |
| consumer | `consumer:stop` | 停止消费 |
| consumer | `consumer:ack` | 确认消息 |
| consumer | `consumer:nack` | 拒绝消息 |
| config | `config:saveProducer` | 保存生产者配置 |
| config | `config:saveConsumer` | 保存消费者配置 |
| config | `config:loadProducer` | 加载生产者配置 |
| config | `config:loadConsumer` | 加载消费者配置 |
| dialog | `dialog:selectFile` | 选择文件 |

### 事件推送 (main → renderer)

| 通道 | 说明 |
|------|------|
| `connection:statusChanged` | 连接状态变更 |
| `connection:lastConfigLoaded` | 窗口加载完成后推送上次配置 |
| `producer:progress` | 批量发送进度 |
| `consumer:message` | 收到消息 |
| `consumer:stopped` | 消费停止 |
| `log:entry` | 日志条目 |

## 共享类型

```typescript
// 连接配置
interface ConnectionConfig {
  host: string
  port: number          // 默认 61614
  username: string
  password: string
  sslEnabled: boolean
  heartbeatOutgoing: number  // 默认 10000
  heartbeatIncoming: number  // 默认 10000
}

// 连接状态
type ConnectionStatus = 'disconnected' | 'connecting' | 'connected' | 'error'

// 发送参数
interface SendParams {
  destination: string    // /queue/xxx 或 /topic/xxx
  body: string
  contentType: string    // application/json, text/plain, application/xml
  headers: Record<string, string>
  persistent: boolean
  priority?: number
  expires?: number
  batch?: BatchConfig
}

// 批量配置
interface BatchConfig {
  count: number
  intervalMs: number
}

// 消费配置
interface ConsumerConfig {
  destination: string
  ackMode: 'auto' | 'client' | 'client-individual'
  selector?: string       // JMS selector
  prefetchCount?: number  // client-individual 模式
  maxReceive: number
  filterRoutingKey?: string
  filterHeaders?: Record<string, string>
}

// 消费消息
interface ConsumedMessage {
  seq: number
  receivedAt: string
  destination: string
  messageId: string
  body: string
  headers: Record<string, string>
  acked: boolean
}

// 日志
interface LogEntry {
  time: string
  level: 'info' | 'warn' | 'error'
  message: string
}
```

## UI 设计

与 ERabbitMQToolPlus 完全一致：

### 布局
- `el-container` → header + main + footer
- header：标题 + 连接状态标签 + 日志/设置按钮
- main：ConnectionForm + el-tabs（生产者/消费者）
- footer：状态栏 + 发送/接收计数

### 连接表单
- host、port、username、password
- SSL 开关（ws:// ↔ wss://）
- 端口自动切换：SSL 开启时 61614→61617，关闭时 61617→61614
- 连接/断开/测试按钮

### 生产者页
- destination 输入（/queue/xxx 或 /topic/xxx）
- 消息格式选择（JSON/文本/XML）
- 消息体 textarea
- headers 键值对编辑
- persistent 开关
- 批量发送配置
- 发送历史折叠面板

### 消费者页
- destination 输入
- ack 模式选择（auto/client/client-individual）
- selector 输入（可选）
- prefetch-count（client-individual 模式）
- 最大接收数
- 消息过滤配置
- 开始/暂停/恢复/停止按钮
- 消息列表（ElTableV2 虚拟表格）
- 消息详情弹窗

### 日志面板
- 底部固定，200px 高
- 自动滚动到底部
- 按级别着色

### 设置弹窗
- 主题切换（浅色/深色）
- 最大消息缓存数
- 消息体最大显示长度

## 持久化

使用 electron-store，AES-256-CBC 加密密码字段。

**存储结构**：
```typescript
interface StoreSchema {
  lastConnection?: ConnectionConfig  // 密码加密
  lastProducer?: SendParams          // 明文
  lastConsumer?: ConsumerConfig      // 明文
  settings?: {
    theme: 'light' | 'dark'
    maxMessageCache: number
    maxDisplayLength: number
  }
}
```

## 构建配置

### package.json scripts
```json
{
  "dev": "electron-vite dev",
  "build": "electron-vite build",
  "typecheck:node": "tsc --noEmit -p tsconfig.node.json --composite false",
  "typecheck:web": "vue-tsc --noEmit -p tsconfig.web.json --composite false",
  "typecheck": "npm run typecheck:node && npm run typecheck:web",
  "pack": "electron-vite build && electron-builder --win"
}
```

### electron-builder
- appId: com.eactivemq.tool
- productName: EActiveMQTool
- 输出目录: release/
- 打包源: out/**/*
- Windows: NSIS 安装包，允许选择安装目录

## 依赖

### dependencies
- @stomp/stompjs — STOMP 客户端
- dayjs — 时间格式化
- electron-store — 持久化
- element-plus — UI 组件库
- pinia — 状态管理
- uuid — 消息 ID 生成

### devDependencies
- @electron-toolkit/tsconfig
- @electron-toolkit/utils
- @types/node
- @types/uuid
- @vitejs/plugin-vue
- electron
- electron-builder
- electron-vite
- typescript
- vite
- vue
- vue-tsc