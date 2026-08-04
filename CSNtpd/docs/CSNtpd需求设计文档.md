下面给出一份可直接用于立项、评审与开发的《WPF + C# NTP 时间同步小工具需求设计文档》。文档按“需求说明 + 功能设计 + 架构设计 + 协议设计 + 安全与非功能设计 + 测试验收”方式组织，适配 `.NET SDK 10.0.300`、WPF、C#，目标平台为 Windows 桌面应用。

---

# WPF NTP 时间同步小工具需求设计文档

## 1. 项目概述

### 1.1 项目名称

WPF NTP TimeSync Tool

### 1.2 项目背景

在局域网、内网测试环境、工厂设备、实验室环境等场景中，经常需要一个轻量级时间同步工具：

1. 本机可以作为 NTP 客户端，定时从指定 NTP 服务器同步时间；
2. 本机可以作为 NTP 服务端，为其他设备提供时间同步服务；
3. 工具需要提供可视化界面，便于配置、启动、停止、查看状态与日志；
4. 支持 Windows 桌面环境，基于 .NET 10 与 WPF 实现。

### 1.3 项目目标

开发一个基于 `.NET 10.0-windows` 的 WPF 桌面应用，实现以下能力：

- NTP Client：支持从一个或多个 NTP 服务器获取时间；
- NTP Server：监听 UDP 端口，响应 NTP 客户端时间请求；
- 支持手动同步与定时自动同步；
- 支持显示本地时间、服务器时间、时间偏差；
- 可选同步 Windows 系统时间；
- 支持运行日志、同步记录、错误告警；
- 支持配置持久化；
- 提供简单、稳定、可扩展的单机小工具。

### 1.4 运行环境

| 项目     | 要求                                                         |
| -------- | ------------------------------------------------------------ |
| 操作系统 | Windows 10 / Windows 11 / Windows Server 2016+               |
| 框架     | .NET 10.0-windows                                            |
| UI       | WPF                                                          |
| 语言     | C#                                                           |
| SDK      | .NET SDK 10.0.300 或更高                                     |
| 权限     | 普通用户可运行；修改系统时间或监听 123 端口通常需要管理员权限 |
| 网络     | UDP，默认端口 123，可配置                                    |

### 1.5 项目范围

本项目包含：

- WPF 桌面客户端程序；
- NTP Client 模块；
- NTP Server 模块；
- 配置管理模块；
- 日志模块；
- 系统时间同步模块；
- 状态监控与历史记录展示。

本项目不包含：

- 完整企业级 NTP 集群部署；
- NTP 认证扩展；
- 组播 / 广播模式；
- 复杂 NTP 时钟筛选算法、漂移补偿算法；
- Linux / macOS 平台支持；
- Web 管理后台。

---

## 2. 术语说明

| 术语                | 说明                                                       |
| ------------------- | ---------------------------------------------------------- |
| NTP                 | Network Time Protocol，网络时间协议                        |
| SNTP                | Simple Network Time Protocol，简化版 NTP                   |
| Stratum             | 时钟层级，1 表示一级时钟源，2 表示从一级时钟源同步的服务器 |
| Leap Indicator      | 闰秒指示                                                   |
| Poll Interval       | 轮询间隔                                                   |
| Precision           | 时钟精度                                                   |
| Root Delay          | 根延迟                                                     |
| Root Dispersion     | 根离散度                                                   |
| Originate Timestamp | 客户端发送时间                                             |
| Receive Timestamp   | 服务端接收时间                                             |
| Transmit Timestamp  | 服务端发送时间                                             |
| Offset              | 本地时钟与远端时间偏差                                     |
| Round Trip Delay    | 网络往返延迟                                               |
| UTC                 | 协调世界时                                                 |

---

## 3. 用户角色

| 角色            | 说明                                                        |
| --------------- | ----------------------------------------------------------- |
| 普通用户        | 查看时间、查看状态、查看日志、执行不修改系统时间的同步测试  |
| 管理员用户      | 修改系统时间、启动 NTP Server、监听低端口、配置自动同步策略 |
| NTP 客户端设备  | 向本工具发起时间同步请求                                    |
| 上游 NTP 服务器 | 本工具作为客户端时请求的远端时间源                          |

---

## 4. 总体功能需求

系统主要包含以下功能模块：

1. 主界面模块；
2. NTP Client 模块；
3. NTP Server 模块；
4. 系统时间同步模块；
5. 配置管理模块；
6. 日志模块；
7. 状态监控模块；
8. 异常处理模块。

功能总览如下：

| 模块         | 功能                                          |
| ------------ | --------------------------------------------- |
| 主界面       | 显示本地时间、UTC 时间、运行状态、快捷操作    |
| NTP Client   | 配置上游服务器、立即同步、定时同步、显示偏差  |
| NTP Server   | 启动 / 停止服务、监听地址端口、响应客户端请求 |
| 系统时间同步 | 根据 NTP 结果设置本机系统时间                 |
| 配置管理     | 保存 / 读取 / 校验配置                        |
| 日志         | 记录运行日志、同步日志、错误日志              |
| 状态监控     | 显示服务端连接数、请求次数、最近同步结果      |
| 异常处理     | 网络异常、端口占用、权限不足、超时等处理      |

---

## 5. 功能性需求

## 5.1 主界面需求

### 5.1.1 界面目标

提供一个简洁的 WPF 主窗口，包含：

- 当前本地时间；
- 当前 UTC 时间；
- NTP Client 状态；
- NTP Server 状态；
- 最近一次同步结果；
- 操作按钮；
- 日志区域。

### 5.1.2 显示信息

主界面应显示：

| 信息项        | 说明                           |
| ------------- | ------------------------------ |
| 本地时间      | 当前系统本地时间               |
| UTC 时间      | 当前 UTC 时间                  |
| Client 状态   | 停止 / 运行中 / 同步中 / 错误  |
| Server 状态   | 停止 / 监听中 / 错误           |
| 最近同步时间  | 最后一次成功同步时间           |
| 上游服务器    | 当前使用的 NTP 服务器地址      |
| 时间偏差      | 本地时间与上游服务器时间偏差   |
| 往返延迟      | 请求上游服务器耗时             |
| Server 请求数 | 本工具作为服务端接收到的请求数 |
| 最近客户端    | 最近访问的客户端 IP            |

