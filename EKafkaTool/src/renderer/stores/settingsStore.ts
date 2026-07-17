import { defineStore } from 'pinia'
import { ref, watch } from 'vue'

const THEME_KEY = 'kafka-tool-theme'

function applyTheme(t: 'light' | 'dark') {
  document.documentElement.classList.toggle('dark', t === 'dark')
  // 同步 Electron 原生标题栏
  try { window.kafkaApi.themeSet(t) } catch { /* preload 未就绪时忽略 */ }
}

function loadTheme(): 'light' | 'dark' {
  try {
    const saved = localStorage.getItem(THEME_KEY)
    if (saved === 'dark' || saved === 'light') return saved
  } catch { /* localStorage 不可用时忽略 */ }
  return 'light'
}

export const useSettingsStore = defineStore('settings', () => {
  const deleteTopicEnabled = ref(false)
  const messageBufferLimit = ref(5000)
  const theme = ref<'light' | 'dark'>(loadTheme())

  // 初始化时立即应用主题
  applyTheme(theme.value)

  // 监听变化，持久化 + 应用
  watch(theme, (t) => {
    applyTheme(t)
    try { localStorage.setItem(THEME_KEY, t) } catch { /* 忽略 */ }
  })

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