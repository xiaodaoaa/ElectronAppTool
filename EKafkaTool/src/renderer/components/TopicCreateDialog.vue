<template>
  <el-dialog
    :model-value="visible"
    title="创建 Topic"
    width="450px"
    @update:model-value="$emit('update:visible', $event)"
  >
    <el-form ref="formRef" :model="form" :rules="rules" label-width="100px">
      <el-form-item label="Topic 名称" prop="name">
        <el-input v-model="form.name" placeholder="如：demo-topic" />
      </el-form-item>
      <el-form-item label="分区数" prop="numPartitions">
        <el-input-number v-model="form.numPartitions" :min="1" :max="100" />
      </el-form-item>
      <el-form-item label="副本因子" prop="replicationFactor">
        <el-input-number v-model="form.replicationFactor" :min="1" :max="10" />
        <el-text type="warning" size="small" style="margin-left: 8px">
          单机环境请设为 1
        </el-text>
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button @click="$emit('update:visible', false)">取消</el-button>
      <el-button type="primary" @click="handleCreate" :loading="creating">创建</el-button>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, reactive, watch } from 'vue'
import { ElMessage } from 'element-plus'
import type { FormInstance, FormRules } from 'element-plus'
import { getKafkaApi } from '@/api/kafkaApi'

const props = defineProps<{ visible: boolean }>()
const emit = defineEmits<{
  'update:visible': [value: boolean]
  created: []
}>()

const formRef = ref<FormInstance>()
const creating = ref(false)

const form = reactive({
  name: '',
  numPartitions: 3,
  replicationFactor: 1,
})

const rules: FormRules = {
  name: [{ required: true, message: '请输入 Topic 名称', trigger: 'blur' }],
  numPartitions: [{ required: true, message: '请输入分区数', trigger: 'blur' }],
  replicationFactor: [{ required: true, message: '请输入副本因子', trigger: 'blur' }],
}

watch(() => props.visible, (val) => {
  if (val) {
    form.name = ''
    form.numPartitions = 3
    form.replicationFactor = 1
    formRef.value?.resetFields()
  }
})

async function handleCreate() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return

  creating.value = true
  try {
    await getKafkaApi().createTopic({
      name: form.name,
      numPartitions: form.numPartitions,
      replicationFactor: form.replicationFactor,
    })
    ElMessage.success(`Topic "${form.name}" 已创建`)
    emit('created')
    emit('update:visible', false)
  } catch (err: unknown) {
    ElMessage.error(`创建失败: ${err instanceof Error ? err.message : String(err)}`)
  } finally {
    creating.value = false
  }
}
</script>