### 5.1.3 操作按钮

主界面应提供：

| 按钮               | 功能                                |
| ------------------ | ----------------------------------- |
| 立即同步           | 客户端立即向上游 NTP 服务器同步一次 |
| 启动客户端定时任务 | 开启自动定时同步                    |
| 停止客户端定时任务 | 停止自动同步                        |
| 启动服务端         | 启动 NTP Server                     |
| 停止服务端         | 停止 NTP Server                     |
| 打开配置           | 打开配置页面或配置弹窗              |
| 清除日志           | 清空当前界面日志显示                |
| 导出日志           | 导出日志文件                        |

### 5.1.4 状态指示

建议使用颜色或图标表示状态：

| 状态   | 颜色 |
| ------ | ---- |
| 正常   | 绿色 |
| 运行中 | 蓝色 |
| 警告   | 黄色 |
| 错误   | 红色 |
| 停止   | 灰色 |

---

## 5.2 NTP Client 功能需求

### 5.2.1 服务器配置

用户可以配置一个或多个上游 NTP 服务器：

| 配置项     | 说明               |
| ---------- | ------------------ |
| 服务器地址 | IP 或域名          |
| 端口       | 默认 123           |
| 优先级     | 数值越小优先级越高 |
| 启用状态   | 是否参与同步       |
| 超时时间   | 默认 3000 ms       |
| 备注       | 可选               |

示例：

```text
ntp1.example.com:123
192.168.1.10:123
time.windows.com:123
```

### 5.2.2 手动立即同步

功能要求：

1. 用户点击“立即同步”；
2. 程序按照服务器优先级依次尝试；
3. 向服务器发送 NTP 请求；
4. 解析响应；
5. 计算 Offset 与 Round Trip Delay；
6. 展示结果；
7. 根据配置决定是否修改系统时间。

### 5.2.3 定时自动同步

支持定时自动同步：

| 配置项         | 说明               |
| -------------- | ------------------ |
| 是否启用       | 开 / 关            |
| 同步周期       | 支持秒、分钟、小时 |
| 最小周期       | 建议不小于 10 秒   |
| 默认周期       | 30 分钟            |
| 启动时执行一次 | 可选               |
| 失败重试次数   | 默认 3             |
| 失败重试间隔   | 默认 10 秒         |

定时任务要求：

- 应用启动后可根据配置自动启动；
- 同步过程中不应阻塞 UI；
- 支持随时停止；
- 同步失败不影响下一轮调度；
- 连续失败达到阈值后显示警告；
- 支持多服务器自动切换。

### 5.2.4 多服务器故障切换

当配置多个上游服务器时：

1. 按优先级排序；
2. 优先请求第一服务器；
3. 如果失败，尝试下一服务器；
4. 记录失败原因；
5. 可选择“仅使用第一个成功服务器”或“全部尝试后选择最优结果”。

推荐策略：

- 默认采用“按优先级依次尝试，第一个成功即返回”；
- 可配置“采样多个服务器，选择最小延迟或最小偏差”。

### 5.2.5 同步结果展示

每次客户端同步后，应记录并展示：

| 字段             | 说明                  |
| ---------------- | --------------------- |
| 同步时间         | 本地执行同步时间      |
| 服务器地址       | 实际使用的服务器      |
| 是否成功         | 成功 / 失败           |
| Offset           | 时间偏差，单位 ms     |
| Delay            | 网络往返延迟，单位 ms |
| Stratum          | 服务器层级            |
| Leap Indicator   | 闰秒标识              |
| Reference ID     | 参考源标识            |
| 是否修改系统时间 | 是 / 否               |
| 错误信息         | 失败时显示            |

### 5.2.6 系统时间更新策略

系统时间更新策略应可配置：

| 模式             | 说明                           |
| ---------------- | ------------------------------ |
| 仅显示           | 只显示偏差，不修改系统时间     |
| 提示确认         | 偏差超过阈值后询问用户是否修改 |
| 自动修改         | 偏差超过阈值后自动修改系统时间 |
| 仅管理员模式修改 | 非管理员运行时不修改系统时间   |

建议默认：

```text
仅显示，不自动修改系统时间
```

原因：修改系统时间影响较大，应谨慎处理。

### 5.2.7 时间偏差阈值

| 配置项       | 默认值 | 说明                           |
| ------------ | -----: | ------------------------------ |
| 最小同步阈值 |  50 ms | 小于该值不建议修改系统时间     |
| 最大允许跳变 |  30 秒 | 超过该值需要提示或禁止自动修改 |
| 自动修改阈值 | 500 ms | 超过该值且开启自动修改时执行   |

当偏差过大时，应提示：

```text
检测到时间偏差过大，请确认是否强制同步系统时间。
```

---

## 5.3 NTP Server 功能需求

### 5.3.1 基本目标

本工具可以作为 NTP Server：

- 监听 UDP 端口；
- 接收客户端 NTP 请求；
- 返回标准 NTP 响应；
- 支持局域网内客户端同步；
- 支持查看请求记录。

### 5.3.2 服务端配置

| 配置项          | 说明                  |
| --------------- | --------------------- |
| 启用服务端      | 是 / 否               |
| 监听地址        | 0.0.0.0 / 指定本机 IP |
| 监听端口        | 默认 123              |
| Stratum         | 默认 2                |
| Leap Indicator  | 默认 0                |
| Precision       | 默认根据系统时钟估算  |
| Root Delay      | 可配置或自动计算      |
| Root Dispersion | 可配置或自动计算      |
| Reference ID    | 可配置，例如 LOCAL    |
| 访问控制        | 允许所有 / 白名单     |
| 最大并发请求    | 默认限制              |
| 日志记录请求    | 是 / 否               |

