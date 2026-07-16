<template>
  <div class="consumer-view">
    <el-card shadow="never" class="consumer-card">
      <el-form :model="form" label-width="100px" size="small" :disabled="consumerStore.running">
        <el-divider content-position="left">消费配置</el-divider>
        <el-form-item label="Destination">
          <el-input v-model="form.destination" placeholder="/queue/test 或 /topic/test" style="width: 320px" />
        </el-form-item>
        <el-form-item label="ACK 模式">
          <el-select v-model="form.ackMode" style="width: 200px">
            <el-option label="auto（自动确认）" value="auto" />
            <el-option label="client（客户端确认）" value="client" />
            <el-option label="client-individual（单条确认）" value="client-individual" />
          </el-select>
        </el-form-item>
        <el-form-item v-if="form.ackMode === 'client-individual'" label="预取数">
          <el-input-number v-model="form.prefetchCount" :min="1" controls-position="right" style="width: 120px" />
        </el-form-item>
        <el-form-item label="Selector">
          <el-input v-model="form.selector" placeholder="JMS selector，如 color='red'" style="width: 320px" />
        </el-form-item>
        <el-form-item label="路由键过滤">
          <el-input v-model="form.filterRoutingKey" placeholder="如 order.create" style="width: 200px" />
        </el-form-item>
        <el-form-item label="Header过滤键">
          <el-input v-model="form.filterByHeaderKey" style="width: 160px" />
        </el-form-item>
        <el-form-item label="Header过滤值">
          <el-input v-model="form.filterByHeaderValue" style="width: 160px" />
        </el-form-item>
        <el-form-item label="最大接收数">
          <el-input-number v-model="form.maxReceive" :min="0" controls-position="right" style="width: 120px" />
          <span class="hint">0=无限制</span>
        </el-form-item>
      </el-form>

      <div class="consumer-actions">
        <el-button v-if="!consumerStore.running" type="primary" @click="onStart">开始消费</el-button>
        <el-button v-if="consumerStore.running && !consumerStore.paused" @click="onPause">暂停</el-button>
        <el-button v-if="consumerStore.paused" type="primary" @click="onResume">恢复</el-button>
        <el-button v-if="consumerStore.running" type="danger" @click="onStop">停止</el-button>
      </div>
    </el-card>

    <div class="message-list">
      <MessageTable />
    </div>
  </div>
</template>

<script setup lang="ts">
import { reactive, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import MessageTable from '../components/MessageTable.vue'
import { useConsumerStore } from '../stores/consumer'
import { useConnectionStore } from '../stores/connection'
import type { ConsumerConfig } from '../../../shared/types'

const consumerStore = useConsumerStore()
const connectionStore = useConnectionStore()

const form = reactive<ConsumerConfig>({
  destination: '',
  ackMode: 'auto',
  selector: '',
  prefetchCount: 1,
  maxReceive: 0,
  filterRoutingKey: '',
  filterByHeaderKey: '',
  filterByHeaderValue: ''
})

async function onStart() {
  if (connectionStore.status.status !== 'connected') {
    ElMessage.warning('请先建立连接')
    return
  }
  const cfg = { ...form }
  const r = await consumerStore.start(cfg)
  if (!r.success) ElMessage.error(r.error || '启动失败')
  else {
    ElMessage.success('已开始消费')
    window.api.config.saveConsumer(cfg)
  }
}

async function onPause() {
  const r = await consumerStore.pause()
  if (!r.success) ElMessage.error(r.error || '暂停失败')
}

async function onResume() {
  const r = await consumerStore.resume()
  if (!r.success) ElMessage.error(r.error || '恢复失败')
}

async function onStop() {
  await consumerStore.stop()
  ElMessage.info('已停止消费')
}

onMounted(async () => {
  const cfg = await window.api.config.loadConsumer()
  if (cfg) {
    Object.assign(form, cfg)
  }
})
</script>

<style scoped>
.consumer-view { padding: 8px; display: flex; flex-direction: column; height: 100%; }
.consumer-card { margin-bottom: 8px; }
.consumer-actions { margin: 8px 0; }
.hint { color: var(--el-text-color-secondary); font-size: 12px; margin-left: 8px; }
.message-list { flex: 1; min-height: 200px; overflow: hidden; }
</style>