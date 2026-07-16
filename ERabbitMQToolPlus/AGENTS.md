# AGENTS.md

Electron + Vue 3 桌面 RabbitMQ 工具。中文交流，中文提交信息。

## 开发命令

```bash
npm run dev          # 启动开发（electron-vite 三进程热更新）
npm run typecheck    # 必须：tsc(node) + vue-tsc(web) 两段类型检查
npm run build        # 构建产物到 out/（不启动窗口）
npm run build:win    # 打包 Windows NSIS 安装包到 release/
```

改完代码必须跑 `npm run typecheck`。不要跑 `npm run dev` 验证窗口（长驻进程，无法在自动化中确认 UI）。

## 架构

三进程结构（electron-vite）：
- `src/main/` — 主进程。`services/` 承载业务逻辑（ConnectionManager/ProducerService/ConsumerService 单例），`ipc/` 只做参数转发，`utils/` 是工具（ssl/store/logger）。入口 `index.ts`。
- `src/preload/index.ts` — preload 脚本。`export const api` + `contextBridge.exposeInMainWorld('api', api)`。渲染进程通过 `window.api.*` 调用。
- `src/renderer/` — 渲染进程。Vue 3 + Element Plus + Pinia。`stores/` 调 `window.api`，`views/` 管页面状态，`components/` 是复用组件。
- `src/shared/types.ts` — 跨进程共享类型，main/preload/renderer 都引用。

## 关键约束

- **sandbox: false**（`src/main/index.ts`）。preload 需要加载非 Electron 模块（shared/types），sandbox 模式下不可用。
- **contextBridge only**：不开 `nodeIntegration`，渲染进程只通过 `window.api` 访问主进程。
- **IPC 通道名**：preload 的 `invoke('xxx:yyy')` 必须与 main 的 `ipcMain.handle('xxx:yyy')` 完全一致。新增 IPC 时两端同步改。
- **`window.api` 类型**：`src/renderer/src/env.d.ts` 用 `import type { api } from '../../preload'` 推导。preload 必须 `export const api`。
- **amqplib 的 `noAck` 语义**：`noAck: true` = 服务器自动确认。ConsumerService 中 `noAck` 值等于 `config.autoAck`（不是 `!autoAck`）。

## 路径别名

- `@shared/*` → `src/shared/*`（main/preload/renderer 三端通用）
- `@renderer/*` → `src/renderer/src/*`（仅 renderer）
- `@main/*`、`@preload/*`（仅 tsconfig.node.json，实际代码用相对路径）

## 持久化

- `src/main/utils/store.ts` 用 electron-store，密码用 AES-256-CBC 加密。
- 连接/生产者/消费者配置在操作成功时保存（connect/send/start），渲染进程 onMounted 时主动 `loadProducer`/`loadConsumer` 拉取（不用推送，有时序问题）。

## 已知 gotcha

- Electron 二进制可能未下载（`node_modules/electron/dist/electron.exe` 缺失），手动跑 `node node_modules/electron/install.js`。
- `npm run dev` 报 `Electron uninstall` 错误 = 二进制未下载。
