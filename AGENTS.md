# AGENTS.md

Electron 桌面工具集合 monorepo。中文交流，中文提交信息。

## 仓库结构

**七个独立子项目**，无根 `package.json`。每个子项目有自己的 `package.json`、依赖和 `node_modules`。**必须先 `cd` 到子项目目录再执行命令。**

| 子项目 | 构建方式 | 关键依赖 |
|--------|----------|----------|
| EHttpServerTool | 独立 `electron/` 目录 | React 18 + Ant Design 5 |
| EWebsocketTool | 独立 `electron/` 目录 | React 18 + Ant Design 5 + `ws` |
| EWebsocketMan | vite-plugin-electron | Vue 3 + `ws` |
| ERabbitMQTool | 独立 `electron/` 目录 | React 18 + Ant Design 5 + `amqplib` |
| ERabbitMQToolPlus | electron-vite | Vue 3 + Element Plus + Pinia + `amqplib` |
| EActiveMQTool | electron-vite | Vue 3 + Element Plus + Pinia + `@stomp/stompjs` |
| EKafkaTool | electron-vite | Vue 3 + Element Plus + Pinia + `kafkajs` |

## 开发命令

```bash
# 通用（所有子项目）
npm install        # 在子项目目录内执行
npm run dev        # 启动开发
npm run build      # 构建
npm run pack       # 打包 Windows NSIS 安装包（EKafkaTool 为 `npm run package:win`）

# electron-vite 项目（ERabbitMQToolPlus / EActiveMQTool / EKafkaTool）
npm run typecheck  # 必须：类型检查，改完代码必须跑
npm run preview    # 预览构建产物
```

> EKafkaTool 的 typecheck 是单段 `vue-tsc`（仅 web 侧），ERabbitMQToolPlus/EActiveMQTool 是 `tsc(node) + vue-tsc(web)` 两段。

### dev 机制

- **方式 A/B 项目**（EHttpServerTool/EWebsocketTool/ERabbitMQTool/EWebsocketMan）：`concurrently` + `wait-on`，Vite 先启动（port 5173, `strictPort: true`），Electron 等待 `http://localhost:5173` 就绪后加载 dev server。
- **方式 C 项目**（ERabbitMQToolPlus/EActiveMQTool/EKafkaTool）：`electron-vite dev` 统一管理三进程 HMR。

### pack 命令差异

- **EWebsocketTool / EWebsocketMan / ERabbitMQTool**（Win7 兼容）：`pack` 含 `-c.electronDist="node_modules/electron/dist" -c.electronVersion="22.3.27"`，强制使用本地 Electron 22 二进制。
- **EHttpServerTool**：`pack` 含 `-c.electronDist="node_modules/electron/dist" -c.electronVersion="33.4.11"`。
- **ERabbitMQToolPlus / EActiveMQTool**：`pack` 为 `electron-vite build && electron-builder --win`，无需 electronDist 覆盖。
- **EKafkaTool**：`package:win` 为 `electron-vite build && electron-builder --win`，无需 electronDist 覆盖。

## 架构

所有子项目遵循三进程模型：Main（网络逻辑 + IPC）→ Preload（contextBridge）→ Renderer（纯 UI）。

三种构建方式：
- **方式 A**（EHttpServerTool/EWebsocketTool/ERabbitMQTool）：`electron/` 目录放主进程源码，Vite 只构建渲染进程，`"main": "electron/main.js"` 直接加载源码
- **方式 B**（EWebsocketMan）：vite-plugin-electron 构建三进程，`"main": "dist-electron/main.js"` 加载构建产物
- **方式 C**（ERabbitMQToolPlus/EActiveMQTool/EKafkaTool）：electron-vite 构建三进程，`src/main/`/`src/preload/`/`src/renderer/`，`"main": "./out/main/index.js"`（EKafkaTool 为 `./out/main/index.mjs`）。共享类型：ERabbitMQToolPlus/EActiveMQTool 在 `src/shared/`，EKafkaTool 在 `src/main/kafka/types.ts`（preload/renderer 相对路径引用）

## 关键约束

- **sandbox: false**（方式 C 项目）：preload 需要加载非 Electron 模块
- **contextIsolation: true, nodeIntegration: false**：渲染进程只通过 `window.api`（方式 C，ERabbitMQToolPlus/EActiveMQTool）、`window.kafkaApi`（方式 C，EKafkaTool）或 `window.electronAPI`（方式 A/B）通信
- **IPC 通道名**：preload 的 `invoke('xxx:yyy')` 必须与 main 的 `ipcMain.handle('xxx:yyy')` 完全一致
- **事件监听清理**：每个事件监听器注册返回 unsubscribe 函数，组件卸载时必须调用
- **主进程安全**：`webContents.send()` 前检查 `mainWindow && !mainWindow.isDestroyed()`

## EActiveMQTool 特有

- 使用 `@stomp/stompjs` 连接 ActiveMQ，支持 **TCP 原生连接**（`net.Socket` 适配器）和 **WebSocket 连接**
- TCP 适配器在 `src/main/utils/tcp-socket.ts`，将 `net.Socket` 包装为 `IStompSocket` 接口
- 连接表单有 TCP 开关，开启 TCP 时自动隐藏 SSL 选项，端口默认切换为 61613
- 消费者 ACK 通过 `messageId` 而非 `deliveryTag`，`client.ack(messageId, subscriptionId)` 需要两个参数
- 配置持久化用 electron-store，密码 AES-256-CBC 加密

## 已知 gotcha

- Electron 二进制可能未下载：`node node_modules/electron/install.js`
- `npm run dev` 报 `Electron uninstall` 错误 = 二进制未下载
- ERabbitMQToolPlus 详见 `ERabbitMQToolPlus/AGENTS.md`
- EKafkaTool 详见 `EKafkaTool/AGENTS.md`