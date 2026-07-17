<template>
  <div class="scenario-view">
    <div class="view-header">
      <h2>演示场景</h2>
    </div>

    <div class="scenario-cards">
      <el-card
        v-for="s in scenarioStore.scenarios"
        :key="s.id"
        class="scenario-card"
        :class="{ running: scenarioStore.runningId !== null }"
      >
        <div class="card-title">{{ s.title }}</div>
        <div class="card-desc">{{ s.description }}</div>
        <div class="card-meta">
          <el-tag size="small" type="info">{{ s.duration }}</el-tag>
          <el-tag size="small" style="margin-left: 8px">{{ s.id }}</el-tag>
        </div>
        <el-button
          type="primary"
          style="margin-top: 12px; width: 100%"
          :disabled="scenarioStore.runningId !== null"
          @click="handleRun(s.id)"
        >
          <el-icon><VideoPlay /></el-icon> 运行
        </el-button>
      </el-card>
    </div>

    <div v-if="scenarioStore.runningId" class="subtitle-bar">
      <div class="subtitle-content">
        <el-icon class="subtitle-icon"><Microphone /></el-icon>
        <span>{{ scenarioStore.currentStep?.message ?? '场景运行中...' }}</span>
      </div>
      <el-button type="danger" size="small" @click="handleStop">停止场景</el-button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { VideoPlay, Microphone } from '@element-plus/icons-vue'
import { useScenarioStore } from '@/stores/scenarioStore'

const scenarioStore = useScenarioStore()

async function handleRun(id: string) {
  try {
    await scenarioStore.runScenario(id)
    ElMessage.success('场景已启动')
  } catch (err: unknown) {
    ElMessage.error(`启动失败: ${err instanceof Error ? err.message : String(err)}`)
  }
}

async function handleStop() {
  await scenarioStore.stopScenario()
  ElMessage.info('场景已停止')
}

onMounted(async () => {
  scenarioStore.setupListener()
  await scenarioStore.loadScenarios()
})
</script>

<style scoped>
.scenario-view {
  max-width: 1000px;
}
.view-header {
  margin-bottom: 16px;
}
.view-header h2 {
  margin: 0;
}
.scenario-cards {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 16px;
}
.scenario-card {
  transition: opacity 0.3s;
}
.scenario-card.running {
  opacity: 0.6;
}
.card-title {
  font-size: 16px;
  font-weight: 600;
  margin-bottom: 8px;
}
.card-desc {
  font-size: 13px;
  color: var(--el-text-color-secondary);
  margin-bottom: 8px;
  line-height: 1.5;
}
.card-meta {
  display: flex;
  align-items: center;
}
.subtitle-bar {
  position: fixed;
  bottom: 0;
  left: 180px;
  right: 0;
  background: var(--el-color-primary-light-9);
  border-top: 2px solid var(--el-color-primary);
  padding: 12px 24px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  z-index: 100;
}
.subtitle-content {
  display: flex;
  align-items: center;
  font-size: 15px;
  color: var(--el-color-primary-dark-2);
}
.subtitle-icon {
  margin-right: 8px;
  font-size: 18px;
}
</style>