### 5.3.3 启动与停止

服务端应支持：

1. 启动 NTP Server；
2. 停止 NTP Server；
3. 应用启动时根据配置自动启动；
4. 应用退出时自动停止；
5. 端口被占用时提示明确错误。

### 5.3.4 端口冲突处理

Windows 上 UDP 123 可能被 `Windows Time` 服务占用。

程序应在启动服务端时检测：

| 情况       | 处理方式                             |
| ---------- | ------------------------------------ |
| 端口被占用 | 提示用户停止 Windows Time 或更换端口 |
| 权限不足   | 提示以管理员运行或更换高端口         |
| 防火墙阻止 | 提示添加防火墙规则                   |

建议提示信息：

```text
无法启动 NTP Server：UDP 123 端口可能被 Windows Time 服务占用。
请停止 w32time 服务，或修改本工具监听端口。
```

### 5.3.5 响应客户端请求

服务端收到 NTP Client 请求后：

1. 校验报文长度；
2. 校验 Mode；
3. 记录 Originate Timestamp；
4. 填充 Receive Timestamp；
5. 填充 Transmit Timestamp；
6. 设置 Stratum、Reference ID 等字段；
7. 返回响应报文。

### 5.3.6 请求统计

服务端应统计：

| 统计项        | 说明               |
| ------------- | ------------------ |
| 总请求数      | 启动后累计         |
| 有效请求数    | 成功解析并响应     |
| 非法请求数    | 报文非法或长度错误 |
| 最近客户端 IP | 最近请求来源       |
| 最近请求时间  | 最近请求时间       |
| 每秒请求数    | 可选               |

### 5.3.7 访问控制

支持简单访问控制：

| 模式     | 说明                 |
| -------- | -------------------- |
| 允许所有 | 默认                 |
| 白名单   | 仅允许指定 IP 或网段 |
| 黑名单   | 可选扩展             |

白名单示例：

```text
192.168.1.0/24
10.0.0.5
172.16.10.0/24
```

### 5.3.8 请求限流

为防止异常流量冲击，建议支持：

| 配置项                 |     默认值 |
| ---------------------- | ---------: |
| 单 IP 每分钟最大请求数 |        120 |
| 超限处理               | 丢弃并记录 |
| 限流日志               |       可选 |

---

## 5.4 系统时间同步模块

### 5.4.1 获取系统时间

程序应能获取：

- 本地时间：`DateTime.Now`
- UTC 时间：`DateTime.UtcNow`

### 5.4.2 设置系统时间

在用户授权且具备管理员权限时，程序可将本地系统时间修改为同步后的时间。

要求：

1. 修改前显示确认；
2. 记录修改前后时间；
3. 修改失败时提示原因；
4. 支持日志审计；
5. 非管理员模式下禁用或提示。

### 5.4.3 Windows 时间服务冲突

如果 Windows Time 服务正在运行，可能会影响系统时间或被系统重新校时。

程序可提示：

```text
检测到 Windows Time 服务正在运行，自动修改系统时间可能被系统服务覆盖。
```

可选处理策略：

| 策略                     | 说明             |
| ------------------------ | ---------------- |
| 不干预                   | 默认推荐         |
| 提示用户手动停止 w32time | 安全可控         |
| 管理员权限下尝试停止     | 可选，不默认开启 |

### 5.4.4 时间跳变与平滑调整

第一版建议：

- 仅支持直接设置系统时间；
- 不做复杂的频率补偿与平滑调整；
- 当偏差过大时需要用户确认。

后续扩展：

- 小偏差逐步调整；
- 记录本地时钟漂移；
- 支持 clock discipline。

---

## 5.5 配置管理需求

### 5.5.1 配置内容

配置应至少包括：

```text
AppSettings
├── ClientSettings
│   ├── EnableAutoSync
│   ├── SyncInterval
│   ├── Servers
│   ├── TimeoutMs
│   ├── RetryCount
│   ├── RetryIntervalMs
│   ├── ApplySystemTime
│   ├── AutoApplyThresholdMs
│   └── MaxAllowedOffsetMs
├── ServerSettings
│   ├── EnableServer
│   ├── ListenAddress
│   ├── Port
│   ├── Stratum
│   ├── ReferenceId
│   ├── AllowAllClients
│   ├── AllowedNetworks
│   ├── RateLimitPerMinute
│   └── LogRequests
└── LogSettings
    ├── LogLevel
    ├── LogDirectory
    ├── MaxFileSizeMb
    └── RetentionDays
```

### 5.5.2 配置文件格式

推荐使用 JSON 文件：

```text
appsettings.json
或
ntp-tool-config.json
```

示例：

```json
{
  "client": {
    "enableAutoSync": false,
    "syncIntervalMinutes": 30,
    "timeoutMs": 3000,
    "retryCount": 3,
    "applySystemTime": false,
    "autoApplyThresholdMs": 500,
    "maxAllowedOffsetMs": 30000,
    "servers": [
      {
        "host": "time.windows.com",
        "port": 123,
        "priority": 1,
        "enabled": true
      },
      {
        "host": "pool.ntp.org",
        "port": 123,
        "priority": 2,
        "enabled": true
      }
    ]
  },
  "server": {
    "enableServer": false,
    "listenAddress": "0.0.0.0",
    "port": 123,
    "stratum": 2,
    "referenceId": "LOCAL",
    "allowAllClients": true,
    "allowedNetworks": [],
    "rateLimitPerMinute": 120,
    "logRequests": true
  },
  "log": {
    "level": "Information",
    "directory": "logs",
    "maxFileSizeMb": 10,
    "retentionDays": 30
  }
}
```

### 5.5.3 配置校验

配置加载时需要校验：

| 校验项     | 规则                |
| ---------- | ------------------- |
| 服务器地址 | 非空，IP / 域名合法 |
| 端口       | 1 - 65535           |
| 同步周期   | 不小于 10 秒        |
| 超时时间   | 100 ms - 30000 ms   |
| Stratum    | 1 - 15              |
| 监听地址   | 合法 IP             |
| 白名单     | CIDR 或 IP 合法     |
| 日志路径   | 可写                |

