import { defineStore } from 'pinia'
import { ref } from 'vue'

export const useSettingsStore = defineStore('settings', () => {
  const theme = ref<'light' | 'dark'>('light')
  const maxMessageCache = ref(1000)
  const maxDisplayLength = ref(1000)

  function setTheme(t: 'light' | 'dark') {
    theme.value = t
    document.documentElement.classList.toggle('dark', t === 'dark')
  }

  return { theme, maxMessageCache, maxDisplayLength, setTheme }
})
