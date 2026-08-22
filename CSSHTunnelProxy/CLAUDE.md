# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概述

SSHTunnelProxy 是一个 Windows 桌面 SSH 隧道代理软件：在本地启动 SOCKS5 与 HTTP 代理监听端口，所有经过代理的流量通过 SSH 加密隧道（direct-tcpip）转发到远程目标。技术栈为 C# / .NET 10 / WPF / SSH.NET。

## 常用命令

```bash
# 构建（解决方案为 slnx 格式）
dotnet build SSHTunnelProxy.slnx

# 运行全部测试（xUnit）
dotnet test

# 运行单个测试类/方法
dotnet test --filter "FullyQualifiedName~Socks5ProtocolTests"
dotnet test --filter "FullyQualifiedName~Socks5EndToEndTests.Connect_PassthroughAuth_EchoRoundtrip_And_Logs"

# 调试运行（WPF 应用）
dotnet run --project src/SSHTunnelProxy.App

# 便携发布：自带 .NET 运行时，仅 win-x64，用户机器无需装 .NET
dotnet publish src/SSHTunnelProxy.App/SSHTunnelProxy.App.csproj -c Release -p:Portable=true
```

提交信息用中文。

## 架构

### 分层与项目

解决方案含三个项目，依赖单向：`App → Core`，`Tests → Core`。

- **SSHTunnelProxy.Core**（`net10.0`，无 UI 依赖）：全部业务逻辑。Models / Services / Proxy / Tunnel / Security / Utils。
- **SSHTunnelProxy.App**（`net10.0-windows`，WPF）：Views (XAML) + ViewModels (MVVM) + Framework（托盘、日志桥接、UI 调度）。
- **SSHTunnelProxy.Tests**：Unit + Integration，xUnit + Moq + FluentAssertions。

### 依赖注入

`App.xaml.cs.OnStartup` 构建 `ServiceCollection`，Core 层服务通过 `ServiceCollectionExtensions.AddSSHTunnelProxyCore()`（在 `Core/ServiceCollectionExtensions.cs`）集中注册。`ILogger<T>` 通过 `SerilogLoggerProvider` 桥接到静态 `Serilog.Log`。`MainViewModel`/`MainWindow`/`TrayIconController` 等在 App 中注册。

### 数据流（代理转发链路）

```
代理客户端
  → Socks5ProxyServer / HttpProxyServer（监听本地端口，解析协议）
  → ISshTunnelTransport.OpenChannelAsync(host, port)（SshTunnelTransport）
  → SshDirectTcpipChannel.OpenAsync（ForwardedPortLocal 临时本地端口 + TcpClient 桥接）
  → SSH 隧道 → 远程目标
  → StreamRelay.RelayAsync 双向透传（任一方向 EOF 即取消另一方向）
```

### 关键设计决策

- **不使用 SSH.NET 的 `ForwardedPortDynamic`**，而是自建 SOCKS5/HTTP 协议解析层 + direct-tcpip Channel 转发。理由：SOCKS5 与 HTTP 共用同一个 `SshTunnelTransport`，可精确统计每条连接的流量与目标地址，便于扩展规则分流与本地认证。
- **SSH.NET 2026 的 direct-tcpip 低层 Channel API 为 internal**，故 `SshDirectTcpipChannel` 改用公开的 `ForwardedPortLocal` 实现：绑定临时本地端口（`boundPort=0`），再用 `TcpClient` 连接该本地端口获得双向 `Stream`。每个代理连接对应一个临时 `ForwardedPortLocal`，用完即 `Stop` + `RemoveForwardedPort`。
- **HTTP 代理首期仅支持 CONNECT 隧道模式**（覆盖 HTTPS），普通 GET/POST 转发不在首期范围。

### 隧道生命周期与重连

`TunnelManager` 用 `ConcurrentDictionary<Guid, TunnelContext>` 管理多隧道。`SshTunnelTransport` 有后台 `MonitorLoopAsync`（每 2s 检查 `client.IsConnected`），断线触发 `ConnectionLost` 事件，`TunnelManager` 据此进入 `ReconnectLoopAsync`（指数退避 5→10→20→40→60 封顶，`MaxReconnectAttempts = -1` 为无限）。

