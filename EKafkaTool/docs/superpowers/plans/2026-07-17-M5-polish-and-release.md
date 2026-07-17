# M5: 打磨与发布 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 错误处理补全、日志完善、设置页面、electron-builder 打包配置、最终验证。

**Architecture:** 补全全局错误处理、统一 IPC 错误格式、设置页面（日志目录/删除 Topic 开关/消息缓冲上限）、electron-builder 多平台打包配置。

**Tech Stack:** 同 M1（electron-builder, electron-log）

**前置条件:** M1 + M2 + M3 + M4 完成

---

## 文件结构规划

| 文件 | 职责 |
|---|---|
| `src/main/ipc/registerHandlers.ts`（修改） | 统一错误处理包装 |
| `src/main/index.ts`（修改） | 全局异常捕获、disconnect 清理 |
| `src/renderer/views/SettingsView.vue` | 设置页面 |
| `src/renderer/stores/settingsStore.ts` | Pinia 设置状态管理 |
| `src/renderer/router/index.ts`（修改） | 添加设置页路由 |
| `src/renderer/App.vue`（修改） | 侧边栏添加设置入口 |
| `package.json`（修改） | electron-builder 完整配置 |
| `resources/icon.ico` | Windows 图标 |
| `resources/icon.icns` | macOS 图标 |
| `resources/icon.png` | Linux 图标（512x512） |

---

### Task 1: 统一 IPC 错误处理

**Files:**
- Modify: `src/main/ipc/registerHandlers.ts`

- [ ] **Step 1: 在 registerHandlers.ts 中添加错误处理包装器**

在 `registerAllHandlers()` 函数开头添加辅助函数：

```ts
function wrapHandler<T extends (...args: unknown[]) => unknown>(fn: T): T {
  return (async (...args: unknown[]) => {
    try {
      return await fn(...args)
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : String(err)
      logger.error('IPC Handler 错误:', message)
      throw { code: 'IPC_ERROR', message }
    }
  }) as T
}
```

将所有 `ipcMain.handle` 的第二个参数用 `wrapHandler` 包装。例如：

```ts
ipcMain.handle(IPC_CHANNELS.CONN_LIST, wrapHandler(async () => {
  return loadConnections()
}))

ipcMain.handle(IPC_CHANNELS.CONN_CONNECT, wrapHandler(async (_event, id: string) => {
  const configs = loadConnections()
  const config = configs.find(c => c.id === id)
  if (!config) throw new Error(`连接配置不存在: ${id}`)
  return kafkaClientManager.connect(config)
}))
```

对所有 handler 统一应用此包装。

- [ ] **Step 2: Commit**

```bash
git add src/main/ipc/registerHandlers.ts && git commit -m "feat: 统一 IPC 错误处理，返回 { code, message } 结构"
```

---

### Task 2: 全局异常捕获与优雅关闭

**Files:**
- Modify: `src/main/index.ts`

- [ ] **Step 1: 在 src/main/index.ts 中添加全局异常捕获**

在 `app.whenReady()` 之前添加：

```ts
process.on('uncaughtException', (error) => {
  logger.error('未捕获异常:', error)
})

process.on('unhandledRejection', (reason) => {
  logger.error('未处理的 Promise 拒绝:', reason)
})

app.on('before-quit', async () => {
  logger.info('应用即将退出，清理资源...')
  // 断开 Kafka 连接
  const { kafkaClientManager } = await import('./kafka/KafkaClientManager')
  await kafkaClientManager.disconnect().catch(() => {})
})
```

- [ ] **Step 2: Commit**

```bash
git add src/main/index.ts && git commit -m "feat: 添加全局异常捕获与优雅关闭"
```

---

### Task 3: 设置页面

**Files:**
- Create: `src/renderer/stores/settingsStore.ts`
- Create: `src/renderer/views/SettingsView.vue`
- Modify: `src/renderer/router/index.ts`
- Modify: `src/renderer/App.vue`

- [ ] **Step 1: 编写 src/renderer/stores/settingsStore.ts**

