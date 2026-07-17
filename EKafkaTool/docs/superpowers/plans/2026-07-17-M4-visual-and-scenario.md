# M4: 可视化与演示场景 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现分区分配可视化图、消息流动画、6 个内置演示场景与讲解字幕。

**Architecture:** 分区分配图使用 CSS + SVG 实现分区-消费者连线。消息流动画用 CSS transition 实现气泡飞入效果。演示场景引擎 DemoScenarioService 顺序执行 JSON 声明的步骤，通过 event:scenarioStep 推送讲解字幕。

**Tech Stack:** 同 M1（Vue 3, CSS Transition, SVG）

**前置条件:** M1 + M2 + M3 完成

---

## 文件结构规划

| 文件 | 职责 |
|---|---|
| `src/main/kafka/DemoScenarioService.ts` | 演示场景脚本引擎 |
| `src/main/ipc/registerHandlers.ts`（修改） | 新增 scenario 相关 IPC handlers |
| `src/renderer/stores/scenarioStore.ts` | Pinia 场景状态管理 |
| `src/renderer/views/VisualView.vue`（重写） | 可视化页面：分区分配图 + 消息流动画 + 柱状图 |
| `src/renderer/views/ScenarioView.vue`（重写） | 演示场景页面：卡片列表 + 讲解字幕 |
| `src/renderer/components/PartitionMap.vue` | 分区-消费者分配图 |
| `src/renderer/components/MessageFlowCanvas.vue` | 消息流动画 |
| `src/main/data/scenarios.ts` | 6 个内置场景定义 |

---

### Task 1: 演示场景数据定义

**Files:**
- Create: `src/main/data/scenarios.ts`

- [ ] **Step 1: 编写 src/main/data/scenarios.ts**