配置错误时：

- 使用默认值替代；
- 记录错误日志；
- UI 提示具体字段错误。

### 5.5.4 配置保存

支持：

- 启动时加载；
- 用户修改后保存；
- 保存前校验；
- 保存失败提示；
- 支持恢复默认配置。

---

## 5.6 日志需求

### 5.6.1 日志类型

| 类型       | 说明                 |
| ---------- | -------------------- |
| 运行日志   | 启动、停止、配置加载 |
| 同步日志   | 客户端同步结果       |
| 服务端日志 | 请求接收、响应、拒绝 |
| 错误日志   | 异常、超时、权限不足 |
| 审计日志   | 修改系统时间记录     |

### 5.6.2 日志级别

| 级别        | 说明         |
| ----------- | ------------ |
| Trace       | 详细跟踪     |
| Debug       | 调试信息     |
| Information | 正常运行信息 |
| Warning     | 警告         |
| Error       | 错误         |
| Fatal       | 严重错误     |

默认级别：

```text
Information
```

### 5.6.3 日志内容

每条日志至少包含：

- 时间戳；
- 日志级别；
- 模块；
- 消息；
- 异常信息。

示例：

```text
2026-06-22 10:30:00 [INFO] [NtpClient] 同步成功，服务器=time.windows.com，offset=+12.34ms，delay=28.5ms
2026-06-22 10:30:01 [WARN] [NtpServer] UDP 123 端口启动失败：端口被占用
2026-06-22 10:30:02 [ERROR] [SystemTime] 设置系统时间失败：权限不足
```

### 5.6.4 日志输出

日志应输出到：

1. UI 日志面板；
2. 本地文件；
3. 可选导出。

文件日志要求：

- 按日期或大小滚动；
- 默认保存到程序目录 `logs`；
- 单文件默认 10 MB；
- 保留天数默认 30 天。

---

## 5.7 状态监控需求

### 5.7.1 客户端状态机

```text
Stopped
  ↓ Start
Idle
  ↓ SyncRequest
Syncing
  ↓ Success
Success
  ↓ Failure
Failed
  ↓ Timer
Idle
```

状态说明：

| 状态    | 说明             |
| ------- | ---------------- |
| Stopped | 自动同步停止     |
| Idle    | 空闲等待下次同步 |
| Syncing | 正在同步         |
| Success | 最近一次同步成功 |
| Failed  | 最近一次同步失败 |

### 5.7.2 服务端状态机

```text
Stopped
  ↓ Start
Starting
  ↓ BindSuccess
Listening
  ↓ BindFailure
Error
  ↓ Stop
Stopped
```

状态说明：

| 状态      | 说明               |
| --------- | ------------------ |
| Stopped   | 未启动             |
| Starting  | 正在启动           |
| Listening | 正在监听           |
| Error     | 启动失败或运行异常 |

---

## 6. NTP 协议设计

## 6.1 NTP 报文格式

本工具采用 NTP v4 或兼容 SNTP v4，报文固定 48 字节。

报文结构如下：

```text
 0                   1                   2                   3
 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|LI | VN  |Mode |    Stratum    |     Poll      |   Precision   |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                          Root Delay                           |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                       Root Dispersion                         |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                     Reference Identifier                      |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                                                               |
|                   Reference Timestamp (64)                    |
|                                                               |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                                                               |
|                   Originate Timestamp (64)                    |
|                                                               |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                                                               |
|                    Receive Timestamp (64)                     |
|                                                               |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                                                               |
|                   Transmit Timestamp (64)                     |
|                                                               |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
```

### 6.2 字段说明

| 字段                |   长度 | 说明                   |
| ------------------- | -----: | ---------------------- |
| LI                  |  2 bit | Leap Indicator         |
| VN                  |  3 bit | Version Number，建议 4 |
| Mode                |  3 bit | 模式                   |
| Stratum             |  8 bit | 时钟层级               |
| Poll                |  8 bit | 轮询间隔               |
| Precision           |  8 bit | 精度                   |
| Root Delay          | 32 bit | 根延迟                 |
| Root Dispersion     | 32 bit | 根离散度               |
| Reference ID        | 32 bit | 参考标识               |
| Reference Timestamp | 64 bit | 参考时间戳             |
| Originate Timestamp | 64 bit | 客户端发送时间         |
| Receive Timestamp   | 64 bit | 服务端接收时间         |
| Transmit Timestamp  | 64 bit | 服务端发送时间         |

### 6.3 Mode 定义

| Mode | 含义              |
| ---: | ----------------- |
|    0 | reserved          |
|    1 | symmetric active  |
|    2 | symmetric passive |
|    3 | client            |
|    4 | server            |
|    5 | broadcast         |
|    6 | reserved          |
|    7 | reserved          |

本工具使用：

- Client 请求：Mode = 3；
- Server 响应：Mode = 4。

### 6.4 NTP 时间戳

NTP 时间戳为 64 位：

- 高 32 位：自 1900-01-01 00:00:00 UTC 起的秒数；
- 低 32 位：秒内小数部分。

与 .NET `DateTime` 转换时需要注意：

```text
NTP Epoch = 1900-01-01T00:00:00Z
Unix Epoch = 1970-01-01T00:00:00Z
差值 = 70 years = 2208988800 seconds
```

转换要求：

- 使用 UTC；
- 避免本地时区干扰；
- 使用高精度时间获取；
- Transmit Timestamp 应在发送前尽可能更新。

### 6.5 客户端请求报文

客户端发送请求时：

| 字段                | 值                    |
| ------------------- | --------------------- |
| LI                  | 0                     |
| VN                  | 4                     |
| Mode                | 3                     |
| Stratum             | 0                     |
| Poll                | 默认值，例如 3-6      |
| Precision           | 0                     |
| Root Delay          | 0                     |
| Root Dispersion     | 0                     |
| Reference ID        | 0                     |
| Reference Timestamp | 0                     |
| Originate Timestamp | 0                     |
| Receive Timestamp   | 0                     |
| Transmit Timestamp  | 发送时的本机 UTC 时间 |

