<template>
  <div class="settings-view">
    <h2>设置</h2>

    <el-card class="section-card">
      <template #header>安全设置</template>
      <el-form label-width="180px">
        <el-form-item label="允许删除 Topic">
          <el-switch v-model="settingsStore.deleteTopicEnabled" />
          <el-text type="warning" size="small" style="margin-left: 8px">
            开启后集群概览页将显示删除按钮，需二次确认
          </el-text>
        </el-form-item>
      </el-form>
    </el-card>

    <el-card class="section-card" style="margin-top: 16px">
      <template #header>性能设置</template>
      <el-form label-width="180px">
        <el-form-item label="消息缓冲区上限（条）">
          <el-input-number
            :model-value="settingsStore.messageBufferLimit"
            :min="1000"
            :max="50000"
            :step="1000"
            @change="settingsStore.setMessageBufferLimit"
          />
          <el-text type="info" size="small" style="margin-left: 8px">
            超出上限时自动丢弃最旧消息
          </el-text>
        </el-form-item>
      </el-form>
    </el-card>

    <el-card class="section-card" style="margin-top: 16px">
      <template #header>外观</template>
      <el-form label-width="180px">
        <el-form-item label="主题">
          <el-radio-group :model-value="settingsStore.theme" @change="settingsStore.setTheme">
            <el-radio value="light">浅色（推荐投屏）</el-radio>
            <el-radio value="dark">深色</el-radio>
          </el-radio-group>
        </el-form-item>
      </el-form>
    </el-card>

    <el-card class="section-card" style="margin-top: 16px">
      <template #header>关于</template>
      <div class="about-info">
        <p><strong>KafkaTeach</strong> v1.0.0</p>
        <p>Kafka 教学演示工具</p>
        <p>基于 Electron + Vue 3 + KafkaJS</p>
      </div>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { useSettingsStore } from '@/stores/settingsStore'

const settingsStore = useSettingsStore()
</script>

<style scoped>
.settings-view {
  max-width: 700px;
}
.section-card {
  margin-bottom: 0;
}
.about-info p {
  margin: 4px 0;
  font-size: 14px;
}
</style>