```ts
export interface ScenarioDefinition {
  id: string
  title: string
  description: string
  duration: string
  steps: ScenarioStepDef[]
}

export interface ScenarioStepDef {
  type: 'createTopic' | 'note' | 'createConsumer' | 'produce' | 'sleep' | 'stopConsumer'
  [key: string]: unknown
}

export const SCENARIOS: ScenarioDefinition[] = [
  {
    id: 'S1',
    title: '第一条消息',
    description: '创建 1 分区 Topic，起 1 个消费者，发 3 条消息，观察基本收发与 Offset 递增',
    duration: '约 1 分钟',
    steps: [
      { type: 'createTopic', name: 'demo-hello', partitions: 1 },
      { type: 'note', text: '已创建 1 分区 Topic "demo-hello"，现在启动消费者' },
      { type: 'createConsumer', alias: 'Consumer-Hello', groupId: 'demo-g0', topics: ['demo-hello'], fromBeginning: true },
      { type: 'note', text: '消费者已启动，从 earliest 开始消费。现在发送 3 条消息' },
      { type: 'produce', topic: 'demo-hello', count: 3, intervalMs: 1000, keyStrategy: 'fixed', value: '{"msg": "hello {{seq}}"}' },
      { type: 'note', text: '观察：每条消息 Offset 从 0 递增，消息持久化后可重放' },
      { type: 'sleep', seconds: 5 },
      { type: 'note', text: '场景结束。可尝试手动重置 Offset 到 0 重放消息' },
    ],
  },
  {
    id: 'S2',
    title: '分区与 Key',
    description: '创建 3 分区 Topic，分别以无 Key 和固定 Key 各发 12 条，观察分区分布',
    duration: '约 2 分钟',
    steps: [
      { type: 'createTopic', name: 'demo-partition', partitions: 3 },
      { type: 'createConsumer', alias: 'Consumer-P', groupId: 'demo-g1', topics: ['demo-partition'], fromBeginning: true },
      { type: 'note', text: '已创建 3 分区 Topic。先发送 12 条无 Key 消息，观察轮询分布' },
      { type: 'produce', topic: 'demo-partition', count: 12, intervalMs: 500, keyStrategy: 'fixed', value: '{"msg": "no-key {{seq}}"}' },
      { type: 'sleep', seconds: 3 },
      { type: 'note', text: '无 Key 消息均匀分布到 3 个分区。现在发送 12 条固定 Key="user1" 的消息' },
      { type: 'produce', topic: 'demo-partition', count: 12, intervalMs: 500, keyStrategy: 'fixed', key: 'user1', value: '{"msg": "user1-{{seq}}"}' },
      { type: 'sleep', seconds: 3 },
      { type: 'note', text: '同 Key 消息全部落入同一分区（哈希取模）。这就是分区策略的核心' },
    ],
  },
  {
    id: 'S3',
    title: '消费组负载均衡',
    description: '3 分区 Topic + Consumer-A 持续消费，60 秒后自动加入 Consumer-B，观察再均衡',
    duration: '约 3 分钟',
    steps: [
      { type: 'createTopic', name: 'demo-group', partitions: 3 },
      { type: 'note', text: '先启动 Consumer-A，独占 3 个分区' },
      { type: 'createConsumer', alias: 'Consumer-A', groupId: 'demo-g2', topics: ['demo-group'], fromBeginning: true },
      { type: 'produce', topic: 'demo-group', count: 0, intervalMs: 2000, keyStrategy: 'random', value: '{"msg": "data-{{seq}}"}' },
      { type: 'sleep', seconds: 15 },
      { type: 'note', text: 'Consumer-A 持续消费中。现在加入 Consumer-B，观察再均衡' },
      { type: 'createConsumer', alias: 'Consumer-B', groupId: 'demo-g2', topics: ['demo-group'], fromBeginning: false },
      { type: 'note', text: '再均衡触发！分区从 A 独占变为 A(2) + B(1) 分配。观察分配图变化' },
      { type: 'sleep', seconds: 30 },
      { type: 'note', text: '观察两张卡片的分区色块与各自消费速率。这就是消费组负载均衡' },
    ],
  },
  {
    id: 'S4',
    title: '再均衡（手动）',
    description: '手动新增/停止消费者，观察分区分配图动画迁移与 lag 变化',
    duration: '约 2 分钟',
    steps: [
      { type: 'createTopic', name: 'demo-rebalance', partitions: 4 },
      { type: 'createConsumer', alias: 'Consumer-X', groupId: 'demo-g3', topics: ['demo-rebalance'], fromBeginning: true },
      { type: 'produce', topic: 'demo-rebalance', count: 0, intervalMs: 1000, keyStrategy: 'random', value: '{"msg": "{{seq}}"}' },
      { type: 'note', text: 'Consumer-X 当前独占 4 个分区。请手动点击"新增消费者实例"创建 Consumer-Y（同 groupId demo-g3）' },
      { type: 'sleep', seconds: 30 },
      { type: 'note', text: '观察分配图动画迁移。暂停 Consumer-X 期间消息积压 lag 增长' },
      { type: 'sleep', seconds: 30 },
      { type: 'note', text: '场景结束。可继续手动操作观察再均衡行为' },
    ],
  },
  {
    id: 'S5',
    title: '消息重放',
    description: '消费者消费完 20 条后，执行 seek 重置到 Offset 0 再消费，演示消息不删除特性',
    duration: '约 2 分钟',
    steps: [
      { type: 'createTopic', name: 'demo-replay', partitions: 1 },
      { type: 'note', text: '先发送 20 条消息，消费者消费完毕' },
      { type: 'createConsumer', alias: 'Consumer-R', groupId: 'demo-g4', topics: ['demo-replay'], fromBeginning: true },
      { type: 'produce', topic: 'demo-replay', count: 20, intervalMs: 300, keyStrategy: 'fixed', value: '{"msg": "replay-{{seq}}"}' },
      { type: 'sleep', seconds: 10 },
      { type: 'note', text: '20 条消息已消费完毕。现在执行 Offset 重置到 0，重新消费' },
      { type: 'sleep', seconds: 3 },
      { type: 'note', text: '观察：Kafka 消息不删除，Offset 回拨即可重放。对比传统 MQ 的删除机制' },
      { type: 'sleep', seconds: 10 },
      { type: 'note', text: '场景结束。这就是 Kafka 消息持久化与重放的核心优势' },
    ],
  },
  {
    id: 'S6',
    title: '顺序性与 acks',
    description: '1 分区 Topic 发 20 条带序号消息，展示严格顺序；切换 acks=0 演示无 Offset',
    duration: '约 2 分钟',
    steps: [
      { type: 'createTopic', name: 'demo-order', partitions: 1 },
      { type: 'createConsumer', alias: 'Consumer-O', groupId: 'demo-g5', topics: ['demo-order'], fromBeginning: true },
      { type: 'note', text: '在 1 分区 Topic 中发送 20 条带序号消息（acks=all），观察严格顺序' },
      { type: 'produce', topic: 'demo-order', count: 20, intervalMs: 300, keyStrategy: 'fixed', value: '{"seq": {{seq}}}' },
      { type: 'sleep', seconds: 8 },
      { type: 'note', text: '观察：分区内消息严格按 Offset 递增顺序消费。顺序性只在分区内成立' },
      { type: 'sleep', seconds: 5 },
      { type: 'note', text: '现在切换到生产者页面，将 acks 设为 0 发送消息，观察"发送即返回"无 Offset 确认' },
      { type: 'sleep', seconds: 15 },
      { type: 'note', text: '场景结束。acks=0 不等待 Broker 确认，吞吐最高但可能丢消息' },
    ],
  },
]
```

