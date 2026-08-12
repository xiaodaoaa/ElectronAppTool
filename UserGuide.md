# ElectronAppTool

桌面工具集合，由八个独立子项目组成的 monorepo — 其中七个为 Electron 应用，一个为 .NET/WPF 应用。

> **注意**：这是 monorepo，**没有根目录 `package.json`**。每个子项目有独立的依赖、构建工具和运行脚本。请先 `cd` 到对应目录再执行命令。

---

## 项目一览

### Electron 工具（七个）

| 子项目 | 技术栈 | Win7 兼容 | 用途 |
|--------|--------|-----------|------|
| [EHttpServerTool](./EHttpServerTool/) | Electron 33 + React 18 + TS + Ant Design 5 + Vite 6 | ❌ | Mock HTTP 服务端 — 定义路径/方法响应、Echo 模式、请求日志记录 |
| [EWebsocketTool](./EWebsocketTool/) | Electron 22 + React 18 + TS + Ant Design 5 + Vite 6 | ✅ | WebSocket 客户端(Browser API) + 服务端(`ws` 库) 双模式调试工具 |
| [EWebsocketMan](./EWebsocketMan/) | Electron 22 + Vue 3 (Composition API) + Vite 5 + `ws` | ✅ | 复刻 WebSocketMan v1.0.9 — WS 服务端/客户端双模式 |
| [ERabbitMQTool](./ERabbitMQTool/) | Electron 22 + React 18 + TS + Ant Design 5 + Vite 6 + `amqplib` | ✅ | RabbitMQ 调试工具 — 连接管理、生产者发布、消费者订阅（队列/交换机模式）、SSL 支持 |
| [ERabbitMQToolPlus](./ERabbitMQToolPlus/) | Electron 43 + Vue 3 + Element Plus + Pinia + electron-vite + `amqplib` | ❌ | RabbitMQ 调试工具增强版 — 三进程架构、连接/生产者/消费者单例服务、配置加密持久化 |
| [EActiveMQTool](./EActiveMQTool/) | Electron 43 + Vue 3 + Element Plus + Pinia + electron-vite + `@stomp/stompjs` | ❌ | ActiveMQ 调试工具 — STOMP over TCP/WebSocket 双模式、连接管理、生产者发布、消费者订阅、JMS selector 支持 |
| [EKafkaTool](./EKafkaTool/) | Electron 33 + Vue 3 + Element Plus + Pinia + electron-vite + `kafkajs` | ❌ | Kafka 教学演示工具 — 连接管理、Topic 管理、生产者发布、消费者订阅、6 个教学演示场景、消息重放 |

### .NET 工具（一个）

| 子项目 | 技术栈 | Win7 兼容 | 用途 |
|--------|--------|-----------|------|
| [CSNtpd](./CSNtpd/) | .NET 10 + WPF + xUnit | ❌ | NTP 时间同步工具 — NTP 客户端（向上游同步）+ NTP 服务端（局域网授时）、图形化设置、系统托盘、系统时间修正 |

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

### ERabbitMQTool

```bash
cd ERabbitMQTool
npm install
npm run dev       # Vite + Electron，HMR 热重载
npm run build     # Vite 生产构建 → dist/
npm run preview   # 预览 Vite 构建产物
npm run pack      # 打包 Windows 安装包（使用本地 Electron 22 二进制）
```

### ERabbitMQToolPlus

```bash
cd ERabbitMQToolPlus
npm install
npm run dev          # electron-vite 三进程热更新（main/preload/renderer）
npm run typecheck    # tsc(node) + vue-tsc(web) 两段类型检查（改完代码必须跑）
npm run build        # electron-vite 构建产物 → out/（不启动窗口）
npm run preview      # 预览 out/ 构建产物
npm run pack         # electron-vite build + electron-builder --win → release/ NSIS 安装包
```

### EActiveMQTool

```bash
cd EActiveMQTool
npm install
npm run dev          # electron-vite 三进程热更新（main/preload/renderer）
npm run typecheck    # tsc(node) + vue-tsc(web) 两段类型检查
npm run build        # electron-vite 构建产物 → out/
npm run pack         # electron-vite build + electron-builder --win → release/ NSIS 安装包
```

### EKafkaTool

