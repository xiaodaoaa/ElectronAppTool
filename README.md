# ElectronAppTool

Electron 桌面工具集合，五个独立子项目组成的 monorepo。

> **注意**：这是 monorepo，**没有根目录 `package.json`**。每个子项目有独立的依赖、`node_modules` 和运行脚本。请先 `cd` 到对应目录再执行命令。

---

## 项目一览

| 子项目 | 技术栈 | Win7 兼容 | 用途 |
|--------|--------|-----------|------|
| [EHttpServerTool](./EHttpServerTool/) | Electron 33 + React 18 + TS + Ant Design 5 + Vite 6 | ❌ | Mock HTTP 服务端 — 定义路径/方法响应、Echo 模式、请求日志记录 |
| [EWebsocketTool](./EWebsocketTool/) | Electron 22 + React 18 + TS + Ant Design 5 + Vite 6 | ✅ | WebSocket 客户端(Browser API) + 服务端(`ws` 库) 双模式调试工具 |
| [EWebsocketMan](./EWebsocketMan/) | Electron 22 + Vue 3 (Composition API) + Vite 5 + `ws` | ✅ | 复刻 WebSocketMan v1.0.9 — WS 服务端/客户端双模式 |
| [EPinyinStudy](./EPinyinStudy/) | Electron 33 + Vue 3 + Vite 6 | ❌ | 小学一年级拼音学习工具 — 声母、韵母、整体认读音节、汉字认读、测验 |
| [ERabbitMQTool](./ERabbitMQTool/) | Electron 22 + React 18 + TS + Ant Design 5 + Vite 6 + `amqplib` | ✅ | RabbitMQ 调试工具 — 连接管理、生产者发布、消费者订阅（队列/交换机模式）、SSL 支持 |

---

## 快速开始

### EHttpServerTool

```bash
cd EHttpServerTool
npm install
npm run dev       # Vite dev server + Electron 窗口，HMR 热重载
npm run build     # Vite 生产构建 → dist/
npm run pack      # 构建 + electron-builder → Windows NSIS 安装包 (release/)
npm run preview   # 预览 Vite 构建产物
```

### EWebsocketTool

```bash
cd EWebsocketTool
npm install
npm run dev       # Vite + Electron，HMR 热重载
npm run pack      # 打包 Windows 安装包（使用本地 Electron 22 二进制）
```

### EWebsocketMan

```bash
cd EWebsocketMan
npm install
npm run dev       # Vite + Electron，HMR 热重载
npm run build     # Vite 构建
npm run pack      # 打包 Windows 安装包（使用本地 Electron 22 二进制）
```

### EPinyinStudy

```bash
cd EPinyinStudy
npm install
npm run dev             # Vite dev server（浏览器模式，port 5173）
npm run electron:dev    # Vite + Electron，HMR 热重载
npm run build           # Vite 生产构建 → dist/
npm run pack            # 构建 + electron-builder → Windows NSIS 安装包
npm run electron:linux  # 构建 + electron-builder → Linux AppImage 安装包
```

### ERabbitMQTool

```bash
cd ERabbitMQTool
npm install
npm run dev       # Vite + Electron，HMR 热重载
npm run build     # Vite 生产构建 → dist/
npm run preview   # 预览 Vite 构建产物
npm run pack      # 打包 Windows 安装包（使用本地 Electron 22 二进制）
```

---

## npm 脚本说明

| 命令 | 适用项目 | 说明 |
|------|----------|------|
| `npm run dev` | 除 EPinyinStudy | Vite dev server（port 5173, strictPort）+ wait-on 后启动 Electron |
| `npm run dev` | EPinyinStudy | **仅启动 Vite dev server**（浏览器模式，无 Electron） |
| `npm run electron:dev` | EPinyinStudy | `concurrently` + `wait-on` 启动 Vite + Electron |
| `npm run build` | 全部 | Vite 构建输出到 `dist/` |
| `npm run preview` | 不含 EPinyinStudy | 预览 Vite 构建产物 |
| `npm run pack` | 全部 | Vite 构建 → electron-builder 打包 Windows NSIS 安装包 |
| `npm run electron:linux` | EPinyinStudy | 打包 Linux AppImage 安装包 |

### dev 机制

`dev` / `electron:dev` 脚本均使用 `concurrently` + `wait-on`：Vite 先启动，Electron 等待 `http://localhost:5173` 就绪后加载 dev server（通过 `VITE_DEV_SERVER_URL` 环境变量），而非构建产物 `dist/index.html`。Vite 配置了 `strictPort: true`，如果 5173 被占用会直接报错（不会回退到其他端口导致 Electron 挂起）。

### pack 命令差异

- **EWebsocketTool**、**EWebsocketMan** 和 **ERabbitMQTool**（Win7 兼容）：pack 命令包含 `-c.electronDist="node_modules/electron/dist" -c.electronVersion="22.3.27"`，强制 electron-builder 使用本地安装的 Electron 22 二进制。
- **EHttpServerTool** 和 **EPinyinStudy**（无需 Win7）：pack 命令不含 electronDist 覆盖，electron-builder 自动使用 `node_modules` 中的 Electron 版本。