- [ ] **Step 2: Commit**

```bash
git add src/main/data/scenarios.ts && git commit -m "feat: 定义 6 个内置演示场景数据"
```

---

### Task 2: DemoScenarioService 实现

**Files:**
- Create: `src/main/kafka/DemoScenarioService.ts`

- [ ] **Step 1: 编写 src/main/kafka/DemoScenarioService.ts**

```ts
import { SCENARIOS, type ScenarioDefinition, type ScenarioStepDef } from '../data/scenarios'
import { logger } from '../util/logger'

interface RunContext {
  runId: string
  scenarioId: string
  currentStep: number
  aborted: boolean
  consumers: string[]
  topics: string[]
}

export class DemoScenarioService {
  private runs = new Map<string, RunContext>()

  constructor(
    private push: (channel: string, payload: unknown) => void,
    private createTopic: (name: string, partitions: number) => Promise<void>,
    private createConsumer: (opts: { alias: string; groupId: string; topics: string[]; fromBeginning: boolean }) => Promise<string>,
    private startConsumer: (id: string) => Promise<void>,
    private stopConsumer: (id: string) => Promise<void>,
    private produce: (topic: string, count: number, intervalMs: number, keyStrategy: string, key?: string, value?: string) => Promise<string>,
    private stopBatch: (taskId: string) => Promise<void>,
  ) {}

  listScenarios() {
    return SCENARIOS.map(s => ({
      id: s.id,
      title: s.title,
      description: s.description,
      duration: s.duration,
    }))
  }

  async run(scenarioId: string): Promise<string> {
    const scenario = SCENARIOS.find(s => s.id === scenarioId)
    if (!scenario) throw new Error(`场景不存在: ${scenarioId}`)

    const runId = crypto.randomUUID()
    const ctx: RunContext = {
      runId,
      scenarioId,
      currentStep: 0,
      aborted: false,
      consumers: [],
      topics: [],
    }
    this.runs.set(runId, ctx)

    this.execute(scenario, ctx).catch(err => {
      logger.error(`场景执行异常 [${scenarioId}]:`, err)
    })

    return runId
  }

  stop(runId: string): void {
    const ctx = this.runs.get(runId)
    if (ctx) {
      ctx.aborted = true
      this.runs.delete(runId)
    }
  }

  private async execute(scenario: ScenarioDefinition, ctx: RunContext): Promise<void> {
    let batchTaskId: string | null = null

    for (let i = 0; i < scenario.steps.length; i++) {
      if (ctx.aborted) break

      const step = scenario.steps[i]
      ctx.currentStep = i

      try {
        switch (step.type) {
          case 'createTopic': {
            const name = step.name as string
            const partitions = (step.partitions as number) ?? 3
            await this.createTopic(name, partitions)
            ctx.topics.push(name)
            this.pushNote(ctx, `已创建 Topic "${name}"（${partitions} 分区）`)
            break
          }

          case 'createConsumer': {
            const alias = step.alias as string
            const groupId = step.groupId as string
            const topics = step.topics as string[]
            const fromBeginning = (step.fromBeginning as boolean) ?? true
            const id = await this.createConsumer({ alias, groupId, topics, fromBeginning })
            await this.startConsumer(id)
            ctx.consumers.push(id)
            this.pushNote(ctx, `消费者 "${alias}" 已启动（groupId: ${groupId}）`)
            break
          }

          case 'produce': {
            if (batchTaskId) {
              await this.stopBatch(batchTaskId)
            }
            const topic = step.topic as string
            const count = step.count as number
            const intervalMs = (step.intervalMs as number) ?? 500
            const keyStrategy = (step.keyStrategy as string) ?? 'fixed'
            const key = step.key as string | undefined
            const value = step.value as string | undefined

            if (count > 0) {
              batchTaskId = await this.produce(topic, count, intervalMs, keyStrategy, key, value)
              this.pushNote(ctx, `开始发送 ${count} 条消息到 "${topic}"`)
            } else {
              batchTaskId = await this.produce(topic, 999999, intervalMs, keyStrategy, key, value)
              this.pushNote(ctx, `开始持续发送消息到 "${topic}"（速率: ${intervalMs}ms/条）`)
            }
            break
          }

          case 'sleep': {
            const seconds = step.seconds as number
            await this.sleep(seconds * 1000, ctx)
            break
          }

          case 'note': {
            this.pushNote(ctx, step.text as string)
            break
          }

          case 'stopConsumer': {
            const alias = step.alias as string
            this.pushNote(ctx, `停止消费者 "${alias}"`)
            break
          }
        }
      } catch (err) {
        logger.error(`场景步骤执行失败 [${scenario.id}:${i}]:`, err)
        this.pushNote(ctx, `步骤执行出错: ${err instanceof Error ? err.message : String(err)}`)
      }
    }

    if (batchTaskId) {
      await this.stopBatch(batchTaskId).catch(() => {})
    }

    this.pushNote(ctx, '场景执行完毕')
    this.runs.delete(ctx.runId)
  }

  private pushNote(ctx: RunContext, message: string): void {
    this.push('event:scenarioStep', {
      runId: ctx.runId,
      stepIndex: ctx.currentStep,
      message,
    })
  }

  private sleep(ms: number, ctx: RunContext): Promise<void> {
    return new Promise(resolve => {
      const check = () => {
        if (ctx.aborted) { resolve(); return }
        setTimeout(() => { resolve() }, ms)
      }
      check()
    })
  }
}
```