```ts
import { defineStore } from 'pinia'
import { ref } from 'vue'

export const useSettingsStore = defineStore('settings', () => {
  const deleteTopicEnabled = ref(false)
  const messageBufferLimit = ref(5000)
  const theme = ref<'light' | 'dark'>('light')

  function toggleDeleteTopic() {
    deleteTopicEnabled.value = !deleteTopicEnabled.value
  }

  function setMessageBufferLimit(limit: number) {
    messageBufferLimit.value = limit
  }

  function setTheme(t: 'light' | 'dark') {
    theme.value = t
  }

  return {
    deleteTopicEnabled, messageBufferLimit, theme,
    toggleDeleteTopic, setMessageBufferLimit, setTheme,
  }
})
```

- [ ] **Step 2: 编写 src/renderer/views/SettingsView.vue**

```vue
<template>
  <div class="settings-view">
    <h2>设置</h2>

    <el-card class="section-card">
      <template #header>安全设置</template>
      <el-form label-width="180px">
        <el-form-item label="允许删除 Topic">
          <el-switch v-model="settingsStore.deleteTopicEnabled" />
          <el-text type="warning" size="small" style="margin-left: 8px">
            开启后集群概览页将显示删除按钮，需二次确认
          </el-text>
        </el-form-item>
      </el-form>
    </el-card>

    <el-card class="section-card" style="margin-top: 16px">
      <template #header>性能设置</template>
      <el-form label-width="180px">
        <el-form-item label="消息缓冲区上限（条）">
          <el-input-number
            :model-value="settingsStore.messageBufferLimit"
            :min="1000"
            :max="50000"
            :step="1000"
            @change="settingsStore.setMessageBufferLimit"
          />
          <el-text type="info" size="small" style="margin-left: 8px">
            超出上限时自动丢弃最旧消息
          </el-text>
        </el-form-item>
      </el-form>
    </el-card>

    <el-card class="section-card" style="margin-top: 16px">
      <template #header>外观</template>
      <el-form label-width="180px">
        <el-form-item label="主题">
          <el-radio-group :model-value="settingsStore.theme" @change="settingsStore.setTheme">
            <el-radio value="light">浅色（推荐投屏）</el-radio>
            <el-radio value="dark">深色</el-radio>
          </el-radio-group>
        </el-form-item>
      </el-form>
    </el-card>

    <el-card class="section-card" style="margin-top: 16px">
      <template #header>日志</template>
      <el-form label-width="180px">
        <el-form-item label="日志目录">
          <el-button @click="openLogDir">打开日志目录</el-button>
        </el-form-item>
      </el-form>
    </el-card>

    <el-card class="section-card" style="margin-top: 16px">
      <template #header>关于</template>
      <div class="about-info">
        <p><strong>KafkaTeach</strong> v1.0.0</p>
        <p>Kafka 教学演示工具</p>
        <p>基于 Electron + Vue 3 + KafkaJS</p>
      </div>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { useSettingsStore } from '@/stores/settingsStore'

const settingsStore = useSettingsStore()

function openLogDir() {
  // 通过 IPC 获取日志目录路径并打开
  window.kafkaApi.connList().then(() => {
    // 使用 shell.openPath 需要在 preload 中暴露
  })
}
</script>

<style scoped>
.settings-view {
  max-width: 700px;
}
.section-card {
  margin-bottom: 0;
}
.about-info p {
  margin: 4px 0;
  font-size: 14px;
}
</style>
```

- [ ] **Step 3: 更新路由，添加设置页**

修改 `src/renderer/router/index.ts`，添加路由：

```ts
{ path: '/settings', name: 'settings', component: () => import('@/views/SettingsView.vue') },
```

- [ ] **Step 4: 更新 App.vue 侧边栏，添加设置入口**

在 `src/renderer/App.vue` 的 `<el-menu>` 中添加：

```vue
<el-menu-item index="/settings">
  <el-icon><Setting /></el-icon>
  <span>设置</span>
</el-menu-item>
```

在 import 中添加 `Setting` 图标：

