<template>
  <div class="consumer-view">
    <el-card shadow="never" class="consumer-card">
      <el-form :model="form" label-width="100px" size="small" :disabled="consumerStore.running">
        <el-divider content-position="left">队列配置</el-divider>
        <el-form-item label="队列名称">
          <el-input v-model="form.queue" placeholder="如 order.create 或 order.*" style="width: 240px" />
        </el-form-item>
        <el-form-item label="自动声明">
          <el-switch v-model="form.autoDeclareQueue" />
        </el-form-item>
        <el-form-item label="持久化">
          <el-switch v-model="form.durable" />
        </el-form-item>
        <el-form-item label="排他">
          <el-switch v-model="form.exclusive" />
        </el-form-item>
        <el-form-item label="自动删除">
          <el-switch v-model="form.autoDelete" />
        </el-form-item>
        <el-form-item label="绑定交换机">
          <el-input v-model="form.exchange" placeholder="空=默认交换机" style="width: 200px" />
        </el-form-item>
        <el-form-item label="绑定路由键">
          <el-input v-model="form.bindingKey" placeholder="Topic 支持 # / *" style="width: 200px" />
        </el-form-item>

        <el-divider content-position="left">消费配置</el-divider>
        <el-form-item label="消费模式">
          <el-radio-group v-model="form.mode">
            <el-radio label="push">推模式（实时）</el-radio>
            <el-radio label="pull">拉模式（手动）</el-radio>
          </el-radio-group>
        </el-form-item>
        <el-form-item label="预取QoS">
          <el-input-number v-model="form.prefetch" :min="0" controls-position="right" style="width: 120px" />
        </el-form-item>
        <el-form-item label="自动确认">
          <el-switch v-model="form.autoAck" />
          <span class="hint">开启后消息自动确认</span>
        </el-form-item>
        <el-form-item label="路由键过滤">
          <el-input v-model="form.filterByRoutingKey" placeholder="如 order.create" style="width: 200px" />
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
        <el-form-item label="消费前清空">
          <el-switch v-model="form.purgeOnStart" />
          <span class="hint">开启后开始消费时清空队列历史消息</span>
        </el-form-item>
      </el-form>

      <div class="consumer-actions">
        <el-button v-if="!consumerStore.running" type="primary" @click="onStart">开始消费</el-button>
        <el-button v-if="consumerStore.running && !consumerStore.paused" @click="onPause">暂停</el-button>
        <el-button v-if="consumerStore.paused" type="primary" @click="onResume">恢复</el-button>
        <el-button v-if="form.mode === 'pull' && consumerStore.running" @click="onPull">拉取10条</el-button>
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
  queue: '',
  autoDeclareQueue: false,
  durable: true,
  exclusive: false,
  autoDelete: false,
  exchange: '',
  bindingKey: '',
  mode: 'push',
  prefetch: 0,
  autoAck: true,
  filterByRoutingKey: '',
  filterByHeaderKey: '',
  filterByHeaderValue: '',
  maxReceive: 0,
  purgeOnStart: false
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

async function onPull() {
  await consumerStore.pull(10)
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