- [ ] **Step 2: Commit**

```bash
git add src/main/kafka/DemoScenarioService.ts && git commit -m "feat: 实现 DemoScenarioService 演示场景脚本引擎"
```

---

### Task 3: 注册 Scenario IPC Handlers

**Files:**
- Modify: `src/main/ipc/registerHandlers.ts`

- [ ] **Step 1: 在 registerHandlers.ts 中新增 scenario 相关代码**

在 `src/main/ipc/registerHandlers.ts` 顶部添加 import：
```ts
import { DemoScenarioService } from '../kafka/DemoScenarioService'
```

在 `registerAllHandlers()` 函数中，`consumerService` 初始化之后添加：
```ts
const demoScenarioService = new DemoScenarioService(
  pushToRenderer,
  async (name, partitions) => {
    const admin = kafkaClientManager.getAdmin()
    if (!admin) throw new Error('未连接 Kafka')
    await admin.createTopics({ topics: [{ topic: name, numPartitions: partitions, replicationFactor: 1 }] })
  },
  async (opts) => consumerService.create(opts),
  async (id) => consumerService.start(id),
  async (id) => consumerService.stop(id),
  async (topic, count, intervalMs, keyStrategy, key, value) => {
    return producerService.sendBatch(
      { topic, key, value: value ?? '{"msg": "{{seq}}"}', acks: -1 },
      count,
      intervalMs,
      keyStrategy,
    )
  },
  async (taskId) => producerService.stopBatch(taskId),
)
```

在 `registerAllHandlers()` 函数末尾添加：
```ts
ipcMain.handle(IPC_CHANNELS.SCENARIO_LIST, async () => {
  return demoScenarioService.listScenarios()
})

ipcMain.handle(IPC_CHANNELS.SCENARIO_RUN, async (_event, scenarioId: string) => {
  return demoScenarioService.run(scenarioId)
})

ipcMain.handle(IPC_CHANNELS.SCENARIO_STOP, async (_event, runId: string) => {
  demoScenarioService.stop(runId)
})
```

- [ ] **Step 2: Commit**

```bash
git add src/main/ipc/registerHandlers.ts && git commit -m "feat: 注册 Scenario IPC handlers"
```

---

### Task 4: Scenario Pinia Store

**Files:**
- Create: `src/renderer/stores/scenarioStore.ts`

- [ ] **Step 1: 编写 src/renderer/stores/scenarioStore.ts**

```ts
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { getKafkaApi } from '@/api/kafkaApi'
import type { ScenarioMeta } from '../../main/kafka/types'

export const useScenarioStore = defineStore('scenario', () => {
  const scenarios = ref<ScenarioMeta[]>([])
  const runningId = ref<string | null>(null)
  const currentStep = ref<{ runId: string; stepIndex: number; message: string } | null>(null)

  async function loadScenarios() {
    scenarios.value = await getKafkaApi().listScenarios()
  }

  async function runScenario(id: string) {
    runningId.value = await getKafkaApi().runScenario(id)
  }

  async function stopScenario() {
    if (runningId.value) {
      await getKafkaApi().stopScenario(runningId.value)
      runningId.value = null
      currentStep.value = null
    }
  }

  function setupListener() {
    getKafkaApi().onScenarioStep((data) => {
      currentStep.value = data
    })
  }

  return {
    scenarios, runningId, currentStep,
    loadScenarios, runScenario, stopScenario, setupListener,
  }
})
```

