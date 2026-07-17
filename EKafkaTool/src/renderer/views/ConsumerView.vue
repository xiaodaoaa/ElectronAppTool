<template>
  <div class="consumer-view">
    <div class="view-header">
      <h2>消费者</h2>
      <el-button type="primary" @click="dialogVisible = true">
        <el-icon><Plus /></el-icon> 新增消费者实例
      </el-button>
    </div>

    <div class="cards-row">
      <ConsumerCard
        v-for="inst in consumerStore.instances"
        :key="inst.instanceId"
        :instance="inst"
        @start="handleStart"
        @stop="handleStop"
        @pause="handlePause"
        @resume="handleResume"
        @seek-to-beginning="handleSeekToBeginning"
        @seek-to-end="handleSeekToEnd"
        @commit="handleCommit"
      />
      <el-empty v-if="!consumerStore.instances.length" description="暂无消费者实例，请新增" :image-size="80" />
    </div>

    <el-card class="section-card" style="margin-top: 16px">
      <template #header>消息流</template>
      <MessageTable
        :messages="consumerStore.messages"
        :instances="consumerStore.instances"
        :filter-instance="filterInstance"
        :filter-partition="filterPartition"
        :paused="consumerStore.messagePaused"
        @update:filter-instance="filterInstance = $event"
        @update:filter-partition="filterPartition = $event"
        @toggle-pause="consumerStore.toggleMessagePause()"
        @clear="consumerStore.clearMessages()"
        @export="handleExport"
      />
    </el-card>

    <el-card v-if="consumerStore.rebalanceEvents.length" class="section-card" style="margin-top: 16px">
      <template #header>再均衡事件时间线</template>
      <el-timeline>
        <el-timeline-item
          v-for="evt in consumerStore.rebalanceEvents"
          :key="evt.ts"
          :timestamp="new Date(evt.ts).toLocaleTimeString('zh-CN')"
          type="warning"
        >
          再均衡: Group {{ evt.groupId }} Generation {{ evt.generation }}
          <div class="assign-detail">
            <span v-for="(partitions, member) in evt.assignments" :key="member" class="assign-item">
              {{ member }}: [{{ (partitions as number[]).join(', ') }}]
            </span>
          </div>
        </el-timeline-item>
      </el-timeline>
    </el-card>

    <el-dialog v-model="dialogVisible" title="新增消费者实例" width="500px">
      <el-form ref="formRef" :model="createForm" :rules="createRules" label-width="100px">
        <el-form-item label="别名" prop="alias">
          <el-input v-model="createForm.alias" placeholder="如：Consumer-A" />
        </el-form-item>
        <el-form-item label="Group ID" prop="groupId">
          <el-input v-model="createForm.groupId" placeholder="如：demo-group" />
        </el-form-item>
        <el-form-item label="订阅 Topic" prop="topics">
          <el-select
            v-model="createForm.topics"
            multiple
            filterable
            allow-create
            placeholder="选择 Topic"
            style="width: 100%"
          >
            <el-option v-for="t in metadataStore.topics" :key="t.name" :label="t.name" :value="t.name" />
          </el-select>
        </el-form-item>
        <el-form-item label="起始位置">
          <el-radio-group v-model="createForm.fromBeginning">
            <el-radio :value="true">earliest（从头消费）</el-radio>
            <el-radio :value="false">latest（最新）</el-radio>
          </el-radio-group>
        </el-form-item>
        <el-form-item label="提交方式">
          <el-radio-group v-model="createForm.autoCommit">
            <el-radio :value="true">自动提交</el-radio>
            <el-radio :value="false">手动提交</el-radio>
          </el-radio-group>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleCreate" :loading="creating">创建并启动</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue'
import { ElMessage } from 'element-plus'
import type { FormInstance, FormRules } from 'element-plus'
import { Plus } from '@element-plus/icons-vue'
import { useConsumerStore } from '@/stores/consumerStore'
import { useMetadataStore } from '@/stores/metadataStore'
import type { ConsumerOptions } from '../../main/kafka/types'
import ConsumerCard from '@/components/ConsumerCard.vue'
import MessageTable from '@/components/MessageTable.vue'