---

## 技术架构

### 三进程模型（全部五个项目通用）

```
Main Process
├── 管理 BrowserWindow
├── 持有所有网络服务端逻辑（http / ws / WebSocket）— 渲染进程不直接接触网络
├── 注册 ipcMain.handle() 处理请求/响应
└── 通过 webContents.send() 推送异步事件到渲染进程

Preload
├── contextBridge.exposeInMainWorld('electronAPI', …)
├── contextIsolation: true, nodeIntegration: false
└── 每个事件监听器均返回一个 unsubscribe 清理函数

Renderer (React TSX / Vue 3)
└── 仅通过 window.electronAPI 与主进程通信，绝不直接 import Node/Electron 模块
```

### 两种构建方式

**方式 A — 独立 electron/ 目录**（EHttpServerTool、EWebsocketTool、EPinyinStudy、ERabbitMQTool）：
- 主进程/预加载源码位于 `electron/main.js` 和 `electron/preload.js`
- Vite **只构建渲染进程**（`src/` → `dist/`）
- `package.json` 的 `"main": "electron/main.js"` — Electron 直接加载源码
- electron-builder 的 `files` 配置包含 `electron/**/*`

**方式 B — vite-plugin-electron**（EWebsocketMan）：
- 主进程/预加载源码位于 `src/main/main.js` 和 `src/preload/preload.js`
- Vite 构建**全部三部分**：渲染进程 → `dist/`，主进程 → `dist-electron/main.js`，预加载 → `dist-electron/preload.js`
- `package.json` 的 `"main": "dist-electron/main.js"` — Electron 加载构建产物
- electron-builder 的 `files` 配置包含 `dist-electron/**/*`

### IPC 模式

所有项目使用同一套 IPC 规范，始终通过 `window.electronAPI` 通信：

**请求/响应** — `ipcRenderer.invoke(channel, …args)` ↔ `ipcMain.handle(channel, handler)`：
- 用于主动操作：启动/停止服务、保存/加载配置、发送消息、文件操作

**事件推送** — `ipcRenderer.on(channel, handler)`（主进程 → 渲染进程）：
- 用于异步通知：服务启动/停止、客户端连接/断开、消息到达、错误发生
- **关键**：每个事件监听注册返回一个取消订阅函数，**组件卸载时必须调用**（React `useEffect` return / Vue `onUnmounted`），否则标签页切换会导致监听器重复累积，造成消息重复处理和内存泄漏。

**安全守卫**：主进程永远不假定 `mainWindow` 存活。全部四个项目都有 `sendToRenderer()` 包装函数，调用前做 `mainWindow && !mainWindow.isDestroyed()` 空值检查。

### Windows 7 兼容

EWebsocketTool 和 EWebsocketMan 使用 **Electron 22.3.27**（最后一个支持 Windows 7 的版本，Electron 23+ 已放弃 Win7 支持）。**ERabbitMQTool** 同样使用 Electron 22.3.27 以兼容 Win7。适配只需两处改动：
1. `package.json` 中 electron 版本指定为 `^22.3.27`
2. `pack` 脚本添加 `-c.electronDist` 和 `-c.electronVersion` 参数

### IPv4 地址标准化

三个网络调试工具统一对 `remoteAddress` 做 IPv6 映射前缀剥离：
- `::ffff:127.0.0.1` → `127.0.0.1`
- `::1` → `127.0.0.1`
- 纯 IPv6 地址被过滤/置 null

### 配置持久化

配置（服务设置、连接 URL）以 JSON 文件形式保存到 `app.getPath('userData')`。EWebsocketMan 有 300ms 防抖自动保存；EHttpServerTool 使用 `PathConfigManager` 类管理；ERabbitMQTool 保存连接配置（host/port/vhost/凭据/SSL 选项）到 `config.json`。

---

## 子项目详细介绍

### EHttpServerTool — Mock HTTP 服务端调试工具

功能最复杂的项目，支持：
- 定义多个 HTTP 路径/方法的路由和响应内容
- Echo 模式（原样返回请求）
- 请求日志记录与查看
- 日志文件持久化

**特有结构**：主进程逻辑拆分到 `electron/modules/` 目录下的多个管理器类：
```
electron/
├── main.js                 # 纯胶水代码：窗口创建 + IPC 注册 + 事件转发
├── preload.js
└── modules/
    ├── http-server.js      # HTTP 服务管理
    ├── path-config.js      # 路由配置管理
    ├── request-logger.js   # 请求日志记录
    └── logger.js           # 文件日志
```

**测试**：项目根目录有三个 `*.test.js` 文件（`http-server.test.js`、`path-config.test.js`、`request-logger.test.js`），使用 Jest 风格的 globals（`describe`/`it`/`expect`）测试真实的 `http`/`fs` 操作（临时目录、真实端口）。**但未配置测试运行器**，使用前需安装 Jest 并添加 `test` 脚本。

