# SSHTunnelProxy — 实施计划

> 对应设计文档：`docs/superpowers/specs/2026-08-17-sshtunnelproxy-design.md`
> 计划日期：2026-08-17
> 状态：已确认

本计划将项目拆分为 **S0–S7 共 8 个阶段**。每个阶段包含明确的任务步骤、依赖关系与验收标准。按阶段顺序推进，每阶段完成后进行验收再进入下一阶段。

**开发顺序依赖**：
```
S0 骨架 → S1 SSH核心 ──→ S2 SOCKS5 ──→ S3 HTTP ──┐
              └────────┴──────────────┴─────────→ S4 隧道管理/重连 → S5 配置/日志
                                                              └→ S6 WPF UI
                                                                   └→ S7 测试加固
```
S1→S4→S5 是核心引擎；S6 依赖核心引擎稳定；S7 贯穿始终（每个阶段伴随单元测试）。

---

## S0 — 项目骨架

**目标**：搭建可编译的解决方案骨架，配置 DI 与 NuGet 依赖，建立测试项目。

### 步骤
- [ ] S0.1 创建解决方案 `SSHTunnelProxy.sln`
- [ ] S0.2 创建三个子项目：
  - `src/SSHTunnelProxy.Core`（classlib，net10.0）
  - `src/SSHTunnelProxy.App`（WPF，net10.0-windows）
  - `src/SSHTunnelProxy.Tests`（xunit）
- [ ] S0.3 Core 项目添加 NuGet 依赖：
  - SSH.NET、Serilog、Serilog.Sinks.File、Microsoft.Data.Sqlite、Microsoft.Extensions.DependencyInjection、System.Text.Json
- [ ] S0.4 App 项目添加 NuGet 依赖：
  - CommunityToolkit.Mvvm、Hardcodet.NotifyIcon.Wpf、Serilog.Sinks.Debug、Microsoft.Extensions.DependencyInjection
- [ ] S0.5 Tests 项目：xUnit、Moq、FluentAssertions、覆盖 Core
- [ ] S0.6 建立 DI 容器（`ServiceCollection`），注册核心服务接口，作为依赖注入的核心扩展点
- [ ] S0.7 建立目录结构（`Models` / `Services` / `Proxy` / `Tunnel` / `Security` / `Utils`）
- [ ] S0.8 配置统一日志（Serilog 初始化到文件 + Debug）
- [ ] S0.9 验证：`dotnet build` 通过，测试项目可运行

**验收**：解决方案从零可编译，DI 可解析核心服务，测试项目空跑通过。

---

## S1 — SSH 隧道核心

**目标**：实现 SSH 连接管理与 direct-tcpip Channel 转发（核心风险点，先做 PoC）。

### 步骤
- [x] S1.1 **PoC 验证**：确认 SSH.NET 2026 的 direct-tcpip Channel API 为 internal。
  **结论**：采用**方案 B（ForwardedPortLocal）** —— SSH.NET 官方动态端口转发，公开受支持。
  `SshDirectTcpipChannel`：`AddForwardedPort(new ForwardedPortLocal("127.0.0.1",0,host,port))` + `Start()`，
  再以 `TcpClient` 连接 `BoundPort` 得到对流。已实现并编译通过。
- [ ] S1.2 实现 `TunnelState` 枚举（Disconnected/Connecting/Connected/Reconnecting/Error）
- [ ] S1.3 实现 `SshDirectTcpipChannel`：
  - 封装 `SendChannelOpenRequest(direct-tcpip)`，暴露 `Stream`
  - 参数：targetHost / targetPort / origin
  - 实现 `IAsyncDisposable`
- [ ] S1.4 实现 `HostKeyVerifier`：
  - 首次连接 TOFU 模式（保存 Host Key）
  - 后续连接严格校验
- [ ] S1.5 实现 `SshTunnelTransport : ISshTunnelTransport`：
  - `ConnectAsync()`：构建 ConnectionInfo，支持密码/私钥/键盘交互认证
  - `DisconnectAsync()`
  - `OpenChannelAsync(host, port)`：调用 SshDirectTcpipChannel
  - 配置 KeepAliveInterval
  - 维护 State、触发 StateChanged 事件
- [ ] S1.6 单元测试：HostKey 校验逻辑、认证方式选择逻辑
- [ ] S1.7 **验证**：内嵌/本地 SSH 服务器连接成功，打开 channel 可双向读写

**验收**：密码与私钥认证均可连上 SSH 服务器，`OpenChannelAsync` 返回可用 Stream，用户手动 SSH 服务器上测试可建立到目标的 TCP 连接。

---

## S2 — SOCKS5 代理

**目标**：实现 SOCKS5 代理服务器，支持 CONNECT + 认证 + 双向转发 + 流量统计。

