import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { ConsumedMessage, ConsumerConfig, IpcResult } from '../../../shared/types'

export const useConsumerStore = defineStore('consumer', () => {
  const messages = ref<ConsumedMessage[]>([])
  const running = ref(false)
  const paused = ref(false)
  const maxCache = ref(1000)
  const autoAck = ref(true)

  async function start(config: ConsumerConfig): Promise<IpcResult> {
    const r = await window.api.consumer.start(config)
    if (r.success) {
      running.value = true
      paused.value = false
      autoAck.value = config.autoAck
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

  async function pull(count: number) {
    return await window.api.consumer.pull(count)
  }

  async function ack(deliveryTag: number) {
    const r = await window.api.consumer.ack(deliveryTag)
    if (r.success) {
      messages.value = messages.value.filter((m) => m.deliveryTag !== deliveryTag)
    }
    return r
  }

  async function nack(deliveryTag: number, requeue: boolean) {
    const r = await window.api.consumer.nack(deliveryTag, requeue)
    if (r.success) {
      messages.value = messages.value.filter((m) => m.deliveryTag !== deliveryTag)
    }
    return r
  }

  function bindIpc() {
    window.api.consumer.onMessage((msg) => {
      messages.value.push(msg)
      if (messages.value.length > maxCache.value) {
        messages.value = messages.value.slice(messages.value.length - maxCache.value)
      }
    })
    window.api.consumer.onStop(() => {
      running.value = false
      paused.value = false
    })
  }

  return { messages, running, paused, maxCache, autoAck, start, pause, resume, stop, pull, ack, nack, bindIpc }
})
