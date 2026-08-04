# NTP TimeSync Tool 用户说明书

> 基于 `.NET 10` 与 WPF 的轻量级 NTP 时间同步工具。本工具同时具备 **NTP 客户端**与 **NTP 服务端**能力，并可通过**图形化设置界面**完成全部配置。本说明书对应 `docs/CSNtpd需求设计文档.md` 的实现版本。

## 1. 运行环境

| 项目 | 要求 |
| --- | --- |
| 操作系统 | Windows 10 / 11 / Server 2016+（**不支持 Windows 7**） |
| 运行时 | .NET 10（或使用"绿色单文件"自包含发布，则无需安装运行时） |
| 权限 | 普通用户可运行；**修改系统时间**或**监听 123 端口**需要管理员权限 |

## 2. 获取与启动

### 2.1 从源码运行

```bash
dotnet run --project src/NtpTool.App
```

### 2.2 绿色单文件发布（推荐）

一条命令生成单个 `NtpTool.App.exe`，**自包含 .NET 运行时**，目标电脑无需安装任何依赖，双击即用：

```bash
dotnet publish src/NtpTool.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

产物位于 `src/NtpTool.App/bin/Release/net10.0-windows/win-x64/publish/NtpTool.App.exe`（约 165 MB），拷贝到任意 Win10/11 电脑即可运行。

> 首次运行未签名 exe 时，Windows 可能弹出"安全警告"，点**仍要运行**即可。

### 2.3 启动流程

程序启动时会：

1. 加载配置（`ntp-tool-config.json`，位于程序目录，不存在则使用默认值）；
2. 初始化日志（默认写入 `logs/` 目录）；
3. 显示主窗口；
4. 若配置启用，自动启动 NTP Server 与自动同步。

### 2.4 单实例限制

程序**同一时间只允许一个实例运行**。重复启动时，新实例会自动激活已有的主窗口并退出，不会重复运行。

## 3. 主界面说明

主窗口包含：

- **本地时间 / UTC 时间**：每秒刷新。
- **NTP Client 区域**：显示上游服务器、状态、最近同步、时间偏差、往返延迟。
  - `立即同步`：立即向上游服务器同步一次。
  - `启动自动同步` / `停止自动同步`：控制定时同步。
  - `修改系统时间`：按最近一次同步结果的偏差调整本机时间（需管理员）。
- **NTP Server 区域**：显示监听地址、状态、总/有效/非法/拒绝请求数、最近客户端。
  - `启动服务端` / `停止服务端`：控制 UDP 监听。
- **操作栏**：`打开配置`（查看配置路径）、`清除日志`。
- **日志面板**：实时显示运行 / 同步 / 服务端日志。

## 4. 系统托盘

关闭主窗口时**最小化到系统托盘**（不会退出程序）。托盘图标提供：

- `显示主界面`：重新打开主窗口（双击托盘图标亦可）。
- `立即同步`：手动同步一次。
- `启动服务端` / `停止服务端`：快捷启停 NTP 服务端。
- `设置`：打开图形化设置界面。
- `退出`：真正结束程序。

> 只有从托盘选择"退出"才会彻底退出程序；关闭主窗口只是最小化到托盘。

## 5. 图形化设置界面

通过托盘菜单 `设置` 或主界面 `打开配置` 打开。设置界面采用 **Windows 11 风格**（左侧导航 + 右侧卡片），分为 **客户端 / 服务端 / 日志** 三个页面。修改后点 `保存` 即生效并应用，`恢复默认` 还原配置，`取消` 放弃修改。

### 5.1 客户端页

- **自动同步**：`启用自动同步`、`启动时执行一次`、`同步周期（分钟）`、`超时（ms）`、`失败重试次数`、`重试间隔（ms）`。
- **系统时间修正**：`同步后自动修正系统时间`、`自动修正阈值（ms）`、`最大允许偏差（ms）`。
- **上游服务器**：列表增删、编辑所选服务器（主机/IP、端口、优先级、备注、启用开关）。

### 5.2 服务端页

- **监听**：`启用 NTP Server`、`监听地址`、`监听端口`、`Stratum`、`Reference ID`、`每 IP 每分钟限流`。
- **访问控制**：`允许所有客户端`、`记录请求日志`、访问白名单（CIDR/IP，可增删）。

### 5.3 日志页

- `日志级别`、`日志目录`、`单文件大小（MB）`、`保留天数`、`显示 UDP 详细日志`。

## 6. 常用配置（手动编辑）

设置界面的修改都会写入程序目录下的 `ntp-tool-config.json`（示例见 `ntp-tool-config.json.sample`）。也可直接手动编辑：

### 客户端

```json
"client": {
  "enableAutoSync": false,        // 是否启用定时同步
  "syncIntervalMinutes": 30,      // 同步周期（分钟）
  "timeoutMs": 3000,              // 单次请求超时（毫秒）
  "retryCount": 3,                // 失败重试次数
  "retryIntervalMs": 10000,       // 重试间隔（毫秒）
  "runOnceOnStart": false,        // 启动时是否立即同步一次
  "applySystemTime": false,       // 是否自动修改系统时间（建议 false）
  "autoApplyThresholdMs": 500,    // 偏差超过该值且开启自动修改时才修改
  "maxAllowedOffsetMs": 30000,    // 超过该偏差不会自动修改（需人工）
  "maxAcceptableDelayMs": 10000,  // 超过该往返延迟视为无效结果
  "failureWarningThreshold": 3,   // 连续失败次数触发告警
  "servers": [                    // 按 priority 升序尝试
    { "host": "time.windows.com", "port": 123, "priority": 1, "enabled": true, "remark": "" }
  ]
}
```

> 建议场景：局域网内使用 `192.168.x.x` 服务器，公网使用 `pool.ntp.org`。

### 服务端

```json
"server": {
  "enableServer": false,          // 是否自动启动服务端
  "listenAddress": "0.0.0.0",     // 监听地址
  "port": 123,                    // 端口；测试可用 10123 / 20123
  "stratum": 2,
  "referenceId": "LOCAL",         // Reference ID 标识
  "allowAllClients": true,        // 关闭后启用白名单
  "allowedNetworks": ["192.168.1.0/24"],   // 访问白名单（CIDR/IP）
  "rateLimitPerMinute": 120,      // 每 IP 每分钟限流
  "logRequests": true,            // 记录请求日志
  "logRejectedRequests": true     // 记录被拒绝的请求
}
```

### 日志

```json
"log": {
  "level": "Information",         // Trace/Debug/Information/Warning/Error/Fatal
  "directory": "logs",
  "maxFileSizeMb": 10,            // 单文件大小，超出滚动
  "retentionDays": 30,            // 保留天数，超出清理
  "logUdpDetails": false          // 是否记录 UDP 详细日志
}
```

## 7. 关键操作要点

### 修改系统时间

1. 先执行一次「立即同步」（需得到成功的同步结果）。
2. 点击「修改系统时间」。
3. 需**以管理员身份运行**程序；程序会弹出确认，确认后写入并记录审计日志。

> 注意：若 `Windows Time (w32time)` 服务运行，其可能覆盖手动修改的系统时间。可在服务管理器停止 `w32time`。

### 作为服务端服务局域网

1. 将 `enableServer` 设为 `true`，端口可保持 `123`（标准端口）。
2. **以管理员身份运行**（低端口需要权限）。
3. 若 `123` 被占用（常为 `w32time`），停止该服务或改用高端口。
4. 局域网客户端配置为指向本机 IP 与对应端口即可。

### 端口占用排查

```powershell
# 查看 w32time 服务
Get-Service w32time
# 停止（可选）
Stop-Service w32time
```

程序启动服务端失败时会显示明确错误提示。

## 8. 安全建议

- 默认**不要**在公网开放 NTP 服务端；建议仅在局域网使用。
- 开启服务端时建议配置 `allowedNetworks` 白名单并保持限流。
- 自动修改系统时间默认关闭；大偏差（超过 `maxAllowedOffsetMs`）不会自动修改，需要人工确认。
- 配置文件不包含敏感凭据。

## 9. 故障排查

| 现象 | 处理 |
| --- | --- |
| 端口被占用 / 需管理员 | 停止 `w32time` 或改用高端口，或管理员运行 |
| 同步失败 | 检查网络、服务器可达性；启用多服务器自动切换 |
| 无法修改系统时间 | 需要管理员权限 |
| 重复启动无反应 | 属正常，单实例限制会自动激活已有窗口 |
| 日志文件损坏 | 不影响主流程，删除后重新生成 |
| 配置损坏 | 删除 `ntp-tool-config.json`，程序回退默认值 |

## 10. 构建与测试

```bash
# 构建
dotnet build NtpTool.slnx

# 测试
dotnet test NtpTool.slnx

# 绿色单文件发布（页面含 .NET 运行时，无需安装）
dotnet publish src/NtpTool.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```