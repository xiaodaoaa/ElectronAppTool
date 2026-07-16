<template>
  <el-dialog :model-value="visible" @update:model-value="$emit('update:visible', $event)" title="设置" width="480px">
    <el-form label-width="160px" size="small">
      <el-form-item label="主题">
        <el-radio-group :model-value="settingsStore.theme" @update:model-value="settingsStore.setTheme($event)">
          <el-radio label="light">浅色</el-radio>
          <el-radio label="dark">深色</el-radio>
        </el-radio-group>
      </el-form-item>
      <el-form-item label="最大消息缓存数">
        <el-input-number v-model="settingsStore.maxMessageCache" :min="100" :step="100" controls-position="right" />
      </el-form-item>
      <el-form-item label="消息体最大显示长度">
        <el-input-number v-model="settingsStore.maxDisplayLength" :min="100" :step="100" controls-position="right" />
      </el-form-item>
    </el-form>
  </el-dialog>
</template>

<script setup lang="ts">
import { useSettingsStore } from '../stores/settings'

defineProps<{ visible: boolean }>()
defineEmits<{ 'update:visible': [boolean] }>()

const settingsStore = useSettingsStore()
</script>