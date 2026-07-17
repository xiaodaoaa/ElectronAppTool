# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

> 本子项目还有 `AGENTS.md`,内容与本文互补但**有一处过时错误**:它写的打包命令是 `npm run build:win`,实际应为 `npm run pack`(见 `package.json`)。两份文档冲突时以本文为准。

## 开发命令

```bash
npm install          # 装依赖
npm run dev          # electron-vite 三进程 HMR(主/preload/渲染)
npm run typecheck    # 必须跑:tsc(node) + vue-tsc(web) 两段类型检查
npm run build        # 构建产物到 out/(不启动窗口)
npm run pack         # electron-vite build && electron-builder --win → release/ NSIS 安装包
```

- **改完代码必须跑 `npm run typecheck`。** 两段分别覆盖 main/preload(`tsconfig.node.json`)与 renderer(`tsconfig.web.json`),任一失败都不行。
- **不要用 `npm run dev` 验证 UI**:长驻进程,自动化中无法确认窗口行为。验证靠 typecheck + 逻辑审查。
- 没有测试套件,没有 lint 脚本。`typecheck` 是唯一的自动化质量门。

## 三进程架构(electron-vite,Approach C)

源码在 `src/{main,preload,renderer}/`,共享类型在 `src/shared/types.ts`(三端都引用)。构建产物落到 `out/{main,preload,renderer}/`,`package.json` 的 `"main": "./out/main/index.js"` 指向构建后的主进程入口。

- **`src/main/`** — 主进程。`index.ts` 是 Electron 接线(建窗、注册 IPC、`did-finish-load` 推送已存配置);业务逻辑全在 `services/` 三个单例里,`ipc/` 只做参数转发,`utils/` 是 ssl/store/logger。
- **`src/preload/index.ts`** — `export const api` + `contextBridge.exposeInMainWorld('api', api)`。渲染进程通过 `window.api.*` 调用,绝不直接碰 Node/Electron。
- **`src/renderer/`** — Vue 3 + Element Plus + Pinia。`main.ts` 装配 Pinia/ElementPlus 并调用各 store 的 `bindIpc()`;`stores/` 调 `window.api`,`views/` 管页面状态,`components/` 复用。

### 路径别名

- `@shared/*` → `src/shared/*`(三端通用,在 `electron.vite.config.ts` 与两个 tsconfig 都配了)
- `@renderer/*` → `src/renderer/src/*`(仅 renderer)
- `@main/*`、`@preload/*` 仅在 `tsconfig.node.json` 声明,**实际代码用相对路径**(如 `'../../../shared/types'`),别依赖这两个别名写新代码。

## IPC 全表(通道名 preload↔main 必须逐字一致)

新增 IPC 时,preload 的 `invoke('xxx:yyy')` 与 main 的 `ipcMain.handle('xxx:yyy')` 两端同步改。

**请求/响应**(`invoke` ↔ `handle`):

| 域 | 通道 |
|----|------|
| connection | `connection:connect` / `connection:test` / `connection:disconnect` |
| producer | `producer:send` / `producer:getHistory` |
| consumer | `consumer:start` / `consumer:pause` / `consumer:resume` / `consumer:stop` / `consumer:pull` / `consumer:ack` / `consumer:nack` |
| config | `config:saveProducer` / `config:saveConsumer` / `config:loadProducer` / `config:loadConsumer` |
| dialog | `dialog:selectFile` |

**事件**(main → renderer,`webContents.send`):

| 通道 | 触发点 | 渲染进程是否消费 |
|------|--------|------------------|
| `connection:statusChanged` | `ConnectionManager` 状态变更 | ✅ connection store |
| `connection:lastConfigLoaded` | `did-finish-load` 推送上次连接配置 | ✅ connection store → 回填表单 |
| `producer:progress` | 批量发送每条 | ✅ producer store |
| `consumer:message` | 收到消息 | ✅ consumer store |
| `consumer:stopped` | channel 关闭 / 达 maxReceive / 连接断开 | ✅ consumer store |
| `log:entry` | `logger` 每次调用 | ✅ log store |
| `config:producerLoaded` | `did-finish-load` 推送 | ⚠️ preload 暴露了 `onProducerLoaded`,但**无 store 调用** |
| `config:consumerLoaded` | `did-finish-load` 推送 | ⚠️ 同上,`onConsumerLoaded` 未被消费 |

## 关键约束与 gotcha

### sandbox: false
`src/main/index.ts` 建 `BrowserWindow` 时 `sandbox: false`。preload 要 `require` 非 Electron 模块(`shared/types`、`amqplib` 类型),sandbox 模式下不可用。**不要改成 true。** preload 路径是 `join(__dirname, '../preload/index.mjs')`——注意 `.mjs` 后缀(因 `package.json` 有 `"type": "module"`)。

