# SSHTunnelProxy

Windows 桌面 SSH 隧道代理软件：在本地启动 SOCKS5 与 HTTP 代理监听端口，所有经过代理的流量通过 SSH 加密隧道（direct-tcpip）转发到远程目标。

适用于安全代理上网、内网穿透等场景。

## 功能特性

- **双协议代理**：本地同时监听 SOCKS5（默认 1080）与 HTTP（默认 8118）代理端口，共用同一条 SSH 隧道
- **多隧道并行**：可同时运行多个 SSH 隧道实例，独立启停
- **断线自动重连**：指数退避 5→10→20→40→60 秒，支持无限重连
- **流量统计**：实时上传/下载速率（5 秒滑动窗口）+ 累计字节 + 活跃连接数
- **连接日志**：SQLite 记录每次代理连接的目标、时间、字节数（仅元数据，不含传输内容）
- **认证**：SSH 密码 / 私钥（含 Passphrase）/ 键盘交互；代理层可选用户名密码认证
- **安全存储**：密码、私钥 Passphrase 等敏感字段用 Windows DPAPI 加密；主机密钥 TOFU 信任
- **系统托盘**：关闭/最小化收进托盘、双击托盘恢复窗口、托盘菜单快捷启停隧道
- **开机自启**：可选注册到系统启动项（注册表）
- **启动即最小化**：可选启动后直接驻留托盘，不显示主窗口
- **自动恢复连接**：退出时记录仍处于已连接状态的隧道，下次启动自动连接
- **单实例运行**：同一台电脑同时只能运行一个实例，重复启动会自动激活已运行的窗口（即使已最小化到托盘）
- **连接按钮智能禁用**：连接、断开、重连按钮按隧道状态动态启用/禁用，避免非法操作
- **运行态隧道保护**：处于连接状态（已连接/连接中/重连中）的隧道不可编辑、不可删除，需先断开
- **运行时长跨天显示**：连接时长超过一天时，以"X天 HH:MM:SS"格式显示

## 技术栈

| 类别 | 选型 |
| --- | --- |
| 语言/框架 | C# / .NET 10 / WPF |
| SSH 库 | SSH.NET 2026 |
| MVVM | CommunityToolkit.Mvvm（源生成器） |
| 依赖注入 | Microsoft.Extensions.DependencyInjection |
| 日志 | Serilog（文件 + Debug） |
| 数据库 | Microsoft.Data.Sqlite |
| 加密 | System.Security.Cryptography（DPAPI） |
| 托盘 | Hardcodet.NotifyIcon.Wpf |
| 测试 | xUnit + Moq + FluentAssertions |

## 构建与运行

```bash
# 构建
dotnet build SSHTunnelProxy.slnx

# 调试运行（WPF 应用）
dotnet run --project src/SSHTunnelProxy.App

# 运行测试
dotnet test

# 便携发布：自带 .NET 运行时，仅 win-x64，用户机器无需安装 .NET
dotnet publish src/SSHTunnelProxy.App/SSHTunnelProxy.App.csproj -c Release -p:Portable=true
```

> 解决方案为 `slnx` 格式，需 .NET 10 SDK（10.0.300+）。

## 架构

解决方案含三个项目，依赖单向：`App → Core`，`Tests → Core`。

```
src/
├── SSHTunnelProxy.Core/      # 业务逻辑（无 UI 依赖）
│   ├── Models/               # 数据模型
│   ├── Services/             # 隧道管理、配置、日志服务
│   ├── Proxy/                # SOCKS5 / HTTP 代理协议解析与监听
│   ├── Tunnel/               # SSH 隧道传输 + direct-tcpip Channel
│   ├── Security/             # DPAPI 加密 + 主机密钥 TOFU
│   └── Utils/                # 流桥接、强调色读取
├── SSHTunnelProxy.App/       # WPF 表现层
│   ├── Views/                # MainWindow / ConfigDialog
│   ├── ViewModels/           # Main / TunnelItem / Config / Log / Settings
│   ├── Framework/            # 托盘、日志桥接、UI 调度、开机自启
│   └── Resources/            # Win11 Fluent 控件样式
└── SSHTunnelProxy.Tests/     # 单元 + 集成测试
```

### 代理转发链路

```
代理客户端
  → Socks5ProxyServer / HttpProxyServer（监听本地端口，解析协议）
  → ISshTunnelTransport.OpenChannelAsync(host, port)
  → SshDirectTcpipChannel（ForwardedPortLocal 临时本地端口 + TcpClient 桥接）
  → SSH 隧道 → 远程目标
  → StreamRelay.RelayAsync 双向透传（任一方向 EOF 即取消另一方向）
```

> **关键设计**：不使用 SSH.NET 的 `ForwardedPortDynamic`，而是自建协议解析层 + direct-tcpip Channel 转发。理由：SOCKS5 与 HTTP 共用同一条隧道，可精确统计每条连接的流量与目标地址，便于扩展规则分流与本地认证。
>
> SSH.NET 2026 将 direct-tcpip 低层 Channel API 设为 internal，故 `SshDirectTcpipChannel` 改用公开的 `ForwardedPortLocal` 实现：绑定临时本地端口（`boundPort=0`），再用 `TcpClient` 连接该端口获得双向流。

## 数据存储

所有运行时数据写在**程序所在目录**（便携式，非 `%APPDATA%`）：

| 数据 | 文件 | 说明 |
| --- | --- | --- |
| 服务器配置 | `profiles.json` | 敏感字段 DPAPI 加密 |
| 全局设置 | `settings.json` | |
| 连接日志 | `logs.db` | SQLite |
| 已信任主机密钥 | `known_hosts.json` | TOFU 模式 |
| 应用日志 | `logs/app-.log` | Serilog 按天滚动 |

> DPAPI 加密的数据绑定当前 Windows 用户，跨机器不可迁移。

## UI

Win11 Fluent 风格，浅色单主题。系统强调色在运行时读取并注入配色键。侧边栏导航：隧道 / 日志 / 设置。