### EWebsocketTool — WebSocket 双模式调试工具

支持客户端和服务端两种模式：
- **客户端模式**：使用浏览器原生 `WebSocket API` 在渲染进程中直接连接远程 WS 服务端（不经过 IPC）
- **服务端模式**：使用 `ws` 库在主进程中启动 WS 服务端，通过 IPC 与渲染进程交互

结构相比 EHttpServerTool 更简洁，WS 服务端逻辑直接内联在 `electron/main.js` 中。

### EWebsocketMan — WebSocketMan 复刻

功能同 EWebsocketTool（WS 服务端 + 客户端），但使用 **Vue 3** 构建，且构建方式不同（`vite-plugin-electron`）。包含完整的文件操作功能（读文件、写文件、二进制文件、文件选择对话框）。

### EPinyinStudy — 拼音学习工具

与其他三个网络调试工具不同，这是一个离线教育应用：
- **无后端、无 API**：所有拼音/汉字数据构建时打包在 `src/data/pinyin.js` 中
- **发音**：使用 Web Speech API（`SpeechSynthesisUtterance`，`lang: 'zh-CN'`），无需音频文件。拼音语速 0.8x，汉字 0.7x
- **无 Vue Router**：通过 `App.vue` 的 `currentTab` ref + `v-if` 切换六个标签页
- **无状态管理**：全部使用局部 `ref`，数据通过 props/$emit 单向流动
- **跨平台**：支持 Windows NSIS 和 Linux AppImage 两种打包格式
- 可用作纯 Web 应用（`npm run dev` 在浏览器中直接运行，无需 Electron）

六个标签页：首页、声母表、韵母表、整体认读音节、汉字认读、测验。

### ERabbitMQTool — RabbitMQ 调试工具

基于 `amqplib` 的 RabbitMQ 消息队列调试工具，支持完整的连接、发布、订阅流程：

- **连接管理**：支持 amqp/amqps 协议，SSL 连接可选（`sslEnabled` + `sslValidateServerCert`），连接配置持久化
- **生产者**：可向指定队列或交换机发布消息，支持 persistent、contentType、priority、headers、messageId、replyTo 等消息属性
- **消费者**：支持两种订阅模式：
  - **队列模式**：直接消费指定队列（`assertQueue` + `consume`，`noAck: true`），检测已有消费者时给出 round-robin 警告
  - **交换机模式**：创建临时排他队列并绑定到交换机进行订阅
- **日志面板**：实时显示发送/接收/订阅/错误事件
- **连接生命周期**：主进程管理 connection/channel/consumerTags，断开时自动清理所有消费者

**结构**：采用方式 A（独立 `electron/` 目录），主进程逻辑内联在 `electron/main.js` 中（连接管理 + amqplib 操作 + IPC 注册），渲染进程使用 React + Ant Design，组件分为 `ConnectionPanel`、`ProducerTab`、`ConsumerTab`、`LogPanel`。

---

## 目录结构规范

### 方式 A（独立 electron/ 目录）

```
<subproject>/
├── electron/
│   ├── main.js          # 主进程入口 — 窗口管理 + IPC 注册
│   ├── preload.js       # 预加载脚本 — contextBridge 暴露 API
│   └── modules/         # [EHttpServerTool 特有] 网络逻辑管理器类
├── src/                 # 渲染进程源码
│   ├── main.tsx/js      # 入口文件
│   ├── App.tsx/vue      # 根组件
│   ├── components/      # UI 组件
│   ├── types/           # [React 项目] TypeScript 类型定义
│   └── hooks/           # [EWebsocketTool] React Hooks
├── public/              # 静态资源（图标等）
├── vite.config.ts       # Vite 配置
├── electron-builder.yml # 打包配置
├── package.json
└── *.test.js            # [EHttpServerTool] 测试文件
```

### 方式 B（vite-plugin-electron）

```
EWebsocketMan/
├── src/
│   ├── main/
│   │   └── main.js      # 主进程入口
│   ├── preload/
│   │   └── preload.js   # 预加载脚本
│   └── renderer/        # Vue 渲染进程
│       ├── main.js      # Vue 入口
│       ├── App.vue      # 根组件
│       └── components/  # Vue 组件
├── vite.config.js       # Vite 配置（含 vite-plugin-electron 配置）
├── electron-builder.yml
└── package.json
```

---

## 开发说明

- 修改共享工具配置（vite config、electron-builder、dev 启动脚本）时，**EHttpServerTool、EWebsocketTool 和 ERabbitMQTool** 脚手架几乎一致，通常三处需同步更新。EWebsocketMan 和 EPinyinStudy 配置独立。
- 设计文档和决策记录位于各子项目的 `docs/superpowers/` 和 `.superpowers/sdd/` 目录，供追溯历史决策。

---

## 许可证

MIT