### 步骤
- [ ] S2.1 实现 `Socks5Protocol`（静态常量与帧解析工具）：
  - 握手、CONNECT 请求解析（IPv4/域名/IPv6）
  - 认证（NO AUTH / USERNAME-PASSWORD）
  - 响应构造
- [ ] S2.2 实现 `TrafficCounter`：
  - TotalBytesSent/Received、滑动窗口速率、Active/Total 连接数
  - 提供 `TrafficUpdated` 事件
- [ ] S2.3 实现 `StreamRelay`：
  - 双向 CopyAsync，任一方向结束取消另一方向
  - 接入 TrafficCounter
- [ ] S2.4 实现 `Socks5ProxyServer : ISocks5Server`：
  - `StartAsync()`：监听指定地址/端口
  - `AcceptLoop()` / `HandleClientAsync()`
  - 握手解析、认证、CONNECT 请求（BIND/UDP 返回不支持）
  - 调用 `ISshTunnelTransport.OpenChannelAsync` 建立隧道
  - 通过 Proxy 认证后 Relay 数据
  - 记录连接日志（交给上层/注入回调）
- [ ] S2.5 单元测试：Socks5 握手解析、CONNECT 解析、认证成功/失败、响应构造
- [ ] S2.6 集成测试：Socks5 → SSH → HTTP 目标 端到端（用本地模拟目标）
- [ ] S2.7 **验证**：浏览器/curl 通过 SOCKS5 代理访问目标网站

**验收**：通过 SOCKS5 代理可成功访问 HTTP/HTTPS 目标，认证开关生效，流量统计准确。

---

## S3 — HTTP 代理

**目标**：实现 HTTP 代理 CONNECT 隧道模式。

### 步骤
- [ ] S3.1 实现 `HttpParser`：
  - 解析请求行（CONNECT 方法）
  - 解析 Headers，提取 host:port
- [ ] S3.2 实现 `HttpProxyServer : IHttpServer`：
  - `StartAsync()`：监听
  - `AcceptLoop()` / `HandleClientAsync()`
  - CONNECT 请求：解析目标 → OpenChannelAsync → 返回 `200 Connection Established` → Relay
  - 非 CONNECT 请求：返回错误（首期不支持普通转发）
  - 可选 Proxy-Authorization 认证
- [ ] S3.3 单元测试：HTTP CONNECT 请求解析
- [ ] S3.4 集成测试：HTTP CONNECT → SSH → HTTPS 目标 端到端
- [ ] S3.5 **验证**：浏览器通过 HTTP 代理访问 HTTPS 网站

**验收**：通过 HTTP CONNECT 代理可成功访问 HTTPS 目标。

---

## S4 — 隧道管理与重连

**目标**：实现多隧道并行管理、断线检测、指数退避重连、Keep-Alive。

### 步骤
- [ ] S4.1 实现 `TunnelContext`：绑定 Profile + Transport + Socks5Server + HttpServer + Traffic
- [ ] S4.2 实现 `TunnelManager : ITunnelManager`：
  - `StartTunnelAsync` / `StopTunnelAsync` / `RestartTunnelAsync`
  - `ConcurrentDictionary<Guid, TunnelContext>` 管理多隧道
  - 触发 `TunnelStateChanged` 事件
  - 端口占用检测与优雅关闭
- [ ] S4.3 实现断线检测：SSH 连接异常 / Socket 异常 → 触发重连
- [ ] S4.4 实现重连策略：
  - 指数退避 5s→10s→20s→40s→60s
  - 最大重试次数控制（默认无限）
  - 重连期间代理端口保持监听，新连接排队（30s 超时返回错误）
- [ ] S4.5 实现应用层保活（keepalive@openssh.com 心跳）
- [ ] S4.6 单元测试：重连退避计算、状态机转换
- [ ] S4.7 集成测试：模拟断线后自动重连并恢复代理（IT-05）

**验收**：多隧道可并行运行；模拟断线后按退避策略自动重连且无需重启代理端口。

---

## S5 — 配置持久化与日志

**目标**：实现 JSON + DPAPI 配置持久化、SQLite 连接日志、Serilog 应用日志。

### 步骤
- [ ] S5.1 实现 `DpapiProtector`：Encrypt/Decrypt（ProtectedData，CurrentUser）
- [ ] S5.2 实现 `ConfigService : IConfigService`：
  - `LoadProfilesAsync` / `SaveProfilesAsync`
  - `LoadSettingsAsync` / `SaveSettingsAsync`
  - 敏感字段 DPAPI 加密后序列化
  - 配置导入/导出 JSON（导出时敏感字段需用户确认，P2 可选）
