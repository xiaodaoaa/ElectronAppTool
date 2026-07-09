<template>
  <div class="hanzi-reader">
    <h2 class="section-title">🀄 汉字认读</h2>

    <div class="filters">
      <button v-for="cat in categories" :key="cat" :class="['filter-btn', { active: filter === cat }]" @click="filter = cat">
        {{ cat }}
      </button>
    </div>

    <div class="grid">
      <div
        v-for="item in filteredList"
        :key="item.hanzi + item.pinyin"
        class="card"
        @click="speak(item.hanzi)"
      >
        <div class="hanzi">{{ item.hanzi }}</div>
        <div class="pinyin">{{ item.pinyin }}</div>
        <div class="meaning">{{ item.meaning }}</div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue'

const props = defineProps({ hanziList: Array })
const filter = ref('全部')

const categories = computed(() => ['全部', ...new Set(props.hanziList.map(h => h.category))])

const filteredList = computed(() => {
  if (filter.value === '全部') return props.hanziList
  return props.hanziList.filter(h => h.category === filter.value)
})

function speak(text) {
  if ('speechSynthesis' in window) {
    const u = new SpeechSynthesisUtterance(text)
    u.lang = 'zh-CN'; u.rate = 0.7
    speechSynthesis.speak(u)
  }
}
</script>

<style scoped>
.section-title { font-size: 1.3rem; color: #e91e63; margin-bottom: 12px; }
.filters { display: flex; gap: 6px; flex-wrap: wrap; margin-bottom: 14px; }
.filter-btn {
  padding: 5px 12px; border: 1px solid #ddd; border-radius: 12px; background: white;
  cursor: pointer; font-size: 0.85rem; transition: all 0.15s;
}
.filter-btn.active { background: #e91e63; color: white; border-color: #e91e63; }
.grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(90px, 1fr)); gap: 8px; }
.card {
  background: #fafbff; border: 2px solid #e8e8f0; border-radius: 12px;
  padding: 12px 8px; text-align: center; cursor: pointer; transition: all 0.15s;
}
.card:hover { border-color: #ff9800; background: #fff8e1; }
.hanzi { font-size: 2rem; font-weight: bold; color: #333; }
.pinyin { font-size: 0.95rem; color: #e91e63; margin-top: 4px; }
.meaning { font-size: 0.7rem; color: #aaa; margin-top: 2px; }
</style>