### 6.6 服务端响应报文

服务端响应时：

| 字段                | 值                                  |
| ------------------- | ----------------------------------- |
| LI                  | 0 或配置值                          |
| VN                  | 4                                   |
| Mode                | 4                                   |
| Stratum             | 配置值，默认 2                      |
| Poll                | 可配置或默认                        |
| Precision           | 本机精度估算                        |
| Root Delay          | 配置或默认                          |
| Root Dispersion     | 配置或默认                          |
| Reference ID        | LOCAL 或配置值                      |
| Reference Timestamp | 本机最近一次时间源更新时间          |
| Originate Timestamp | 请求报文中客户端 Transmit Timestamp |
| Receive Timestamp   | 服务端收到请求时间                  |
| Transmit Timestamp  | 服务端发送响应时间                  |

### 6.7 Offset 与 Delay 计算

客户端根据四个时间戳计算：

```text
T1 = Originate Timestamp     客户端发送请求时间
T2 = Receive Timestamp       服务端接收请求时间
T3 = Transmit Timestamp      服务端发送响应时间
T4 = Destination Timestamp   客户端接收响应时间
```

计算公式：

```text
Offset = ((T2 - T1) + (T3 - T4)) / 2
RoundTripDelay = (T4 - T1) - (T3 - T2)
```

要求：

- Offset 支持毫秒显示；
- Delay 支持毫秒显示；
- Delay 异常为负或过大时标记不可信；
- 可配置最大可接受 Delay。

---

## 7. 系统架构设计

### 7.1 总体架构

系统采用分层架构：

```text
+--------------------------------------------------+
|                   WPF UI Layer                   |
|  MainWindow / SettingsView / LogsView / Status   |
+--------------------------------------------------+
|                 ViewModel Layer                  |
| MainViewModel / ClientViewModel / ServerViewModel|
+--------------------------------------------------+
|                 Application Layer                |
| NtpClientService / NtpServerService / Scheduler  |
+--------------------------------------------------+
|                   Domain Layer                   |
| NtpPacket / NtpSettings / SyncResult / TimeCalc  |
+--------------------------------------------------+
|                Infrastructure Layer              |
| UdpSocket / ConfigRepository / Logger / Win32Time|
+--------------------------------------------------+
```

### 7.2 模块划分

| 模块                   | 职责                        |
| ---------------------- | --------------------------- |
| UI                     | WPF 页面、控件、数据绑定    |
| ViewModel              | 状态管理、命令绑定、UI 逻辑 |
| NtpClientService       | 发起 NTP 请求、解析响应     |
| NtpServerService       | 监听 UDP、响应 NTP 请求     |
| SyncScheduler          | 定时任务调度                |
| SystemTimeService      | 获取 / 设置系统时间         |
| ConfigurationService   | 配置加载与保存              |
| LoggingService         | 日志记录                    |
| NetworkPermissionGuard | 端口、权限、防火墙检查      |

### 7.3 技术选型

| 类型     | 技术                                     |
| -------- | ---------------------------------------- |
| UI       | WPF                                      |
| 模式     | MVVM                                     |
| 语言     | C#                                       |
| 网络     | UdpClient / Socket                       |
| 异步     | async / await                            |
| 依赖注入 | Microsoft.Extensions.DependencyInjection |
| 配置     | Microsoft.Extensions.Configuration       |
| 日志     | Microsoft.Extensions.Logging             |
| 序列化   | System.Text.Json                         |
| 单元测试 | xUnit                                    |
| 打包     | MSIX / ClickOnce / 绿色目录发布，可选    |

### 7.4 项目结构建议

```text
NtpTool.sln
├── src
│   ├── NtpTool.App
│   │   ├── App.xaml
│   │   ├── MainWindow.xaml
│   │   ├── Views
│   │   ├── ViewModels
│   │   ├── Converters
│   │   └── Resources
│   ├── NtpTool.Core
│   │   ├── Models
│   │   ├── Services
│   │   ├── Ntp
│   │   └── Utilities
│   └── NtpTool.Infrastructure
│       ├── Config
│       ├── Logging
│       ├── Network
│       └── Windows
└── tests
    ├── NtpTool.Core.Tests
    └── NtpTool.Infrastructure.Tests
```

### 7.5 推荐项目文件

`NtpTool.App.csproj` 示例：

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <ApplicationManifest>app.manifest</ApplicationManifest>
  </PropertyGroup>

</Project>
```

如果默认需要管理员权限，可在 `app.manifest` 中配置：

```xml
<requestedExecutionLevel level="requireAdministrator" uiAccess="false" />
```

但更推荐：

```xml
<requestedExecutionLevel level="asInvoker" uiAccess="false" />
```

原因：普通功能不需要强制管理员，仅在修改系统时间或绑定 123 端口时提示提升。

---

## 8. 关键流程设计

## 8.1 应用启动流程

```text
1. 启动 WPF 应用
2. 加载配置
3. 初始化日志
4. 初始化依赖注入
5. 创建主窗口
6. 检查权限
7. 如果配置启用 Server，则尝试启动 NTP Server
8. 如果配置启用自动同步，则启动定时同步
9. 更新 UI 状态
```

## 8.2 客户端立即同步流程

```text
用户点击立即同步
  ↓
禁用同步按钮，状态置为 Syncing
  ↓
选择有效 NTP 服务器列表
  ↓
按优先级遍历服务器
  ↓
构造 NTP Request 报文
  ↓
记录 T1
  ↓
发送 UDP 请求
  ↓
等待响应，超时控制
  ↓
记录 T4
  ↓
解析 NTP Response
  ↓
计算 Offset / Delay
  ↓
判断结果是否有效
  ↓
展示结果
  ↓
如果开启自动修改系统时间且满足条件
  ↓
请求管理员权限或确认
  ↓
