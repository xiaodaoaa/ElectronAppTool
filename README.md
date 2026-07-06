# ElectronAppTool

Electron 桌面网络调试工具集合，独立子项目组成的 monorepo。

> **注意**：这是 monorepo，没有根目录 `package.json`。每个子项目独立的依赖和运行脚本，请先 `cd` 到对应目录再执行命令。

---

## 项目一览

| 子项目 | 技术栈 | 用途 |
|--------|--------|------|
| [EHttpServerTool](./EHttpServerTool/) | Electron 33 + React 18 + TypeScript + Vite 6 + Ant Design 5 | Mock HTTP 服务端 — 定义路径/方法的响应、Echo 模式、请求日志 |
| [EWebsocketTool](./EWebsocketTool/) | Electron 33 + React 18 + TypeScript + Vite 6 + Ant Design 5 + `ws` | WebSocket 客户端 + 服务端双模式调试工具 |
| [EWebsocketMan](./EWebsocketMan/) | Electron 28 + Vue 3 (Composition API) + Vite 5 + `ws` | 复刻 WebSocketMan v1.0.9（WS 服务端/客户端模式） |

---

## 开始使用

### EHttpServerTool

```bash
cd EHttpServerTool
npm install
npm run dev       # Vite dev server → Electron 窗口
npm run build     # Vite 构建 → dist/
npm run pack      # 构建 + electron-builder → Windows NSIS 安装包 (release/)
npm run preview   # 预览 Vite 构建
```

### EWebsocketTool

```bash
cd EWebsocketTool
npm install
npm run dev       # 启动开发模式
npm run pack      # 打包 Windows 安装包
```

### EWebsocketMan

```bash
cd EWebsocketMan
npm install
npm run dev       # 启动开发模式
npm run build     # 构建并打包
```

---

## 技术架构

三个项目均采用统一的 **Electron 三进程模型**：

```
Main Process  — 窗口管理 + 网络服务端逻辑（http / ws），通过 IPC handler 暴露
Preload       — contextBridge 暴露 electronAPI 给渲染进程（contextIsolation: true）
Renderer      — UI 层（React / Vue），仅通过 window.electronAPI 通信
```

### 关键约定

- **渲染进程不直接接触网络**：服务端逻辑运行在主进程的 manager 类中（`electron/modules/`）；客户端模式（WebSocket）使用浏览器原生 `WebSocket` 在渲染进程执行。
- **IPv4 标准化**：`getIPv4(remoteAddress)` 剥离 `::ffff:` 前缀，`::1` → `127.0.0.1`。
- **配置持久化**：JSON 文件保存到 `app.getPath('userData')`。
- **IPC 事件**：主进程通过 `sendToRenderer()` 推送事件（已做 `mainWindow.isDestroyed()` 空值检查）；渲染进程在组件卸载时必须清理监听器。
- **窗口标签页**：使用 `v-show`（Vue）/ 条件渲染保留标签页切换时的连接状态。

---

## npm 脚本说明

| 命令 | 说明 |
|------|------|
| `npm run dev` | Vite dev server（port 5173, strictPort）+ wait-on 等待就绪后启动 Electron |
| `npm run build` | Vite 构建输出到 `dist/` |
| `npm run preview` | 预览 Vite 构建产物 |
| `npm run pack` | Vite 构建 → electron-builder 打包 Windows NSIS 安装包（输出到 `release/`） |

`npm run dev` 使用 `concurrently` + `wait-on`：Vite 先启动，Electron 等待 `http://localhost:5173` 就绪后加载 dev server（`VITE_DEV_SERVER_URL` 环境变量），而非构建产物 `dist/index.html`。

---

## 目录结构规范

```
<subproject>/
├── electron/
│   ├── main.js          # 主进程入口 — 窗口管理 + IPC 注册
│   ├── preload.js       # 预加载脚本 — contextBridge 暴露 API
│   └── modules/         # 网络逻辑管理器类（EHttpServerTool）
├── src/                 # 渲染进程源码（React / Vue）
├── vite.config.ts       # Vite 配置（strictPort: true）
├── electron-builder.yml # 打包配置
└── package.json
```

EWebsocketTool / EWebsocketMan 的 manager 类可能内联在主进程文件中而非独立 `modules/` 目录。

---

## 开发说明

- 修改共享工具配置（vite config、electron-builder、dev 启动脚本）时，`EHttpServerTool` 和 `EWebsocketTool` 脚手架几乎一致，通常两处需同步更新。
- 设计文档和决策记录位于各子项目的 `docs/superpowers/` 和 `.superpowers/sdd/`。

---

## 许可证

MIT