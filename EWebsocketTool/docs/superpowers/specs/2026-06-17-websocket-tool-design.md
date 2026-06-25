# WebSocket 调试工具 — 设计文档

**日期：** 2026-06-17
**状态：** 已确认

---

## 1. 概述

一个基于 Electron + React + Ant Design 的桌面 WebSocket 调试工具，包含客户端和服务端两个 Tab，支持消息收发、客户端管理、连接日志等功能。

## 2. 技术栈

| 层面 | 技术选型 |
|------|----------|
| 桌面框架 | Electron |
| 前端框架 | React |
| 组件库 | Ant Design（亮色主题） |
| 构建工具 | Vite |
| WebSocket 服务端 | Node.js `ws` 库 |
| WebSocket 客户端 | 浏览器原生 `WebSocket` API |

## 3. 架构

```
Electron App
├── Main Process（main.js）
│   ├── 创建 BrowserWindow
│   ├── 通过 IPC 桥接 renderer 与 Node.js（ws 服务端）
│   └── 管理应用生命周期
│
└── Renderer Process（React App）
    ├── App.tsx — 顶层布局，Tab 切换
    ├── ClientTab — WebSocket 客户端
    │   ├── ConnectionPanel — 连接配置 + 连接/断开
    │   ├── MessagePanel — 消息发送
    │   └── LogPanel — 消息日志
    ├── ServerTab — WebSocket 服务端
    │   ├── ServerPanel — 端口配置 + 启动/停止
    │   ├── ClientList — 已连接客户端列表
    │   ├── MessagePanel — 消息发送（选中/广播）
    │   └── LogPanel — 消息日志
    └── shared/
        └── LogPanel — 共用日志组件
```

### 3.1 进程间通信

服务端 WebSocket（`ws` 库）运行在 Main Process，Renderer 通过 IPC 与 Main Process 通信：

- **Renderer → Main：** `start-server(port)`、`stop-server()`、`send-to-client(clientId, message)`、`broadcast(message)`
- **Main → Renderer：** `server-started`、`server-stopped`、`client-connected`、`client-disconnected`、`message-received`、`server-error`

客户端 WebSocket 直接由 Renderer 进程使用浏览器原生 `WebSocket` API 管理，无需经过 Main Process。

## 4. 功能规格

### 4.1 客户端 Tab

**连接配置区：**
- URL 输入框（`ws://` 或 `wss://`）
- 连接 / 断开按钮
- 连接状态指示（已连接 = 绿色，断开 = 灰色，连接中 = 加载动画）

**消息发送区：**
- 文本输入框 + 发送按钮
- 支持 Enter 键发送

**消息日志区：**
- 每条日志：`[HH:mm:ss] [发送/接收] 消息内容`
- 自动滚动到最新
- 连接、断开、异常事件同样输出日志

**持久化：**
- URL 保存到 localStorage，下次启动自动填充

### 4.2 服务端 Tab

**服务配置区：**
- 端口输入框 + 启动 / 停止按钮
- 服务状态指示（运行中 = 绿色，停止 = 灰色）

**已连接客户端列表：**
- 表格展示：客户端 ID、IP 地址、连接时间
- 支持单选（选中后向该客户端发送）
- 支持多选（勾选多个客户端）
- 包含"全选"复选框
- 未选中任何客户端时，"发送"按钮禁用

**消息发送区：**
- 文本输入框
- "发送"按钮 — 向选中的客户端（一个或多个）发送消息
- "广播"按钮 — 向全部已连接客户端发送消息（无需选中，直接发送）

**消息日志区：**
- 同客户端格式
- 服务启动/停止事件、客户端连接/断开事件也输出日志

**持久化：**
- 端口号保存到 localStorage

### 4.3 日志格式

```
[14:30:05] [连接] 已连接到 ws://localhost:8080
[14:30:10] [发送] Hello Server
[14:30:12] [接收] Hello Client
[14:30:20] [断开] 连接已关闭: code=1000
[14:30:25] [错误] 连接失败: Connection refused
```

## 5. UI 布局

参考截图，整体采用左右分栏布局：

```
┌────────────────────────────────────────────────┐
│  [客户端] [服务端]                              │
├────────────────────────────────────────────────┤
│  ┌──────────────┐  ┌────────────────────────┐  │
│  │  配置区       │  │                        │  │
│  │  URL + 按钮   │  │   消息日志              │  │
│  │              │  │                        │  │
│  │  消息发送区   │  │   [14:30:05] [发送]...  │  │
│  │  输入框+按钮  │  │   [14:30:12] [接收]...  │  │
│  │              │  │                        │  │
│  │  （服务端Tab  │  │                        │  │
│  │   还多一个    │  │                        │  │
│  │   客户端列表）│  │                        │  │
│  └──────────────┘  └────────────────────────┘  │
└────────────────────────────────────────────────┘
```

- 左侧固定宽度约 360px，右侧自适应
- 日志区域垂直占满，内容超出时自动滚动

## 6. 项目结构

```
WebsocketTool/
├── package.json
├── vite.config.ts
├── electron/
│   ├── main.ts          — Electron 主进程
│   └── preload.ts       — 预加载脚本（contextBridge）
├── src/
│   ├── main.tsx          — React 入口
│   ├── App.tsx           — 顶层布局 + Tab
│   ├── App.css           — 全局样式
│   ├── components/
│   │   ├── ClientTab.tsx
│   │   ├── ServerTab.tsx
│   │   └── LogPanel.tsx
│   ├── hooks/
│   │   └── useWebSocket.ts  — 客户端 WebSocket hook
│   └── types/
│       └── index.ts         — 公共类型定义
└── index.html
```

## 7. 边界情况与错误处理

| 场景 | 处理方式 |
|------|----------|
| 客户端连接失败 | 日志输出错误信息，状态变为"已断开" |
| 服务端连接异常断开 | 日志输出关闭原因，从客户端列表移除 |
| 服务端端口被占用 | 日志输出错误，服务启动失败 |
| 向已断开的客户端发消息 | 日志输出错误，客户端自动从列表移除 |
| 未连接时点击发送 | 发送按钮禁用 |
| 服务未启动时操作 | 发送按钮和客户端列表不可用 |
| 消息内容为空 | 不发送，忽略 |
| 输入非法 URL | 连接时校验，不合法则提示 |

## 8. 不包含的内容

- 消息加密 / WSS 证书管理
- 消息历史记录导出
- 多标签页 / 多窗口
- 二进制消息支持
- 国际化
- 自动更新