设置系统时间
  ↓
写入日志
  ↓
恢复按钮状态
```

## 8.3 客户端定时同步流程

```text
定时器触发
  ↓
检查是否正在同步
  ↓
如果正在同步则跳过
  ↓
执行同步流程
  ↓
成功：更新最近同步时间、状态
失败：记录错误，按策略重试
  ↓
连续失败达到阈值：状态告警
```

## 8.4 服务端接收请求流程

```text
UDP 监听线程收到数据
  ↓
记录 Receive Timestamp
  ↓
检查来源 IP 是否允许
  ↓
检查限流
  ↓
校验报文长度
  ↓
解析 NTP 报文
  ↓
校验 Mode 是否为 Client
  ↓
填充 Server 响应字段
  ↓
Originate Timestamp = Client Transmit Timestamp
  ↓
设置 Receive Timestamp
  ↓
设置 Transmit Timestamp
  ↓
发送响应
  ↓
更新统计信息
  ↓
记录日志
```

## 8.5 修改系统时间流程

```text
同步完成获得目标时间
  ↓
判断是否开启修改系统时间
  ↓
判断 Offset 是否超过阈值
  ↓
判断是否管理员权限
  ↓
如果权限不足，提示用户
  ↓
如果偏差过大，弹窗确认
  ↓
记录修改前时间
  ↓
调用 Windows API 设置系统时间
  ↓
记录修改后时间
  ↓
