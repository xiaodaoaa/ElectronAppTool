<template>
  <div class="app">
    <header class="header">
      <h1 class="title">🎵 拼音学习乐园</h1>
      <p class="subtitle">小学一年级 · 快乐学拼音</p>
    </header>
    <nav class="nav-tabs">
      <button v-for="tab in tabs" :key="tab.id" :class="['tab-btn', { active: currentTab === tab.id }]" @click="currentTab = tab.id">
        {{ tab.icon }} {{ tab.label }}
      </button>
    </nav>
    <main class="main-content">
      <HomePage v-if="currentTab === 'home'" @navigate="currentTab = $event" />
      <PinyinTable v-if="currentTab === 'initials'" title="🔤 声母学习" :items="initials" :columns="5" />
      <PinyinTable v-if="currentTab === 'finals'" title="🔊 韵母学习" :items="finals" :columns="4" />
      <PinyinTable v-if="currentTab === 'whole'" title="📖 整体认读音节" :items="wholeSyllables" :columns="4" />
      <HanziReader v-if="currentTab === 'hanzi'" :hanzi-list="hanziList" />
      <QuizGame v-if="currentTab === 'quiz'" :hanzi-list="hanziList" />
    </main>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import { initials, finals, hanziList, wholeSyllables } from './data/pinyin.js'
import HomePage from './components/HomePage.vue'
import PinyinTable from './components/PinyinTable.vue'
import HanziReader from './components/HanziReader.vue'
import QuizGame from './components/QuizGame.vue'

const currentTab = ref('home')
const tabs = [
  { id: 'home', label: '首页', icon: '🏠' },
  { id: 'initials', label: '声母', icon: '🔤' },
  { id: 'finals', label: '韵母', icon: '🔊' },
  { id: 'whole', label: '整体认读', icon: '📖' },
  { id: 'hanzi', label: '汉字认读', icon: '🀄' },
  { id: 'quiz', label: '练习测验', icon: '✍️' },
]
</script>

<style>
* { margin: 0; padding: 0; box-sizing: border-box; }
body {
  font-family: 'PingFang SC', 'Microsoft YaHei', sans-serif;
  background: linear-gradient(135deg, #fce4ec 0%, #e8f5e9 50%, #e3f2fd 100%);
  min-height: 100vh; color: #333;
}
.app { max-width: 960px; margin: 0 auto; padding: 16px; min-height: 100vh; }
.header { text-align: center; padding: 24px 0 12px; }
.title { font-size: 2rem; color: #e91e63; text-shadow: 2px 2px 4px rgba(0,0,0,0.1); }
.subtitle { color: #666; font-size: 0.95rem; margin-top: 4px; }
.nav-tabs { display: flex; gap: 6px; justify-content: center; flex-wrap: wrap; margin-bottom: 20px; padding: 8px 0; }
.tab-btn {
  padding: 10px 18px; border: 2px solid #e0e0e0; border-radius: 24px; background: white;
  cursor: pointer; font-size: 0.95rem; font-weight: 500; transition: all 0.2s; color: #555;
}
.tab-btn:hover { border-color: #42a5f5; color: #1976d2; background: #e3f2fd; }
.tab-btn.active {
  border-color: #e91e63; background: linear-gradient(135deg, #e91e63, #f06292);
  color: white; box-shadow: 0 2px 8px rgba(233,30,99,0.3);
}
.main-content { min-height: 400px; }
</style>