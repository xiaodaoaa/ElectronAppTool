<template>
  <div class="connection-view">
    <div class="view-header">
      <h2>连接管理</h2>
      <el-button type="primary" @click="openDialog()">
        <el-icon><Plus /></el-icon> 新增连接
      </el-button>
    </div>

    <el-table :data="connectionStore.configs" style="width: 100%">
      <el-table-column prop="name" label="名称" min-width="150" />
      <el-table-column prop="brokers" label="Bootstrap Servers" min-width="200">
        <template #default="{ row }">
          {{ row.brokers.join(', ') }}
        </template>
      </el-table-column>
      <el-table-column prop="clientId" label="Client ID" min-width="120" />
      <el-table-column label="SASL" width="80">
        <template #default="{ row }">
          {{ row.sasl ? row.sasl.mechanism : '-' }}
        </template>
      </el-table-column>
      <el-table-column label="SSL" width="60">
        <template #default="{ row }">
          {{ row.ssl?.enabled ? '是' : '否' }}
        </template>
      </el-table-column>
      <el-table-column label="操作" width="340" fixed="right">
        <template #default="{ row }">
          <template v-if="connectionStore.activeId === row.id">
            <el-button size="small" type="warning" @click="handleDisconnect">断开</el-button>
          </template>
          <template v-else>
            <el-button size="small" :loading="testingId === row.id" @click="handleTest(row)">测试</el-button>
            <el-button size="small" type="primary" @click="handleConnect(row)">连接</el-button>
            <el-button size="small" @click="openDialog(row)">编辑</el-button>
            <el-button size="small" type="danger" @click="handleDelete(row)">删除</el-button>
          </template>
        </template>
      </el-table-column>
    </el-table>

    <el-empty v-if="!connectionStore.configs.length" description="暂无连接配置，请新增" />

    <ConnectionDialog
      v-model:visible="dialogVisible"
      :edit-config="editingConfig"
      @saved="onSaved"
    />
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus } from '@element-plus/icons-vue'
import { useConnectionStore } from '@/stores/connectionStore'
import type { ConnectionConfig } from '../../main/kafka/types'
import ConnectionDialog from '@/components/ConnectionDialog.vue'

const connectionStore = useConnectionStore()
const dialogVisible = ref(false)
const editingConfig = ref<ConnectionConfig | null>(null)
const testingId = ref<string | null>(null)

function openDialog(config?: ConnectionConfig) {
  editingConfig.value = config ?? null
  dialogVisible.value = true
}

function onSaved() {
  dialogVisible.value = false
  editingConfig.value = null
}

async function handleTest(config: ConnectionConfig) {
  testingId.value = config.id
  try {
    const result = await connectionStore.testConnection(config)
    if (result.success) {
      ElMessage.success(`连接成功！Broker 数: ${result.brokerCount}, Controller ID: ${result.controllerId}`)
    } else {
      ElMessage.error(`连接失败: ${result.error}`)
    }
  } catch (err: unknown) {
    ElMessage.error(`测试失败: ${err instanceof Error ? err.message : String(err)}`)
  } finally {
    testingId.value = null
  }
}

async function handleConnect(config: ConnectionConfig) {
  try {
    await connectionStore.connect(config.id)
    ElMessage.success(`已连接到 ${config.name}`)
  } catch (err: unknown) {
    ElMessage.error(`连接失败: ${err instanceof Error ? err.message : String(err)}`)
  }
}

async function handleDisconnect() {
  try {
    await connectionStore.disconnect()
    ElMessage.success('已断开连接')
  } catch (err: unknown) {
    ElMessage.error(`断开失败: ${err instanceof Error ? err.message : String(err)}`)
  }
}

async function handleDelete(config: ConnectionConfig) {
  try {
    await ElMessageBox.confirm(`确定删除连接 "${config.name}"？`, '确认删除', {
      type: 'warning',
      confirmButtonText: '删除',
      cancelButtonText: '取消',
    })
    await connectionStore.deleteConfig(config.id)
    ElMessage.success('已删除')
  } catch {
    // 取消
  }
}
</script>

<style scoped>
.connection-view {
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
</style>