```bash
cd EKafkaTool
npm install
npm run dev          # electron-vite 三进程热更新（main/preload/renderer）
npm run typecheck    # vue-tsc 类型检查（单段，仅 web 侧）
npm run lint         # eslint（.ts/.vue）
npm run build        # electron-vite 构建产物 → out/
npm run preview      # 预览 out/ 构建产物
npm run package:win  # electron-vite build + electron-builder --win → release/ NSIS 安装包
```

本地 Kafka（教学演示用）：`docker compose -f docker/docker-compose.yml up -d` 启动单节点 Kafka 3.9.0（KRaft 模式，端口 9092），连接配置 brokers 填 `localhost:9092`。

> 如遇 `Electron uninstall` 错误或 `node_modules/electron/dist/electron.exe` 缺失，手动跑 `node node_modules/electron/install.js` 下载二进制。

### CSNtpd

```bash
cd CSNtpd
dotnet restore                        # 还原 NuGet 依赖
dotnet build NtpTool.slnx             # 构建全部项目（App/Core/Infrastructure/Tests）
dotnet test NtpTool.slnx              # 运行 xUnit 单元/集成测试
dotnet run --project src/NtpTool.App  # 启动 WPF 应用（开发调试）

# 绿色单文件发布（自包含 .NET 运行时，目标机无需安装依赖，双击即用）
dotnet publish src/NtpTool.App -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

> 产物位于 `src/NtpTool.App/bin/Release/net10.0-windows/win-x64/publish/NtpTool.App.exe`。修改系统时间或监听 123 端口需管理员权限。详见 `CSNtpd/docs/USER_GUIDE.md`。

---

## 命令说明

### Electron 项目（npm 脚本）

| 命令 | 适用项目 | 说明 |
|------|----------|------|
| `npm run dev` | 全部 | Vite dev server（port 5173, strictPort）+ wait-on 后启动 Electron |
| `npm run build` | 全部 | Vite 构建输出到 `dist/` |
| `npm run preview` | 全部 | 预览 Vite 构建产物 |
| `npm run pack` | 全部（EKafkaTool 为 `package:win`） | Vite 构建 → electron-builder 打包 Windows NSIS 安装包 |
| `npm run typecheck` | ERabbitMQToolPlus / EActiveMQTool / EKafkaTool | 类型检查（前两者 tsc + vue-tsc 两段，EKafkaTool 仅 vue-tsc 单段） |

### .NET 项目（dotnet CLI）

| 命令 | 适用项目 | 说明 |
|------|----------|------|
| `dotnet restore` | CSNtpd | 还原 NuGet 依赖 |
| `dotnet build NtpTool.slnx` | CSNtpd | 构建解决方案（App/Core/Infrastructure/Tests） |
| `dotnet test NtpTool.slnx` | CSNtpd | 运行 xUnit 测试（Core.Tests + Infrastructure.Tests） |
| `dotnet run --project src/NtpTool.App` | CSNtpd | 启动 WPF 应用 |
| `dotnet publish ... --self-contained` | CSNtpd | 生成绿色单文件 exe（含运行时） |

### dev 机制

`dev` 脚本均使用 `concurrently` + `wait-on`：Vite 先启动，Electron 等待 `http://localhost:5173` 就绪后加载 dev server（通过 `VITE_DEV_SERVER_URL` 环境变量），而非构建产物 `dist/index.html`。Vite 配置了 `strictPort: true`，如果 5173 被占用会直接报错（不会回退到其他端口导致 Electron 挂起）。

> electron-vite 项目（ERabbitMQToolPlus / EActiveMQTool / EKafkaTool）使用 `electron-vite dev` 统一管理三进程 HMR，不依赖 concurrently + wait-on。

### pack 命令差异

- **EWebsocketTool**、**EWebsocketMan** 和 **ERabbitMQTool**（Win7 兼容）：pack 命令包含 `-c.electronDist="node_modules/electron/dist" -c.electronVersion="22.3.27"`，强制 electron-builder 使用本地安装的 Electron 22 二进制。
- **EHttpServerTool**（无需 Win7）：pack 命令不含 electronDist 覆盖，electron-builder 自动使用 `node_modules` 中的 Electron 版本。
- **ERabbitMQToolPlus / EActiveMQTool**：`pack` 为 `electron-vite build && electron-builder --win`，无需 electronDist 覆盖。
- **EKafkaTool**：`package:win` 为 `electron-vite build && electron-builder --win`，无需 electronDist 覆盖。