写入审计日志
```

---

## 9. 数据模型设计

### 9.1 NtpServerConfig

```csharp
public class NtpServerConfig
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 123;
    public int Priority { get; set; } = 100;
    public bool Enabled { get; set; } = true;
}
```

### 9.2 NtpClientOptions

```csharp
public class NtpClientOptions
{
    public bool EnableAutoSync { get; set; }
    public int SyncIntervalMinutes { get; set; } = 30;
    public int TimeoutMs { get; set; } = 3000;
    public int RetryCount { get; set; } = 3;
    public bool ApplySystemTime { get; set; }
    public double AutoApplyThresholdMs { get; set; } = 500;
    public double MaxAllowedOffsetMs { get; set; } = 30000;
    public List<NtpServerConfig> Servers { get; set; } = new();
}
```

### 9.3 NtpServerOptions

```csharp
public class NtpServerOptions
{
    public bool EnableServer { get; set; }
    public string ListenAddress { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 123;
    public byte Stratum { get; set; } = 2;
    public string ReferenceId { get; set; } = "LOCAL";
    public bool AllowAllClients { get; set; } = true;
    public List<string> AllowedNetworks { get; set; } = new();
    public int RateLimitPerMinute { get; set; } = 120;
    public bool LogRequests { get; set; } = true;
}
```

### 9.4 NtpSyncResult

```csharp
public class NtpSyncResult
{
    public DateTime SyncTimeUtc { get; set; }
    public string Server { get; set; } = string.Empty;
    public bool Success { get; set; }
    public double OffsetMs { get; set; }
    public double RoundTripDelayMs { get; set; }
    public byte Stratum { get; set; }
    public byte LeapIndicator { get; set; }
    public string? ErrorMessage { get; set; }
    public bool SystemTimeChanged { get; set; }
}
```

### 9.5 NtpPacket

```csharp
public class NtpPacket
{
    public byte LeapIndicator { get; set; }
    public byte VersionNumber { get; set; }
    public byte Mode { get; set; }
    public byte Stratum { get; set; }
    public byte Poll { get; set; }
    public byte Precision { get; set; }
    public uint RootDelay { get; set; }
    public uint RootDispersion { get; set; }
    public uint ReferenceId { get; set; }
    public DateTime ReferenceTimestamp { get; set; }
    public DateTime OriginateTimestamp { get; set; }
    public DateTime ReceiveTimestamp { get; set; }
    public DateTime TransmitTimestamp { get; set; }
}
```

### 9.6 NtpServerStatistics

```csharp
public class NtpServerStatistics
{
    public long TotalRequests { get; set; }
    public long ValidRequests { get; set; }
    public long InvalidRequests { get; set; }
    public long RejectedRequests { get; set; }
    public string? LastClientAddress { get; set; }
    public DateTime? LastRequestTimeUtc { get; set; }
}
```

---

## 10. 界面设计建议

## 10.1 主窗口布局

```text
+--------------------------------------------------------------+
|  NTP TimeSync Tool                                    [_][□][X]|
+--------------------------------------------------------------+
|  本地时间：2026-06-22 10:30:00                                |
|  UTC 时间：2026-06-22 02:30:00                                |
|                                                              |
|  Client 状态：运行中       Server 状态：监听中                |
|  上游服务器：time.windows.com                                 |
|  最近同步：2026-06-22 10:00:00                                |
|  时间偏差：+12.34 ms       往返延迟：28.5 ms                  |
+--------------------------------------------------------------+
| [立即同步] [启动自动同步] [停止自动同步] [配置] [日志]        |
+--------------------------------------------------------------+
| NTP Server                                                   |
| 状态：监听中  地址：0.0.0.0:123                               |
| 总请求：128  有效请求：120  非法请求：8                       |
| 最近客户端：192.168.1.25                                     |
| [启动服务端] [停止服务端]                                     |
+--------------------------------------------------------------+
| 日志                                                         |
| 2026-06-22 10:00:00 [INFO] 同步成功...                       |
| 2026-06-22 10:00:01 [INFO] NTP Server 已启动...              |
+--------------------------------------------------------------+
```

## 10.2 配置窗口布局

配置窗口可分为三个 Tab：

### Client Tab

```text
[x] 启用自动同步
同步周期：30 分钟
超时时间：3000 ms
重试次数：3
[x] 启动时执行一次

服务器列表：
+----------------------+------+-------+------+
| 服务器地址           | 端口 | 优先级 | 启用 |
+----------------------+------+-------+------+
| time.windows.com     | 123  | 1     | √    |
| pool.ntp.org         | 123  | 2     | √    |
+----------------------+------+-------+------+

[添加] [删除] [上移] [下移]
```

### Server Tab

```text
[x] 启用 NTP Server
监听地址：0.0.0.0
监听端口：123
Stratum：2
Reference ID：LOCAL
[x] 允许所有客户端
访问白名单：
192.168.1.0/24
[x] 记录请求日志
限流：120 次/分钟/IP
```

### Log Tab

```text
日志级别：Information
日志目录：logs
单文件大小：10 MB
保留天数：30
[x] 显示 UDP 详细日志
```

---

## 11. 异常处理需求

### 11.1 网络异常

| 异常               | 处理                 |
| ------------------ | -------------------- |
| DNS 解析失败       | 提示服务器地址无效   |
| 连接超时           | 切换下一服务器或重试 |
| UDP 无响应         | 标记超时             |
| 报文长度错误       | 丢弃并记录           |
| 响应 Mode 错误     | 丢弃并记录           |
| Stratum 为 0 或 16 | 标记服务器不可用     |

### 11.2 服务端异常

| 异常         | 处理                 |
| ------------ | -------------------- |
| 端口占用     | 提示用户             |
| 权限不足     | 提示管理员或更换端口 |
| Socket 异常  | 停止服务并记录       |
| 请求报文非法 | 丢弃并统计           |
| 高并发请求   | 限流保护             |

### 11.3 系统时间修改异常

| 异常              | 处理                 |
| ----------------- | -------------------- |
| 非管理员权限      | 提示需要管理员       |
| 修改失败          | 记录错误，不中断应用 |
| 偏差过大          | 弹窗确认             |
| Windows Time 干扰 | 提示用户             |

### 11.4 UI 异常

要求：

- 后台异常不能导致 UI 崩溃；
- 所有后台任务异常应捕获；
- 使用 `Dispatcher` 更新 UI；
- 长时间操作不能阻塞 UI 线程；
- 重要错误需要用户可见提示。

---

## 12. 安全需求

### 12.1 权限控制

| 功能               | 权限要求               |
| ------------------ | ---------------------- |
| 查询时间           | 普通用户               |
| 作为客户端同步显示 | 普通用户               |
| 修改系统时间       | 管理员                 |
| 监听 UDP 123       | 管理员或已具备端口权限 |
| 监听高端口         | 普通用户               |
| 停止 Windows Time  | 管理员，可选           |

### 12.2 网络安全

- 默认不提供公网服务端；
- 服务端建议仅在局域网启用；
- 支持白名单；
- 支持限流；
- 不响应非法 NTP 模式；
- 不记录敏感数据；
- 不开放 TCP 管理端口；
- 配置文件不包含敏感凭据。

### 12.3 时间安全

- 对 Stratum 为 0 或 16 的响应不采用；
- 对异常巨大 Offset 需要确认；
- 对 Delay 异常结果不自动修改系统时间；
- 可配置最大允许偏差；
- 可配置是否允许自动跳变。

---

## 13. 性能需求

| 指标           | 要求                               |
| -------------- | ---------------------------------- |
| UI 启动时间    | 小于 3 秒                          |
| 客户端同步超时 | 默认 3 秒                          |
| 客户端同步耗时 | 正常局域网小于 500 ms              |
| 服务端响应延迟 | 正常局域网小于 50 ms，不含网络传输 |
| 服务端并发能力 | 支持每秒至少 100 个简单 NTP 请求   |
| 内存占用       | 正常小于 200 MB                    |
| CPU 占用       | 空闲小于 1%                        |
| 日志写入       | 异步写入，不阻塞网络响应           |

---

## 14. 可靠性需求

1. 客户端同步失败不应导致程序崩溃；
2. 服务端异常应能自动停止并提示；
3. 配置损坏时可恢复默认配置；
4. 日志文件损坏不影响主流程；
5. 定时任务重复触发应防重入；
6. 应用退出时应释放 UDP Socket；
7. 应用退出时应停止定时器；
8. 系统休眠恢复后定时任务应能继续工作。

---

## 15. 可维护性需求

1. 代码遵循 C# 规范；
2. 核心协议模块独立于 UI；
3. NTP 报文序列化与反序列化单独封装；
4. 使用接口抽象服务；
5. 关键模块可单元测试；
6. 日志清晰可追踪；
7. 配置结构可扩展；
8. 避免在 UI 中写网络逻辑。

---

## 16. 测试需求

## 16.1 单元测试

### NTP 报文测试

- 请求报文长度为 48 字节；
- Mode = 3；
- Version = 4；
- Transmit Timestamp 正确写入；
- 响应报文能正确解析；
- 错误长度报文抛出异常或被拒绝；
- 时间戳转换正确。

### Offset / Delay 测试

给定：

```text
T1 = 10:00:00.000
T2 = 10:00:00.100
T3 = 10:00:00.110
T4 = 10:00:00.030
```

期望：

```text
Offset = ((0.100) + (0.080)) / 2 = 0.090s = 90ms
Delay = (0.030) - (0.010) = 0.020s = 20ms
```

### 配置测试

- 正常配置加载；
- 非法端口拒绝；
- 非法周期修正；
- 空配置使用默认值；
- JSON 序列化与反序列化一致。

## 16.2 集成测试

| 测试项                   | 预期结果         |
| ------------------------ | ---------------- |
| 启动服务端并监听高端口   | 成功             |
| 使用客户端请求本机服务端 | 成功返回时间     |
| 请求公共 NTP 服务器      | 成功获得 Offset  |
| 请求不可达服务器         | 超时并提示       |
| 端口被占用               | 提示端口冲突     |
| 非管理员修改系统时间     | 提示权限不足     |
| 管理员修改系统时间       | 成功并可记录日志 |
| 白名单拒绝               | 不响应或拒绝记录 |
| 限流测试                 | 超过阈值后丢弃   |

## 16.3 手动验收用例

### 用例 1：客户端立即同步

步骤：

1. 配置有效 NTP 服务器；
2. 点击立即同步；
3. 查看结果。

预期：

- 显示成功；
- 显示 Offset；
- 显示 Delay；
- 日志记录成功。

### 用例 2：服务端响应

步骤：

1. 启动 NTP Server，端口设为 10123；
2. 使用 `w32tm /stripchart /computer:127.0.0.1 /port:10123` 或自定义客户端测试；
3. 查看请求统计。

预期：

- 服务端成功响应；
- 请求数增加；
- 日志记录请求。

### 用例 3：端口冲突

步骤：

1. 启动其他程序占用 UDP 123；
2. 启动本工具 NTP Server；
3. 观察提示。

预期：

- 启动失败；
- UI 显示明确错误；
- 日志记录 SocketException。

### 用例 4：自动同步

步骤：

1. 开启自动同步；
2. 设置周期为 10 秒；
3. 观察日志。

预期：

- 每 10 秒触发一次；
- 不重复并发执行；
- 可随时停止。

---

## 17. 部署与运行说明

### 17.1 编译运行

```bash
dotnet --version
dotnet build
dotnet run --project src/NtpTool.App
```

### 17.2 发布

```bash
dotnet publish src/NtpTool.App -c Release -r win-x64 --self-contained false
```

或自包含发布：

```bash
dotnet publish src/NtpTool.App -c Release -r win-x64 --self-contained true
```

### 17.3 端口说明

| 场景     | 端口             |
| -------- | ---------------- |
| 标准 NTP | UDP 123          |
| 测试推荐 | UDP 10123、20123 |
| 防火墙   | 需允许 UDP 入站  |

### 17.4 Windows 时间服务说明

如果本机 UDP 123 被占用，可检查：

```powershell
Get-Service w32time
```

可停止：

```powershell
Stop-Service w32time
```

不建议默认由程序自动停止系统服务。

---

## 18. 风险与对策

| 风险                         | 影响                 | 对策                       |
| ---------------------------- | -------------------- | -------------------------- |
| UDP 123 被 Windows Time 占用 | 服务端无法启动       | 提示停止服务或更换端口     |
| 修改系统时间需要管理员权限   | 自动校时失败         | 明确提示，默认不修改       |
| 上游 NTP 不可达              | 同步失败             | 多服务器、超时、重试       |
| 局域网防火墙拦截             | 客户端无法访问服务端 | 提示添加防火墙规则         |
| 时间偏差过大                 | 业务异常             | 大偏差需确认               |
| 系统休眠导致定时器异常       | 定时同步失败         | 休眠恢复后检查并执行       |
| 高并发请求                   | 服务端性能下降       | 限流与异步处理             |
| 配置文件损坏                 | 启动失败             | 使用默认配置并备份错误文件 |

---

## 19. 非目标与限制

本工具第一版不实现：

1. NTP 认证；
2. 广播 / 组播模式；
3. 复杂时钟筛选与最优源选择算法；
4. 时钟频率漂移补偿；
5. 多实例高可用部署；
6. Linux / macOS 支持；
7. Web 管理界面；
8. 用户账号与远程访问控制；
9. 数据库存储历史记录；
10. 自动更新。

---

## 20. 后续扩展方向

| 扩展项           | 说明                         |
| ---------------- | ---------------------------- |
| 多源优选         | 同时请求多个服务器，选择最优 |
| 平滑校时         | 小偏差采用缓慢调整           |
| NTP 认证         | 支持对称密钥认证             |
| Prometheus 指标  | 输出运行指标                 |
| Windows 服务版本 | 提供后台服务运行             |
| 托盘程序         | 最小化到系统托盘             |
| 历史记录图表     | Offset / Delay 曲线          |
| IPv6 支持        | 监听与请求 IPv6              |
| 日志导出         | 支持 CSV / TXT               |
| 多语言           | 中文 / 英文                  |

---

## 21. 验收标准

### 21.1 功能验收

1. 可以成功作为 NTP 客户端从配置的服务器同步时间；
2. 可以成功启动 NTP Server 并响应客户端请求；
3. 可以手动立即同步；
4. 可以配置定时自动同步；
5. 可以查看 Offset 与 Delay；
6. 可以在管理员权限下修改系统时间；
7. 可以保存和加载配置；
8. 可以记录日志；
9. 端口冲突时有明确提示；
10. 应用退出后无端口残留。

### 21.2 质量验收

1. 连续运行 8 小时无崩溃；
2. 自动同步任务无重复并发；
3. UI 操作不卡顿；
4. 网络异常不导致程序退出；
5. 日志文件可正常写入；
6. 单元测试通过；
7. 集成测试通过。

---

## 22. 推荐开发计划

| 阶段   | 内容                             | 建议工期 |
| ------ | -------------------------------- | -------: |
| 阶段 1 | 项目框架、配置、日志、主界面骨架 |     1 天 |
| 阶段 2 | NTP 报文封装、客户端请求与解析   |     2 天 |
| 阶段 3 | 定时同步、同步结果展示           |     1 天 |
| 阶段 4 | NTP Server 监听与响应            |     2 天 |
| 阶段 5 | 系统时间修改、权限处理           |     1 天 |
| 阶段 6 | 白名单、限流、统计               |     1 天 |
| 阶段 7 | 测试、打包、文档完善             |     2 天 |

总计约：

```text
10 个工作日
```

---

## 23. 最终交付物

1. 源代码；
2. 解决方案文件；
3. 可运行程序或发布包；
4. 配置文件；
5. 用户说明书；
6. 测试报告；
7. 需求设计文档；
8. 部署说明。

---

## 24. 结论

本项目为基于 `.NET 10.0-windows`、WPF 和 C# 的轻量级 NTP 时间同步工具。工具同时具备 NTP Client 与 NTP Server 能力，支持手动同步、定时同步、服务端监听、日志记录、配置管理和系统时间更新。第一版应以稳定、简单、可维护为目标，优先实现核心 NTP 客户端与服务端功能，不引入复杂时钟算法和认证机制，确保工具适合在 Windows 桌面、局域网测试和轻量级时间同步场景中使用。