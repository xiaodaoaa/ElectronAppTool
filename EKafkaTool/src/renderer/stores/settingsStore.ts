import { defineStore } from 'pinia'
import { ref } from 'vue'

export const useSettingsStore = defineStore('settings', () => {
  const deleteTopicEnabled = ref(false)
  const messageBufferLimit = ref(5000)
  const theme = ref<'light' | 'dark'>('light')

  function toggleDeleteTopic() {
    deleteTopicEnabled.value = !deleteTopicEnabled.value
  }

  function setMessageBufferLimit(limit: number) {
    messageBufferLimit.value = limit
  }

  function setTheme(t: 'light' | 'dark') {
    theme.value = t
  }

  return {
    deleteTopicEnabled, messageBufferLimit, theme,
    toggleDeleteTopic, setMessageBufferLimit, setTheme,
  }
})