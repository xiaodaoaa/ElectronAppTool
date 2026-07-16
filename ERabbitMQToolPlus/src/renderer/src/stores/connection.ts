import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { ConnectionConfig, ConnectionStatusInfo } from '../../../shared/types'

export const useConnectionStore = defineStore('connection', () => {
  const status = ref<ConnectionStatusInfo>({
    status: 'disconnected',
    display: '未连接',
    sslEnabled: false,
    rejectUnauthorized: false
  })
  const lastConfig = ref<ConnectionConfig | null>(null)
  const connecting = ref(false)

  async function connect(config: ConnectionConfig) {
    connecting.value = true
    try {
      const r = await window.api.connection.connect(config)
      return r
    } finally {
      connecting.value = false
    }
  }

  async function test(config: ConnectionConfig) {
    return await window.api.connection.test(config)
  }

  async function disconnect() {
    return await window.api.connection.disconnect()
  }

  function bindIpc() {
    window.api.connection.onStatusChange((info) => {
      status.value = info
    })
    window.api.connection.onLastConfigLoaded?.((config) => {
      lastConfig.value = config
    })
  }

  return { status, lastConfig, connecting, connect, test, disconnect, bindIpc }
})
