# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概述

EKafkaTool 是 ElectronAppTool monorepo 中的一个独立子项目，基于 Electron 的 Kafka 教学演示桌面工具。使用 `kafkajs` 连接 Kafka Broker，提供连接管理、Topic 管理、生产者发布、消费者订阅，以及 6 个引导式教学演示场景（第一条消息、分区与 Key、消费组负载均衡、再均衡、消息重放、顺序性与 acks）。

## 命令

在 EKafkaTool 目录下执行：

```bash
npm install          # 安装依赖
npm run dev          # 开发模式（electron-vite dev，三进程 HMR）
npm run build        # 构建（electron-vite build → out/）
npm run typecheck    # 类型检查：vue-tsc --noEmit（单段，仅 web 侧）
npm run lint         # eslint（.ts/.vue）
npm run preview      # 预览 out/ 构建产物
npm run package:win  # 构建 + 打包 Windows NSIS 安装包 → release/
```

注意：Electron 二进制可能未下载（`node_modules/electron/dist/electron.exe` 缺失），运行 `node node_modules/electron/install.js` 手动安装。

## 本地 Kafka

`docker/docker-compose.yml` 启动单节点 Kafka 3.9.0（KRaft 模式，端口 9092）：

```bash
docker compose -f docker/docker-compose.yml up -d
```

连接配置 brokers 填 `localhost:9092`。

## 架构

采用 **electron-vite 三进程架构**（方式 C，与 ERabbitMQToolPlus/EActiveMQTool 相同）：

```
src/main/index.ts              # 主进程入口，创建 BrowserWindow，注册 IPC
src/main/kafka/                # 业务逻辑：KafkaClientManager 单例 + ProducerService/ConsumerService/DemoScenarioService
src/main/ipc/                  # IPC 层：channels.ts（通道名集中定义）+ registerHandlers.ts（统一注册）
src/main/store/                # connectionStore.ts（JSON 文件 + safeStorage 加密）
src/main/util/                 # logger（electron-log）+ messageBuffer（消费者消息批量缓冲）
src/main/data/                 # scenarios.ts（6 个教学场景定义）
src/preload/index.ts           # contextBridge.exposeInMainWorld('kafkaApi', api)
src/main/kafka/types.ts        # 共享类型（preload/renderer 相对路径引用，无 src/shared/）
src/renderer/                  # Vue 3 + Element Plus + Pinia + vue-router
  ├── main.ts                  # 入口，创建 app + pinia + ElementPlus + router
  ├── App.vue                  # 根组件：header(连接选择+状态) + aside(菜单) + main(router-view)
  ├── api/kafkaApi.ts          # KafkaApi interface 类型封装 + declare global Window.kafkaApi
  ├── router/index.ts          # 路由：connection/cluster/producer/consumer/visual/scenario/settings
  ├── stores/                  # Pinia stores：connection/consumer/producer/metadata/scenario/settings
  ├── views/                   # ConnectionView/ClusterView/ProducerView/ConsumerView/VisualView/ScenarioView/SettingsView
  └── components/              # ConnectionDialog/ConsumerCard/MessageFlowCanvas/MessageTable/PartitionMap/TopicCreateDialog
```

**构建输出**：`out/main/index.mjs`, `out/preload/index.mjs`, `out/renderer/index.html`

**路径别名**：`@/*` → `src/renderer/*`（仅 renderer，见 `tsconfig.web.json` 和 `electron.vite.config.ts`）；main/preload 用相对路径。

## 核心设计决策

### `window.kafkaApi`（不是 `window.api`）

preload 用 `contextBridge.exposeInMainWorld('kafkaApi', api)` 暴露 API，并 `export type KafkaApi = typeof api`。渲染进程的类型在 `src/renderer/api/kafkaApi.ts` **手写** `KafkaApi` interface + `declare global { interface Window { kafkaApi: KafkaApi } }`。**两边接口需手动保持同步**，改 preload 的方法签名时必须同步改 renderer 的 interface。

### IPC 通道集中管理

通道名集中在 `src/main/ipc/channels.ts` 的 `IPC_CHANNELS` 常量。preload 的 `invoke('xxx:yyy')` 必须与 main 的 `ipcMain.handle('xxx:yyy')` 完全一致。新增 IPC 时两端同步改。

`registerHandlers.ts` 用 `wrapHandler` 包装所有 handler（统一 try/catch + logger.error + rethrow），用 `pushToRenderer` 封装事件推送（`win && !win.isDestroyed()` 检查）。

### KafkaClientManager 单例

