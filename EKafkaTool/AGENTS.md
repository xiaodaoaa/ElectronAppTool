# AGENTS.md

Electron + Vue 3 桌面 Kafka 教学演示工具。中文交流，中文提交信息。

## 开发命令

```bash
npm run dev          # 启动开发（electron-vite 三进程热更新）
npm run typecheck    # vue-tsc 类型检查（单段，仅 web 侧）
npm run lint         # eslint（.ts/.vue）
npm run build        # 构建产物到 out/（不启动窗口）
npm run package:win  # 打包 Windows NSIS 安装包到 release/
npm run preview      # 预览构建产物
```

改完代码必须跑 `npm run typecheck`。不要跑 `npm run dev` 验证窗口（长驻进程，无法在自动化中确认 UI）。

## 本地 Kafka

`docker/docker-compose.yml` 启动单节点 Kafka 3.9.0（KRaft 模式，端口 9092）：

```bash
docker compose -f docker/docker-compose.yml up -d
```

连接配置 brokers 填 `localhost:9092`。

## 架构

三进程结构（electron-vite）：
- `src/main/` — 主进程。`kafka/` 承载业务逻辑（KafkaClientManager 单例 + ProducerService/ConsumerService/DemoScenarioService），`ipc/` 只做参数转发（`registerHandlers.ts` 统一注册，`channels.ts` 集中定义通道名），`store/` 连接配置持久化，`util/` 工具（logger/messageBuffer），`data/` 演示场景定义。入口 `index.ts`。
- `src/preload/index.ts` — preload 脚本。`contextBridge.exposeInMainWorld('kafkaApi', api)`。渲染进程通过 `window.kafkaApi.*` 调用。事件监听器返回 unsubscribe 函数。
- `src/renderer/` — 渲染进程。Vue 3 + Element Plus + Pinia + vue-router。`stores/` 调 `window.kafkaApi`，`views/` 管页面状态，`components/` 复用组件，`api/kafkaApi.ts` 是类型封装。
- 共享类型在 `src/main/kafka/types.ts`，preload 和 renderer 都通过相对路径引用（无 `src/shared/` 目录）。

## 关键约束

- **sandbox: false**（`src/main/index.ts`）。preload 需要加载非 Electron 模块（kafkajs 类型），sandbox 模式下不可用。
- **contextBridge only**：`contextIsolation: true, nodeIntegration: false`，渲染进程只通过 `window.kafkaApi` 访问主进程。
- **IPC 通道名**：preload 的 `invoke('xxx:yyy')` 必须与 main 的 `ipcMain.handle('xxx:yyy')` 完全一致。通道名集中在 `src/main/ipc/channels.ts` 的 `IPC_CHANNELS` 常量，新增 IPC 时两端同步改。
- **`window.kafkaApi` 类型**：`src/renderer/api/kafkaApi.ts` 手写 `KafkaApi` interface + `declare global { interface Window { kafkaApi } }`。preload 必须 `export type KafkaApi = typeof api`，两边接口需手动保持同步。
- **事件监听清理**：每个 `onXxx` 返回 unsubscribe 函数，组件卸载时必须调用（见 `App.vue` 的 `onUnmounted`）。
- **主进程安全**：`pushToRenderer`（`registerHandlers.ts`）已封装 `win && !win.isDestroyed()` 检查。

## 持久化

- `src/main/store/connectionStore.ts` 用 JSON 文件（`app.getPath('userData')/connections.json`），**不是** electron-store。
- 密码用 `safeStorage`（`safeStorage.isEncryptionAvailable()` 时 `encryptString`，否则 base64 兜底）。
- 连接配置在 save/delete 时持久化，渲染进程 onMounted 时主动 `loadConfigs` 拉取。

## 消息推送

- **MessageBuffer**（`src/main/util/messageBuffer.ts`）：消费者消息批量缓冲，`maxSize=100` 或 `intervalMs=200ms` 触发 flush，通过 `event:consumerMessage` 批量推送。避免高频消息逐条 IPC。
- 其他事件（connStatus/consumerState/rebalance/produceAck/scenarioStep）即时推送。

## 演示场景

- `src/main/data/scenarios.ts` 定义 6 个教学场景（S1-S6）：第一条消息、分区与 Key、消费组负载均衡、再均衡、消息重放、顺序性与 acks。
- `DemoScenarioService` 按步骤编排（createTopic/createConsumer/produce/sleep/note/stopConsumer），通过 `event:scenarioStep` 推送进度。
- `produce` 的 `count=0` 表示持续发送（999999 条）。

## 路径别名

- `@/*` → `src/renderer/*`（仅 renderer，见 `tsconfig.web.json` 和 `electron.vite.config.ts`）
- main/preload 用相对路径

## 已知 gotcha

- Electron 二进制可能未下载（`node_modules/electron/dist/electron.exe` 缺失），手动跑 `node node_modules/electron/install.js`。
- `npm run dev` 报 `Electron uninstall` 错误 = 二进制未下载。
- `package.json` 的 `main` 是 `./out/main/index.mjs`（注意是 `.mjs` 不是 `.js`），preload 路径是 `../preload/index.mjs`。
- `ProducerService.send` 的 offset 取 `result[0].baseOffset`（kafkajs 类型未导出，用 `as Record<string, unknown>` 断言）。
