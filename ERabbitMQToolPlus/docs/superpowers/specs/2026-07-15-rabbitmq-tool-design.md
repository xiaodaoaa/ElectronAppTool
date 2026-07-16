# Electron RabbitMQ 工具 — 设计文档

> 基于 `1.md` 需求文档，经 brainstorming 澄清后的实现设计。
> 日期：2026-07-15

## 0. 关键决策（澄清结论）

| 决策点 | 结论 | 理由 |
| :--- | :--- | :--- |
| Windows 7 兼容 | **放弃，以 Win10+ 为准** | 文档开头「必须兼容 Win7」与 5.2「Win10+」矛盾；Electron 23+ 已放弃 Win7，现代栈无法兼容 Win7。全部用最新稳定版。 |
| UI 框架 | **Vue 3 + Element Plus** | 文档推荐，表单/表格组件丰富，中文友好。 |
| 脚手架 | **electron-vite** | 专为 Electron+Vue 优化，三进程热更新，打包用 electron-builder。 |
| AMQP 进程归属 | **主进程 + IPC** | 安全（密码/证书不进渲染进程），符合 Electron 最佳实践。 |
| 证书存储 | **存路径** | 选择后只存文件路径，连接时主进程按路径读取。 |
| 实现范围 | **MVP 核心闭环** | 连接管理（含 SSL）+ 生产者 + 消费者 + 基础日志；设置/帮助/导出/快捷键后续迭代。 |
| 架构组织 | **方案 A：单连接管理器 + 按功能分服务** | 单连接多 channel（amqplib 推荐做法），服务层抽象让 IPC 干净，为多连接扩展留口子。 |

## 1. 技术栈

- 桌面框架：Electron（最新稳定版，Win10+）
- 构建：electron-vite（主/preload/渲染三进程热更新）+ electron-builder（打包）
- 前端：Vue 3 + Element Plus + Pinia（状态管理）
- AMQP 客户端：amqplib
- 本地存储：electron-store（连接配置持久化）
- 工具库：dayjs（时间格式化）、uuid（消息ID）

## 2. 目录结构

```
ERabbitMQTool/
├─ electron.vite.config.ts        # electron-vite 配置
├─ package.json
├─ resources/                     # 图标等静态资源
├─ src/
│  ├─ main/                       # 主进程
│  │  ├─ index.ts                 # 入口：创建窗口、注册 IPC
│  │  ├─ ipc/                     # IPC handler 注册
│  │  │  ├─ connection.ts         # 连接：connect/test/disconnect
│  │  │  ├─ producer.ts           # 生产者：send/listHistory
│  │  │  └─ consumer.ts           # 消费者：start/pause/stop/ack/nack
│  │  ├─ services/                # RabbitMQ 服务层
│  │  │  ├─ ConnectionManager.ts  # 单例：管连接 + channel 池
│  │  │  ├─ ProducerService.ts    # 声明交换机 + publish
│  │  │  └─ ConsumerService.ts    # 声明队列 + consume + ack/nack
│  │  └─ utils/
│  │     ├─ ssl.ts                # SSL 配置构造
│  │     └─ store.ts              # electron-store 封装（密码加密）
│  ├─ preload/                    # 预加载脚本
│  │  └─ index.ts                 # contextBridge 暴露 window.api
│  └─ renderer/                   # 渲染进程
│     ├─ index.html
│     ├─ src/
│     │  ├─ main.ts               # Vue 入口
│     │  ├─ App.vue               # 根布局（顶部导航 + Tab + 状态栏）
│     │  ├─ components/           # ConnectionForm、MessageTable、LogPanel
│     │  ├─ views/               # ProducerView、ConsumerView
│     │  ├─ stores/              # connection、producer、consumer、log
│     │  └─ utils/               # amqp 类型、格式化
```

主进程 `services/` 层承载业务逻辑，`ipc/` 层只做参数转发 + 调用 service。preload 用 `contextBridge` 暴露最小 API 面。渲染进程 Pinia store 调 `window.api.*`，UI 只管状态。

## 3. 连接管理模块

### 3.1 连接配置数据结构

