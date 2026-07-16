import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { SendParams, SendResult, HistoryItem, ProgressInfo } from '../../../shared/types'

export const useProducerStore = defineStore('producer', () => {
  const history = ref<HistoryItem[]>([])
  const progress = ref<ProgressInfo | null>(null)
  const sending = ref(false)

  async function send(params: SendParams): Promise<SendResult> {
    sending.value = true
    try {
      const r = await window.api.producer.send(params)
      return r
    } finally {
      sending.value = false
    }
  }

  async function loadHistory() {
    history.value = await window.api.producer.getHistory()
  }

  function bindIpc() {
    window.api.producer.onProgress((p) => {
      progress.value = p
    })
  }

  return { history, progress, sending, send, loadHistory, bindIpc }
})