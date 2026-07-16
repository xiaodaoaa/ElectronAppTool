import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { LogEntry } from '../../../shared/types'

export const useLogStore = defineStore('log', () => {
  const entries = ref<LogEntry[]>([])
  const visible = ref(false)

  function add(entry: LogEntry) {
    entries.value.push(entry)
    if (entries.value.length > 500) {
      entries.value = entries.value.slice(entries.value.length - 500)
    }
  }

  function toggle() {
    visible.value = !visible.value
  }

  function clear() {
    entries.value = []
  }

  function bindIpc() {
    window.api.log.onLog((entry) => add(entry))
  }

  return { entries, visible, add, toggle, clear, bindIpc }
})
