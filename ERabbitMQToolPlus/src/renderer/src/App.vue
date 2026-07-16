<template>
  <el-container class="app-container">
    <el-header class="app-header">
      <div class="app-title">ERabbitMQ Tool</div>
      <div class="header-status">
        <el-tag :type="statusTagType" size="small">{{ connectionStore.status.display }}</el-tag>
        <el-tag v-if="connectionStore.status.sslEnabled" type="success" size="small">SSL</el-tag>
        <el-tag v-if="connectionStore.status.sslEnabled && !connectionStore.status.rejectUnauthorized" type="warning" size="small">证书认证已关闭</el-tag>
      </div>
      <div class="header-actions">
        <el-button size="small" @click="logStore.toggle()">日志</el-button>
        <el-button size="small" @click="showSettings = true">设置</el-button>
      </div>
    </el-header>

    <el-main class="app-main">
      <ConnectionForm />
      <el-tabs v-model="activeTab">
        <el-tab-pane label="生产者" name="producer" lazy="false">
          <ProducerView v-show="activeTab === 'producer'" />
        </el-tab-pane>
        <el-tab-pane label="消费者" name="consumer" lazy="false">
          <ConsumerView v-show="activeTab === 'consumer'" />
        </el-tab-pane>
      </el-tabs>
    </el-main>

    <el-footer class="app-footer">
      <span>{{ connectionStore.status.display }}</span>
      <span class="footer-counts">
        发送：{{ producerStore.history.length }} | 接收：{{ consumerStore.messages.length }}
      </span>
    </el-footer>

    <LogPanel v-if="logStore.visible" class="app-log-panel" />
    <SettingsView v-model:visible="showSettings" />
  </el-container>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useConnectionStore } from './stores/connection'
import { useProducerStore } from './stores/producer'
import { useConsumerStore } from './stores/consumer'
import { useLogStore } from './stores/log'
import ProducerView from './views/ProducerView.vue'
import ConsumerView from './views/ConsumerView.vue'
import LogPanel from './components/LogPanel.vue'
import SettingsView from './views/SettingsView.vue'
import ConnectionForm from './components/ConnectionForm.vue'

const connectionStore = useConnectionStore()
const producerStore = useProducerStore()
const consumerStore = useConsumerStore()
const logStore = useLogStore()

const activeTab = ref<'producer' | 'consumer'>('producer')
const showSettings = ref(false)

const statusTagType = computed(() => {
  switch (connectionStore.status.status) {
    case 'connected': return 'success'
    case 'connecting': return 'warning'
    case 'error': return 'danger'
    default: return 'info'
  }
})
</script>

<style scoped>
.app-container { height: 100vh; display: flex; flex-direction: column; }
.app-header { display: flex; align-items: center; gap: 16px; border-bottom: 1px solid var(--el-border-color); }
.app-title { font-weight: bold; font-size: 16px; }
.header-status { display: flex; gap: 6px; align-items: center; }
.header-actions { margin-left: auto; display: flex; gap: 8px; }
.app-main { flex: 1; overflow: auto; }
.app-footer { display: flex; justify-content: space-between; align-items: center; border-top: 1px solid var(--el-border-color); font-size: 12px; height: 32px; }
.app-log-panel { border-top: 1px solid var(--el-border-color); }
</style>
