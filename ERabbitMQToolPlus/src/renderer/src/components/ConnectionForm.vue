<template>
  <el-card class="connection-form" shadow="never">
    <el-form :model="form" label-width="100px" size="small" inline>
      <el-form-item label="主机">
        <el-input v-model="form.host" placeholder="localhost" style="width: 160px" />
      </el-form-item>
      <el-form-item label="端口">
        <el-input-number v-model="form.port" :min="1" :max="65535" controls-position="right" style="width: 110px" />
      </el-form-item>
      <el-form-item label="虚拟主机">
        <el-input v-model="form.vhost" style="width: 100px" />
      </el-form-item>
      <el-form-item label="用户名">
        <el-input v-model="form.username" style="width: 120px" />
      </el-form-item>
      <el-form-item label="密码">
        <el-input v-model="form.password" type="password" show-password style="width: 140px" />
      </el-form-item>
      <el-form-item label="超时(ms)">
        <el-input-number v-model="form.timeout" :min="1000" :step="1000" controls-position="right" style="width: 120px" />
      </el-form-item>
      <el-form-item label="SSL">
        <el-switch v-model="form.sslEnabled" />
      </el-form-item>
    </el-form>

    <el-collapse-transition>
      <div v-if="form.sslEnabled" class="ssl-config">
        <el-form :model="form" label-width="120px" size="small" inline>
          <el-form-item label="验证证书">
            <el-switch v-model="form.rejectUnauthorized" />
          </el-form-item>
          <el-form-item label="CA 证书">
            <el-input v-model="form.caPath" placeholder="文件路径" style="width: 240px">
              <template #append>
                <el-button @click="selectFile('caPath')">选择</el-button>
              </template>
            </el-input>
          </el-form-item>
          <el-form-item label="客户端证书">
            <el-input v-model="form.certPath" placeholder="文件路径" style="width: 240px">
              <template #append>
                <el-button @click="selectFile('certPath')">选择</el-button>
              </template>
            </el-input>
          </el-form-item>
          <el-form-item label="客户端私钥">
            <el-input v-model="form.keyPath" placeholder="文件路径" style="width: 240px">
              <template #append>
                <el-button @click="selectFile('keyPath')">选择</el-button>
              </template>
            </el-input>
          </el-form-item>
          <el-form-item label="私钥密码">
            <el-input v-model="form.passphrase" type="password" show-password style="width: 160px" />
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
  port: 5672,
  vhost: '/',
  username: 'guest',
  password: 'guest',
  timeout: 5000,
  sslEnabled: false,
  rejectUnauthorized: true,
  caPath: '',
  certPath: '',
  keyPath: '',
  passphrase: ''
})

watch(
  () => connectionStore.lastConfig,
  (cfg) => {
    if (cfg) Object.assign(form, cfg)
  },
  { immediate: true }
)

watch(
  () => form.sslEnabled,
  (ssl, old) => {
    if (old === undefined) return
    const defaultPort = ssl ? 5671 : 5672
    const otherDefault = ssl ? 5672 : 5671
    if (form.port === otherDefault) {
      form.port = defaultPort
    }
  }
)

async function selectFile(field: 'caPath' | 'certPath' | 'keyPath'): Promise<void> {
  const p = await window.api.dialog.selectFile()
  if (p) form[field] = p
}

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
