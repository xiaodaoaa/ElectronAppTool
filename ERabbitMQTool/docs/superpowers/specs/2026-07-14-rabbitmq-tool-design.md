# RabbitMQ 调试工具 — 设计文档

**日期：** 2026-07-14
**状态：** 已确认

---

## 1. 概述与技术栈

ERabbitMQTool 是一个 RabbitMQ 调试工具，包含**生产者**和**消费者**两个 Tab，支持连接 RabbitMQ broker、发送消息（到 exchange 或 queue）、订阅 queue 接收消息（自动 ack）。兼容 Windows 7 x64。

**技术栈**（完全对齐 EWebsocketTool）：

| 层面 | 选型 |
|------|------|
| 桌面框架 | Electron 22.3.27（Win7 兼容最后版本，Electron 23+ 已放弃 Win7） |
| 前端框架 | React 18 + TypeScript |
| 组件库 | Ant Design 5 |
| 构建工具 | Vite 6（`base: './'`，`build.target: 'chrome108'`） |
| RabbitMQ 客户端 | amqplib（纯 JS，运行在主进程） |
| 构建方式 | 方式 A — 独立 `electron/` 目录，Vite 只构建渲染进程 |

**Win7 兼容三要素**：
1. Electron 锁定 `^22.3.27`
2. `pack` 脚本带 `-c.electronDist="node_modules/electron/dist" -c.electronVersion="22.3.27"`
3. `build.target: 'chrome108'`（Electron 22 内核 Chromium 108）

**amqplib 兼容性说明**：amqplib 是纯 JS 实现（核心依赖 `bitsyntax`、`buffer-more-ints`、`crypto` 均为纯 JS，不依赖 node-gyp 原生编译），在 Electron 22 的 Node 16.17 环境下可正常运行。Win7 兼容性只取决于 Electron 22 本身，amqplib 不引入额外原生模块负担。

---

## 2. 架构

三进程模型，与仓库其他三个网络工具一致：

```
ERabbitMQTool/
├── Main Process（electron/main.js，CommonJS）
│   ├── 创建 BrowserWindow（1200×800）
│   ├── 持有所有 RabbitMQ 逻辑（amqplib 连接/生产/消费）— 渲染进程不直接接触 amqplib
│   ├── 注册 ipcMain.handle() 处理请求/响应
│   ├── 通过 sendToRenderer() 推送异步事件（带空值守卫）
│   └── 模块级状态：mainWindow、connection、channel、consumerTags、isConnecting 等
│
├── Preload（electron/preload.js，CommonJS）
│   ├── contextBridge.exposeInMainWorld('electronAPI', …)
│   ├── contextIsolation: true, nodeIntegration: false
│   └── 每个 on 事件监听器返回 unsubscribe 函数
│
└── Renderer（src/，React TSX）
    └── 仅通过 window.electronAPI 通信，绝不 import Node/Electron 模块
```

### 2.1 单连接模型

主进程同时只维护一个 amqplib `Connection` + 一个 `Channel`。生产者发送和消费者订阅共享同一个 channel（amqplib 推荐一个 channel 单线程使用，避免多 channel 并发问题）。连接/断开、生产者发送、消费者订阅全部围绕这一个 channel。

### 2.2 配置持久化

连接信息 + 生产者表单 + 消费者订阅 queue 名存到 `app.getPath('userData')/config.json`，自动保存（300ms 防抖，仿 EWebsocketMan）。密码明文存储。

---

## 3. IPC 通道设计

### 3.1 请求/响应（Renderer → Main，`ipcRenderer.invoke` ↔ `ipcMain.handle`）

| 通道 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `connect` | `{ host, port, vhost, username, password }` | `{ success, serverInfo? }` | 建立连接 + 创建 channel |
| `disconnect` | — | `{ success }` | 关闭连接，清理消费者订阅 |
| `publish` | `{ target: 'exchange'\|'queue', exchange?, routingKey?, queue?, message, properties }` | `{ success }` | 发送消息；queue 模式走默认 exchange（`''`），routingKey=queue 名 |
| `subscribe` | `{ queue, noAck: true }` | `{ success, consumerTag? }` | 订阅 queue，自动 ack |
| `unsubscribe` | `{ consumerTag }` | `{ success }` | 取消订阅 |
| `save-config` | `config` | `{ success }` | 存 config.json |
| `load-config` | — | `{ success, config? }` | 读 config.json |

