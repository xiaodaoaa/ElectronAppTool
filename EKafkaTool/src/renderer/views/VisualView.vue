<template>
  <div class="visual-view">
    <el-card class="section-card">
      <PartitionMap
        :partitions="maxPartitions"
        :consumers="consumerStore.instances"
      />
    </el-card>

    <el-card class="section-card" style="margin-top: 16px">
      <MessageFlowCanvas
        :partitions="maxPartitions"
        :consumers="consumerStore.instances"
      />
    </el-card>

    <el-card class="section-card" style="margin-top: 16px">
      <template #header>分区消息量柱状图</template>
      <div class="bar-chart">
        <div
          v-for="bar in barData"
          :key="bar.partition"
          class="bar-item"
        >
          <div class="bar-label">P{{ bar.partition }}</div>
          <div class="bar-track">
            <div
              class="bar-fill"
              :style="{
                width: bar.percent + '%',
                backgroundColor: partitionColor(bar.partition),
              }"
            />
          </div>
          <div class="bar-value">{{ bar.count }}</div>
        </div>
        <el-empty v-if="!barData.length" description="暂无数据" :image-size="60" />
      </div>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, onMounted, onUnmounted } from 'vue'
import { useConsumerStore } from '@/stores/consumerStore'
import { useMetadataStore } from '@/stores/metadataStore'
import PartitionMap from '@/components/PartitionMap.vue'
import MessageFlowCanvas from '@/components/MessageFlowCanvas.vue'

const consumerStore = useConsumerStore()
const metadataStore = useMetadataStore()

const PARTITION_COLORS = [
  '#409EFF', '#67C23A', '#E6A23C', '#F56C6C', '#909399',
  '#337ECC', '#529B2E', '#B88230', '#C45656', '#73767A',
]

function partitionColor(p: number): string {
  return PARTITION_COLORS[p % PARTITION_COLORS.length]
}

const maxPartitions = computed(() => {
  let max = 0
  for (const t of metadataStore.topics) {
    if (t.partitionCount > max) max = t.partitionCount
  }
  return Math.max(max, 1)
})

interface BarItem {
  partition: number
  count: number
  percent: number
}

const barData = ref<BarItem[]>([])

function updateBarData() {
  const counts = new Map<number, number>()
  for (const m of consumerStore.messages.slice(0, 1000)) {
    counts.set(m.partition, (counts.get(m.partition) ?? 0) + 1)
  }
  const max = Math.max(1, ...Array.from(counts.values()))
  const items: BarItem[] = []
  for (const [p, c] of counts) {
    items.push({ partition: p, count: c, percent: Math.round((c / max) * 100) })
  }
  items.sort((a, b) => a.partition - b.partition)
  barData.value = items
}

let barTimer: ReturnType<typeof setInterval> | null = null

onMounted(() => {
  barTimer = setInterval(updateBarData, 2000)
})

onUnmounted(() => {
  if (barTimer) clearInterval(barTimer)
})
</script>

<style scoped>
.visual-view {
  max-width: 100%;
}
.section-card {
  margin-bottom: 0;
}
.bar-chart {
  padding: 8px 0;
}
.bar-item {
  display: flex;
  align-items: center;
  margin-bottom: 8px;
}
.bar-label {
  width: 40px;
  font-size: 13px;
  font-weight: 600;
}
.bar-track {
  flex: 1;
  height: 20px;
  background: var(--el-fill-color-light);
  border-radius: 4px;
  overflow: hidden;
  margin: 0 12px;
}
.bar-fill {
  height: 100%;
  border-radius: 4px;
  transition: width 0.5s ease;
}
.bar-value {
  width: 50px;
  font-size: 13px;
  text-align: right;
}
</style>