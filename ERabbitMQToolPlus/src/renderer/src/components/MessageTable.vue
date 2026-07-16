<template>
  <el-table :data="displayMessages" size="small" height="100%" border>
    <el-table-column prop="seq" label="序号" width="70" />
    <el-table-column prop="receivedAt" label="接收时间" width="180" />
    <el-table-column prop="exchange" label="交换机" width="120" />
    <el-table-column prop="routingKey" label="路由键" width="140" />
    <el-table-column prop="messageId" label="消息ID" width="160" show-overflow-tooltip />
    <el-table-column prop="deliveryTag" label="deliveryTag" width="100" />
    <el-table-column label="消息体" show-overflow-tooltip>
      <template #default="{ row }">
        {{ truncateContent(row.content) }}
      </template>
    </el-table-column>
    <el-table-column label="操作" width="240" fixed="right">
      <template #default="{ row }">
        <el-button size="small" link @click="onView(row)">详情</el-button>
        <template v-if="!autoAck">
          <el-button size="small" link type="success" @click="onAck(row)">确认</el-button>
          <el-button size="small" link type="danger" @click="onNack(row)">拒绝</el-button>
        </template>
      </template>
    </el-table-column>
  </el-table>

  <MessageDetail v-model:visible="detailVisible" :message="detailMessage" />
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import MessageDetail from './MessageDetail.vue'
import { useConsumerStore } from '../stores/consumer'
import { useSettingsStore } from '../stores/settings'
import type { ConsumedMessage } from '../../../shared/types'

const consumerStore = useConsumerStore()
const settingsStore = useSettingsStore()
const autoAck = computed(() => consumerStore.autoAck)

const detailVisible = ref(false)
const detailMessage = ref<ConsumedMessage | null>(null)

const displayMessages = computed(() => consumerStore.messages)

function truncateContent(content: string): string {
  const max = settingsStore.maxDisplayLength
  return content.length > max ? content.slice(0, max) + '...' : content
}

function onView(row: ConsumedMessage) {
  detailMessage.value = row
  detailVisible.value = true
}

async function onAck(row: ConsumedMessage) {
  const r = await consumerStore.ack(row.deliveryTag)
  if (r.success) ElMessage.success('已确认')
  else ElMessage.error(r.error || '确认失败')
}

async function onNack(row: ConsumedMessage) {
  try {
    const action = await ElMessageBox.confirm('选择处理方式', '拒绝消息', {
      distinguishCancelAndClose: true,
      confirmButtonText: '重新入队',
      cancelButtonText: '丢弃（进死信）',
      type: 'warning'
    })
    const requeue = action === 'confirm'
    const r = await consumerStore.nack(row.deliveryTag, requeue)
    if (r.success) ElMessage.success(requeue ? '已重新入队' : '已丢弃')
    else ElMessage.error(r.error || '拒绝失败')
  } catch {
    // 用户关闭弹窗
  }
}
</script>
