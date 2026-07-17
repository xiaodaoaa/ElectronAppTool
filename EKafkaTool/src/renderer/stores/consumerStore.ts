import { defineStore } from 'pinia'
import { ref } from 'vue'
import { getKafkaApi } from '@/api/kafkaApi'
import type { ConsumerOptions, ConsumerInstanceState, MessageRecord, RebalanceEvent } from '../../main/kafka/types'

const MAX_MESSAGES = 5000

export const useConsumerStore = defineStore('consumer', () => {
  const instances = ref<ConsumerInstanceState[]>([])
  const messages = ref<MessageRecord[]>([])
  const rebalanceEvents = ref<RebalanceEvent[]>([])
  const messagePaused = ref(false)
  let unsubs: (() => void)[] = []

  console.log('[consumerStore] store 初始化')

  async function createInstance(opts: ConsumerOptions): Promise<string> {
    const id = await getKafkaApi().createConsumer(opts)
    await refreshInstances()
    return id
  }

  async function startInstance(id: string) {
    await getKafkaApi().startConsumer(id)
    await refreshInstances()
  }

  async function stopInstance(id: string) {
    await getKafkaApi().stopConsumer(id)
    await refreshInstances()
  }

  async function pauseInstance(id: string) {
    await getKafkaApi().pauseConsumer(id)
    await refreshInstances()
  }

  async function resumeInstance(id: string) {
    await getKafkaApi().resumeConsumer(id)
    await refreshInstances()
  }

  async function seekInstance(instanceId: string, topic: string, partition: number, offset: string) {
    await getKafkaApi().seekConsumer({ instanceId, topic, partition, offset })
  }

  async function commitInstance(id: string) {
    await getKafkaApi().commitConsumer(id)
  }

  async function refreshInstances() {
    instances.value = await getKafkaApi().listConsumerInstances()
  }

  function clearMessages() {
    messages.value = []
  }

  function toggleMessagePause() {
    messagePaused.value = !messagePaused.value
  }

  function setupListeners() {
    console.log('[consumerStore] setupListeners 调用, 当前 unsubs 数量:', unsubs.length)
    teardownListeners()

    unsubs.push(getKafkaApi().onConsumerMessage((batch) => {
      console.log('[consumerStore] onConsumerMessage 回调:', batch.length, '条, paused:', messagePaused.value)
      if (batch.length > 0) {
        const countMap = new Map<string, number>()
        for (const m of batch) {
          countMap.set(m.instanceId, (countMap.get(m.instanceId) ?? 0) + 1)
        }
        for (const [instId, cnt] of countMap) {
          const idx = instances.value.findIndex(i => i.instanceId === instId)
          if (idx >= 0) {
            instances.value[idx] = { ...instances.value[idx], consumedCount: instances.value[idx].consumedCount + cnt }
          }
        }
      }
      if (messagePaused.value) return
      messages.value = [...batch, ...messages.value].slice(0, MAX_MESSAGES)
      console.log('[consumerStore] messages 更新后:', messages.value.length, '条')
    }))

    unsubs.push(getKafkaApi().onConsumerState((state) => {
      console.log('[consumerStore] onConsumerState 回调:', state.alias, state.status)
      const idx = instances.value.findIndex(i => i.instanceId === state.instanceId)
      if (idx >= 0) {
        instances.value[idx] = state
      } else {
        instances.value.push(state)
      }
    }))

    unsubs.push(getKafkaApi().onRebalance((event) => {
      console.log('[consumerStore] onRebalance 回调:', event.groupId)
      rebalanceEvents.value = [event, ...rebalanceEvents.value].slice(0, 100)
    }))

    console.log('[consumerStore] setupListeners 完成, unsubs 数量:', unsubs.length)
  }

  function teardownListeners() {
    console.log('[consumerStore] teardownListeners 调用, unsubs 数量:', unsubs.length)
    for (const unsub of unsubs) {
      unsub()
    }
    unsubs = []
  }

  return {
    instances, messages, rebalanceEvents, messagePaused,
    createInstance, startInstance, stopInstance, pauseInstance, resumeInstance,
    seekInstance, commitInstance, refreshInstances, clearMessages, toggleMessagePause,
    setupListeners, teardownListeners,
  }
})