---

## 技术架构

### Electron 三进程模型（七个 Electron 项目通用）

```
Main Process
├── 管理 BrowserWindow
├── 持有所有网络服务端逻辑（http / ws / WebSocket / Kafka / STOMP）— 渲染进程不直接接触网络
├── 注册 ipcMain.handle() 处理请求/响应
└── 通过 webContents.send() 推送异步事件到渲染进程

Preload
├── contextBridge.exposeInMainWorld('electronAPI'/'api'/'kafkaApi', …)
├── contextIsolation: true, nodeIntegration: false
└── 每个事件监听器均返回一个 unsubscribe 清理函数

Renderer (React TSX / Vue 3)
└── 仅通过 window.electronAPI / window.api / window.kafkaApi 与主进程通信，绝不直接 import Node/Electron 模块
```

### 三种构建方式

**方式 A — 独立 electron/ 目录**（EHttpServerTool、EWebsocketTool、ERabbitMQTool）：
- 主进程/预加载源码位于 `electron/main.js` 和 `electron/preload.js`
- Vite **只构建渲染进程**（`src/` → `dist/`）
- `package.json` 的 `"main": "electron/main.js"` — Electron 直接加载源码
- electron-builder 的 `files` 配置包含 `electron/**/*`

**方式 B — vite-plugin-electron**（EWebsocketMan）：
- 主进程/预加载源码位于 `src/main/main.js` 和 `src/preload/preload.js`
- Vite 构建**全部三部分**：渲染进程 → `dist/`，主进程 → `dist-electron/main.js`，预加载 → `dist-electron/preload.js`
- `package.json` 的 `"main": "dist-electron/main.js"` — Electron 加载构建产物
- electron-builder 的 `files` 配置包含 `dist-electron/**/*`

**方式 C — electron-vite**（ERabbitMQToolPlus、EActiveMQTool、EKafkaTool）：
- 主进程/预加载/渲染源码分别位于 `src/main/`、`src/preload/`、`src/renderer/`
- electron-vite 统一构建三进程：main → `out/main/`，preload → `out/preload/`，renderer → `out/renderer/`
- `package.json` 的 `"main": "./out/main/index.js"`（EKafkaTool 为 `./out/main/index.mjs`）— Electron 加载构建产物
- electron-builder 的 `files` 配置包含 `out/**/*`
- 跨进程共享类型：ERabbitMQToolPlus/EActiveMQTool 放 `src/shared/`，三端通过 `@shared/*` 别名引用；EKafkaTool 放 `src/main/kafka/types.ts`，preload/renderer 用相对路径引用（无 `src/shared/` 目录）

### IPC 模式

所有项目使用同一套 IPC 规范，通过 `window.electronAPI`（前四个项目）、`window.api`（ERabbitMQToolPlus/EActiveMQTool）或 `window.kafkaApi`（EKafkaTool）通信：

**请求/响应** — `ipcRenderer.invoke(channel, …args)` ↔ `ipcMain.handle(channel, handler)`：
- 用于主动操作：启动/停止服务、保存/加载配置、发送消息、文件操作

**事件推送** — `ipcRenderer.on(channel, handler)`（主进程 → 渲染进程）：
- 用于异步通知：服务启动/停止、客户端连接/断开、消息到达、错误发生
- **关键**：每个事件监听注册返回一个取消订阅函数，**组件卸载时必须调用**（React `useEffect` return / Vue `onUnmounted`），否则标签页切换会导致监听器重复累积，造成消息重复处理和内存泄漏。

**安全守卫**：主进程永远不假定 `mainWindow` 存活。全部四个项目都有 `sendToRenderer()` 包装函数，调用前做 `mainWindow && !mainWindow.isDestroyed()` 空值检查。

### Windows 7 兼容

EWebsocketTool、EWebsocketMan 和 ERabbitMQTool 使用 **Electron 22.3.27**（最后一个支持 Windows 7 的版本，Electron 23+ 已放弃 Win7 支持）。适配只需两处改动：
1. `package.json` 中 electron 版本指定为 `^22.3.27`
2. `pack` 脚本添加 `-c.electronDist` 和 `-c.electronVersion` 参数

