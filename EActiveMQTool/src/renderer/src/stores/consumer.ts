import { defineStore } from 'pinia'
import { ref, shallowRef } from 'vue'
import type { ConsumedMessage, ConsumerConfig, IpcResult } from '../../../shared/types'

export const useConsumerStore = defineStore('consumer', () => {
  const messages = shallowRef<ConsumedMessage[]>([])
  const running = ref(false)
  const paused = ref(false)
  const maxCache = ref(1000)
  const ackMode = ref<'auto' | 'client' | 'client-individual'>('auto')

  async function start(config: ConsumerConfig): Promise<IpcResult> {
    const r = await window.api.consumer.start(config)
    if (r.success) {
      running.value = true
      paused.value = false
      ackMode.value = config.ackMode
    }
    return r
  }

  async function pause() {
    const r = await window.api.consumer.pause()
    if (r.success) paused.value = true
    return r
  }

  async function resume() {
    const r = await window.api.consumer.resume()
    if (r.success) paused.value = false
    return r
  }

  async function stop() {
    const r = await window.api.consumer.stop()
    if (r.success) {
      running.value = false
      paused.value = false
      messages.value = []
    }
    return r
  }

  async function ack(messageId: string) {
    const r = await window.api.consumer.ack(messageId)
    if (r.success) {
      messages.value = messages.value.filter((m) => m.messageId !== messageId)
    }
    return r
  }

  async function nack(messageId: string) {
    const r = await window.api.consumer.nack(messageId)
    if (r.success) {
      messages.value = messages.value.filter((m) => m.messageId !== messageId)
    }
    return r
  }

  let rafId: number | null = null
  let pendingBatch: ConsumedMessage[] = []

  function flushPending() {
    if (pendingBatch.length === 0) return
    const frozen = pendingBatch.map((m) => Object.freeze(m))
    pendingBatch = []
    const current = messages.value
    const merged = current.concat(frozen)
    if (merged.length > maxCache.value) {
      messages.value = merged.slice(merged.length - maxCache.value)
    } else {
      messages.value = merged
    }
  }

  function bindIpc() {
    window.api.consumer.onMessage((msgs) => {
      pendingBatch = pendingBatch.concat(msgs)
      if (rafId === null) {
        rafId = requestAnimationFrame(() => {
          rafId = null
          flushPending()
        })
      }
    })
    window.api.consumer.onStop(() => {
      if (rafId !== null) {
        cancelAnimationFrame(rafId)
        rafId = null
      }
      flushPending()
      running.value = false
      paused.value = false
    })
  }

  return { messages, running, paused, maxCache, ackMode, start, pause, resume, stop, ack, nack, bindIpc }
})