- [ ] **Step 2: Commit**

```bash
git add src/renderer/stores/scenarioStore.ts && git commit -m "feat: 添加 Scenario Pinia Store"
```

---

### Task 5: PartitionMap 分区分配图组件

**Files:**
- Create: `src/renderer/components/PartitionMap.vue`

- [ ] **Step 1: 编写 src/renderer/components/PartitionMap.vue**

```vue
<template>
  <div class="partition-map">
    <div class="map-title">分区分配图</div>
    <div class="map-canvas" ref="canvasRef">
      <div class="partitions-row">
        <div
          v-for="p in partitions"
          :key="p"
          class="partition-block"
          :style="{ backgroundColor: partitionColor(p) }"
        >
          P{{ p }}
        </div>
      </div>
      <svg class="connections-svg" :viewBox="`0 0 ${svgWidth} ${svgHeight}`">
        <line
          v-for="(conn, idx) in connections"
          :key="idx"
          :x1="conn.x1"
          :y1="conn.y1"
          :x2="conn.x2"
          :y2="conn.y2"
          :stroke="partitionColor(conn.partition)"
          stroke-width="3"
          stroke-linecap="round"
        />
      </svg>
      <div class="consumers-row">
        <div
          v-for="c in consumers"
          :key="c.instanceId"
          class="consumer-block"
          :class="{ active: c.status === 'running' }"
        >
          <div class="consumer-name">{{ c.alias }}</div>
          <div class="consumer-partitions">
            <span
              v-for="p in getConsumerPartitions(c)"
              :key="p"
              class="consumer-partition-dot"
              :style="{ backgroundColor: partitionColor(p) }"
            >P{{ p }}</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import type { ConsumerInstanceState } from '../../main/kafka/types'

const props = defineProps<{
  partitions: number
  consumers: ConsumerInstanceState[]
}>()

const canvasRef = ref<HTMLElement>()

const PARTITION_COLORS = [
  '#409EFF', '#67C23A', '#E6A23C', '#F56C6C', '#909399',
  '#337ECC', '#529B2E', '#B88230', '#C45656', '#73767A',
]

function partitionColor(p: number): string {
  return PARTITION_COLORS[p % PARTITION_COLORS.length]
}

function getConsumerPartitions(c: ConsumerInstanceState): number[] {
  const parts: number[] = []
  for (const assign of c.assignments) {
    parts.push(...assign.partitions)
  }
  return parts.sort((a, b) => a - b)
}

const svgWidth = computed(() => Math.max(props.partitions * 80, 200))
const svgHeight = computed(() => Math.max(props.consumers.length * 60, 60))

const connections = computed(() => {
  const result: Array<{ x1: number; y1: number; x2: number; y2: number; partition: number }> = []
  const blockWidth = 60
  const gap = 20
  const partitionY = 30
  const consumerStartY = 50

  props.consumers.forEach((c, ci) => {
    const parts = getConsumerPartitions(c)
    parts.forEach(p => {
      result.push({
        x1: p * (blockWidth + gap) + blockWidth / 2,
        y1: partitionY,
        x2: ci * 140 + 70,
        y2: consumerStartY + ci * 60,
        partition: p,
      })
    })
  })
  return result
})
</script>

<style scoped>
.partition-map {
  padding: 12px;
}
.map-title {
  font-weight: 600;
  margin-bottom: 12px;
}
.map-canvas {
  position: relative;
}
.partitions-row {
  display: flex;
  gap: 12px;
  justify-content: center;
  margin-bottom: 8px;
}
.partition-block {
  width: 56px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  font-size: 12px;
  font-weight: 600;
  border-radius: 4px;
}
.connections-svg {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  pointer-events: none;
}
.consumers-row {
  display: flex;
  gap: 16px;
  justify-content: center;
  margin-top: 16px;
}
.consumer-block {
  width: 120px;
  padding: 8px;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  text-align: center;
  transition: border-color 0.3s;
}
.consumer-block.active {
  border-color: var(--el-color-success);
}
.consumer-name {
  font-size: 13px;
  font-weight: 600;
  margin-bottom: 4px;
}
.consumer-partitions {
  display: flex;
  gap: 4px;
  justify-content: center;
  flex-wrap: wrap;
}
.consumer-partition-dot {
  font-size: 11px;
  color: #fff;
  padding: 1px 6px;
  border-radius: 3px;
}
</style>
```

- [ ] **Step 2: Commit**

```bash
git add src/renderer/components/PartitionMap.vue && git commit -m "feat: 实现 PartitionMap 分区-消费者分配图组件"
```

---