```ts
import { Link, DataBoard, Upload, Download, PieChart, VideoPlay, Setting } from '@element-plus/icons-vue'
```

- [ ] **Step 5: Commit**

```bash
git add src/renderer/stores/settingsStore.ts src/renderer/views/SettingsView.vue src/renderer/router/index.ts src/renderer/App.vue && git commit -m "feat: 添加设置页面（安全/性能/外观/日志/关于）"
```

---

### Task 4: electron-builder 打包配置

**Files:**
- Modify: `package.json`

- [ ] **Step 1: 完善 package.json 中的 build 配置**

将 `package.json` 中的 `build` 字段更新为：

```json
{
  "build": {
    "appId": "com.kafka.teach",
    "productName": "KafkaTeach",
    "directories": {
      "output": "release"
    },
    "files": [
      "out/**/*",
      "resources/**/*"
    ],
    "win": {
      "target": "nsis",
      "icon": "resources/icon.ico"
    },
    "nsis": {
      "oneClick": false,
      "allowToChangeInstallationDirectory": true,
      "installerLanguages": ["zh_CN", "en_US"]
    },
    "mac": {
      "target": "dmg",
      "icon": "resources/icon.icns",
      "category": "public.app-category.developer-tools"
    },
    "linux": {
      "target": "AppImage",
      "icon": "resources/icon.png",
      "category": "Development"
    }
  }
}
```

- [ ] **Step 2: 创建占位图标文件**

```bash
# 使用 PowerShell 创建 1x1 像素占位 PNG（后续替换为实际图标）
# 这里先创建空文件占位
```

创建 `resources/icon.png`（256x256 占位图）、`resources/icon.ico`、`resources/icon.icns`。

- [ ] **Step 3: 测试打包**

```bash
npm run package:win
```

预期：在 `release/` 目录生成 NSIS 安装包。

- [ ] **Step 4: Commit**

```bash
git add package.json resources/ && git commit -m "feat: 完善 electron-builder 多平台打包配置"
```

---

### Task 5: 最终验证与收尾

- [ ] **Step 1: 运行完整类型检查**

```bash
npm run typecheck
```

预期：无类型错误。

- [ ] **Step 2: 启动本地 Kafka 并端到端验证**

```bash
docker compose -f docker/docker-compose.yml up -d
npm run dev
```

验证清单：
- [ ] 连接管理：新增/编辑/删除/测试连接
- [ ] 集群概览：Broker 列表、Topic 列表、分区详情
- [ ] 生产者：单条发送、批量发送、模板变量、Key 策略、分区颜色
- [ ] 消费者：多实例、启动/停止/暂停/继续、seek/commit、消息流、再均衡时间线
- [ ] 可视化：分区分配图、消息流动画、柱状图
- [ ] 演示场景：S1~S6 全部可运行，字幕正确
- [ ] 设置页：各开关正常
- [ ] 消息导出 JSON/CSV

- [ ] **Step 3: 压测验证（500 msg/s）**

在生产者页面使用自动流量模式，设置 500 条/秒速率，观察：
- UI 不卡顿（虚拟滚动 + 限频生效）
- 内存稳定（环形缓冲生效）
- 消息流正常显示

- [ ] **Step 4: 构建安装包**

```bash
npm run package:win
```

验证安装包可正常安装和运行。

- [ ] **Step 5: 最终 Commit**

```bash
git add -A && git commit -m "chore: M5 打磨与发布最终验证通过"
```

---

## M5 完成检查清单

- [ ] 所有 IPC handler 统一错误处理
- [ ] 全局未捕获异常和 Promise 拒绝日志记录
- [ ] 应用退出时优雅断开 Kafka 连接
- [ ] 设置页面：删除 Topic 开关、消息缓冲上限、主题切换、日志目录
- [ ] electron-builder 三平台打包配置正确
- [ ] 500 msg/s 压测 UI 不卡顿
- [ ] 安装包可正常安装运行
- [ ] `npm run typecheck` 无错误
- [ ] 全功能端到端验证通过