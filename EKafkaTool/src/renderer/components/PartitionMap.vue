<template>
  <div class="partition-map">
    <div class="map-title">分区分配图</div>
    <div class="map-canvas" ref="canvasRef">
      <div class="partitions-row">
        <div
          v-for="p in partitions"
          :key="p"
          class="partition-block"
          :style="{ backgroundColor: partitionColor(p) }"
        >
          P{{ p }}
        </div>
      </div>
      <svg class="connections-svg" :viewBox="`0 0 ${svgWidth} ${svgHeight}`">
        <line
          v-for="(conn, idx) in connections"
          :key="idx"
          :x1="conn.x1"
          :y1="conn.y1"
          :x2="conn.x2"
          :y2="conn.y2"
          :stroke="partitionColor(conn.partition)"
          stroke-width="3"
          stroke-linecap="round"
        />
      </svg>
      <div class="consumers-row">
        <div
          v-for="c in consumers"
          :key="c.instanceId"
          class="consumer-block"
          :class="{ active: c.status === 'running' }"
        >
          <div class="consumer-name">{{ c.alias }}</div>
          <div class="consumer-partitions">
            <span
              v-for="p in getConsumerPartitions(c)"
              :key="p"
              class="consumer-partition-dot"
              :style="{ backgroundColor: partitionColor(p) }"
            >P{{ p }}</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import type { ConsumerInstanceState } from '../../main/kafka/types'

const props = defineProps<{
  partitions: number
  consumers: ConsumerInstanceState[]
}>()

const canvasRef = ref<HTMLElement>()

const PARTITION_COLORS = [
  '#409EFF', '#67C23A', '#E6A23C', '#F56C6C', '#909399',
  '#337ECC', '#529B2E', '#B88230', '#C45656', '#73767A',
]

function partitionColor(p: number): string {
  return PARTITION_COLORS[p % PARTITION_COLORS.length]
}

function getConsumerPartitions(c: ConsumerInstanceState): number[] {
  const parts: number[] = []
  for (const assign of c.assignments) {
    parts.push(...assign.partitions)
  }
  return parts.sort((a, b) => a - b)
}

const svgWidth = computed(() => Math.max(props.partitions * 80, 200))
const svgHeight = computed(() => Math.max(props.consumers.length * 60, 60))

const connections = computed(() => {
  const result: Array<{ x1: number; y1: number; x2: number; y2: number; partition: number }> = []
  const blockWidth = 60
  const gap = 20
  const partitionY = 30
  const consumerStartY = 50

  props.consumers.forEach((c, ci) => {
    const parts = getConsumerPartitions(c)
    parts.forEach(p => {
      result.push({
        x1: p * (blockWidth + gap) + blockWidth / 2,
        y1: partitionY,
        x2: ci * 140 + 70,
        y2: consumerStartY + ci * 60,
        partition: p,
      })
    })
  })
  return result
})
</script>

<style scoped>
.partition-map {
  padding: 12px;
}
.map-title {
  font-weight: 600;
  margin-bottom: 12px;
}
.map-canvas {
  position: relative;
}
.partitions-row {
  display: flex;
  gap: 12px;
  justify-content: center;
  margin-bottom: 8px;
}
.partition-block {
  width: 56px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  font-size: 12px;
  font-weight: 600;
  border-radius: 4px;
}
.connections-svg {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  pointer-events: none;
}
.consumers-row {
  display: flex;
  gap: 16px;
  justify-content: center;
  margin-top: 16px;
}
.consumer-block {
  width: 120px;
  padding: 8px;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  text-align: center;
  transition: border-color 0.3s;
}
.consumer-block.active {
  border-color: var(--el-color-success);
}
.consumer-name {
  font-size: 13px;
  font-weight: 600;
  margin-bottom: 4px;
}
.consumer-partitions {
  display: flex;
  gap: 4px;
  justify-content: center;
  flex-wrap: wrap;
}
.consumer-partition-dot {
  font-size: 11px;
  color: #fff;
  padding: 1px 6px;
  border-radius: 3px;
}
</style>