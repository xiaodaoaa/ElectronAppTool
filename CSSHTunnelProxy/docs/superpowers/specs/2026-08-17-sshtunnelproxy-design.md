# SSHTunnelProxy — 设计文档

> 来源：基于 `docs/需求设计文档.md`（v1.0, 2026-08-14）整理
> 设计日期：2026-08-17
> 状态：已确认

---

## 1. 项目概述

**目标**：开发一款 Windows 桌面客户端（C# / .NET 10 / WPF），通过 SSH 加密隧道同时提供 SOCKS5 和 HTTP 代理能力，实现安全代理上网和内网穿透。

**交付范围**：P0（MVP）+ P1 全部功能。

### 已确认的关键决策

| 决策项 | 结论 |
|--------|------|
| 核心方案 | 自建 SOCKS5/HTTP 协议解析层 + SSH direct-tcpip Channel 转发 |
| HTTP 首期范围 | 仅 CONNECT 隧道模式，普通 GET/POST 转发延后 |
| 启动行为 | 手动启动，不自动连接 |
| UI 完整度 | 完整（主窗口 + 配置编辑 + 日志 + 设置 + 系统托盘） |
| 技术栈 | C# 14 / .NET 10 / WPF / SSH.NET / CommunityToolkit.Mvvm / Serilog / SQLite |

---

## 2. 项目结构

```
SSHTunnelProxy.sln/
├── src/
│   ├── SSHTunnelProxy.App/          # WPF 启动项目
│   │   ├── App.xaml(.cs)            # 入口 + DI 容器注册
│   │   ├── Views/
│   │   │   ├── MainWindow.xaml      # 主窗口（侧边栏 + 隧道列表 + 详情面板 + 状态栏）
│   │   │   ├── ConfigDialog.xaml    # SSH 服务器配置编辑
│   │   │   ├── LogView.xaml         # 连接日志查看
│   │   │   └── SettingsView.xaml    # 全局设置
│   │   ├── ViewModels/
│   │   │   ├── MainViewModel.cs
│   │   │   ├── ConfigViewModel.cs
│   │   │   ├── LogViewModel.cs
│   │   │   └── SettingsViewModel.cs
│   │   ├── Converters/              # 值转换器
│   │   └── Resources/               # 主题 (Dark/Light) + 样式
│   │
│   ├── SSHTunnelProxy.Core/         # 核心业务（零 UI 依赖）
│   │   ├── Models/
│   │   │   ├── SshServerProfile.cs
│   │   │   ├── AppSettings.cs
│   │   │   ├── ConnectionLog.cs
│   │   │   └── TunnelState.cs
│   │   ├── Services/
│   │   │   ├── ITunnelManager.cs / TunnelManager.cs
│   │   │   ├── IConfigService.cs / ConfigService.cs
│   │   │   └── ILogService.cs / LogService.cs
│   │   ├── Proxy/
│   │   │   ├── ISocks5Server.cs / Socks5ProxyServer.cs
│   │   │   ├── Socks5Protocol.cs
│   │   │   ├── IHttpServer.cs / HttpProxyServer.cs
│   │   │   └── HttpParser.cs
│   │   ├── Tunnel/
│   │   │   ├── ISshTunnelTransport.cs / SshTunnelTransport.cs
│   │   │   ├── SshDirectTcpipChannel.cs
│   │   │   └── TrafficCounter.cs
│   │   ├── Security/
│   │   │   ├── DpapiProtector.cs
│   │   │   └── HostKeyVerifier.cs
│   │   └── Utils/
│   │       ├── StreamRelay.cs
│   │       └── AsyncLock.cs
│   │
│   └── SSHTunnelProxy.Tests/        # xUnit + Moq + FluentAssertions
│       ├── Unit/
│       └── Integration/
├── docs/
└── README.md
```

**分层原则**：Core 零 UI 依赖可独立测试；View 纯 XAML 绑定无业务逻辑；Service 面向接口通过 DI 注入。

---

## 3. 核心数据流

```
代理客户端
    │  SOCKS5/HTTP 请求
    ▼
[ Socks5ProxyServer / HttpProxyServer ]   ← 协议解析，提取 (targetHost, targetPort)
    │
    ▼
[ SshTunnelTransport.OpenChannelAsync() ]  ← 通过 SSH.NET Session 打开 direct-tcpip Channel
    │
    ▼
[ SSH direct-tcpip Channel ]             ← SSH 加密隧道
    │
    ▼
远程目标服务器

并行：TrafficCounter 统计双向流量 → TrafficUpdated 事件
      LogService 记录连接元数据到 SQLite
      StateChanged 事件通知 UI 更新
```