const consumerStore = useConsumerStore()
const metadataStore = useMetadataStore()

const dialogVisible = ref(false)
const creating = ref(false)
const formRef = ref<FormInstance>()
const filterInstance = ref<string | null>(null)
const filterPartition = ref<number | null>(null)

const createForm = reactive({
  alias: '',
  groupId: '',
  topics: [] as string[],
  fromBeginning: true,
  autoCommit: true,
})

const createRules: FormRules = {
  alias: [{ required: true, message: '请输入别名', trigger: 'blur' }],
  groupId: [{ required: true, message: '请输入 Group ID', trigger: 'blur' }],
  topics: [{ required: true, message: '请选择 Topic', trigger: 'change', type: 'array', min: 1 }],
}

async function handleCreate() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return

  creating.value = true
  try {
    const opts: ConsumerOptions = {
      alias: createForm.alias,
      groupId: createForm.groupId,
      topics: [...createForm.topics],
      fromBeginning: createForm.fromBeginning,
      autoCommit: createForm.autoCommit,
    }
    const id = await consumerStore.createInstance(opts)
    await consumerStore.startInstance(id)
    ElMessage.success(`消费者 "${createForm.alias}" 已启动`)
    dialogVisible.value = false
    createForm.alias = ''
    createForm.groupId = ''
    createForm.topics = []
  } catch (err: unknown) {
    ElMessage.error(`创建失败: ${err instanceof Error ? err.message : String(err)}`)
  } finally {
    creating.value = false
  }
}

async function handleStart(id: string) {
  try {
    await consumerStore.startInstance(id)
  } catch (err: unknown) {
    ElMessage.error(`启动失败: ${err instanceof Error ? err.message : String(err)}`)
  }
}

async function handleStop(id: string) {
  await consumerStore.stopInstance(id)
}

async function handlePause(id: string) {
  await consumerStore.pauseInstance(id)
}

async function handleResume(id: string) {
  await consumerStore.resumeInstance(id)
}

async function handleSeekToBeginning(id: string) {
  const inst = consumerStore.instances.find(i => i.instanceId === id)
  if (!inst) return
  for (const assign of inst.assignments) {
    for (const p of assign.partitions) {
      await consumerStore.seekInstance(id, assign.topic, p, '0')
    }
  }
  ElMessage.success('Offset 已重置到开头')
}

async function handleSeekToEnd(id: string) {
  const inst = consumerStore.instances.find(i => i.instanceId === id)
  if (!inst) return
  for (const assign of inst.assignments) {
    for (const p of assign.partitions) {
      await consumerStore.seekInstance(id, assign.topic, p, '-1')
    }
  }
  ElMessage.success('Offset 已重置到结尾')
}

async function handleCommit(id: string) {
  await consumerStore.commitInstance(id)
  ElMessage.success('Offset 已提交')
}

function handleExport(format: 'json' | 'csv') {
  const data = consumerStore.messages
  if (format === 'json') {
    const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' })
    downloadBlob(blob, `kafka-messages-${Date.now()}.json`)
  } else {
    const header = '时间,实例,分区,Offset,Key,Value\n'
    const rows = data.map(m =>
      `"${new Date(m.receivedAt).toISOString()}","${m.instanceId}","${m.partition}","${m.offset}","${m.key ?? ''}","${m.value.replace(/"/g, '""')}"`
    ).join('\n')
    const blob = new Blob([header + rows], { type: 'text/csv;charset=utf-8' })
    downloadBlob(blob, `kafka-messages-${Date.now()}.csv`)
  }
}

function downloadBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  a.click()
  URL.revokeObjectURL(url)
  ElMessage.success(`已导出: ${filename}`)
}
</script>

<style scoped>
.consumer-view {
  max-width: 100%;
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
.cards-row {
  display: flex;
  gap: 12px;
  overflow-x: auto;
  padding-bottom: 8px;
}
.section-card {
  margin-bottom: 0;
}
.assign-detail {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  margin-top: 4px;
}
.assign-item {
  margin-right: 12px;
}
</style>