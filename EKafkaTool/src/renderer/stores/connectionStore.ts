import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { getKafkaApi } from '@/api/kafkaApi'
import type { ConnectionConfig, ClusterSummary } from '../../main/kafka/types'

export const useConnectionStore = defineStore('connection', () => {
  const configs = ref<ConnectionConfig[]>([])
  const activeId = ref<string | null>(null)
  const status = ref<'disconnected' | 'connecting' | 'connected' | 'error'>('disconnected')
  const cluster = ref<ClusterSummary | null>(null)
  const error = ref<string | null>(null)

  const isConnected = computed(() => status.value === 'connected')
  const activeConfig = computed(() => configs.value.find(c => c.id === activeId.value) ?? null)

  async function loadConfigs() {
    configs.value = await getKafkaApi().connList()
  }

  async function saveConfig(config: ConnectionConfig) {
    await getKafkaApi().connSave(JSON.parse(JSON.stringify(config)))
    await loadConfigs()
  }

  async function deleteConfig(id: string) {
    await getKafkaApi().connDelete(id)
    if (activeId.value === id) {
      activeId.value = null
      status.value = 'disconnected'
      cluster.value = null
    }
    await loadConfigs()
  }

  async function testConnection(config: ConnectionConfig) {
    return getKafkaApi().connTest(JSON.parse(JSON.stringify(config)))
  }

  async function connect(id: string) {
    status.value = 'connecting'
    error.value = null
    try {
      cluster.value = await getKafkaApi().connConnect(id)
      activeId.value = id
      status.value = 'connected'
    } catch (err: unknown) {
      status.value = 'error'
      error.value = err instanceof Error ? err.message : String(err)
      throw err
    }
  }

  async function disconnect() {
    await getKafkaApi().connDisconnect()
    activeId.value = null
    status.value = 'disconnected'
    cluster.value = null
  }

  function setupStatusListener() {
    getKafkaApi().onConnStatus((data) => {
      if (data.status === 'disconnected' || data.status === 'error') {
        status.value = data.status
        if (data.detail) error.value = data.detail
      }
    })
  }

  return {
    configs, activeId, status, cluster, error,
    isConnected, activeConfig,
    loadConfigs, saveConfig, deleteConfig, testConnection, connect, disconnect, setupStatusListener,
  }
})