<template>
  <el-card class="connection-form" shadow="never">
    <el-form :model="form" label-width="100px" size="small" inline>
      <el-form-item label="主机">
        <el-input v-model="form.host" placeholder="localhost" style="width: 160px" />
      </el-form-item>
      <el-form-item label="端口">
        <el-input-number v-model="form.port" :min="1" :max="65535" controls-position="right" style="width: 110px" />
      </el-form-item>
      <el-form-item label="用户名">
        <el-input v-model="form.username" style="width: 120px" />
      </el-form-item>
      <el-form-item label="密码">
        <el-input v-model="form.password" type="password" show-password style="width: 140px" />
      </el-form-item>
      <el-form-item label="TCP">
        <el-switch v-model="form.useTCP" />
      </el-form-item>
      <el-form-item v-if="!form.useTCP" label="SSL">
        <el-switch v-model="form.sslEnabled" />
      </el-form-item>
    </el-form>

    <el-collapse-transition>
      <div class="ssl-config">
        <el-form :model="form" label-width="120px" size="small" inline>
          <el-form-item label="心跳发送(ms)">
            <el-input-number v-model="form.heartbeatOutgoing" :min="0" :step="1000" controls-position="right" style="width: 140px" />
          </el-form-item>
          <el-form-item label="心跳接收(ms)">
            <el-input-number v-model="form.heartbeatIncoming" :min="0" :step="1000" controls-position="right" style="width: 140px" />
          </el-form-item>
        </el-form>
      </div>
    </el-collapse-transition>

    <div class="form-actions">
      <el-button
        type="primary"
        :loading="connectionStore.connecting"
        :disabled="connectionStore.connecting || connectionStore.status.status === 'connected'"
        @click="onConnect"
      >连接</el-button>
      <el-button
        :loading="testing"
        :disabled="connectionStore.connecting"
        @click="onTest"
      >测试连接</el-button>
      <el-button
        :loading="disconnecting"
        :disabled="connectionStore.status.status !== 'connected'"
        @click="onDisconnect"
      >断开</el-button>
    </div>
  </el-card>
</template>

<script setup lang="ts">
import { reactive, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { useConnectionStore } from '../stores/connection'
import type { ConnectionConfig } from '../../../shared/types'

const connectionStore = useConnectionStore()

const testing = ref(false)
const disconnecting = ref(false)

const form = reactive<ConnectionConfig>({
  host: 'localhost',
  port: 61614,
  username: 'admin',
  password: 'admin',
  sslEnabled: false,
  useTCP: false,
  heartbeatOutgoing: 10000,
  heartbeatIncoming: 10000
})

watch(
  () => connectionStore.lastConfig,
  (cfg) => {
    if (cfg) Object.assign(form, cfg)
  },
  { immediate: true }
)

watch(
  () => form.useTCP,
  (tcp, old) => {
    if (old === undefined) return
    if (tcp) {
      form.sslEnabled = false
      if (form.port === 61614 || form.port === 61617) {
        form.port = 61613
      }
    } else {
      if (form.port === 61613) {
        form.port = 61614
      }
    }
  }
)

watch(
  () => form.sslEnabled,
  (ssl, old) => {
    if (old === undefined) return
    const defaultPort = ssl ? 61617 : 61614
    const otherDefault = ssl ? 61614 : 61617
    if (form.port === otherDefault) {
      form.port = defaultPort
    }
  }
)

async function onConnect(): Promise<void> {
  const r = await connectionStore.connect({ ...form })
  if (r.success) ElMessage.success('连接成功')
  else ElMessage.error(r.error || '连接失败')
}

async function onTest(): Promise<void> {
  testing.value = true
  try {
    const r = await connectionStore.test({ ...form })
    if (r.success) ElMessage.success('测试连接成功')
    else ElMessage.error(r.error || '测试失败')
  } finally {
    testing.value = false
  }
}

async function onDisconnect(): Promise<void> {
  disconnecting.value = true
  try {
    await connectionStore.disconnect()
  } finally {
    disconnecting.value = false
  }
}
</script>

<style scoped>
.connection-form {
  margin-bottom: 12px;
}
.ssl-config {
  padding: 8px 0;
  border-top: 1px dashed var(--el-border-color);
  margin-top: 8px;
}
.form-actions {
  margin-top: 8px;
}
</style>