### Task 6: MessageFlowCanvas 消息流动画组件

**Files:**
- Create: `src/renderer/components/MessageFlowCanvas.vue`

- [ ] **Step 1: 编写 src/renderer/components/MessageFlowCanvas.vue**

```vue
<template>
  <div class="message-flow">
    <div class="flow-title">消息流动画（演示模式）</div>
    <div class="flow-stage" ref="stageRef">
      <div class="flow-producer">
        <el-tag type="primary" size="small">Producer</el-tag>
      </div>
      <div class="flow-partitions">
        <div
          v-for="p in partitions"
          :key="p"
          class="flow-partition"
          :style="{ backgroundColor: partitionColor(p) }"
        >
          P{{ p }}
        </div>
      </div>
      <div class="flow-consumers">
        <div
          v-for="c in consumers"
          :key="c.instanceId"
          class="flow-consumer"
        >
          <el-tag :type="c.status === 'running' ? 'success' : 'info'" size="small">
            {{ c.alias }}
          </el-tag>
        </div>
      </div>
      <div
        v-for="bubble in bubbles"
        :key="bubble.id"
        class="flow-bubble"
        :style="bubbleStyle(bubble)"
      >
        ●
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import type { ConsumerInstanceState, ProduceResult } from '../../main/kafka/types'
import { getKafkaApi } from '@/api/kafkaApi'

const props = defineProps<{
  partitions: number
  consumers: ConsumerInstanceState[]
}>()

const PARTITION_COLORS = [
  '#409EFF', '#67C23A', '#E6A23C', '#F56C6C', '#909399',
]

function partitionColor(p: number): string {
  return PARTITION_COLORS[p % PARTITION_COLORS.length]
}

interface Bubble {
  id: number
  partition: number
  phase: 'toPartition' | 'toConsumer'
  progress: number
  consumerIdx: number
}

const bubbles = ref<Bubble[]>([])
let bubbleId = 0
let unsub: (() => void) | null = null
let animFrame: number | null = null

function bubbleStyle(b: Bubble) {
  const color = partitionColor(b.partition)
  const partitionX = 160 + b.partition * 80
  const partitionY = 50
  const consumerX = 160 + b.consumerIdx * 120
  const consumerY = 100

  let x: number, y: number
  if (b.phase === 'toPartition') {
    x = 40 + (partitionX - 40) * b.progress
    y = 20 + (partitionY - 20) * b.progress
  } else {
    x = partitionX + (consumerX - partitionX) * b.progress
    y = partitionY + (consumerY - partitionY) * b.progress
  }

  return {
    left: `${x}px`,
    top: `${y}px`,
    color,
    fontSize: '14px',
    transition: 'none',
  }
}

function animate() {
  for (const b of bubbles.value) {
    b.progress += 0.02
    if (b.progress >= 1) {
      if (b.phase === 'toPartition') {
        b.phase = 'toConsumer'
        b.progress = 0
      }
    }
  }
  bubbles.value = bubbles.value.filter(b => b.progress < 1 || b.phase === 'toConsumer')
  animFrame = requestAnimationFrame(animate)
}

function onProduce(result: ProduceResult) {
  const consumerIdx = Math.floor(Math.random() * Math.max(props.consumers.length, 1))
  bubbles.value.push({
    id: ++bubbleId,
    partition: result.partition,
    phase: 'toPartition',
    progress: 0,
    consumerIdx,
  })
  if (bubbles.value.length > 20) {
    bubbles.value = bubbles.value.slice(-20)
  }
}

onMounted(() => {
  unsub = getKafkaApi().onProduceAck(onProduce)
  animFrame = requestAnimationFrame(animate)
})

onUnmounted(() => {
  unsub?.()
  if (animFrame) cancelAnimationFrame(animFrame)
})
</script>

<style scoped>
.message-flow {
  padding: 12px;
}
.flow-title {
  font-weight: 600;
  margin-bottom: 12px;
}
.flow-stage {
  position: relative;
  height: 140px;
  border: 1px dashed var(--el-border-color);
  border-radius: 8px;
  overflow: hidden;
}
.flow-producer {
  position: absolute;
  left: 8px;
  top: 12px;
}
.flow-partitions {
  position: absolute;
  left: 120px;
  top: 40px;
  display: flex;
  gap: 12px;
}
.flow-partition {
  width: 56px;
  height: 28px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  font-size: 11px;
  font-weight: 600;
  border-radius: 4px;
}
.flow-consumers {
  position: absolute;
  left: 120px;
  top: 90px;
  display: flex;
  gap: 12px;
}
.flow-bubble {
  position: absolute;
  z-index: 10;
  pointer-events: none;
}
</style>
```

