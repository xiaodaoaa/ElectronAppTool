<template>
  <el-container id="app-root">
    <el-header class="app-header">
      <div class="header-left">
        <span class="app-title">KafkaTeach</span>
      </div>
      <div class="header-right">
        <el-select
          v-model="selectedConnId"
          placeholder="选择连接"
          :disabled="!connectionStore.configs.length"
          @change="onConnSelect"
          style="width: 260px"
        >
          <el-option
            v-for="c in connectionStore.configs"
            :key="c.id"
            :label="c.name"
            :value="c.id"
          />
        </el-select>
        <el-tag
          :type="statusTagType"
          size="small"
          style="margin-left: 8px"
        >
          {{ statusText }}
        </el-tag>
      </div>
    </el-header>
    <el-container>
      <el-aside width="180px" class="app-aside">
        <el-menu
          :default-active="activeMenu"
          router
          @select="onMenuSelect"
        >
          <el-menu-item index="/connection">
            <el-icon><Link /></el-icon>
            <span>连接管理</span>
          </el-menu-item>
          <el-menu-item index="/cluster" :disabled="!connectionStore.isConnected">
            <el-icon><DataBoard /></el-icon>
            <span>集群概览</span>
          </el-menu-item>
          <el-menu-item index="/producer" :disabled="!connectionStore.isConnected">
            <el-icon><Upload /></el-icon>
            <span>生产者</span>
          </el-menu-item>
          <el-menu-item index="/consumer" :disabled="!connectionStore.isConnected">
            <el-icon><Download /></el-icon>
            <span>消费者</span>
          </el-menu-item>
          <el-menu-item index="/visual" :disabled="!connectionStore.isConnected">
            <el-icon><PieChart /></el-icon>
            <span>可视化</span>
          </el-menu-item>
          <el-menu-item index="/scenario" :disabled="!connectionStore.isConnected">
            <el-icon><VideoPlay /></el-icon>
            <span>演示场景</span>
          </el-menu-item>
          <el-menu-item index="/settings">
            <el-icon><Setting /></el-icon>
            <span>设置</span>
          </el-menu-item>
        </el-menu>
      </el-aside>
      <el-main class="app-main">
        <router-view />
      </el-main>
    </el-container>
  </el-container>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRoute } from 'vue-router'
import { Link, DataBoard, Upload, Download, PieChart, VideoPlay, Setting } from '@element-plus/icons-vue'
import { useConnectionStore } from '@/stores/connectionStore'
import { useConsumerStore } from '@/stores/consumerStore'
import { useProducerStore } from '@/stores/producerStore'

const route = useRoute()
const connectionStore = useConnectionStore()
const consumerStore = useConsumerStore()
const producerStore = useProducerStore()

const selectedConnId = computed({
  get: () => connectionStore.activeId,
  set: () => {},
})
const activeMenu = ref(route.path)

const statusTagType = computed(() => {
  const map: Record<string, string> = {
    connected: 'success',
    connecting: 'warning',
    error: 'danger',
    disconnected: 'info',
  }
  return map[connectionStore.status] ?? 'info'
})

const statusText = computed(() => {
  const map: Record<string, string> = {
    connected: '已连接',
    connecting: '连接中...',
    error: '连接失败',
    disconnected: '未连接',
  }
  return map[connectionStore.status] ?? '未连接'
})

function onMenuSelect(index: string) {
  activeMenu.value = index
}

async function onConnSelect(id: string) {
  try {
    await connectionStore.connect(id)
  } catch {
    selectedConnId.value = null
  }
}

onMounted(async () => {
  connectionStore.setupStatusListener()
  consumerStore.setupListeners()
  producerStore.setupAckListener()
  await connectionStore.loadConfigs()
})

onUnmounted(() => {
  connectionStore.teardownStatusListener?.()
  consumerStore.teardownListeners()
  producerStore.teardownAckListener()
})
</script>

<style>
html, body, #app {
  margin: 0;
  padding: 0;
  height: 100%;
}
#app-root {
  height: 100vh;
}
.app-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  border-bottom: 1px solid var(--el-border-color-light);
  background: var(--el-bg-color);
}
.header-left .app-title {
  font-size: 18px;
  font-weight: 600;
  color: var(--el-color-primary);
}
.header-right {
  display: flex;
  align-items: center;
}
.app-aside {
  border-right: 1px solid var(--el-border-color-light);
}
.app-main {
  background: var(--el-bg-color-page);
  padding: 16px;
  overflow-y: auto;
}
</style>