```ts
interface ConnectionConfig {
  host: string;            // 必填，默认 localhost
  port: number;            // 必填，默认 5672（SSL 默认 5671）
  vhost: string;           // 默认 '/'
  username: string;        // 必填，默认 guest
  password: string;        // 必填
  timeout: number;         // 默认 5000ms
  sslEnabled: boolean;     // 启用 SSL 开关
  caPath?: string;         // CA 证书路径
  certPath?: string;       // 客户端证书路径（双向认证）
  keyPath?: string;        // 客户端私钥路径
  passphrase?: string;     // 私钥密码
  rejectUnauthorized: boolean; // true=验证证书，false=关闭证书认证
}
```

### 3.2 SSL 配置构造逻辑（`src/main/utils/ssl.ts`）

```
sslEnabled = false → 协议 amqp://，无 TLS 选项
sslEnabled = true:
  rejectUnauthorized = false（关闭证书认证）→ amqps://，TLS 仅设 rejectUnauthorized:false
  rejectUnauthorized = true（验证）→ amqps://，读取 ca/cert/key 文件，构造 TLS 选项
```

### 3.3 校验规则（连接前）

- 开启 SSL 但未填 caPath 且 rejectUnauthorized=true → 报错「请上传 CA 证书或开启关闭证书认证」
- 填了 certPath 但未填 keyPath → 报错「上传客户端证书时需同时填写私钥」
- 证书文件路径不存在 → 报错「CA 证书路径无效：文件不存在」（具体化错误，对应 5.4 易用性）

### 3.4 ConnectionManager（单例）职责

- `connect(config)`：构造 amqplib 连接选项（含 SSL），调用 `amqp.connect()`，保存连接实例，更新状态。
- `getConnection()`：返回当前连接，供 Producer/ConsumerService 复用。
- `isConnected()`：连接状态查询。
- `disconnect()`：关闭所有 channel + 连接，重置生产者/消费者状态（通过事件通知）。
- 连接级事件：`close`/`error` 监听，异常断开时主动通知渲染进程更新状态栏。

### 3.5 IPC 接口

```ts
window.api.connection.connect(config): Promise<{success, error?}>
window.api.connection.test(config): Promise<{success, error?}>  // 测试连接，不保存实例
window.api.connection.disconnect(): Promise<void>
window.api.connection.onStatusChange(cb): void  // 主进程推送连接状态变化
```

### 3.6 持久化（`src/main/utils/store.ts`）

- electron-store 保存最近一次连接配置，启动时自动加载。
- 密码字段用 Node `crypto` 模块加密存储（对应 5.3 安全性），不存明文。
- 证书只存路径，不复制文件。

### 3.7 状态栏显示

- 未连接：灰色「未连接」
- 普通连接：`amqp://user@host:port/vhost`
- SSL 连接：`amqps://...` + 「SSL加密连接」标识，附「证书认证已关闭」提示（若 rejectUnauthorized=false）

## 4. 生产者 Tab 页

### 4.1 目标交换机配置

- 交换机名称（空=默认交换机）、类型（Direct/Topic/Fanout/Headers）、路由键（Fanout 可留空）
- 自动声明交换机开关、交换机持久化开关

### 4.2 消息内容配置

- 消息格式下拉（JSON/纯文本/XML）
- 消息体代码编辑器（JSON 自动格式化 + 语法高亮，最大 10MB）
- 消息属性表单（动态添加）：`contentType`、`contentEncoding`、`deliveryMode`（1=非持久化/2=持久化）、`priority`、`expiration`、`headers`
- 批量发送：发送次数（默认1）+ 间隔（如每100ms发1条）

### 4.3 ProducerService 职责（`src/main/services/ProducerService.ts`）

- `send(params)`：从 ConnectionManager 取连接，建 channel
  - 自动声明开启 → `assertExchange(exchange, type, {durable})`
  - 自动声明关闭 + 交换机不存在 → 抛错「交换机不存在：请检查名称或开启自动声明」
  - 构造消息属性对象 → `channel.publish(exchange, routingKey, Buffer, options)`
  - 批量发送：循环 publish，按间隔延时，进度通过 `webContents.send('producer:progress', {current, total})` 推送
