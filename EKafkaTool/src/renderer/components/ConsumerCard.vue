<template>
  <el-card class="consumer-card" :class="statusClass">
    <template #header>
      <div class="card-header">
        <span class="card-alias">{{ instance.alias }}</span>
        <el-tag :type="statusTagType" size="small">{{ statusText }}</el-tag>
      </div>
    </template>

    <div class="card-body">
      <div class="card-info">
        <div>Group: {{ instance.groupId }}</div>
        <div>Topic: {{ instance.topics.join(', ') }}</div>
        <div>已消费: {{ instance.consumedCount }} 条</div>
        <div v-if="instance.error" class="error-text">{{ instance.error }}</div>
      </div>

      <div class="card-partitions">
        <div v-if="instance.assignments.length === 0" class="no-assign">未分配分区</div>
        <div v-for="assign in instance.assignments" :key="assign.topic" class="partition-group">
          <span class="topic-label">{{ assign.topic }}:</span>
          <el-tag
            v-for="p in assign.partitions"
            :key="p"
            :color="partitionColor(p)"
            size="small"
            class="partition-tag"
            style="color: #fff; border: none"
          >
            P{{ p }}
          </el-tag>
        </div>
      </div>
    </div>

    <div class="card-actions">
      <el-button
        v-if="instance.status === 'created' || instance.status === 'stopped'"
        type="primary"
        size="small"
        @click="$emit('start', instance.instanceId)"
      >
        启动
      </el-button>
      <el-button
        v-if="instance.status === 'running'"
        size="small"
        @click="$emit('pause', instance.instanceId)"
      >
        暂停
      </el-button>
      <el-button
        v-if="instance.status === 'paused'"
        type="success"
        size="small"
        @click="$emit('resume', instance.instanceId)"
      >
        继续
      </el-button>
      <el-button
        v-if="instance.status === 'running' || instance.status === 'paused'"
        type="danger"
        size="small"
        @click="$emit('stop', instance.instanceId)"
      >
        停止
      </el-button>
      <el-dropdown v-if="instance.status !== 'stopped'" trigger="click" style="margin-left: 4px">
        <el-button size="small">
          更多<el-icon class="el-icon--right"><ArrowDown /></el-icon>
        </el-button>
        <template #dropdown>
          <el-dropdown-menu>
            <el-dropdown-item @click="$emit('seekToBeginning', instance.instanceId)">
              重置到开头
            </el-dropdown-item>
            <el-dropdown-item @click="$emit('seekToEnd', instance.instanceId)">
              重置到结尾
            </el-dropdown-item>
            <el-dropdown-item @click="$emit('commit', instance.instanceId)">
              手动提交
            </el-dropdown-item>
          </el-dropdown-menu>
        </template>
      </el-dropdown>
    </div>
  </el-card>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { ArrowDown } from '@element-plus/icons-vue'
import type { ConsumerInstanceState } from '../../main/kafka/types'

const props = defineProps<{
  instance: ConsumerInstanceState
}>()

defineEmits<{
  start: [id: string]
  stop: [id: string]
  pause: [id: string]
  resume: [id: string]
  seekToBeginning: [id: string]
  seekToEnd: [id: string]
  commit: [id: string]
}>()

const PARTITION_COLORS = [
  '#409EFF', '#67C23A', '#E6A23C', '#F56C6C', '#909399',
  '#337ECC', '#529B2E', '#B88230', '#C45656', '#73767A',
]

function partitionColor(partition: number): string {
  return PARTITION_COLORS[partition % PARTITION_COLORS.length]
}

const statusClass = computed(() => `status-${props.instance.status}`)
const statusTagType = computed(() => {
  const map: Record<string, string> = {
    created: 'info',
    running: 'success',
    paused: 'warning',
    stopped: 'info',
    error: 'danger',
  }
  return map[props.instance.status] ?? 'info'
})
const statusText = computed(() => {
  const map: Record<string, string> = {
    created: '已创建',
    running: '运行中',
    paused: '已暂停',
    stopped: '已停止',
    error: '错误',
  }
  return map[props.instance.status] ?? props.instance.status
})
</script>

<style scoped>
.consumer-card {
  width: 280px;
  flex-shrink: 0;
}
.consumer-card.status-error {
  border-color: var(--el-color-danger);
}
.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.card-alias {
  font-weight: 600;
}
.card-body {
  font-size: 13px;
  line-height: 1.8;
}
.card-partitions {
  margin-top: 8px;
}
.no-assign {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}
.partition-group {
  margin-top: 4px;
}
.topic-label {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  margin-right: 4px;
}
.partition-tag {
  margin-right: 4px;
  margin-bottom: 2px;
}
.error-text {
  color: var(--el-color-danger);
  font-size: 12px;
}
.card-actions {
  margin-top: 12px;
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}
</style>