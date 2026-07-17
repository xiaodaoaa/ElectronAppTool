<template>
  <div class="message-flow">
    <div class="flow-title">消息流动画（演示模式）</div>
    <div class="flow-stage" ref="stageRef">
      <div class="flow-producer">
        <el-tag type="primary" size="small">Producer</el-tag>
      </div>
      <div class="flow-partitions">
        <div
          v-for="p in partitions"
          :key="p"
          class="flow-partition"
          :style="{ backgroundColor: partitionColor(p) }"
        >
          P{{ p }}
        </div>
      </div>
      <div class="flow-consumers">
        <div
          v-for="c in consumers"
          :key="c.instanceId"
          class="flow-consumer"
        >
          <el-tag :type="c.status === 'running' ? 'success' : 'info'" size="small">
            {{ c.alias }}
          </el-tag>
        </div>
      </div>
      <div
        v-for="bubble in bubbles"
        :key="bubble.id"
        class="flow-bubble"
        :style="bubbleStyle(bubble)"
      >
        ●
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import type { ConsumerInstanceState, ProduceResult } from '../../main/kafka/types'
import { getKafkaApi } from '@/api/kafkaApi'

const props = defineProps<{
  partitions: number
  consumers: ConsumerInstanceState[]
}>()

const PARTITION_COLORS = [
  '#409EFF', '#67C23A', '#E6A23C', '#F56C6C', '#909399',
]

function partitionColor(p: number): string {
  return PARTITION_COLORS[p % PARTITION_COLORS.length]
}

interface Bubble {
  id: number
  partition: number
  phase: 'toPartition' | 'toConsumer'
  progress: number
  consumerIdx: number
}

const bubbles = ref<Bubble[]>([])
let bubbleId = 0
let unsub: (() => void) | null = null
let animFrame: number | null = null

function bubbleStyle(b: Bubble) {
  const color = partitionColor(b.partition)
  const partitionX = 160 + b.partition * 80
  const partitionY = 50
  const consumerX = 160 + b.consumerIdx * 120
  const consumerY = 100

  let x: number, y: number
  if (b.phase === 'toPartition') {
    x = 40 + (partitionX - 40) * b.progress
    y = 20 + (partitionY - 20) * b.progress
  } else {
    x = partitionX + (consumerX - partitionX) * b.progress
    y = partitionY + (consumerY - partitionY) * b.progress
  }

  return {
    left: `${x}px`,
    top: `${y}px`,
    color,
    fontSize: '14px',
    transition: 'none',
  }
}

function animate() {
  for (const b of bubbles.value) {
    b.progress += 0.02
    if (b.progress >= 1) {
      if (b.phase === 'toPartition') {
        b.phase = 'toConsumer'
        b.progress = 0
      }
    }
  }
  bubbles.value = bubbles.value.filter(b => b.progress < 1 || b.phase === 'toConsumer')
  animFrame = requestAnimationFrame(animate)
}

function onProduce(result: ProduceResult) {
  const consumerIdx = Math.floor(Math.random() * Math.max(props.consumers.length, 1))
  bubbles.value.push({
    id: ++bubbleId,
    partition: result.partition,
    phase: 'toPartition',
    progress: 0,
    consumerIdx,
  })
  if (bubbles.value.length > 20) {
    bubbles.value = bubbles.value.slice(-20)
  }
}

onMounted(() => {
  unsub = getKafkaApi().onProduceAck(onProduce)
  animFrame = requestAnimationFrame(animate)
})

onUnmounted(() => {
  unsub?.()
  if (animFrame) cancelAnimationFrame(animFrame)
})
</script>

<style scoped>
.message-flow {
  padding: 12px;
}
.flow-title {
  font-weight: 600;
  margin-bottom: 12px;
}
.flow-stage {
  position: relative;
  height: 140px;
  border: 1px dashed var(--el-border-color);
  border-radius: 8px;
  overflow: hidden;
}
.flow-producer {
  position: absolute;
  left: 8px;
  top: 12px;
}
.flow-partitions {
  position: absolute;
  left: 120px;
  top: 40px;
  display: flex;
  gap: 12px;
}
.flow-partition {
  width: 56px;
  height: 28px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  font-size: 11px;
  font-weight: 600;
  border-radius: 4px;
}
.flow-consumers {
  position: absolute;
  left: 120px;
  top: 90px;
  display: flex;
  gap: 12px;
}
.flow-bubble {
  position: absolute;
  z-index: 10;
  pointer-events: none;
}
</style>