ERabbitMQToolPlus 使用 Electron 43，**不支持 Win7**。EActiveMQTool 同样使用 Electron 43，不支持 Win7。EKafkaTool 使用 Electron 33，不支持 Win7。

### IPv4 地址标准化

三个网络调试工具统一对 `remoteAddress` 做 IPv6 映射前缀剥离：
- `::ffff:127.0.0.1` → `127.0.0.1`
- `::1` → `127.0.0.1`
- 纯 IPv6 地址被过滤/置 null

### 配置持久化

配置（服务设置、连接 URL）以 JSON 文件形式保存到 `app.getPath('userData')`。EWebsocketMan 有 300ms 防抖自动保存；EHttpServerTool 使用 `PathConfigManager` 类管理；ERabbitMQTool 保存连接配置（host/port/vhost/凭据/SSL 选项）到 `config.json`。ERabbitMQToolPlus/EActiveMQTool 使用 `electron-store` + AES-256-CBC 加密密码。EKafkaTool 用 JSON 文件（`connections.json`）+ `safeStorage` 加密密码（不可用时 base64 兜底），**不使用 electron-store**。

### CSNtpd 架构（.NET/WPF，独立于 Electron 体系）

CSNtpd 采用经典的分层 + 依赖注入架构，与七个 Electron 项目的技术栈完全不同：

```
NtpTool.App（WPF 启动层）
├── MVVM：MainViewModel / SettingsViewModel + ObservableObject / RelayCommand 基类
├── CompositionRoot：Microsoft.Extensions.DependencyInjection 依赖注入容器
├── Services：SettingsService（设置加载）、TrayIcon（系统托盘）
└── 引用 Core + Infrastructure

NtpTool.Core（核心业务层，无 UI 依赖）
├── Ntp/：NtpPacket（RFC 4330 报文）、NtpPacketCodec（编解码）、NtpQueryClient（UDP 查询）、TimeCalculator（时间偏差计算）
├── Services/：INtpClientService / INtpServerService / ISystemTimeService 等接口 + 实现（NtpClientService / NtpServerService / SyncScheduler / NetworkAccessController / ConfigValidator）
├── Models/：AppSettings / NtpClientOptions / NtpServerOptions / NtpSyncResult 等配置与结果模型
└── Logging/：IAppLogger / LogLevel 抽象

NtpTool.Infrastructure（基础设施层）
├── Config/：JsonConfigurationRepository（JSON 配置读写，实现 Core 的 IConfigurationRepository）
├── Logging/：FileLogger（文件日志 + 滚动 + 保留期，实现 Core 的 IAppLogger）
└── Windows/：WindowsSystemTimeService（调用 Win32 API 修改系统时间，实现 Core 的 ISystemTimeService）

tests/（xUnit 测试）
├── NtpTool.Core.Tests：NtpPacketCodec / TimeCalculator / ConfigValidator / NetworkAccessController / SyncScheduler / NtpServerIntegrationTests
└── NtpTool.Infrastructure.Tests：FileLogger / JsonConfigurationRepository
```

**关键设计**：
- **客户端 + 服务端一体**：既能向上游 NTP 服务器同步时间（UDP 123），也能作为 NTP 服务端为局域网授时
- **系统时间修正**：通过 `WindowsSystemTimeService` 调用 Win32 `SetSystemTime` API，需管理员权限；大偏差（超过 `maxAllowedOffsetMs`）不会自动修改，需人工确认
- **单实例**：通过互斥锁保证同一时间只运行一个实例，重复启动激活已有窗口
- **系统托盘**：关闭主窗口最小化到托盘，托盘菜单提供同步/启停服务端/设置/退出
- **配置**：`ntp-tool-config.json`（程序目录），图形化设置界面（Windows 11 风格，客户端/服务端/日志三页）
- **限流与白名单**：服务端支持每 IP 每分钟限流、CIDR 白名单、请求日志记录

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

### ERabbitMQToolPlus — RabbitMQ 调试工具增强版

基于 `amqplib` 的 RabbitMQ 调试工具，使用 electron-vite 三进程架构，是 ERabbitMQTool 的重构增强版：