### 数据与持久化

所有运行时数据写在 **程序所在目录**（`AppPaths.Root = AppContext.BaseDirectory`，便携式，非 `%APPDATA%`）：

| 数据 | 文件 | 说明 |
| --- | --- | --- |
| 服务器配置 | `profiles.json` | 敏感字段 DPAPI 加密 |
| 全局设置 | `settings.json` | |
| 连接日志 | `logs.db` | SQLite，`LogService` |
| 已信任主机密钥 | `known_hosts.json` | TOFU 模式 |
| 应用日志 | `logs/app-.log` | Serilog 按天滚动 |

- **敏感数据**：`DpapiProtector`（`ProtectedData`，CurrentUser 作用域 + 附加熵）。密码、私钥 Passphrase、代理密码、内嵌私钥内容均加密存储。
- **主机密钥**：`HostKeyVerifier` TOFU——首次保存指纹并信任，后续校验一致性。
- **连接日志**：`LogService` 同时实现 `ILogService`（查询）与 `IConnectionSink`（代理服务器回写）。代理服务器在连接结束时通过 `IConnectionSink.RecordConnectionAsync` 写入元数据（不含传输内容）。

## 重要约定与陷阱

- **`TunnelItemViewModel` 的重启不能直接调 `_manager.RestartTunnelAsync`**：后者内部新建 transport，但新 transport 的 `StateChanged` 事件未接到 ViewModel，会导致重连成功后状态圆点/文字不更新。必须复用「停止旧上下文 → `StartTunnelAsync` → `AttachEvents`」流程。同理，首次连接成功需手动设 `State = Connected`，因为 transport 的 `StateChanged(Connected)` 在 `ConnectAsync` 内部已触发，早于 `AttachEvents` 订阅，事件会丢失。
- **托盘菜单只在构造时构建一次**：隧道项需在菜单 `Opened` 事件中由 `RebuildTunnelItems` 动态重建，否则停留在启动时的旧列表。菜单结构固定为 `[显示主窗口, (隧道项...), Separator, 退出]`。
- **设置即时生效**：`MainWindow` 在 `Closing`/`StateChanged` 时每次重新读取 `settings.json`（`CloseToTray`/`MinimizeToTray`），不缓存。
- **`MainWindow.IsQuitting` 静态标志**：区分「真正退出」与「最小化到托盘」。托盘「退出」菜单需先置 `IsQuitting = true` 再 `Application.Shutdown()`，否则关闭拦截会把退出转成隐藏。
- **UI 线程封送**：非 UI 线程的事件回调（如 transport 的 `StateChanged`）必须经 `DispatcherUI.Run` 封送到 UI 线程后再更新绑定属性。
- **`SshServerProfile.Id` 不可变**：`TunnelManager` 字典键依赖它，`TunnelItemViewModel.ApplyProfile` 编辑时逐字段复制但保留 `Id`。

## 测试

集成测试不依赖真实 SSH 服务端：`FakeSshTunnelTransport` 的 `OpenChannelAsync` 直接对目标建立真实 TCP 连接（绕过 SSH），`LocalTargetServer` 提供回显/固定响应两种目标模拟，`CollectingSink` 内存收集连接日志供断言（`WaitForAsync` 阻塞等待满足谓词的日志）。代理服务器监听端口用 `0`（系统自动分配），通过 `BoundPort` 获取实际端口。

## UI

Win11 Fluent 风格，浅色单主题。配色键定义在 `App.xaml`，控件样式在 `Resources/Controls.xaml`。系统强调色由 `AccentColorProvider` 在运行时读取并注入覆盖 `AccentColor`/`AccentBrush` 等资源键。MVVM 用 `CommunityToolkit.Mvvm`（`[ObservableProperty]`/`[RelayCommand]` 源生成器）。`ShutdownMode="OnMainWindowClose"`。