### direct-tcpip Channel 实现策略

- **主方案**：`SshClient` → 内部 `Session` → `SendChannelOpenRequest` 打开 `direct-tcpip` Channel，封装为 `SshDirectTcpipChannel`，对外暴露 `Stream`
- **备选方案**：`ForwardedPortLocal` 动态绑定临时端口（若主方案 SSH.NET 版本不兼容则回退）
- S1 阶段先做 PoC 验证

---

## 4. 关键数据模型

### SshServerProfile

```csharp
public class SshServerProfile
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Host { get; set; }
    public int Port { get; set; } = 22;
    public string Username { get; set; }
    public AuthMethod AuthMethod { get; set; }

    public string EncryptedPassword { get; set; }
    public string PrivateKeyPath { get; set; }
    public string EncryptedPassphrase { get; set; }
    public string PrivateKeyContent { get; set; }

    public int Socks5ListenPort { get; set; } = 1080;
    public int HttpListenPort { get; set; } = 8118;
    public string ListenAddress { get; set; } = "127.0.0.1";

    public bool EnableProxyAuth { get; set; }
    public string ProxyUsername { get; set; }
    public string EncryptedProxyPassword { get; set; }

    public int ConnectTimeoutSec { get; set; } = 15;
    public int KeepAliveIntervalSec { get; set; } = 30;
    public int MaxReconnectAttempts { get; set; } = -1;
    public int ReconnectDelaySec { get; set; } = 5;
}

public enum AuthMethod { Password, PrivateKey, KeyboardInteractive }
public enum TunnelState { Disconnected, Connecting, Connected, Reconnecting, Error }
```

### AppSettings

```csharp
public class AppSettings
{
    public bool StartWithWindows { get; set; } = false;
    public bool CloseToTray { get; set; } = true;
    public string Theme { get; set; } = "Dark";
    public int LogRetentionDays { get; set; } = 30;
    public Guid LastUsedProfileId { get; set; }
}
```

### ConnectionLog

```csharp
public class ConnectionLog
{
    public DateTime Timestamp { get; set; }
    public string TunnelName { get; set; }
    public string ProxyType { get; set; }      // SOCKS5 / HTTP
    public string ClientEndpoint { get; set; }
    public string TargetEndpoint { get; set; }
    public long BytesSent { get; set; }
    public long BytesReceived { get; set; }
    public TimeSpan Duration { get; set; }
    public string Status { get; set; }         // Success / Failed / Timeout
}
```

---

## 5. 关键接口

```csharp
public interface ISshTunnelTransport : IAsyncDisposable
{
    TunnelState State { get; }
    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync();
    Task<Stream> OpenChannelAsync(string host, int port, CancellationToken ct = default);
    event EventHandler<TrafficEventArgs> TrafficUpdated;
    event EventHandler<TunnelStateEventArgs> StateChanged;
}

public interface ITunnelManager
{
    Task<TunnelContext> StartTunnelAsync(SshServerProfile profile);
    Task StopTunnelAsync(Guid tunnelId);
    Task RestartTunnelAsync(Guid tunnelId);
    event EventHandler<TunnelEventArgs> TunnelStateChanged;
}

public interface IConfigService
{
    Task<IList<SshServerProfile>> LoadProfilesAsync();
    Task SaveProfilesAsync(IList<SshServerProfile> profiles);
    Task<AppSettings> LoadSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);
}

public interface ILogService
{
    Task AddConnectionLogAsync(ConnectionLog log);
    Task<IList<ConnectionLog>> QueryLogsAsync(string tunnelName, DateTime? from, DateTime? to);
    Task CleanupOldLogsAsync(int retainDays);
}
```

---

## 6. SOCKS5 代理

- 支持命令：CONNECT（✅）、BIND（❌ 返回不支持）、UDP ASSOCIATE（⚠️ 预留）
- 地址类型：IPv4 / 域名 / IPv6
- 认证：NO AUTH + USERNAME/PASSWORD，由 `EnableProxyAuth` 控制
- 协议帧解析封装为 `Socks5Protocol` 静态工具类

### SOCKS5 CONNECT 流程

