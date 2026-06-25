# ElectronAppTool

WebSocket 桌面调试工具集合，基于 Electron 构建，包含两个独立项目。

## 项目结构

### WebsocketTool

基于 **React + TypeScript + Ant Design** 的 WebSocket 调试工具。

- **WebSocket 客户端**：连接远程 WS 服务器，发送/接收消息
- **WebSocket 服务端**：启动本地 WS 服务器，接受客户端连接，支持单发/广播
- 技术栈：Electron 33 + React 18 + TypeScript + Vite 6 + Ant Design 5

```bash
cd WebsocketTool
npm install
npm run dev       # 启动开发模式
npm run pack      # 打包 Windows 安装包
```

### EWebsocketMan

基于 **Vue 3 + Vite** 的 WebSocket 测试工具，复刻 WebSocketMan v1.0.9。

- **服务端模式**：启动 WS 服务器，管理连接的客户端
- **客户端模式**：连接远程 WS 服务器
- 支持配置持久化、消息文件导出
- 技术栈：Electron 28 + Vue 3 (Composition API) + Vite 5 + ws

```bash
cd EWebsocketMan
npm install
npm run dev       # 启动开发模式
npm run build     # 构建并打包
```

## 技术架构

两个项目均采用 Electron 三进程模型：

```
Main Process  — 管理窗口、WebSocket 服务端/客户端逻辑
Preload       — contextBridge 暴露 IPC 接口给渲染进程
Renderer      — UI 层（React / Vue）
```

渲染进程与主进程通过 IPC (`ipcMain.handle` / `ipcRenderer.invoke`) 通信，所有 WS 服务端操作在主进程执行。
