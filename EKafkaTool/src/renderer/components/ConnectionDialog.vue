<template>
  <el-dialog
    :model-value="visible"
    :title="isEdit ? '编辑连接' : '新增连接'"
    width="560px"
    @update:model-value="$emit('update:visible', $event)"
    @close="resetForm"
  >
    <el-form
      ref="formRef"
      :model="form"
      :rules="rules"
      label-width="130px"
    >
      <el-form-item label="名称" prop="name">
        <el-input v-model="form.name" placeholder="如：本地教学环境" />
      </el-form-item>
      <el-form-item label="Bootstrap Servers" prop="brokers">
        <el-input v-model="form.brokersStr" placeholder="localhost:9092,host2:9092" />
      </el-form-item>
      <el-form-item label="Client ID" prop="clientId">
        <el-input v-model="form.clientId" placeholder="kafka-teach" />
      </el-form-item>
      <el-form-item label="SASL 机制">
        <el-select v-model="form.saslMechanism" placeholder="无" clearable style="width: 100%">
          <el-option label="无" value="" />
          <el-option label="PLAIN" value="plain" />
          <el-option label="SCRAM-SHA-256" value="scram-sha-256" />
          <el-option label="SCRAM-SHA-512" value="scram-sha-512" />
        </el-select>
      </el-form-item>
      <template v-if="form.saslMechanism">
        <el-form-item label="用户名">
          <el-input v-model="form.saslUsername" />
        </el-form-item>
        <el-form-item label="密码">
          <el-input v-model="form.saslPassword" type="password" show-password />
        </el-form-item>
      </template>
      <el-form-item label="SSL">
        <el-switch v-model="form.sslEnabled" />
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button @click="$emit('update:visible', false)">取消</el-button>
      <el-button type="primary" @click="handleSave" :loading="saving">保存</el-button>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch } from 'vue'
import { ElMessage } from 'element-plus'
import type { FormInstance, FormRules } from 'element-plus'
import { useConnectionStore } from '@/stores/connectionStore'
import type { ConnectionConfig } from '../../main/kafka/types'

const props = defineProps<{
  visible: boolean
  editConfig: ConnectionConfig | null
}>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  saved: []
}>()

const connectionStore = useConnectionStore()
const formRef = ref<FormInstance>()
const saving = ref(false)

const isEdit = computed(() => !!props.editConfig)

const form = reactive({
  name: '',
  brokersStr: '',
  clientId: 'kafka-teach',
  saslMechanism: '' as string,
  saslUsername: '',
  saslPassword: '',
  sslEnabled: false,
})

const rules: FormRules = {
  name: [{ required: true, message: '请输入名称', trigger: 'blur' }],
  brokersStr: [{ required: true, message: '请输入 Bootstrap Servers', trigger: 'blur' }],
  clientId: [{ required: true, message: '请输入 Client ID', trigger: 'blur' }],
}

watch(() => props.visible, (val) => {
  if (val) {
    if (props.editConfig) {
      form.name = props.editConfig.name
      form.brokersStr = props.editConfig.brokers.join(',')
      form.clientId = props.editConfig.clientId
      form.saslMechanism = props.editConfig.sasl?.mechanism ?? ''
      form.saslUsername = props.editConfig.sasl?.username ?? ''
      form.saslPassword = props.editConfig.sasl?.password ?? ''
      form.sslEnabled = props.editConfig.ssl?.enabled ?? false
    } else {
      resetForm()
    }
  }
})

function resetForm() {
  form.name = ''
  form.brokersStr = ''
  form.clientId = 'kafka-teach'
  form.saslMechanism = ''
  form.saslUsername = ''
  form.saslPassword = ''
  form.sslEnabled = false
  formRef.value?.resetFields()
}

async function handleSave() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return

  saving.value = true
  try {
    const brokers = form.brokersStr.split(',').map(s => s.trim()).filter(Boolean)
    const config: ConnectionConfig = {
      id: props.editConfig?.id ?? crypto.randomUUID(),
      name: form.name,
      brokers,
      clientId: form.clientId,
      sasl: form.saslMechanism
        ? {
            mechanism: form.saslMechanism as 'plain' | 'scram-sha-256' | 'scram-sha-512',
            username: form.saslUsername,
            password: form.saslPassword,
          }
        : undefined,
      ssl: form.sslEnabled ? { enabled: true } : undefined,
    }
    await connectionStore.saveConfig(config)
    ElMessage.success(isEdit.value ? '已更新' : '已创建')
    emit('saved')
  } catch (err: unknown) {
    ElMessage.error(`保存失败: ${err instanceof Error ? err.message : String(err)}`)
  } finally {
    saving.value = false
  }
}
</script>