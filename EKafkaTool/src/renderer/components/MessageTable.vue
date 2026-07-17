<template>
  <div class="message-table-container">
    <div class="table-toolbar">
      <div class="toolbar-left">
        <el-select :model-value="filterInstance" @update:model-value="$emit('update:filterInstance', $event)" placeholder="全部实例" clearable size="small" style="width: 140px">
          <el-option
            v-for="inst in instances"
            :key="inst.instanceId"
            :label="inst.alias"
            :value="inst.instanceId"
          />
        </el-select>
        <el-select :model-value="filterPartition" @update:model-value="$emit('update:filterPartition', $event)" placeholder="全部分区" clearable size="small" style="width: 120px; margin-left: 8px">
          <el-option
            v-for="p in allPartitions"
            :key="p"
            :label="`P${p}`"
            :value="p"
          />
        </el-select>
      </div>
      <div class="toolbar-right">
        <el-button size="small" @click="$emit('togglePause')">
          {{ paused ? '▶ 继续' : '⏸ 暂停' }}
        </el-button>
        <el-button size="small" @click="$emit('clear')">清空</el-button>
        <el-button size="small" @click="$emit('export', 'json')">导出 JSON</el-button>
        <el-button size="small" @click="$emit('export', 'csv')">导出 CSV</el-button>
      </div>
    </div>

    <el-auto-resizer>
      <template #default="{ height, width }">
        <el-table-v2
          :columns="columns"
          :data="filteredMessages"
          :width="width"
          :height="height"
          :row-height="36"
          fixed
        />
      </template>
    </el-auto-resizer>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { Column } from 'element-plus'
import type { MessageRecord, ConsumerInstanceState } from '../../main/kafka/types'

const props = defineProps<{
  messages: MessageRecord[]
  instances: ConsumerInstanceState[]
  filterInstance: string | null
  filterPartition: number | null
  paused: boolean
}>()

defineEmits<{
  togglePause: []
  clear: []
  export: [format: 'json' | 'csv']
  'update:filterInstance': [value: string | null]
  'update:filterPartition': [value: number | null]
}>()

const allPartitions = computed(() => {
  const set = new Set<number>()
  for (const m of props.messages) set.add(m.partition)
  return Array.from(set).sort((a, b) => a - b)
})

const filteredMessages = computed(() => {
  let list = props.messages
  if (props.filterInstance) {
    list = list.filter(m => m.instanceId === props.filterInstance)
  }
  if (props.filterPartition !== null) {
    list = list.filter(m => m.partition === props.filterPartition)
  }
  return list
})

const columns: Column<MessageRecord>[] = [
  {
    key: 'receivedAt',
    title: '时间',
    dataKey: 'receivedAt',
    width: 200,
    cellRenderer: ({ cellData }) => {
      const d = new Date(cellData as number)
      const pad = (n: number) => String(n).padStart(2, '0')
      return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}.${String(d.getMilliseconds()).padStart(3, '0')}`
    },
  },
  {
    key: 'instanceId',
    title: '实例',
    dataKey: 'instanceId',
    width: 100,
    cellRenderer: ({ cellData, rowData }) => {
      const inst = props.instances.find(i => i.instanceId === rowData.instanceId)
      return inst?.alias ?? (cellData as string).slice(0, 8)
    },
  },
  {
    key: 'partition',
    title: '分区',
    dataKey: 'partition',
    width: 70,
    cellRenderer: ({ cellData }) => `P${cellData}`,
  },
  {
    key: 'offset',
    title: 'Offset',
    dataKey: 'offset',
    width: 100,
  },
  {
    key: 'key',
    title: 'Key',
    dataKey: 'key',
    width: 100,
    cellRenderer: ({ cellData }) => (cellData as string) ?? '-',
  },
  {
    key: 'value',
    title: 'Value',
    dataKey: 'value',
    width: 300,
    cellRenderer: ({ cellData }) => {
      const v = cellData as string
      return v.length > 80 ? v.slice(0, 80) + '...' : v
    },
  },
]
</script>

<style scoped>
.message-table-container {
  display: flex;
  flex-direction: column;
  height: 400px;
}
.table-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
  flex-shrink: 0;
}
.toolbar-left, .toolbar-right {
  display: flex;
  align-items: center;
}
</style>