```
Client ──① VER=5, METHODS──> Socks5Server
Client <──② VER=5, METHOD── Socks5Server
[可选] Client <──③ USER/PASS 认证── Socks5Server
Client ──④ VER=5, CMD=1, ATYP, DST.ADDR, DST.PORT──> Socks5Server
                          Socks5Server ──⑤ OpenChannel(target)──> SSH Tunnel ──⑥ TCP Connect──> Target
                          Socks5Server <──⑦ Channel Stream──── SSH Tunnel <──⑧ Connected── Target
Client <──⑨ VER=5, REP=0x00(success)── Socks5Server
Client <──⑩ 双向数据透传 (Relay)──> Socks5Server <──> SSH Tunnel <──> Target
```

---

## 7. HTTP 代理

首期仅实现 **CONNECT 隧道模式**。

```
Client ──① CONNECT host:443 HTTP/1.1──> HttpProxyServer
Client ──    Host: host:443─────────────> HttpProxyServer
                         HttpProxyServer ──② OpenChannel(host, 443)──> SSH Tunnel ──③ TCP Connect──> Target
                         HttpProxyServer <──④ Channel Stream──────── SSH Tunnel
Client <──⑤ HTTP/1.1 200 Connection Established── HttpProxyServer
Client <──⑥ 双向数据透传 (Relay)──> HttpProxyServer <──> SSH Tunnel <──> Target
```

---

## 8. 断线重连与保活

### 状态机

```
Disconnected ──Start()──> Connecting ──Success──> Connected
    ▲                                    │
    │    MaxRetry or User Stop           │ Connection Lost
    │                                    ▼
    └──── <────────────── Reconnecting ──┘
                    ▲         │
                    └─────────┘ Success
```

### 重连策略

- 检测：SSH Keep-Alive 超时（30s 间隔 × 3 次无响应）+ Socket 异常 + TCP Reset
- 退避：指数退避 5s → 10s → 20s → 40s → 最大 60s
- 最大重试：可配置（默认无限，-1）
- 重连期间：代理端口保持监听，新连接排队等待（超时 30s 返回错误）

### 保活

- SSH 层：`SshClient.KeepAliveInterval = 30s`
- 应用层：定时发送 `keepalive@openssh.com`
- TCP 层：`Socket.SetSocketOption(SocketOptionName.KeepAlive, true)`

---

## 9. 流量统计

```csharp
public class TrafficCounter
{
    public long TotalBytesSent { get; }
    public long TotalBytesReceived { get; }
    public double CurrentUploadSpeed { get; }    // bytes/s，滑动窗口
    public double CurrentDownloadSpeed { get; }
    public int ActiveConnections { get; }
    public long TotalConnections { get; }
}
```

- 采样周期：1 秒
- 速率：滑动窗口平均（最近 5 秒）
- UI 更新：DispatcherTimer 驱动

---

## 10. 持久化与安全

| 数据 | 存储 | 路径 |
|------|------|------|
| 服务器配置 | JSON + DPAPI 加密敏感字段 | `%APPDATA%\SSHTunnelProxy\profiles.json` |
| 全局设置 | JSON | `%APPDATA%\SSHTunnelProxy\settings.json` |
| 连接日志 | SQLite | `%APPDATA%\SSHTunnelProxy\logs.db` |
| 应用日志 | 文件（Serilog 按天滚动，保留 30 天） | `%APPDATA%\SSHTunnelProxy\logs\app-{date}.log` |

### 安全措施

- SSH 密码 / 私钥 Passphrase / 代理认证密码：DPAPI 加密存储，运行时解密
- Host Key 验证：首次 TOFU 模式，后续严格校验
- 代理监听：默认 `127.0.0.1`，监听 `0.0.0.0` 需用户确认
- 强制 SSH-2，优先 `aes256-gcm` / `chacha20-poly1305`

---

## 11. UI 设计

### 主窗口布局

```
┌─────────────────────────────────────────────────────────────┐
│  SSHTunnelProxy                                    [─][□][✕]│
├────────┬────────────────────────────────────────────────────┤
│        │  [+新建] [▶连接] [■断开] [⟳重连] [⚙设置]          │
│ 隧道   │                                                   │
│ 统计   │  🟢 MyServer-1   1080(SOCKS5) 8118(HTTP) ↑12KB/s │
│ 日志   │     SSH: user@1.2.3.4:22    已连接 02:31:15       │
│ 设置   │                                                   │
│        │  ⚪ MyServer-2   1080(SOCKS5) 8118(HTTP) 未连接   │
│        │                                                   │
│        │  ┌─ 详情面板 ──────────────────────────────────┐  │
│        │  │ 状态: 🟢 已连接                              │  │
│        │  │ SOCKS5: 127.0.0.1:1080  HTTP: 127.0.0.1:8118│  │
│        │  │ 上传: 128.5 MB (↑12.3 KB/s)                 │  │
│        │  │ 下载: 512.7 MB (↓45.8 KB/s)                 │  │
│        │  │ 活跃连接: 23  总连接: 1,024                  │  │
│        │  └──────────────────────────────────────────────┘  │
├────────┴────────────────────────────────────────────────────┤
│ 🟢 MyServer-1 已连接 | SOCKS5:1080 | HTTP:8118             │
└─────────────────────────────────────────────────────────────┘
```