- **连接管理**：`ConnectionManager` 单例管理 amqp/amqps 连接，SSL 可选，断开时自动清理所有消费者订阅
- **生产者**：`ProducerService` 单例，支持向队列/交换机发布消息，消息属性完整（persistent/contentType/priority/headers 等）
- **消费者**：`ConsumerService` 单例，支持队列消费和交换机订阅（direct/topic/fanout），binding key 可配置
- **配置持久化**：`electron-store` 保存连接/生产者/消费者配置，密码用 AES-256-CBC 加密；操作成功时保存，渲染进程 onMounted 主动拉取
- **日志面板**：实时显示发送/接收/错误事件，每条日志带时间戳，支持右键清空
- **类型安全**：`src/shared/types.ts` 跨进程共享类型，`npm run typecheck` 做 tsc + vue-tsc 两段检查

**结构**：采用方式 C（electron-vite），主进程业务逻辑拆分到 `src/main/services/`（三个单例服务），`src/main/ipc/` 只做参数转发，`src/main/utils/` 是工具（ssl/store/logger）。渲染进程 Vue 3 + Element Plus + Pinia，`stores/` 调 `window.api`，`views/` 管页面状态，`components/` 是复用组件。

### EActiveMQTool — ActiveMQ 调试工具

基于 `@stomp/stompjs` 的 ActiveMQ 消息队列调试工具，使用 electron-vite 三进程架构，参照 ERabbitMQToolPlus 设计：

- **连接管理**：`ConnectionManager` 单例管理 STOMP 连接，支持 **TCP 原生连接**（通过 `net.Socket` 适配器）和 **WebSocket 连接**（ws/wss），SSL 可选，断开时自动清理消费者订阅
- **生产者**：`ProducerService` 单例，支持向 Queue（`/queue/xxx`）和 Topic（`/topic/xxx`）发送消息，支持 persistent/priority/expires 等 STOMP 头、批量发送
- **消费者**：`ConsumerService` 单例，支持 auto/client/client-individual 三种 ACK 模式、JMS selector 消息选择器、prefetch 预取控制、消息过滤（routingKey 通配符 + header 键值匹配）
- **配置持久化**：`electron-store` 保存连接/生产者/消费者配置，密码用 AES-256-CBC 加密
- **日志面板**：实时显示连接/发送/接收/错误事件

**结构**：采用方式 C（electron-vite），主进程业务逻辑拆分到 `src/main/services/`（ConnectionManager/ProducerService/ConsumerService），`src/main/ipc/` 只做参数转发，`src/main/utils/` 包含 TCP socket 适配器（`tcp-socket.ts`）、store、logger。渲染进程 Vue 3 + Element Plus + Pinia。

### EKafkaTool — Kafka 教学演示工具

基于 `kafkajs` 的 Kafka 教学演示桌面工具，使用 electron-vite 三进程架构，面向 Kafka 概念学习与可视化演示：

- **连接管理**：`KafkaClientManager` 单例管理 Kafka 连接（kafkajs），支持 SASL（plain/scram-sha-256/scram-sha-512）和 SSL，连接配置持久化到 `connections.json`（`app.getPath('userData')`），密码用 `safeStorage` 加密（不可用时 base64 兜底）
- **Topic 管理**：listTopics/topicDetail/createTopic/deleteTopic，查看分区、leader、replicas、isr、earliest/latest offset
- **生产者**：`ProducerService` 支持单条/批量发送，批量通过 `AbortController` 可停止；消息模板支持 `{{seq}}`/`{{ts}}`/`{{rand}}` 占位符；key 策略（fixed/random/round-robin）
- **消费者**：`ConsumerService` 多实例管理，支持 pause/resume/seek/commit；`MessageBuffer` 批量缓冲（100 条或 200ms 触发 flush）通过 `event:consumerMessage` 批量推送，避免高频消息逐条 IPC
- **演示场景**：`DemoScenarioService` + `src/main/data/scenarios.ts` 定义 6 个教学场景（S1-S6）：第一条消息、分区与 Key、消费组负载均衡、再均衡、消息重放、顺序性与 acks；通过 `event:scenarioStep` 推送进度
- **本地 Kafka**：`docker/docker-compose.yml` 启动单节点 Kafka 3.9.0（KRaft 模式，端口 9092）
- **日志**：`electron-log`，文件 + 控制台双输出

