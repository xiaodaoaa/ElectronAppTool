<template>
  <el-dialog :model-value="visible" @update:model-value="$emit('update:visible', $event)" title="消息详情" width="700px">
    <div v-if="message">
      <el-descriptions :column="2" border size="small">
        <el-descriptions-item label="序号">{{ message.seq }}</el-descriptions-item>
        <el-descriptions-item label="接收时间">{{ message.receivedAt }}</el-descriptions-item>
        <el-descriptions-item label="目标">{{ message.destination }}</el-descriptions-item>
        <el-descriptions-item label="消息ID">{{ message.messageId || '-' }}</el-descriptions-item>
        <el-descriptions-item label="已确认">{{ message.acked ? '是' : '否' }}</el-descriptions-item>
        <el-descriptions-item label="Headers" :span="2">{{ message.headers }}</el-descriptions-item>
      </el-descriptions>

      <div class="detail-body">
        <div class="body-header">
          <span>消息体</span>
          <el-button size="small" @click="compressed = !compressed">{{ compressed ? '格式化' : '压缩' }}</el-button>
        </div>
        <pre class="body-content">{{ displayContent }}</pre>
      </div>
    </div>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import type { ConsumedMessage } from '../../../shared/types'

const props = defineProps<{ message: ConsumedMessage | null; visible: boolean }>()
defineEmits<{ 'update:visible': [boolean] }>()

const compressed = ref(true)

const displayContent = computed(() => {
  if (!props.message) return ''
  const raw = props.message.body
  if (compressed.value) return raw
  try {
    return JSON.stringify(JSON.parse(raw), null, 2)
  } catch {
    return raw
  }
})
</script>

<style scoped>
.detail-body { margin-top: 12px; }
.body-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 4px; }
.body-content { background: var(--el-fill-color-light); padding: 8px; max-height: 300px; overflow: auto; white-space: pre-wrap; word-break: break-all; }
</style>