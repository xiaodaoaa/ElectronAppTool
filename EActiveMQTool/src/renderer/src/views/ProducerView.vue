<template>
  <div class="producer-view">
    <el-card shadow="never" class="producer-card">
      <el-form :model="form" label-width="100px" size="small">
        <el-divider content-position="left">目标地址</el-divider>
        <el-form-item label="Destination">
          <el-input v-model="form.destination" placeholder="/queue/test 或 /topic/test" style="width: 320px" />
        </el-form-item>

        <el-divider content-position="left">消息内容</el-divider>
        <el-form-item label="消息格式">
          <el-select v-model="form.format" style="width: 120px">
            <el-option label="JSON" value="json" />
            <el-option label="纯文本" value="text" />
            <el-option label="XML" value="xml" />
          </el-select>
        </el-form-item>
        <el-form-item label="消息体">
          <el-input
            v-model="form.body"
            type="textarea"
            :rows="8"
            placeholder="输入消息内容"
          />
        </el-form-item>

        <el-divider content-position="left">消息属性</el-divider>
        <el-form-item label="contentType">
          <el-input v-model="form.contentType" style="width: 200px" />
        </el-form-item>
        <el-form-item label="持久化">
          <el-switch v-model="form.persistent" />
        </el-form-item>
        <el-form-item label="priority">
          <el-input-number v-model="form.priority" :min="0" :max="9" controls-position="right" style="width: 100px" />
        </el-form-item>
        <el-form-item label="expires(ms)">
          <el-input-number v-model="form.expires" :min="0" :step="1000" controls-position="right" style="width: 140px" />
        </el-form-item>

        <el-divider content-position="left">批量发送</el-divider>
        <el-form-item label="发送次数">
          <el-input-number v-model="form.batch.count" :min="1" :max="10000" controls-position="right" style="width: 140px" />
        </el-form-item>
        <el-form-item label="间隔(ms)">
          <el-input-number v-model="form.batch.intervalMs" :min="0" :step="100" controls-position="right" style="width: 140px" />
        </el-form-item>
      </el-form>

      <div class="producer-actions">
        <el-button type="primary" :loading="producerStore.sending" @click="onSend">发送消息</el-button>
        <el-button @click="form.body = ''">清空消息</el-button>
        <span v-if="producerStore.progress" class="progress-text">
          {{ producerStore.progress.current }}/{{ producerStore.progress.total }}
        </span>
      </div>

      <el-collapse class="history-collapse">
        <el-collapse-item title="历史记录（最近10条）">
          <el-table :data="producerStore.history" size="small" @row-click="onHistoryClick">
            <el-table-column prop="time" label="时间" width="180" />
            <el-table-column prop="destination" label="目标" width="240" />
            <el-table-column prop="messageSummary" label="消息摘要" />
          </el-table>
        </el-collapse-item>
      </el-collapse>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { reactive, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { useProducerStore } from '../stores/producer'
import { useConnectionStore } from '../stores/connection'
import type { SendParams } from '../../../shared/types'

const producerStore = useProducerStore()
const connectionStore = useConnectionStore()

const form = reactive<SendParams>({
  destination: '',
  body: '',
  format: 'json',
  contentType: 'application/json',
  persistent: true,
  priority: 0,
  expires: 0,
  headers: {},
  batch: { count: 1, intervalMs: 100 }
})

onMounted(async () => {
  producerStore.loadHistory()
  const cfg = await window.api.config.loadProducer()
  if (cfg) {
    Object.assign(form, cfg)
    if (cfg.batch) Object.assign(form.batch, cfg.batch)
  }
})

async function onSend() {
  if (!connectionStore.status.status || connectionStore.status.status !== 'connected') {
    ElMessage.warning('请先建立连接')
    return
  }
  const params = { ...form, headers: { ...form.headers }, batch: { ...form.batch } }
  const r = await producerStore.send(params)
  if (r.success) {
    ElMessage.success(`发送成功，消息ID：${r.messageId}`)
    await producerStore.loadHistory()
    window.api.config.saveProducer(params)
  } else {
    ElMessage.error(r.error || '发送失败')
  }
}

function onHistoryClick(row: any) {
  form.body = row.messageSummary
}
</script>

<style scoped>
.producer-view { padding: 8px; }
.producer-card { margin-top: 12px; }
.producer-actions { margin: 12px 0; display: flex; align-items: center; gap: 12px; }
.progress-text { color: var(--el-color-primary); }
.history-collapse { margin-top: 12px; }
</style>