**结构**：采用方式 C（electron-vite），主进程业务逻辑在 `src/main/kafka/`（KafkaClientManager 单例 + ProducerService/ConsumerService/DemoScenarioService），`src/main/ipc/` 只做参数转发（`registerHandlers.ts` 统一注册，`channels.ts` 集中定义通道名），`src/main/store/` 连接配置持久化，`src/main/util/` 工具（logger/messageBuffer），`src/main/data/` 演示场景定义。渲染进程 Vue 3 + Element Plus + Pinia + vue-router，`stores/` 调 `window.kafkaApi`，`views/` 管页面状态（连接/集群/生产者/消费者/可视化/演示场景/设置），`components/` 复用组件，`api/kafkaApi.ts` 是类型封装。共享类型在 `src/main/kafka/types.ts`（preload/renderer 相对路径引用，无 `src/shared/`）。

> 详见 `EKafkaTool/AGENTS.md`。

### CSNtpd — NTP 时间同步工具

基于 .NET 10 + WPF 的 NTP 时间同步桌面工具，同时具备 NTP 客户端和 NTP 服务端能力，面向 Windows 10/11（不支持 Win7）：

- **NTP 客户端**：向上游 NTP 服务器（如 `time.windows.com`、`pool.ntp.org`、局域网 `192.168.x.x`）发起 UDP 123 同步请求，按 priority 升序尝试多服务器；支持自动定时同步、失败重试、超时控制
- **NTP 服务端**：监听 UDP 端口（标准 123 或自定义）为局域网客户端授时；支持 Stratum/Reference ID 配置、每 IP 限流、CIDR 白名单、请求日志
- **系统时间修正**：按同步结果偏差调整本机时间（Win32 `SetSystemTime` API，需管理员）；大偏差不自动修改需人工确认
- **图形化设置**：Windows 11 风格设置界面（左侧导航 + 右侧卡片），客户端/服务端/日志三页，所见即所得
- **系统托盘**：关闭主窗口最小化到托盘，托盘菜单快捷操作（同步/启停服务端/设置/退出）
- **单实例**：互斥锁保证只运行一个实例，重复启动激活已有窗口
- **日志**：文件日志 + 滚动（按大小）+ 保留期清理，可选 UDP 详细日志

**结构**：三层架构（App/Core/Infrastructure）+ 依赖注入（`CompositionRoot` 用 `Microsoft.Extensions.DependencyInjection`），MVVM 模式（`ObservableObject`/`RelayCommand` 基类）。详见 `CSNtpd/docs/USER_GUIDE.md` 和 `CSNtpd/docs/CSNtpd需求设计文档.md`。

### NStringTool — Notepad++ 字符串转义插件

Notepad++ 字符串转义/去转义插件（C++ + CMake），支持 C/C++、JSON、HTML/XML、URL 四种格式：

