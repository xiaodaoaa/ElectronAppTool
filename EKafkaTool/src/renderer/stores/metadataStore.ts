import { defineStore } from 'pinia'
import { ref } from 'vue'
import { getKafkaApi } from '@/api/kafkaApi'
import type { TopicSummary, TopicDetail } from '../../main/kafka/types'

export const useMetadataStore = defineStore('metadata', () => {
  const topics = ref<TopicSummary[]>([])
  const topicDetails = ref<Map<string, TopicDetail>>(new Map())
  const loading = ref(false)
  let pollTimer: ReturnType<typeof setInterval> | null = null

  async function refreshTopics() {
    loading.value = true
    try {
      topics.value = await getKafkaApi().listTopics()
    } finally {
      loading.value = false
    }
  }

  async function fetchTopicDetail(topic: string) {
    const detail = await getKafkaApi().topicDetail(topic)
    topicDetails.value.set(topic, detail)
    return detail
  }

  function startPolling() {
    stopPolling()
    refreshTopics()
    pollTimer = setInterval(refreshTopics, 30000)
  }

  function stopPolling() {
    if (pollTimer) {
      clearInterval(pollTimer)
      pollTimer = null
    }
  }

  return {
    topics, topicDetails, loading,
    refreshTopics, fetchTopicDetail, startPolling, stopPolling,
  }
})