- `getHistory()`：返回最近 10 条发送记录（时间、交换机、路由键、消息体摘要）

### 4.4 IPC 接口

```ts
window.api.producer.send(params): Promise<{success, messageId?, error?}>
window.api.producer.onProgress(cb): void  // 批量发送进度推送
window.api.producer.getHistory(): Promise<HistoryItem[]>
```

### 4.5 发送参数 params

```ts
interface SendParams {
  exchange: string;
  exchangeType?: 'direct'|'topic'|'fanout'|'headers';
  routingKey: string;
  autoDeclare: boolean;
  durable: boolean;
  message: string;
  format: 'json'|'text'|'xml';
  properties: {
    contentType?: string;
    contentEncoding?: string;
    deliveryMode?: 1|2;
    priority?: number;
    expiration?: string;
    headers?: Record<string, any>;
  };
  batch: { count: number; intervalMs: number };
}
```

### 4.6 操作与反馈

- 「发送消息」按钮：触发 send，批量时显示进度 `5/5`
- 「清空消息」按钮：重置消息体
- 历史记录折叠面板：最近10条，点击重新加载到编辑器
- 成功：绿色提示「发送成功，消息ID：xxx」
- 失败：红色提示具体原因（交换机不存在、路由键无效等）

### 4.7 校验

- 未连接 → 提示「请先建立连接」
- JSON 格式但消息体非法 → 发送前校验，提示「消息体不是合法 JSON」

## 5. 消费者 Tab 页

### 5.1 队列配置

- 队列名称（支持通配符如 `order.*`）、自动声明队列开关、队列持久化开关
- 排他队列开关、自动删除开关
- 绑定交换机（空=默认交换机）、绑定路由键（Topic 支持 `#`/`*`）

### 5.2 消费配置

- 消费模式：推模式（实时）/ 拉模式（手动拉取N条）
- 预取计数 QoS（默认0=无限制）
- 自动确认开关（推荐关闭，避免消息丢失）
- 消息过滤：按 `header` 或 `routingKey`（如 `routingKey=order.create`）
- 最大接收数（0=无限制，达到后自动停止）

### 5.3 ConsumerService 职责（`src/main/services/ConsumerService.ts`）

- `start(config)`：取连接建 channel
  - 自动声明开启 → `assertQueue(queue, {durable, exclusive, autoDelete})` + `bindQueue`
  - 自动声明关闭 + 队列不存在 → 抛错「队列不存在：请检查名称或开启自动声明」
  - `channel.prefetch(qos)`
  - 推模式：`channel.consume(queue, callback, {noAck: !autoAck})`，回调里过滤后 `webContents.send('consumer:message', msg)` 推给渲染进程
  - 拉模式：暴露 `pull(count)`，用 `channel.get(queue, {noAck})` 循环拉取
- `pause()` / `resume()`：通过 `channel.cancel(consumerTag)` 暂停，重新 consume 恢复
- `stop()`：cancel + 关闭 channel，清空未确认消息列表
- `ack(deliveryTag)`：`channel.ack(msg)`
- `nack(deliveryTag, requeue)`：`channel.nack(msg, false, requeue)`（requeue=true 回队列，false 进死信）

### 5.4 消息过滤逻辑（主进程侧，收到消息后判断）

- routingKey 过滤：消息 routingKey 匹配过滤表达式则保留
- header 过滤：消息 headers 中指定 key=值 则保留
- 不匹配的消息处理（明确，避免堆积）：
  - autoAck=true（自动确认开启）：消息由 amqplib 自动 ack，无需处理，不进列表
  - autoAck=false（手动确认）：主进程对不匹配消息主动调用 `channel.ack(msg)` 确认丢弃，不进列表、不推送给渲染进程
  - 即：过滤掉的消息一律不进渲染进程列表，且在手动模式下由主进程代为 ack，避免队列堆积

### 5.5 IPC 接口

