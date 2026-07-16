<template>
  <div class="producer-view">
    <el-card shadow="never" class="producer-card">
      <el-form :model="form" label-width="100px" size="small">
        <el-divider content-position="left">目标交换机</el-divider>
        <el-form-item label="交换机名称">
          <el-input v-model="form.exchange" placeholder="空=默认交换机" style="width: 240px" />
        </el-form-item>
        <el-form-item label="交换机类型">
          <el-select v-model="form.exchangeType" style="width: 140px">
            <el-option label="Direct" value="direct" />
            <el-option label="Topic" value="topic" />
            <el-option label="Fanout" value="fanout" />
            <el-option label="Headers" value="headers" />
          </el-select>
        </el-form-item>
        <el-form-item label="路由键">
          <el-input v-model="form.routingKey" :disabled="form.exchangeType === 'fanout'" style="width: 240px" />
        </el-form-item>
        <el-form-item label="自动声明">
          <el-switch v-model="form.autoDeclare" />
        </el-form-item>
        <el-form-item label="持久化">
          <el-switch v-model="form.durable" />
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
            v-model="form.message"
            type="textarea"
            :rows="8"
            placeholder="输入消息内容"
          />
        </el-form-item>

        <el-divider content-position="left">消息属性</el-divider>
        <el-form-item label="contentType">
          <el-input v-model="form.properties.contentType" style="width: 200px" />
        </el-form-item>
        <el-form-item label="deliveryMode">
          <el-radio-group v-model="form.properties.deliveryMode">
            <el-radio :label="1">非持久化</el-radio>
            <el-radio :label="2">持久化</el-radio>
          </el-radio-group>
        </el-form-item>
        <el-form-item label="priority">
          <el-input-number v-model="form.properties.priority" :min="0" :max="9" controls-position="right" style="width: 100px" />
        </el-form-item>
        <el-form-item label="expiration">
          <el-input v-model="form.properties.expiration" placeholder="如 60000" style="width: 160px" />
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
        <el-button @click="form.message = ''">清空消息</el-button>
        <span v-if="producerStore.progress" class="progress-text">
          {{ producerStore.progress.current }}/{{ producerStore.progress.total }}
        </span>
      </div>

      <el-collapse class="history-collapse">
        <el-collapse-item title="历史记录（最近10条）">
          <el-table :data="producerStore.history" size="small" @row-click="onHistoryClick">
            <el-table-column prop="time" label="时间" width="180" />
            <el-table-column prop="exchange" label="交换机" width="120" />
            <el-table-column prop="routingKey" label="路由键" width="140" />
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
  exchange: '',
  exchangeType: 'direct',
  routingKey: '',
  autoDeclare: false,
  durable: true,
  message: '',
  format: 'json',
  properties: {
    contentType: 'application/json',
    deliveryMode: 2,
    priority: 0
  },
  batch: { count: 1, intervalMs: 100 }
})

onMounted(async () => {
  producerStore.loadHistory()
  const cfg = await window.api.config.loadProducer()
  if (cfg) {
    Object.assign(form, cfg)
    if (cfg.properties) Object.assign(form.properties, cfg.properties)
    if (cfg.batch) Object.assign(form.batch, cfg.batch)
  }
})

async function onSend() {
  if (!connectionStore.status.status || connectionStore.status.status !== 'connected') {
    ElMessage.warning('请先建立连接')
    return
  }
  const params = { ...form, properties: { ...form.properties }, batch: { ...form.batch } }
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
  form.message = row.messageSummary
}
</script>

<style scoped>
.producer-view { padding: 8px; }
.producer-card { margin-top: 12px; }
.producer-actions { margin: 12px 0; display: flex; align-items: center; gap: 12px; }
.progress-text { color: var(--el-color-primary); }
.history-collapse { margin-top: 12px; }
</style>