- [ ] S5.3 实现 `LogService : ILogService`：
  - SQLite 建表（Timestamp/TunnelName/ProxyType/ClientEndpoint/TargetEndpoint/BytesSent/BytesReceived/Duration/Status）
  - AddConnectionLogAsync / QueryLogsAsync / CleanupOldLogsAsync
  - 自动清理（保留 LogRetentionDays）
- [ ] S5.4 统一 Serilog 应用日志（文件滚动、保留 30 天）
- [ ] S5.5 单元测试：DPAPI 加解密往返、配置序列化往返、日志 CRUD
- [ ] S5.6 **验证**：配置存储/加载正确，敏感字段加密存储

**验收**：配置文件加密持久化、日志写入 SQLite 可查询清理、应用日志滚动。

---

## S6 — WPF 完整 UI

**目标**：实现主窗口、配置编辑对话框、日志窗口、设置窗口、系统托盘。

### 步骤
- [ ] S6.1 搭建 App.xaml 启动 + DI 容器注入 ViewModel
- [ ] S6.2 主题系统：自定义 ResourceDictionary（Dark/Light），全局样式
- [ ] S6.3 值转换器：状态→颜色、字节数→可读文本、速率格式化等
- [ ] S6.4 实现 `MainViewModel`（侧边栏导航、隧道列表、详情面板、状态栏、命令）
- [ ] S6.5 实现 `MainWindow.xaml`：侧边栏 + 工具栏 + 隧道列表 + 详情面板 + 状态栏
- [ ] S6.6 实现 `ConfigViewModel` + `ConfigDialog.xaml`：
  - 配置字段表单（SSH 服务器 / 本地代理 / 认证 / 高级）
  - 测试连接按钮
  - 保存/取消
- [ ] S6.7 实现 `LogViewModel` + `LogView.xaml`：列表、筛选、导出 CSV
- [ ] S6.8 实现 `SettingsViewModel` + `SettingsView.xaml`：主题、托盘行为、日志保留天数等
- [ ] S6.9 实现系统托盘（Hardcodet.NotifyIcon）：
  - 状态图标（绿/灰/红）
  - 右键菜单（快速连接/断开、显示主窗口、复制代理地址、退出）
  - 最小化到托盘
- [ ] S6.10 **验证**：完整 UI 流程——新建配置→连接→监控流量→查看日志→托盘控制→断开

**验收**：WPF 完整界面可完成"新建配置→连接→监控→日志→设置→托盘"全流程，状态实时刷新。

---

## S7 — 测试与加固

**目标**：提升测试覆盖率至 ≥ 70%，集成测试覆盖核心流程，性能与安全加固。

### 步骤
- [ ] S7.1 补齐单元测试：协议、配置、流量统计、安全（目标 ≥ 70% 行覆盖率）
- [ ] S7.2 补齐集成测试：SOCKS5→HTTP、SOCKS5→HTTPS、HTTP CONNECT→HTTPS、断线重连、并发 200、端口占用、认证失败
- [ ] S7.3 资源清理 audit：所有 Stream/Socket/Channel 均 IAsyncDisposable，无泄漏
- [ ] S7.4 性能测试：100 并发下载、快速连接/断开 1000 次、内存稳定性检查
- [ ] S7.5 安全审计：DPAPI 使用、Host Key 校验、监听地址默认 127.0.0.1
- [ ] S7.6 编译发布配置：Release 发布，生成可分发包（可选 MSIX）
- [ ] S7.7 更新 README：使用说明、依赖、构建指南

**验收**：单元测试覆盖率 ≥ 70%，关键集成测试通过，Release 可发布，无资源泄漏。

---

## 验收清单（跨阶段）

- ☐ 密码 & 私钥认证均可连 SSH
- ☐ SOCKS5 CONNECT（IPv4/域名/IPv6）可访问目标
- ☐ HTTP CONNECT 可访问 HTTPS 目标
- ☐ 多隧道并行运行
- ☐ 断线自动重连（指数退避）
- ☐ Keep-Alive 生效
- ☐ 流量统计实时准确
- ☐ 连接日志写入并可查询/清理/导出
- ☐ 敏感字段 DPAPI 加密存储
- ☐ 系统托盘（状态图标/菜单/最小化到托盘）
- ☐ 单元测试覆盖率 ≥ 70%
- ☐ 并发 200 连接无泄漏
- ☐ 应用优雅关闭无端口残留

---

## 风险焦点

| 阶段 | 风险 | 缓解 |
|------|------|------|
| S1 | direct-tcpip PoC 失败 | 提前 PoC；回退 ForwardedPortLocal |
| S3 | SSH.NET/HTTP 兼容 | 首期仅 CONNECT，降低复杂度 |
| S2/S7 | 高并发资源泄漏 | 连接池 + CancellationToken 级联 + audit |
| S6 | 托盘/主题平台差异 | 使用成熟库 Hardcodet.NotifyIcon |
