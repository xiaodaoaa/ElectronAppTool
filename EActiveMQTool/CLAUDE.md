# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概述

EActiveMQTool 是 ElectronAppTool monorepo 中的一个独立子项目，基于 Electron 的 ActiveMQ 调试桌面工具。使用 STOMP 协议（@stomp/stompjs）连接 ActiveMQ Broker，提供消息发送（生产者）和消息消费（消费者）功能。

## 命令

在 EActiveMQTool 目录下执行：

```bash
npm install          # 安装依赖
npm run dev          # 开发模式（electron-vite dev，三进程 HMR）
npm run build        # 构建（electron-vite build → out/）
npm run typecheck    # 两阶段类型检查：tsc(node) + vue-tsc(web)
npm run pack         # 构建 + 打包 Windows NSIS 安装包 → release/
```

注意：Electron 二进制可能未下载（`node_modules/electron/dist/electron.exe` 缺失），运行 `node node_modules/electron/install.js` 手动安装。

## 架构

采用 **electron-vite 三进程架构**（与 ERabbitMQToolPlus 相同）：

```
src/main/index.ts          # 主进程入口，创建 BrowserWindow，注册 IPC
src/main/services/         # 单例服务：ConnectionManager, ProducerService, ConsumerService
src/main/ipc/              # IPC 注册层，只做参数转发，不含业务逻辑
src/main/utils/            # logger（日志推送到渲染进程）、store（electron-store 持久化）、tcp-socket（STOMP over TCP 适配器）
src/preload/index.ts       # contextBridge.exposeInMainWorld('api', api)
src/shared/types.ts        # 三进程共享的类型定义
src/renderer/src/          # Vue 3 + Element Plus + Pinia
  ├── main.ts              # 入口，创建 app + pinia + ElementPlus，bindIpc
  ├── App.vue              # 根组件：header(状态) + ConnectionForm + tabs(生产者/消费者) + footer + LogPanel + SettingsView
  ├── stores/              # Pinia stores：connection, producer, consumer, log, settings
  ├── views/               # ProducerView, ConsumerView, SettingsView
  └── components/          # ConnectionForm, LogPanel, MessageTable, MessageDetail
```

**构建输出**：`out/main/index.js`, `out/preload/index.mjs`, `out/renderer/index.html`

**路径别名**：`@shared/*` → `src/shared/*`（三进程通用），`@renderer/*` → `src/renderer/src/*`（仅渲染进程）

## 核心设计决策

### 连接方式：三种传输模式

ConnectionManager 支持三种连接方式，通过 `ConnectionConfig.useTCP` 和 `sslEnabled` 控制：

| 模式 | 条件 | URL/适配器 | 默认端口 |
|------|------|-----------|---------|
| WebSocket | `useTCP=false, sslEnabled=false` | `ws://host:port/ws` | 61614 |
| WebSocket Secure | `useTCP=false, sslEnabled=true` | `wss://host:port/ws` | 61617 |
| TCP | `useTCP=true` | `createTCPSocket()` via `net.Socket` | 61613 |

`src/main/utils/tcp-socket.ts` 将 Node.js `net.Socket` 适配为 `@stomp/stompjs` 的 `IStompSocket` 接口，从而实现 STOMP over TCP（ActiveMQ 的 OpenWire 端口 61613 也支持 STOMP）。TCP 模式下 SSL 开关被强制关闭。

ConnectionForm 中有端口自动切换逻辑：切换 TCP 时端口在 61614/61617 ↔ 61613 之间切换；切换 SSL 时端口在 61614 ↔ 61617 之间切换。

### 单例服务 + 观察者模式

三个服务都是单例，通过 `Set<Listener>` 管理观察者：

- **ConnectionManager**：状态机 `disconnected → connecting → connected → error`，状态变更时通知 `onStatusChange` 监听器。连接成功自动保存配置到 electron-store。
- **ProducerService**：批量发送通过循环 `client.publish()` + `setTimeout` 间隔实现，通过 `onProgress` 推送进度。保留最近 10 条发送历史。
- **ConsumerService**：批量缓冲（100ms 定时器 + 200 条上限）批量推送消息到渲染进程。支持 AMQP 风格的路由键通配符匹配（`*` 匹配一个词，`#` 匹配零个或多个词）。暂停/恢复通过 unsubscribe/re-subscribe 实现。

### 主进程入口的清理逻辑

`src/main/index.ts` 中监听 `connectionManager.onStatusChange`：当状态变为 `disconnected` 或 `error` 时，自动调用 `producerService.clearHistoryOnDisconnect()` 和 `consumerService.clearOnDisconnect()` 清理状态。

### 渲染进程消息批处理

ConsumerService 主进程侧 100ms 批量推送，渲染进程 `consumerStore` 侧使用 `requestAnimationFrame` 二次批处理：收到的消息先放入 `pendingBatch`，在下一个 rAF 中一次性合并到 `messages`（shallowRef），避免高频响应式更新。

### 密码加密

`src/main/utils/store.ts` 使用 AES-256-CBC 加密密码字段。加密密钥由应用名称哈希派生。只有 `lastConnection.password` 加密存储，其他配置（lastProducer、lastConsumer）明文存储。

### sandbox: false

preload 需要 `import type` 引用 `src/shared/types.ts`，这在沙箱模式下不可用，因此 `sandbox: false`。

### IPC 通道命名规范

Request/Response：`domain:action`（如 `connection:connect`、`producer:send`）
Event push：`domain:eventName`（如 `connection:statusChanged`、`consumer:message`）
配置加载：`config:saveXxx` / `config:loadXxx` / `config:xxxLoaded`

### 窗口加载完成后恢复配置

`mainWindow.webContents.on('did-finish-load')` 中通过 `webContents.send` 推送上次保存的 connection/producer/consumer 配置到渲染进程。渲染进程 store 的 `bindIpc()` 中监听这些事件并更新响应式状态。

### 类型检查

`npm run typecheck` 分两步：`tsc` 检查 node 端（main/preload/shared），`vue-tsc` 检查 web 端（renderer/shared）。编辑后必须运行 typecheck 确保无类型错误。

## STOMP vs AMQP 关键差异

EActiveMQTool 使用 STOMP 协议，与 ERabbitMQTool（AMQP 0-9-1）的核心差异：

- **无 Channel 概念**：直接 `client.subscribe()` / `client.publish()`，不涉及 channel 管理
- **目标地址**：destination 字符串如 `/queue/xxx` 或 `/topic/xxx`，而非 exchange + routingKey
- **消息确认**：`client.ack({id})` / `client.nack({id})`，需传入 subscription ID；ack mode 通过 subscribe headers 的 `ack` 参数控制（auto/client/client-individual）
- **预取控制**：`client-individual` 模式下通过 `activemq.prefetchSize` header 控制
- **消息选择器**：支持 JMS selector（`selector` header），这是 AMQP 0-9-1 不原生支持的
- **持久化**：通过 `persistent: true` header 而非 `deliveryMode: 2`