### 配置编辑对话框

配置名称 → SSH 服务器（主机/端口/用户名/认证方式/密码或私钥）→ 本地代理（监听地址/端口/代理认证）→ 高级（超时/保活/重连）→ [测试连接] [保存] [取消]

### 系统托盘

- 图标：绿=已连接，灰=未连接，红=错误
- 右键菜单：快速连接/断开各隧道、显示主窗口、复制代理地址、退出

### UI 技术

| 项目 | 方案 |
|------|------|
| MVVM | CommunityToolkit.Mvvm（源生成器） |
| DI | Microsoft.Extensions.DependencyInjection |
| 主题 | 自定义 ResourceDictionary，Dark/Light |
| 托盘 | Hardcodet.NotifyIcon.Wpf |
| 数据绑定 | ObservableCollection + INotifyPropertyChanged |

---

## 12. 开发阶段

| 阶段 | 范围 | 核心交付 |
|------|------|----------|
| S0 | 项目骨架 | 解决方案 + 三个子项目 + DI + NuGet 依赖 |
| S1 | SSH 隧道核心 | SshTunnelTransport + SshDirectTcpipChannel(PoC) + 密码/私钥认证 |
| S2 | SOCKS5 代理 | Socks5ProxyServer + 协议解析 + 认证 + 双向转发 + 流量统计 |
| S3 | HTTP 代理 | HttpProxyServer + CONNECT 隧道模式 |
| S4 | 隧道管理 + 重连 | TunnelManager + 断线检测 + 指数退避 + Keep-Alive |
| S5 | 配置持久化 + 日志 | ConfigService(JSON+DPAPI) + LogService(SQLite) |
| S6 | WPF 完整 UI | 主窗口 + 配置对话框 + 日志 + 设置 + 托盘 |
| S7 | 测试 + 加固 | 单元测试 ≥ 70% + 集成测试 + 性能优化 |

---

## 13. 测试策略

| 层次 | 范围 | 工具 | 关键用例 |
|------|------|------|----------|
| 单元测试 | 协议解析/配置/流量统计/安全 | xUnit + Moq | SOCKS5握手/CONNECT解析、HTTP CONNECT解析、DPAPI加解密、配置序列化、流量计数 |
| 集成测试 | 代理→SSH→目标端到端 | xUnit + 内嵌Mock SSH | SOCKS5→HTTP目标、HTTP CONNECT→HTTPS、断线重连、并发200连接、端口占用 |
| 性能测试 | 并发/吞吐/内存 | 自研脚本 | 200并发无泄漏、24h内存稳定 |

---

## 14. 风险与应对

| 风险 | 影响 | 应对 |
|------|------|------|
| SSH.NET direct-tcpip 不可用 | 高 | S1 先 PoC 验证，失败回退 ForwardedPortLocal |
| HTTP 普通转发复杂度 | 中 | 首期仅 CONNECT，普通转发延后 |
| 高并发资源耗尽 | 中 | 连接池 + 并发上限 + CancellationToken 级联 |
| DPAPI 跨机器不可迁移 | 低 | 导出时提供明文选项（用户确认） |
| 私钥格式兼容 | 中 | SSH.NET 支持 OpenSSH/PEM/PuTTY，补充 ppk 转换 |

---

## 附录：NuGet 依赖

**Core 项目**：
- SSH.NET
- Serilog + Serilog.Sinks.File
- Microsoft.Data.Sqlite
- Microsoft.Extensions.DependencyInjection
- System.Text.Json

**App 项目**：
- CommunityToolkit.Mvvm
- Hardcodet.NotifyIcon.Wpf
- Serilog.Sinks.Debug
- Microsoft.Extensions.DependencyInjection

**Tests 项目**：
- xUnit + xunit.runner.visualstudio
- Moq
- FluentAssertions