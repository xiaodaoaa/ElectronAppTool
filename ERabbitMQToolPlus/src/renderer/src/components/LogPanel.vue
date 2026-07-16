<template>
  <div class="log-panel">
    <div class="log-header">
      <span>日志</span>
      <div>
        <el-button size="small" @click="logStore.clear()">清空</el-button>
        <el-button size="small" @click="logStore.toggle()">收起</el-button>
      </div>
    </div>
    <div class="log-list" ref="logListRef">
      <div
        v-for="(entry, i) in logStore.entries"
        :key="i"
        :class="['log-entry', `level-${entry.level.toLowerCase()}`]"
      >
        <span class="log-time">{{ entry.time }}</span>
        <span class="log-level">{{ entry.level }}</span>
        <span class="log-msg">{{ entry.message }}</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, nextTick } from 'vue'
import { useLogStore } from '../stores/log'

const logStore = useLogStore()
const logListRef = ref<HTMLElement | null>(null)

watch(() => logStore.entries.length, async () => {
  await nextTick()
  if (logListRef.value) {
    logListRef.value.scrollTop = logListRef.value.scrollHeight
  }
})
</script>

<style scoped>
.log-panel { height: 200px; display: flex; flex-direction: column; background: var(--el-bg-color); }
.log-header { display: flex; justify-content: space-between; align-items: center; padding: 4px 8px; border-bottom: 1px solid var(--el-border-color); }
.log-list { flex: 1; overflow-y: auto; padding: 4px 8px; font-family: monospace; font-size: 12px; }
.log-entry { padding: 2px 0; }
.log-time { color: var(--el-text-color-secondary); margin-right: 8px; }
.log-level { margin-right: 8px; font-weight: bold; }
.level-info .log-level { color: var(--el-color-info); }
.level-warn .log-level { color: var(--el-color-warning); }
.level-error .log-level { color: var(--el-color-danger); }
</style>