- [ ] **Step 2: Commit**

```bash
git add src/renderer/components/MessageFlowCanvas.vue && git commit -m "feat: 实现 MessageFlowCanvas 消息流动画组件"
```

---

### Task 7: 可视化页面 UI

**Files:**
- Modify: `src/renderer/views/VisualView.vue`（重写）

- [ ] **Step 1: 重写 src/renderer/views/VisualView.vue**

```vue
<template>
  <div class="visual-view">
    <el-card class="section-card">
      <PartitionMap
        :partitions="maxPartitions"
        :consumers="consumerStore.instances"
      />
    </el-card>

    <el-card class="section-card" style="margin-top: 16px">
      <MessageFlowCanvas
        :partitions="maxPartitions"
        :consumers="consumerStore.instances"
      />
    </el-card>

    <el-card class="section-card" style="margin-top: 16px">
      <template #header>分区消息量柱状图</template>
      <div class="bar-chart">
        <div
          v-for="bar in barData"
          :key="bar.partition"
          class="bar-item"
        >
          <div class="bar-label">P{{ bar.partition }}</div>
          <div class="bar-track">
            <div
              class="bar-fill"
              :style="{
                width: bar.percent + '%',
                backgroundColor: partitionColor(bar.partition),
              }"
            />
          </div>
          <div class="bar-value">{{ bar.count }}</div>
        </div>
        <el-empty v-if="!barData.length" description="暂无数据" :image-size="60" />
      </div>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, onMounted, onUnmounted } from 'vue'
import { useConsumerStore } from '@/stores/consumerStore'
import { useMetadataStore } from '@/stores/metadataStore'
import PartitionMap from '@/components/PartitionMap.vue'
import MessageFlowCanvas from '@/components/MessageFlowCanvas.vue'

const consumerStore = useConsumerStore()
const metadataStore = useMetadataStore()

const PARTITION_COLORS = [
  '#409EFF', '#67C23A', '#E6A23C', '#F56C6C', '#909399',
  '#337ECC', '#529B2E', '#B88230', '#C45656', '#73767A',
]

function partitionColor(p: number): string {
  return PARTITION_COLORS[p % PARTITION_COLORS.length]
}

const maxPartitions = computed(() => {
  let max = 0
  for (const t of metadataStore.topics) {
    if (t.partitionCount > max) max = t.partitionCount
  }
  return Math.max(max, 1)
})

interface BarItem {
  partition: number
  count: number
  percent: number
}

const barData = ref<BarItem[]>([])

function updateBarData() {
  const counts = new Map<number, number>()
  for (const m of consumerStore.messages.slice(0, 1000)) {
    counts.set(m.partition, (counts.get(m.partition) ?? 0) + 1)
  }
  const max = Math.max(1, ...Array.from(counts.values()))
  const items: BarItem[] = []
  for (const [p, c] of counts) {
    items.push({ partition: p, count: c, percent: Math.round((c / max) * 100) })
  }
  items.sort((a, b) => a.partition - b.partition)
  barData.value = items
}

let barTimer: ReturnType<typeof setInterval> | null = null

onMounted(() => {
  barTimer = setInterval(updateBarData, 2000)
})

onUnmounted(() => {
  if (barTimer) clearInterval(barTimer)
})
</script>

<style scoped>
.visual-view {
  max-width: 100%;
}
.section-card {
  margin-bottom: 0;
}
.bar-chart {
  padding: 8px 0;
}
.bar-item {
  display: flex;
  align-items: center;
  margin-bottom: 8px;
}
.bar-label {
  width: 40px;
  font-size: 13px;
  font-weight: 600;
}
.bar-track {
  flex: 1;
  height: 20px;
  background: var(--el-fill-color-light);
  border-radius: 4px;
  overflow: hidden;
  margin: 0 12px;
}
.bar-fill {
  height: 100%;
  border-radius: 4px;
  transition: width 0.5s ease;
}
.bar-value {
  width: 50px;
  font-size: 13px;
  text-align: right;
}
</style>
```

- [ ] **Step 2: Commit**

```bash
git add src/renderer/views/VisualView.vue && git commit -m "feat: 实现可视化页面（分区分配图 + 消息流动画 + 柱状图）"
```

---

### Task 8: 演示场景页面 UI

**Files:**
- Modify: `src/renderer/views/ScenarioView.vue`（重写）

- [ ] **Step 1: 重写 src/renderer/views/ScenarioView.vue**

