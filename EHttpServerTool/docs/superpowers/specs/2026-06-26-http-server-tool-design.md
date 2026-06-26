# HTTP 服务端工具设计文档

## 概述

基于 Electron 的 HTTP 服务端调试工具，支持自定义请求路径、多种 HTTP 方法、自定义回复内容、echo 模式，以及完整的客户端请求信息展示。

## 技术栈

- Electron 33
- React 18 + TypeScript
- Vite 6
- Ant Design 5
- Node.js http 模块

## 架构设计

### 项目结构

```
EHttpServerTool/
├── electron/
│   ├── main.js              # 主进程入口，窗口管理
│   ├── preload.js           # contextBridge 暴露 IPC 接口
│   └── modules/
│       ├── http-server.js   # HTTP 服务器启停、请求路由
│       ├── path-config.js   # Path 配置管理（增删改查）
│       └── request-logger.js # 请求日志收集、存储、清理
├── src/
│   ├── main.tsx
│   ├── App.tsx              # 根组件，左右分栏布局
│   ├── types/index.ts       # 类型定义
│   └── components/
│       ├── PathList.tsx      # 左侧：path 列表 + 增删
│       ├── PathDetail.tsx    # 左侧：选中 path 的配置面板
│       └── RequestLog.tsx    # 右侧：请求日志面板
├── package.json
├── vite.config.ts
├── tsconfig.json
└── index.html
```

### 进程职责

- **主进程**：HTTP 服务器运行、path 配置管理、请求日志收集，全部通过 IPC 暴露给渲染进程
- **渲染进程**：纯 UI 层，通过 `window.electronAPI` 与主进程通信

## 数据结构

### Path 配置

```typescript
interface PathConfig {
  id: string;              // 唯一标识
  path: string;            // 请求路径，如 /api/users
  methods: string[];       // 支持的 HTTP 方法，如 ['GET', 'POST']
  echoEnabled: boolean;    // 是否开启 echo 模式
  responseType: 'text' | 'json' | 'xml' | 'html';
  responseContent: string; // 自定义回复内容
}
```

### 请求日志

```typescript
interface RequestLog {
  id: string;
  timestamp: number;
  clientIp: string;
  method: string;
  path: string;
  headers: Record<string, string>;
  query: Record<string, string>;
  body: string;
}
```

## IPC 通道

| 通道 | 方向 | 用途 |
|------|------|------|
| `start-server` | renderer → main | 启动 HTTP 服务器（传入端口） |
| `stop-server` | renderer → main | 停止服务器 |
| `server-started` | main → renderer | 服务器已启动 |
| `server-stopped` | main → renderer | 服务器已停止 |
| `server-error` | main → renderer | 服务器错误 |
| `add-path` | renderer → main | 添加 path 配置 |
| `update-path` | renderer → main | 更新 path 配置 |
| `delete-path` | renderer → main | 删除 path 配置 |
| `list-paths` | renderer → main | 获取所有 path 配置 |
| `new-request` | main → renderer | 新请求到达（推送日志） |
| `get-logs` | renderer → main | 获取历史日志 |
| `clear-logs` | renderer → main | 清空日志 |

## UI 布局

### 整体布局

```
┌─────────────────────────────────────────────────────────┐
│  [端口输入: 8080]  [启动/停止按钮]   状态: 运行中        │
├──────────────────────────┬──────────────────────────────┤
│  Path 列表 (左侧)        │  请求日志 (右侧)             │
│  ┌────────────────────┐  │  ┌──────────────────────────┐│
│  │ [+] 添加 Path       │  │  │ [清空日志] [自动滚动 ☑]  ││
│  │                    │  │  ├──────────────────────────┤│
│  │ • /api/users       │  │  │ 14:23:01 GET /api/users  ││
│  │ • /api/products    │  │  │ 14:23:05 POST /api/users ││
│  │ • /api/orders      │  │  │ 14:23:12 GET /api/prod.. ││
│  └────────────────────┘  │  │ ...                      ││
│                          │  └──────────────────────────┘│
│  Path 详情配置           │                              │
│  ┌────────────────────┐  │                              │
│  │ 路径: /api/users   │  │                              │
│  │ 方法: ☑GET ☑POST   │  │                              │
│  │       ☐PUT ☐DELETE  │  │                              │
│  │ Echo: [开关]        │  │                              │
│  │ 回复类型: [JSON ▼]  │  │                              │
│  │ 回复内容:           │  │                              │
│  │ ┌────────────────┐ │  │                              │
│  │ │ {"status":"ok"}│ │  │                              │
│  │ └────────────────┘ │  │                              │
│  │ [保存] [删除]      │  │                              │
│  └────────────────────┘  │                              │
└──────────────────────────┴──────────────────────────────┘
```

### 交互流程

1. **启动服务器**：输入端口号 → 点击启动 → 状态变为"运行中"
2. **添加 Path**：点击 [+] → 列表新增一项 → 右侧显示配置面板 → 填写配置 → 保存
3. **编辑 Path**：点击列表项 → 右侧显示配置 → 修改 → 保存
4. **查看请求**：请求到达 → 日志面板自动追加 → 点击日志项可展开查看完整信息（headers、query、body）
5. **Echo 模式**：开启后，该 path 的请求会返回请求本身的信息，忽略自定义回复

### 日志详情展开

点击单条日志可展开查看：

```
14:23:01 GET /api/users  [展开 ▼]
─────────────────────────────────
Client: 127.0.0.1
Headers:
  Content-Type: application/json
  User-Agent: Mozilla/5.0...
Query:
  page=1&size=10
Body:
  {"name": "test"}
```

## 错误处理

1. **端口冲突**：启动时端口被占用 → 显示错误提示"端口 XXX 已被占用，请更换端口"
2. **无效端口**：输入非数字或超出范围 → 输入框校验拦截
3. **重复 Path**：添加已存在的 path → 提示"该路径已存在"
4. **未配置 Path 收到请求**：返回 404，同时在日志中记录
5. **方法不匹配**：请求方法不在 path 配置中 → 返回 405，同时在日志中记录

## 边界情况

1. **服务器运行中修改配置**：立即生效，无需重启服务器
2. **大量请求日志**：内存中保留最近 1000 条，超出后自动清理最早的
3. **回复内容为空**：允许，返回空 body + 对应 Content-Type header
4. **Path 格式校验**：必须以 `/` 开头，不允许空路径

## 持久化

- Path 配置保存到本地文件（`userData/paths.json`），重启后自动恢复
- 日志不持久化，每次启动清空
