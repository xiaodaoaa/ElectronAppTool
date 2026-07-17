<template>
  <div class="cluster-view">
    <div class="view-header">
      <h2>集群概览</h2>
      <el-button @click="refresh" :loading="metadataStore.loading">
        <el-icon><Refresh /></el-icon> 刷新
      </el-button>
    </div>

    <el-card v-if="connectionStore.cluster" class="section-card">
      <template #header>Broker 列表</template>
      <el-table :data="connectionStore.cluster.brokers" style="width: 100%" size="small">
        <el-table-column prop="nodeId" label="Node ID" width="100" />
        <el-table-column label="地址" min-width="200">
          <template #default="{ row }">{{ row.host }}:{{ row.port }}</template>
        </el-table-column>
        <el-table-column label="Controller" width="100">
          <template #default="{ row }">
            <el-tag v-if="row.isController" type="success" size="small">Controller</el-tag>
            <span v-else>-</span>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <el-card class="section-card" style="margin-top: 16px">
      <template #header>
        <span>Topic 列表（{{ metadataStore.topics.length }}）</span>
      </template>
      <el-table
        :data="metadataStore.topics"
        style="width: 100%"
        size="small"
        @row-click="onTopicClick"
        highlight-current-row
      >
        <el-table-column prop="name" label="Topic 名称" min-width="200" />
        <el-table-column prop="partitionCount" label="分区数" width="100" />
        <el-table-column prop="replicationFactor" label="副本因子" width="100" />
        <el-table-column prop="totalMessages" label="消息总量" width="120" />
      </el-table>
    </el-card>

    <el-card v-if="selectedTopic" class="section-card" style="margin-top: 16px">
      <template #header>
        <span>分区详情：{{ selectedTopic }}</span>
      </template>
      <el-table :data="topicPartitions" style="width: 100%" size="small" v-loading="detailLoading">
        <el-table-column prop="partition" label="Partition" width="100" />
        <el-table-column prop="leader" label="Leader" width="80" />
        <el-table-column label="Replicas" min-width="150">
          <template #default="{ row }">[{{ row.replicas.join(', ') }}]</template>
        </el-table-column>
        <el-table-column label="ISR" min-width="150">
          <template #default="{ row }">[{{ row.isr.join(', ') }}]</template>
        </el-table-column>
        <el-table-column prop="earliestOffset" label="Earliest Offset" width="140" />
        <el-table-column prop="latestOffset" label="Latest Offset" width="140" />
      </el-table>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { Refresh } from '@element-plus/icons-vue'
import { useConnectionStore } from '@/stores/connectionStore'
import { useMetadataStore } from '@/stores/metadataStore'
import type { PartitionDetail } from '../../main/kafka/types'

const connectionStore = useConnectionStore()
const metadataStore = useMetadataStore()

const selectedTopic = ref<string | null>(null)
const topicPartitions = ref<PartitionDetail[]>([])
const detailLoading = ref(false)

async function refresh() {
  await metadataStore.refreshTopics()
}

async function onTopicClick(row: { name: string }) {
  selectedTopic.value = row.name
  detailLoading.value = true
  try {
    const detail = await metadataStore.fetchTopicDetail(row.name)
    topicPartitions.value = detail.partitions
  } finally {
    detailLoading.value = false
  }
}

onMounted(() => {
  metadataStore.startPolling()
})

onUnmounted(() => {
  metadataStore.stopPolling()
})
</script>

<style scoped>
.cluster-view {
  max-width: 1000px;
}
.view-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}
.view-header h2 {
  margin: 0;
}
.section-card {
  margin-bottom: 0;
}
</style>