### preload 事件监听器不返回 unsubscribe(与其它子项目不同)
父仓库 `CLAUDE.md` 把"每个事件监听返回 unsubscribe、组件销毁时必须调用"列为通用约束——**本子项目不遵循此模式**:`preload/index.ts` 的 `onStatusChange`/`onMessage`/`onStop`/`onProgress`/`onLog` 等都只注册不返回取消函数,且各 store 的 `bindIpc()` 在 `src/renderer/src/main.ts` **应用级只调用一次**(非组件级),没有 `onUnmounted` 清理。这是当前实现,新增事件监听时沿用现有模式即可,不要按父级约束去"补 unsubscribe"。

### `window.api` 类型推导
`src/renderer/src/env.d.ts` 用 `import type { api } from '../../preload'` 推导 `window.api` 类型。前提是 preload **必须 `export const api`**。改 preload 的导出形签名时,renderer 类型会跟着变。

### amqplib `noAck` 语义
`noAck: true` = 服务器自动确认。`ConsumerService` 中传给 `channel.consume`/`channel.get` 的 `noAck` 值**等于 `config.autoAck`**(不是 `!autoAck`)。`autoAck=false` 时,收到的消息进 `pendingMessages` Map(按 `deliveryTag` 索引),由 `ack`/`nack` IPC 手动处理。

### 配置加载:推送 + 拉取双轨,以拉取为主
- **推送**:`index.ts` 在 `did-finish-load` 时把上次连接/生产者/消费者配置通过 `connection:lastConfigLoaded`/`config:producerLoaded`/`config:consumerLoaded` 推给渲染进程。只有 `lastConfigLoaded` 被 connection store 消费(回填连接表单),后两者未被消费。
- **拉取**:`ProducerView`/`ConsumerView` 在 `onMounted` 主动 `loadProducer`/`loadConsumer` 拉取并 `Object.assign` 回填表单。**这是主要路径**,因为推送有渲染进程时序问题(组件 mount 早于 `did-finish-load` 时会漏)。
- **保存时机**:操作成功才存——`connection:connect` 成功存连接配置,`producer:send` 成功存生产者配置,`consumer:start` 成功存消费者配置。

### 连接断开时的级联清理
`index.ts` 顶层注册 `connectionManager.onStatusChange`:状态变 `disconnected` 或 `error` 时,调 `producerService.clearHistoryOnDisconnect()`(清历史)和 `consumerService.clearOnDisconnect()`(关 channel、清 pending、重置状态)。改连接/消费清理逻辑时,注意这条主进程级的级联,别只在 service 内部改。

### SSL 的进程级全局副作用
`src/main/utils/ssl.ts` 的 `buildConnectOptions` 在 `rejectUnauthorized=false` 时设 `process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0'`,**这是整个 Node 进程的全局开关**,会放宽本进程所有 TLS 连接的证书校验,不止当前 RabbitMQ 连接。`true` 时设回 `'1'`。改 SSL 逻辑时留意这个全局副作用。

### 持久化加密范围
`src/main/utils/store.ts` 用 `electron-store`,**只加密 `ConnectionConfig.password` 和 `passphrase`**(AES-256-CBC,密钥由 `app.getName()` 派生)。`lastProducer`/`lastConsumer` 整对象明文存。加密失败时 `loadConnection` 把对应字段降级为空串。

### ConsumerService 过滤与自动 ack 的交互
`matchFilter` 支持 routingKey 通配符(`*`/`#`,AMQP topic 语义)和 header 键值匹配。**被过滤掉的消息:在 `autoAck=false` 时会被自动 `ack` 掉**(不进 pending、不通知渲染进程),`autoAck=true` 时由服务器自动确认。`maxReceive>0` 且收到数达上限会自动 `stop()` 并 `emitStop()`。

### ProducerService 的 channel 生命周期
每次 `send` 都 `createChannel` → 发送(批量循环、每条新 `messageId` via uuid)→ `finally` 关 channel。**channel 不复用**,生产者历史只留最近 10 条,断开连接时清空。

## 已知 gotcha

- **Electron 二进制可能未下载**:`node_modules/electron/dist/electron.exe` 缺失时,`npm run dev` 报 `Electron uninstall` 类错误。手动跑 `node node_modules/electron/install.js` 补二进制。
- `appId`(`com.erabbitmq.tool`,在 `package.json` build 段)与 `setAppUserModelId` 的入参(`com.fengchao12.erabbitmqtool`,在 `index.ts`)不一致——目前不影响构建,改其一注意同步。