### 3.2 事件推送（Main → Renderer，`webContents.send`）

| 事件 | 载荷 | 说明 |
|------|------|------|
| `connected` | `{ serverInfo }` | 连接成功（含 RabbitMQ 版本/平台信息） |
| `disconnected` | `{ reason }` | 连接断开（主动或异常） |
| `connection-error` | `{ message }` | 连接/通道错误 |
| `message-received` | `{ queue, message, properties, consumerTag }` | 消费者收到消息（自动 ack 已完成） |
| `publish-confirmed` | `{ success, message? }` | 发送结果回执 |

### 3.3 Preload 暴露的 `window.electronAPI`

```js
{
  // invoke
  connect, disconnect, publish, subscribe, unsubscribe, saveConfig, loadConfig,
  // on（每个返回 unsubscribe）
  onConnected, onDisconnected, onConnectionError,
  onMessageReceived, onPublishConfirmed,
  // 批量清理
  removeAllListeners(channel)
}
```

**关键约束**：每个 `on*` 返回 unsubscribe 函数，React 组件在 `useEffect` return 时调用，防止 Tab 切换导致监听器累积。

---

## 4. 功能规格

### 4.1 连接区（顶部，跨 Tab 共享）

**连接配置**：
- Host、Port（默认 5672）、VHost（默认 `/`）、用户名（默认 `guest`）、密码（默认 `guest`，密码框）
- 连接 / 断开按钮
- 连接状态指示：未连接=灰、连接中=加载、已连接=绿

**持久化**：连接信息存 config.json，启动自动填充。

### 4.2 生产者 Tab

**发送目标选择**：单选 `exchange` 或 `queue`
- exchange 模式：exchange 名 + routingKey
- queue 模式：queue 名（内部走默认 exchange `''`，routingKey=queue 名）

**消息属性**（常用属性集）：
- `persistent`（持久化开关，默认开）
- `content-type`（默认 `text/plain`）
- `priority`（0-9，默认 0）
- `message-id`（可选文本）
- `reply-to`（可选文本）
- `headers`（key-value 键值对，可增删行）

**消息体**：多行文本输入框 + 发送按钮（Enter 发送，Shift+Enter 换行）

**持久化**：目标选择、exchange/queue/routingKey、属性全部存 config.json。

### 4.3 消费者 Tab

**订阅配置**：
- queue 名输入框 + 订阅 / 取消订阅按钮
- 订阅状态指示

**消息列表**：
- 每条消息：`[HH:mm:ss] [queue] 消息内容`，下方折叠显示 properties
- 自动 ack（消息到达即确认，无需手动操作）
- 自动滚动到最新

### 4.4 消息日志

每条日志格式：

```
[14:30:05] [连接] 已连接到 amqp://guest@127.0.0.1:5672/
[14:30:10] [发送→exchange amq.test] Hello
[14:30:12] [接收←queue test.queue] Hello
[14:30:20] [断开] 连接已关闭
[14:30:25] [错误] 连接失败: ECONNREFUSED
```

### 4.5 日志格式

| 事件 | 日志 |
|------|------|
| 连接成功 | `[连接] 已连接到 amqp://...` |
| 连接失败 | `[错误] 连接失败: <原因>` |
| 发送成功 | `[发送→<exchange/queue> <名>] <消息摘要>` |
| 接收消息 | `[接收←<queue> <queue>] <消息摘要>` |
| 订阅/取消 | `[订阅] queue=<名> consumerTag=<tag>` |
| 断开 | `[断开] <原因>` |

---

## 5. UI 布局与项目结构

### 5.1 UI 布局