`src/main/kafka/KafkaClientManager.ts` 是单例（`export const kafkaClientManager`），管理 `Kafka` + `Admin` 实例。`connect` 时先 `disconnect` 旧连接，再创建新 `Kafka`，`admin.connect()` 后 `describeCluster` 返回 brokers + topics。`setStatusCallback` 注册状态变更回调，通过 `event:connStatus` 推送。`testConnection` 创建临时 admin 测试连接（3s 超时，0 重试），用完即 disconnect。

### ProducerService 批量发送与模板

`src/main/kafka/ProducerService.ts`：
- 单 producer 懒加载（首次 `getProducer` 时 `connect`）
- `send` 单条发送，offset 取 `result[0].baseOffset`（kafkajs 类型未导出，用 `as Record<string, unknown>` 断言）
- `sendBatch` 用 `AbortController` 实现可停止的循环发送，每条通过 `event:produceAck` 推送结果
- 消息模板支持 `{{seq}}`/`{{ts}}`/`{{rand}}` 占位符（`renderTemplate`）
- key 策略：fixed（用 req.key）/ random（`user${0-99}`）/ round-robin（`user${i%5}`）

### ConsumerService 多实例 + MessageBuffer 批量推送

`src/main/kafka/ConsumerService.ts`：
- `Map<instanceId, InstanceEntry>` 管理多个消费者实例，每个实例有独立的 `MessageBuffer`
- `create` 创建 consumer（`sessionTimeout: 10000, heartbeatInterval: 3000`），返回 instanceId
- `start` 时 `consumer.on(GROUP_JOIN, ...)` 监听再均衡，推送 `event:rebalance`
- `MessageBuffer`（`src/main/util/messageBuffer.ts`）：`maxSize=100` 或 `intervalMs=200ms` 触发 flush，通过 `event:consumerMessage` 批量推送，**避免高频消息逐条 IPC**
- 支持 pause/resume/seek/commit，状态变更通过 `event:consumerState` 推送

### DemoScenarioService 教学场景编排

`src/main/kafka/DemoScenarioService.ts` + `src/main/data/scenarios.ts`：
- 6 个场景（S1-S6）：第一条消息、分区与 Key、消费组负载均衡、再均衡、消息重放、顺序性与 acks
- 步骤类型：createTopic/note/createConsumer/produce/sleep/stopConsumer
- `produce` 的 `count=0` 表示持续发送（999999 条）
- 通过 `event:scenarioStep` 推送进度（runId + stepIndex + message）
- `stop` 设置 `aborted` 标志，`execute` 循环中检查

### 配置持久化（JSON 文件 + safeStorage，非 electron-store）

`src/main/store/connectionStore.ts`：
- 用 JSON 文件（`app.getPath('userData')/connections.json`），**不是 electron-store**
- 密码用 `safeStorage`：`safeStorage.isEncryptionAvailable()` 时 `encryptString`（存 base64），否则 `Buffer.from(password).toString('base64')` 兜底
- save/delete 时持久化，渲染进程 onMounted 时主动 `loadConfigs` 拉取

### sandbox: false

preload 需要 `import type` 引用 `src/main/kafka/types.ts` 和 kafkajs 类型，sandbox 模式下不可用，因此 `sandbox: false`（`src/main/index.ts`）。preload 路径是 `join(__dirname, '../preload/index.mjs')`——注意 `.mjs` 后缀（因 `package.json` 有 `"type": "module"`）。

### 事件监听清理

每个 `onXxx`（onConnStatus/onConsumerMessage/onConsumerState/onRebalance/onProduceAck/onScenarioStep）返回 unsubscribe 函数。组件卸载时必须调用（见 `App.vue` 的 `onUnmounted`，调用 `connectionStore.teardownStatusListener?.()` / `consumerStore.teardownListeners()` / `producerStore.teardownAckListener()`）。

### 类型检查

`npm run typecheck` 是单段 `vue-tsc --noEmit`（仅 web 侧），与 ERabbitMQToolPlus/EActiveMQTool 的两段 `tsc + vue-tsc` 不同。改完代码必须跑。

## 已知 gotcha

- **Electron 二进制可能未下载**：`node_modules/electron/dist/electron.exe` 缺失时，`npm run dev` 报 `Electron uninstall` 类错误。手动跑 `node node_modules/electron/install.js` 补二进制。
- **`package.json` 的 `main` 是 `./out/main/index.mjs`**（注意是 `.mjs` 不是 `.js`），preload 路径是 `../preload/index.mjs`。
- **`ProducerService.send` 的 offset**：取 `result[0].baseOffset`，kafkajs 类型未导出该字段，用 `as Record<string, unknown>` 断言。
- **`window.kafkaApi` 类型手动同步**：`src/renderer/api/kafkaApi.ts` 的 `KafkaApi` interface 是手写的，改 preload 方法签名时必须同步改。