- 在"插件 → NStringTool"菜单下提供 8 个操作（每种格式转义/去转义）
- 有选中文本时转换选区，无选区时转换整个文档；每次转换可一次 Ctrl+Z 撤销
- 转换核心层有单元测试（`ConverterTests`）
- 成果物 `NStringTool.dll`（x64，PE32+），复制到 `<Notepad++>\plugins\NStringTool\` 即可使用

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

### 方式 C（electron-vite）

```
ERabbitMQToolPlus/
├── src/
│   ├── main/            # 主进程
│   │   ├── index.ts     # 入口 — 窗口创建 + IPC 注册
│   │   ├── services/    # 业务逻辑单例（ConnectionManager/ProducerService/ConsumerService）
│   │   ├── ipc/         # IPC 处理器（只做参数转发）
│   │   └── utils/       # 工具（ssl/store/logger）
│   ├── preload/
│   │   └── index.ts     # 预加载 — contextBridge 暴露 api
│   ├── renderer/        # Vue 3 渲染进程
│   │   └── src/
│   │       ├── main.ts  # Vue 入口
│   │       ├── App.vue  # 根组件
│   │       ├── views/   # 页面（ProducerView/ConsumerView/SettingsView）
│   │       ├── components/  # 复用组件
│   │       └── stores/  # Pinia stores
│   └── shared/
│       └── types.ts     # 跨进程共享类型
├── electron.vite.config.ts  # electron-vite 配置
├── package.json
└── AGENTS.md            # 子项目专属 agent 指引
```

### .NET 项目（CSNtpd）

```
CSNtpd/
├── NtpTool.slnx                    # 解决方案（.slnx 格式）
├── ntp-tool-config.json.sample     # 配置示例
├── res/
│   └── app.ico                     # 应用图标
├── docs/                            # 设计文档 + 用户指南
├── src/
│   ├── NtpTool.App/                # WPF 启动层（MVVM + DI + 托盘）
│   │   ├── App.xaml(.cs)           # WPF 入口
│   │   ├── CompositionRoot.cs      # 依赖注入容器
│   │   ├── MainViewModel.cs        # 主窗口 VM
│   │   ├── MainWindow.xaml(.cs)    # 主窗口
│   │   ├── SettingsViewModel.cs    # 设置 VM
│   │   ├── SettingsWindow.xaml(.cs)# 设置窗口
│   │   ├── Mvvm/                   # ObservableObject / RelayCommand 基类
│   │   ├── Services/               # SettingsService / TrayIcon
│   │   └── Converters/             # HexBrushConverter
│   ├── NtpTool.Core/              # 核心业务层（无 UI 依赖）
│   │   ├── Ntp/                    # NtpPacket / NtpPacketCodec / NtpQueryClient / TimeCalculator
│   │   ├── Services/              # 接口 + 实现（NtpClient/Server/SyncScheduler/NetworkAccess/ConfigValidator）
│   │   ├── Models/                # AppSettings / NtpClientOptions / NtpServerOptions / NtpSyncResult
│   │   └── Logging/               # IAppLogger / LogLevel
│   └── NtpTool.Infrastructure/    # 基础设施层
│       ├── Config/                # JsonConfigurationRepository
│       ├── Logging/                # FileLogger
│       └── Windows/               # WindowsSystemTimeService（Win32 API）
└── tests/
    ├── NtpTool.Core.Tests/         # xUnit：编解码/时间计算/配置校验/网络控制/调度/服务端集成
    └── NtpTool.Infrastructure.Tests/ # xUnit：文件日志/配置仓储
```

### C++ 项目（NStringTool）

```
NStringTool/
├── CMakeLists.txt                 # CMake 构建配置（VS 2015 Win64 / VS 2022 x64）
├── README.md                      # 插件说明与构建/安装指引
├── sdk/                           # Notepad++ Plugin SDK 头文件
│   ├── PluginInterface.h
│   ├── Notepad_plus_msgs.h
│   ├── Scintilla.h
│   └── Sci_Position.h
├── src/
│   ├── PluginEntry.cpp            # 插件入口（DLL 导出 + 菜单注册）
│   ├── EditorOps.cpp/.h           # Scintilla 编辑器操作（选区/文档读写、撤销）
│   ├── Converters.h               # 转换器接口
│   └── converters/                # 四种格式转换实现
│       ├── CppConverter.cpp/.h
│       ├── JsonConverter.cpp/.h
│       ├── HtmlConverter.cpp/.h
│       └── UrlConverter.cpp/.h
├── tests/                          # 转换核心单元测试
│   ├── test_main.cpp
│   ├── test_converters.cpp
│   └── test_macros.h
└── build64/bin/Release/
    └── NStringTool.dll            # 最终成果物（x64 PE32+）
```

---

## 开发说明

- 修改共享工具配置（vite config、electron-builder、dev 启动脚本）时，**EHttpServerTool、EWebsocketTool 和 ERabbitMQTool** 脚手架几乎一致，通常三处需同步更新。EWebsocketMan 配置独立。
- **CSNtpd** 是唯一的 .NET 项目，使用 `dotnet` CLI 而非 npm，构建/测试/运行命令独立，与 Electron 项目无共享配置。修改时注意三层（App/Core/Infrastructure）的依赖方向：App → Core + Infrastructure，Infrastructure → Core，测试项目引用对应被测层。
- **NStringTool** 是唯一的 C++ 项目，使用 CMake + Visual Studio 构建，产物为 DLL（非可执行）。VS 2015 默认生成器是 Win32，必须用 `Win64` 变体或 `-A x64` 指定 64 位，否则 Win32 DLL 无法加载到 64 位 Notepad++。
- 设计文档和决策记录位于各子项目的 `docs/`、`docs/superpowers/` 和 `.superpowers/sdd/` 目录，供追溯历史决策。

---

## 许可证

MIT