```
┌──────────────────────────────────────────────────────┐
│  [生产者] [消费者]              ● 已连接 127.0.0.1:5672│  ← Tab + 连接状态
├──────────────────────────────────────────────────────┤
│  Host [127.0.0.1] Port [5672] VHost [/]              │  ← 连接区（跨Tab共享）
│  用户 [guest] 密码 [····]  [连接] [断开]              │
├──────────────────────────────────────────────────────┤
│  ┌──────────────────┐  ┌─────────────────────────┐  │
│  │ 发送目标          │  │                         │  │
│  │ ○ exchange ○ queue│  │   消息日志               │  │
│  │ exchange [____]   │  │                         │  │
│  │ routingKey [____] │  │   [14:30:05] [连接]...   │  │
│  │ 属性:             │  │   [14:30:10] [发送]...   │  │
│  │  persistent ☑     │  │   [14:30:12] [接收]...   │  │
│  │  content-type ... │  │                         │  │
│  │  headers + -      │  │                         │  │
│  │ ─────────────     │  │                         │  │
│  │ 消息体 [______]    │  │                         │  │
│  │      [发送]        │  │                         │  │
│  └──────────────────┘  └─────────────────────────┘  │
└──────────────────────────────────────────────────────┘
```

- 顶部连接区跨 Tab 共享（`App.tsx` 顶层），Tab 切换不丢失连接
- 左侧配置/发送区固定宽度约 380px，右侧日志区自适应
- 消费者 Tab 左侧换成订阅配置（queue 名 + 订阅按钮），右侧为接收消息列表

### 5.2 项目结构

```
ERabbitMQTool/
├── package.json              # main: electron/main.js，build 内联，pack 带 electronDist 覆盖
├── vite.config.ts            # base: './', build.target: 'chrome108'
├── tsconfig.json             # noEmit, include: ["src"]
├── index.html
├── electron/
│   ├── main.js               # 主进程：窗口 + IPC + amqplib 连接/生产/消费（内联，方案A）
│   └── preload.js            # contextBridge 暴露 electronAPI
├── src/
│   ├── main.tsx              # React 入口
│   ├── App.tsx               # 顶层布局：连接区 + Tab 切换
│   ├── App.css               # 全局样式
│   ├── components/
│   │   ├── ConnectionPanel.tsx   # 连接配置（跨Tab共享）
│   │   ├── ProducerTab.tsx       # 生产者 Tab
│   │   ├── ConsumerTab.tsx       # 消费者 Tab
│   │   └── LogPanel.tsx          # 共用日志组件
│   ├── hooks/
│   │   └── useRabbitMQ.ts        # 封装 electronAPI 调用 + 事件订阅 + unsubscribe
│   └── types/
│       └── index.ts             # 公共类型（连接配置、消息、属性等）
└── docs/
    └── superpowers/
        └── specs/
            └── 2026-07-14-rabbitmq-tool-design.md
```

---

## 6. 边界情况与错误处理

| 场景 | 处理方式 |
|------|----------|
| 连接失败（broker 未启动/认证失败） | `connection-error` 事件 + 日志 `[错误]`，状态回退未连接 |
| 连接中重复点连接 | `isConnecting` 标志位防双连竞态（仿 EWebsocketMan） |
| 连接异常断开 | amqplib `connection.close`/`error` 事件 → `disconnected`，清理 channel + consumerTags |
| 未连接时发送/订阅 | 按钮禁用 + 日志提示 |
| 发送到不存在的 exchange | amqplib 触发 `channel error`，连接会被强制关闭 → `connection-error` + 日志 |
| 订阅不存在的 queue | amqplib 报 `NO_ROUTE`/`NOT_FOUND` → `subscribe` 返回失败 + 日志 |
| 消息体为空 | 不发送，忽略 |
| 断开时未取消订阅 | `disconnect` 主动取消所有 consumerTag 再关连接 |
| 窗口关闭 | `window-all-closed` 清理连接后 `app.quit()` |
| config.json 不存在/损坏 | `load-config` 返回 `{ success: false, config: null }`，UI 用默认值 |
| 端口被占用 | 连接时 amqplib 报 ECONNREFUSED → `[错误] 连接失败` |
| 切换 Tab | useEffect 清理监听器（unsubscribe），状态保留在 hook 顶层不丢失 |

**安全守卫**：`sendToRenderer()` 调用前检查 `mainWindow && !mainWindow.isDestroyed()`，与仓库其他工具一致。

**IPv4 规范化**：RabbitMQ 连接是主动出站，无 `remoteAddress` 规范化需求（与 WS 服务端不同，不涉及）。

### 不包含的内容（YAGNI）

- 多连接管理
- 手动 ack / nack / reject
- exchange/queue 管理面板（创建/删除/绑定）
- 消息历史导出
- TLS/SSL 连接
- 多窗口 / 多 Tab 实例
- 国际化
- 自动更新
