# NTP TimeSync Tool 开发计划

> 依据 `docs/CSNtpd需求设计文档.md` 制定。本计划将需求文档落地为可编译、可测试、可运行的 WPF + C# `.NET 10` 桌面应用。

## 1. 目标范围

按需求文档第二章至第十九章实现全部核心功能：

- NTP Client（手动同步、定时同步、多服务器故障切换、Offset/Delay 计算）
- NTP Server（UDP 监听、响应、统计、白名单、限流）
- 系统时间同步（管理员写入、阈值控制、权限检测）
- 配置管理（JSON 持久化、校验、默认值、恢复默认）
- 日志系统（UI + 文件滚动输出、级别）
- 状态监控（客户端/服务端状态机、请求统计）
- WPF 主界面（MVVM）
- 单元测试与集成测试

## 2. 解决方案与项目结构

按需求文档第 7.4 节建议：

```
NtpTool.sln
├── src
│   ├── NtpTool.Core          # NTP 协议、模型、服务接口
│   │   ├── Models            # NtpPacket / NtpServerConfig / 结果 / 配置
│   │   ├── Ntp               # NtpPacketCodec / NtpTime / TimeCalculator / NtpClient / NtpServer
│   │   ├── Services          # ISyncScheduler / ISystemTimeService 等接口与实现
│   │   └── Abstractions       # 日志、配置、时间服务接口
│   ├── NtpTool.Infrastructure # 配置 JSON、文件日志、UDP 网络、Windows 系统时间
│   └── NtpTool.App           # WPF 主程序（MainWindow、ViewModels、Converters、DI）
└── tests
    ├── NtpTool.Core.Tests     # 报文、偏移计算、配置校验单测
    └── NtpTool.Infrastructure.Tests  # 配置持久化、日志滚动、白名单、限流
```

目标框架：`net10.0-windows`（App 启用 `UseWPF`），Core 用 `net10.0`，测试用 `net10.0`。

## 3. 阶段任务

| 阶段 | 内容 | 产出 |
| --- | --- | --- |
| 阶段 1 | 解决方案骨架、CLI 模板创建 | NtpTool.sln、四个 csproj |
| 阶段 2 | NTP 协议核心 | NtpTime、NtpPacketCodec、TimeCalculator |
| 阶段 3 | 配置管理 | 配置模型、校验、JSON 保存/加载、默认值 |
| 阶段 4 | 日志系统 | 日志抽象、文件日志、滚动、级别 |
| 阶段 5 | NTP 客户端 | NtpClient、多服务器切换、超时重试 |
| 阶段 6 | 定时调度 | SyncScheduler、防重入、启动执行、重试 |
| 阶段 7 | NTP 服务端 | NtpServer、响应逻辑、统计、白名单、限流 |
| 阶段 8 | 系统时间 | SysTimeService、管理员权限、阈值 |
| 阶段 9 | DI + 启动流程 | ServiceCollection、App 启动、自检 |
| 阶段 10 | WPF UI | MainWindow、ViewModel、状态与日志面板、配置窗口 |
| 阶段 11 | 测试 | 单测 + 集成测试 |
| 阶段 12 | 构建验证 | dotnet build + dotnet test |

## 4. NTP 协议要点（实现约束）

- 报文固定 48 字节，LI(2)/VN(3)/Mode(3)/Stratum(8)/Poll(8)/Precision(8) 位于首字节 + 后续字节。
- 时间戳为 64 位：高 32 位自 1900-01-01 起秒数，低 32 位小数；与 Unix Epoch 差 2208988800 秒。
- 客户端请求 Mode=3，服务端响应 Mode=4，VN=4。
- Offest = ((T2-T1)+(T3-T4))/2；RoundTripDelay = (T4-T1)-(T3-T2)。
- 校验：Stratum 0/16 视为不可用；Mode 非 3 的服务端响应丢弃。

## 5. 测试策略

- 单元测试：NtpPacketCodec 序列化往返、时间戳转换、Offset/Delay 数学（用文档第 16.1 节的示例数据）、配置校验。
- 集成测试：启动服务端于高端口并响应客户端请求；白名单拒绝；限流丢弃；配置 JSON 往返。
- 不依赖真实公共 NTP 服务，使用本机回环 TCP/UDP。

## 6. 构建与验收

```bash
dotnet build NtpTool.sln
dotnet test NtpTool.sln
dotnet run --project src/NtpTool.App
```

验收对照需求文档第 21 节：客户端同步、服务端响应、手动/定时同步、Offset/Delay 展示、管理员改时、配置保存加载、日志、端口冲突提示、退出无端口残留。