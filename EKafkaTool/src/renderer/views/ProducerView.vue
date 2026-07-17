<template>
  <div class="producer-view">
    <div class="view-header">
      <h2>生产者</h2>
      <div>
        <el-button @click="topicDialogVisible = true">
          <el-icon><Plus /></el-icon> 创建 Topic
        </el-button>
      </div>
    </div>

    <el-card class="section-card">
      <template #header>发送消息</template>
      <el-form :model="form" label-width="80px">
        <el-form-item label="Topic">
          <el-select
            v-model="form.topic"
            filterable
            allow-create
            placeholder="选择或输入 Topic"
            style="width: 100%"
          >
            <el-option
              v-for="t in metadataStore.topics"
              :key="t.name"
              :label="`${t.name} (${t.partitionCount} 分区)`"
              :value="t.name"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="分区">
          <el-select v-model="form.partition" placeholder="自动" clearable style="width: 200px">
            <el-option
              v-for="p in availablePartitions"
              :key="p"
              :label="`Partition ${p}`"
              :value="p"
            />
          </el-select>
          <el-text type="info" size="small" style="margin-left: 8px">
            留空由分区器自动决定
          </el-text>
        </el-form-item>
        <el-form-item label="Key">
          <el-input v-model="form.key" placeholder="可选，支持 {{seq}} {{ts}} {{rand}}" />
        </el-form-item>
        <el-form-item label="Value">
          <el-input
            v-model="form.value"
            type="textarea"
            :rows="4"
            placeholder='{"message": "hello {{seq}}"}'
          />
        </el-form-item>
        <el-form-item label="Headers">
          <div v-for="(h, idx) in form.headers" :key="idx" class="header-row">
            <el-input v-model="h.key" placeholder="Key" style="width: 160px" />
            <el-input v-model="h.value" placeholder="Value" style="width: 200px; margin-left: 8px" />
            <el-button type="danger" :icon="Delete" circle size="small" @click="removeHeader(idx)" style="margin-left: 8px" />
          </div>
          <el-button size="small" @click="addHeader" style="margin-top: 4px">+ 添加 Header</el-button>
        </el-form-item>
        <el-form-item label="acks">
          <el-radio-group v-model="form.acks">
            <el-radio :value="-1">all</el-radio>
            <el-radio :value="0">0</el-radio>
            <el-radio :value="1">1</el-radio>
          </el-radio-group>
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="handleSend" :loading="producerStore.sending">
            <el-icon><Promotion /></el-icon> 发送
          </el-button>
        </el-form-item>
      </el-form>
    </el-card>

    <el-card class="section-card" style="margin-top: 16px">
      <template #header>批量 / 自动流量</template>
      <el-form :model="batchForm" label-width="100px" inline>
        <el-form-item label="发送条数">
          <el-input-number v-model="batchForm.count" :min="1" :max="10000" />
        </el-form-item>
        <el-form-item label="间隔(ms)">
          <el-input-number v-model="batchForm.intervalMs" :min="0" :max="10000" :step="100" />
        </el-form-item>
        <el-form-item label="Key 策略">
          <el-select v-model="batchForm.keyStrategy" style="width: 140px">
            <el-option label="使用表单 Key" value="fixed" />
            <el-option label="随机 Key" value="random" />
            <el-option label="轮流 Key" value="round-robin" />
          </el-select>
        </el-form-item>
        <el-form-item>
          <el-button
            v-if="!producerStore.batchRunning"
            type="primary"
            @click="handleStartBatch"
            :disabled="!form.topic"
          >
            开始发送
          </el-button>
          <el-button v-else type="danger" @click="handleStopBatch">
            停止
          </el-button>
        </el-form-item>
      </el-form>
    </el-card>

    <el-card class="section-card" style="margin-top: 16px">
      <template #header>
        <span>发送结果（{{ producerStore.sendHistory.length }}）</span>
        <el-button size="small" style="margin-left: 12px" @click="producerStore.clearHistory()">清空</el-button>
      </template>
      <el-table :data="producerStore.sendHistory" style="width: 100%" size="small" max-height="400">
        <el-table-column label="Partition" width="100">
          <template #default="{ row }">
            <el-tag
              :color="partitionColor(row.partition)"
              size="small"
              style="color: #fff; border: none"
            >
              P{{ row.partition }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="offset" label="Offset" width="120" />
        <el-table-column prop="timestamp" label="时间戳" width="180" />
        <el-table-column label="耗时" width="100">
          <template #default="{ row }">{{ row.latencyMs }}ms</template>
        </el-table-column>
      </el-table>
    </el-card>

    <TopicCreateDialog v-model:visible="topicDialogVisible" @created="metadataStore.refreshTopics()" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { ElMessage } from 'element-plus'
import { Plus, Promotion, Delete } from '@element-plus/icons-vue'
import { useProducerStore } from '@/stores/producerStore'
import { useMetadataStore } from '@/stores/metadataStore'
import type { ProduceRequest } from '../../main/kafka/types'
import TopicCreateDialog from '@/components/TopicCreateDialog.vue'

const producerStore = useProducerStore()
const metadataStore = useMetadataStore()
const topicDialogVisible = ref(false)

const PARTITION_COLORS = [
  '#409EFF', '#67C23A', '#E6A23C', '#F56C6C', '#909399',
  '#337ECC', '#529B2E', '#B88230', '#C45656', '#73767A',
]

function partitionColor(partition: number): string {
  return PARTITION_COLORS[partition % PARTITION_COLORS.length]
}

const form = producerStore.form
const batchForm = producerStore.batchForm

const availablePartitions = computed(() => {
  const t = metadataStore.topics.find(t => t.name === form.topic)
  if (!t) return []
  return Array.from({ length: t.partitionCount }, (_, i) => i)
})

function addHeader() {
  form.headers.push({ key: '', value: '' })
}

function removeHeader(idx: number) {
  form.headers.splice(idx, 1)
}

async function handleSend() {
  if (!form.topic) {
    ElMessage.warning('请选择 Topic')
    return
  }
  try {
    const req: ProduceRequest = {
      topic: form.topic,
      partition: form.partition,
      key: form.key || undefined,
      value: form.value,
      headers: form.headers.length > 0
        ? Object.fromEntries(form.headers.filter(h => h.key).map(h => [h.key, h.value]))
        : undefined,
      acks: form.acks,
    }
    const result = await producerStore.sendSingle(req)
    ElMessage.success(`发送成功 → P${result.partition} Offset:${result.offset}`)
  } catch (err: unknown) {
    ElMessage.error(`发送失败: ${err instanceof Error ? err.message : String(err)}`)
  }
}

async function handleStartBatch() {
  if (!form.topic) return
  try {
    const req: ProduceRequest = {
      topic: form.topic,
      partition: form.partition,
      key: form.key || undefined,
      value: form.value,
      acks: form.acks,
    }
    await producerStore.startBatch(req, batchForm.count, batchForm.intervalMs, batchForm.keyStrategy)
    ElMessage.success(`批量发送已开始，共 ${batchForm.count} 条`)
  } catch (err: unknown) {
    ElMessage.error(`启动失败: ${err instanceof Error ? err.message : String(err)}`)
  }
}

async function handleStopBatch() {
  await producerStore.stopBatch()
  ElMessage.info('批量发送已停止')
}
</script>

<style scoped>
.producer-view {
  max-width: 900px;
}
.view-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}
.view-header h2 {
  margin: 0;
}
.section-card {
  margin-bottom: 0;
}
.header-row {
  display: flex;
  align-items: center;
  margin-bottom: 4px;
}
</style>