```vue
<template>
  <div class="scenario-view">
    <div class="view-header">
      <h2>演示场景</h2>
    </div>

    <div class="scenario-cards">
      <el-card
        v-for="s in scenarioStore.scenarios"
        :key="s.id"
        class="scenario-card"
        :class="{ running: scenarioStore.runningId !== null }"
      >
        <div class="card-title">{{ s.title }}</div>
        <div class="card-desc">{{ s.description }}</div>
        <div class="card-meta">
          <el-tag size="small" type="info">{{ s.duration }}</el-tag>
          <el-tag size="small" style="margin-left: 8px">{{ s.id }}</el-tag>
        </div>
        <el-button
          type="primary"
          style="margin-top: 12px; width: 100%"
          :disabled="scenarioStore.runningId !== null"
          @click="handleRun(s.id)"
        >
          <el-icon><VideoPlay /></el-icon> 运行
        </el-button>
      </el-card>
    </div>

    <div v-if="scenarioStore.runningId" class="subtitle-bar">
      <div class="subtitle-content">
        <el-icon class="subtitle-icon"><Microphone /></el-icon>
        <span>{{ scenarioStore.currentStep?.message ?? '场景运行中...' }}</span>
      </div>
      <el-button type="danger" size="small" @click="handleStop">停止场景</el-button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { VideoPlay, Microphone } from '@element-plus/icons-vue'
import { useScenarioStore } from '@/stores/scenarioStore'

const scenarioStore = useScenarioStore()

async function handleRun(id: string) {
  try {
    await scenarioStore.runScenario(id)
    ElMessage.success('场景已启动')
  } catch (err: unknown) {
    ElMessage.error(`启动失败: ${err instanceof Error ? err.message : String(err)}`)
  }
}

async function handleStop() {
  await scenarioStore.stopScenario()
  ElMessage.info('场景已停止')
}

onMounted(async () => {
  scenarioStore.setupListener()
  await scenarioStore.loadScenarios()
})
</script>

<style scoped>
.scenario-view {
  max-width: 1000px;
}
.view-header {
  margin-bottom: 16px;
}
.view-header h2 {
  margin: 0;
}
.scenario-cards {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 16px;
}
.scenario-card {
  transition: opacity 0.3s;
}
.scenario-card.running {
  opacity: 0.6;
}
.card-title {
  font-size: 16px;
  font-weight: 600;
  margin-bottom: 8px;
}
.card-desc {
  font-size: 13px;
  color: var(--el-text-color-secondary);
  margin-bottom: 8px;
  line-height: 1.5;
}
.card-meta {
  display: flex;
  align-items: center;
}
.subtitle-bar {
  position: fixed;
  bottom: 0;
  left: 180px;
  right: 0;
  background: var(--el-color-primary-light-9);
  border-top: 2px solid var(--el-color-primary);
  padding: 12px 24px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  z-index: 100;
}
.subtitle-content {
  display: flex;
  align-items: center;
  font-size: 15px;
  color: var(--el-color-primary-dark-2);
}
.subtitle-icon {
  margin-right: 8px;
  font-size: 18px;
}
</style>
```

- [ ] **Step 2: Commit**

```bash
git add src/renderer/views/ScenarioView.vue && git commit -m "feat: 实现演示场景页面（卡片列表 + 讲解字幕条）"
```

---

### Task 9: 类型检查与验证

- [ ] **Step 1: 运行类型检查**

```bash
npm run typecheck
```

预期：无类型错误。

- [ ] **Step 2: 启动本地 Kafka 并验证功能**

```bash
npm run dev
```

验证：
- 可视化页：分区分配图正确显示连线
- 消息流动画：发送消息时气泡飞入
- 柱状图：各分区消息量实时更新
- 演示场景页：6 个场景卡片显示
- 运行 S1 场景：自动创建 Topic → 起消费者 → 发消息 → 字幕讲解
- 运行 S3 场景：观察再均衡与分配图变化
- 停止场景功能正常

- [ ] **Step 3: Commit（如有修改）**

```bash
git add -A && git commit -m "fix: M4 可视化与场景功能验证修复"
```

---

## M4 完成检查清单

- [ ] 分区分配图正确显示分区-消费者连线
- [ ] 再均衡时分配图更新（需配合消费者多实例）
- [ ] 消息流动画气泡正确飞入
- [ ] 分区消息量柱状图实时更新
- [ ] 6 个演示场景卡片正确展示
- [ ] S1 场景可完整运行：创建 Topic → 起消费者 → 发消息 → 字幕
- [ ] S3 场景可触发再均衡并显示分配变化
- [ ] 场景可中途停止
- [ ] 讲解字幕条正确显示
- [ ] `npm run typecheck` 无错误