```ts
window.api.consumer.start(config): Promise<{success, error?}>
window.api.consumer.pause(): Promise<void>
window.api.consumer.resume(): Promise<void>
window.api.consumer.stop(): Promise<void>
window.api.consumer.pull(count): Promise<Message[]>  // 拉模式
window.api.consumer.ack(deliveryTag): Promise<void>
window.api.consumer.nack(deliveryTag, requeue): Promise<void>
window.api.consumer.onMessage(cb): void  // 推模式消息推送
```

### 5.6 消息数据结构

```ts
interface ConsumedMessage {
  seq: number;              // 序号
  receivedAt: string;       // 接收时间
  exchange: string;
  routingKey: string;
  messageId?: string;
  deliveryTag: number;
  content: string;          // 消息体（列表截断，详情弹窗完整）
  properties: { contentType?, headers?, priority?, deliveryMode?, ... };
}
```

### 5.7 消息展示与操作

- 消息列表表格：序号、接收时间、交换机、路由键、消息ID、deliveryTag、消息体（截断）、操作（查看详情/确认/拒绝）
- 消息详情弹窗：完整消息体（JSON 格式化/压缩切换）+ 所有 AMQP 属性
- 手动确认：「确认」=basicAck；「拒绝」=basicNack，弹选择「重新入队」或「丢弃」
- 控制按钮：开始消费/暂停消费/停止消费（停止后清空未确认列表）
- 最大接收数达到 → 自动 stop

### 5.8 校验

- 未连接 → 提示「请先建立连接」
- 推模式 + 自动确认关闭时，未确认消息列表随 ack/nack 实时更新

## 6. 辅助功能、布局与错误处理

### 6.1 全局布局（`App.vue`）

- 顶部导航栏：应用名、连接状态指示（未连接/已连接）、设置入口（主题切换、日志查看）
- Tab 容器：生产者 Tab、消费者 Tab
- 底部状态栏：连接信息（`amqp://...` 或 `amqps://...` + SSL 标识）、累计发送/接收数

### 6.2 日志系统（MVP 简化版）

- 全局日志面板（可折叠）：记录连接事件、发送/接收事件、错误信息
- 每条日志：`[时间] 级别 内容`，级别 INFO/WARN/ERROR
- MVP 不做级别筛选与导出（后续迭代），只做时间倒序列表展示 + 自动滚动
- 日志来源：渲染进程各 store 主动写入 + 主进程通过 IPC 推送的连接/错误事件

### 6.3 设置中心（MVP 简化）

- 主题切换：浅色/深色（Element Plus 内置 dark 主题切换）
- 性能配置：最大消息缓存数（默认1000）、消息体最大显示长度（默认1000字符）—— 影响消费者消息列表内存
- 快捷键、帮助文档：后续迭代，MVP 不做

### 6.4 错误处理策略

- 所有 IPC 调用返回 `{success, error?}` 结构，错误信息具体化（对应 5.4）：
  - 证书路径无效 → 「CA 证书路径无效：文件不存在」
  - 认证失败 → 「认证失败：用户名/密码错误」
  - 交换机/队列不存在 → 带操作建议
- 主进程 amqplib 异常统一捕获，转成可读消息再回传渲染进程
- 连接级 `close`/`error` 事件：异常断开时弹通知 + 状态栏置为「未连接（连接已断开）」

### 6.5 性能与内存

- 消费者消息列表超过最大缓存数（默认1000）→ 丢弃最旧消息（FIFO），避免内存涨
- 消息体超长 → 列表截断显示，详情弹窗完整展示
- 单连接多 channel 复用（amqplib 推荐做法），生产者/消费者各用独立 channel

### 6.6 安全性

- 渲染进程不开 `nodeIntegration`、不开 `contextIsolation=false`，全部走 contextBridge
- 密码加密存储（electron-store + crypto）
- 「关闭证书认证」默认关闭，开启时状态栏标注风险

## 7. 范围说明（MVP 边界）

**首版实现（MVP）**：连接管理（含 SSL）+ 生产者 Tab + 消费者 Tab + 基础日志 + 主题/性能配置。

**后续迭代（不在首版）**：日志级别筛选与导出、快捷键、帮助文档、消息导出 CSV/JSON、多连接管理、RabbitMQ 管理 API、消息重放、STOMP/MQTT 协议（对应 `1.md` 第 6 章）。
