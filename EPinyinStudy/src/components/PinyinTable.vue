<template>
  <div class="pinyin-table">
    <h2 class="section-title">{{ title }}</h2>
    <div class="grid" :style="{ gridTemplateColumns: `repeat(${columns}, 1fr)` }">
      <div v-for="item in items" :key="item.pinyin" class="card" @click="speak(item.pinyin)">
        <div class="pinyin">{{ item.char }}</div>
        <div class="example">{{ item.examples?.join('、') }}</div>
        <div v-if="item.type" class="badge">{{ item.type }}</div>
      </div>
    </div>
  </div>
</template>

<script setup>
defineProps({
  title: String,
  items: Array,
  columns: { type: Number, default: 4 },
})

function speak(text) {
  if ('speechSynthesis' in window) {
    const u = new SpeechSynthesisUtterance(text)
    u.lang = 'zh-CN'; u.rate = 0.8
    speechSynthesis.speak(u)
  }
}
</script>

<style scoped>
.section-title { font-size: 1.3rem; color: #e91e63; margin-bottom: 16px; }
.grid { display: grid; gap: 10px; }
.card {
  background: #fafbff; border: 2px solid #e8e8f0; border-radius: 12px;
  padding: 16px 10px; text-align: center; cursor: pointer; transition: all 0.15s;
  position: relative;
}
.card:hover { border-color: #42a5f5; background: #e3f2fd; transform: scale(1.04); }
.pinyin { font-size: 1.8rem; font-weight: bold; color: #1565c0; }
.example { font-size: 0.85rem; color: #888; margin-top: 4px; }
.badge {
  position: absolute; top: 4px; right: 4px; font-size: 0.7rem;
  background: #fce4ec; color: #e91e63; padding: 1px 6px; border-radius: 8px;
}
</style>