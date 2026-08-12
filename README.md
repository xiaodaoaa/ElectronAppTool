# ElectronAppTool

桌面工具集合 monorepo — 七个 Electron 应用 + 一个 .NET/WPF 应用，各自独立依赖与构建。

> 无根目录 `package.json`。每个子项目独立，请先 `cd` 到对应目录再执行命令。

## 子项目

| 子项目 | 技术栈 | Win7 | 用途 |
|--------|--------|:----:|------|
| [EHttpServerTool](./EHttpServerTool/) | Electron 33 + React + TS + Ant Design | ❌ | Mock HTTP 服务端 |
| [EWebsocketTool](./EWebsocketTool/) | Electron 22 + React + TS + Ant Design | ✅ | WebSocket 客户端 + 服务端 |
| [EWebsocketMan](./EWebsocketMan/) | Electron 22 + Vue 3 + Vite | ✅ | WebSocketMan 复刻 |
| [ERabbitMQTool](./ERabbitMQTool/) | Electron 22 + React + TS + `amqplib` | ✅ | RabbitMQ 调试 |
| [ERabbitMQToolPlus](./ERabbitMQToolPlus/) | Electron 43 + Vue 3 + Element Plus + electron-vite | ❌ | RabbitMQ 调试增强版 |
| [EActiveMQTool](./EActiveMQTool/) | Electron 43 + Vue 3 + Element Plus + `@stomp/stompjs` | ❌ | ActiveMQ 调试（STOMP over TCP/WS） |
| [EKafkaTool](./EKafkaTool/) | Electron 33 + Vue 3 + Element Plus + `kafkajs` | ❌ | Kafka 教学演示 |
| [CSNtpd](./CSNtpd/) | .NET 10 + WPF + xUnit | ❌ | NTP 时间同步（客户端 + 服务端） |

## 快速开始

Electron 项目（在对应目录内）：

```bash
npm install
npm run dev          # 开发（Vite + Electron HMR）
npm run pack         # 打包 Windows NSIS 安装包（EKafkaTool 用 npm run package:win）
```

CSNtpd（.NET 项目）：

```bash
cd CSNtpd
dotnet restore
dotnet run --project src/NtpTool.App                          # 启动
dotnet publish src/NtpTool.App -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true  # 绿色单文件
```

## 架构

**Electron 项目**统一三进程模型：主进程持有所有网络逻辑（http/ws/Kafka/STOMP），渲染进程仅通过 `window.electronAPI`/`window.api`/`window.kafkaApi` 经 IPC 通信，`contextIsolation: true`。三种构建方式：独立 `electron/` 目录（前四个项目）、`vite-plugin-electron`（EWebsocketMan）、`electron-vite`（后三个项目）。

**CSNtpd** 为三层 + 依赖注入架构：`NtpTool.App`（WPF/MVVM）→ `NtpTool.Core`（NTP 协议与业务接口）→ `NtpTool.Infrastructure`（JSON 配置/文件日志/Win32 系统时间），xUnit 测试覆盖 Core 与 Infrastructure。

Win7 兼容的三个项目（EWebsocketTool/EWebsocketMan/ERabbitMQTool）使用 Electron 22.3.27，`pack` 脚本含 `-c.electronDist`/`-c.electronVersion` 强制本地二进制。

详见各子项目 `CLAUDE.md` / `AGENTS.md` / `docs/`。

## 许可证

MIT
