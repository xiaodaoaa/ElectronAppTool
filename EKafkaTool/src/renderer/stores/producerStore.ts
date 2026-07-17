import { defineStore } from 'pinia'
import { ref, reactive } from 'vue'
import { getKafkaApi } from '@/api/kafkaApi'
import type { ProduceRequest, ProduceResult } from '../../main/kafka/types'

const MAX_HISTORY = 500

export const useProducerStore = defineStore('producer', () => {
  const sendHistory = ref<ProduceResult[]>([])
  const batchTaskId = ref<string | null>(null)
  const batchRunning = ref(false)
  const sending = ref(false)
  let ackUnsub: (() => void) | null = null

  const form = reactive({
    topic: '',
    partition: undefined as number | undefined,
    key: '',
    value: '{"message": "hello {{seq}}"}',
    headers: [] as Array<{ key: string; value: string }>,
    acks: -1 as -1 | 0 | 1,
  })

  const batchForm = reactive({
    count: 100,
    intervalMs: 500,
    keyStrategy: 'fixed',
  })

  async function sendSingle(req: ProduceRequest): Promise<ProduceResult> {
    sending.value = true
    try {
      const result = await getKafkaApi().produce(req)
      sendHistory.value = [result, ...sendHistory.value].slice(0, MAX_HISTORY)
      return result
    } finally {
      sending.value = false
    }
  }

  async function startBatch(
    req: ProduceRequest,
    count: number,
    intervalMs: number,
    keyStrategy: string,
  ) {
    batchRunning.value = true
    sendHistory.value = []
    const taskId = await getKafkaApi().produceBatch({ req, count, intervalMs, keyStrategy })
    batchTaskId.value = taskId
  }

  async function stopBatch() {
    if (batchTaskId.value) {
      await getKafkaApi().stopBatch(batchTaskId.value)
      batchTaskId.value = null
      batchRunning.value = false
    }
  }

  function clearHistory() {
    sendHistory.value = []
  }

  function setupAckListener() {
    teardownAckListener()
    ackUnsub = getKafkaApi().onProduceAck((result) => {
      sendHistory.value = [result, ...sendHistory.value].slice(0, MAX_HISTORY)
    })
  }

  function teardownAckListener() {
    if (ackUnsub) {
      ackUnsub()
      ackUnsub = null
    }
  }

  return {
    sendHistory, batchTaskId, batchRunning, sending,
    form, batchForm,
    sendSingle, startBatch, stopBatch, clearHistory, setupAckListener, teardownAckListener,
  }
})