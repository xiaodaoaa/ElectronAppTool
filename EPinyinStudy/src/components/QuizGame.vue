<template>
  <div class="quiz">
    <h2 class="title">✍️ 练习测验</h2>

    <div v-if="!started" class="start-screen">
      <p>选择答题模式，开始挑战吧！</p>
      <div class="options">
        <button v-for="n in [5, 10, 15]" :key="n" class="btn" @click="start(n)">{{ n }} 题</button>
        <button class="btn infinite" @click="start(0)">∞ 无限模式</button>
      </div>
    </div>

    <div v-else-if="done" class="result">
      <div class="score">
        <span class="num">{{ correct }}</span>
        <span class="slash">/</span>
        <span class="num">{{ questions.length }}</span>
      </div>
      <p>答对了 {{ correct }} 题！</p>
      <p class="msg" v-if="rate >= 90">🌟 太棒了！你是拼音小达人！</p>
      <p class="msg" v-else-if="rate >= 70">👍 不错哦，继续加油！</p>
      <p class="msg" v-else>💪 多练习就会越来越好！</p>
      <button class="btn" @click="reset">再来一次</button>
    </div>

    <div v-else class="quiz-area">
      <div class="progress">
        <template v-if="isInfinite">第 {{ current + 1 }} 题 | 已答对 {{ correct }} 题</template>
        <template v-else>第 {{ current + 1 }} / {{ questions.length }} 题</template>
      </div>
      <div class="question">
        <div class="char">{{ questions[current].hanzi }}</div>
        <p class="hint">请选择正确的拼音</p>
      </div>
      <div class="choices">
        <button
          v-for="(c, i) in questions[current].choices"
          :key="i"
          :class="['choice', { chosen: selected === i, correct: answered && c === questions[current].correct, wrong: answered && selected === i && c !== questions[current].correct }]"
          :disabled="answered"
          @click="select(i)"
        >
          {{ c }}
        </button>
      </div>
      <button v-if="answered" class="btn next-btn" @click="next">
        {{ isInfinite ? '下一题 →' : (current < questions.length - 1 ? '下一题 →' : '查看结果') }}
      </button>
    </div>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue'

const props = defineProps({ hanziList: Array })

const started = ref(false)
const done = ref(false)
const current = ref(0)
const correct = ref(0)
const selected = ref(-1)
const answered = ref(false)
const questions = ref([])
const isInfinite = ref(false)
const totalAnswered = ref(0)

function generateDistractors(correct, pool) {
  const distractors = pool.filter(p => p !== correct)
  const shuffled = [...distractors].sort(() => Math.random() - 0.5)
  return shuffled.slice(0, 3)
}

function generateOne(pool) {
  const h = props.hanziList[Math.floor(Math.random() * props.hanziList.length)]
  const dist = generateDistractors(h.pinyin, pool)
  const choices = [h.pinyin, ...dist].sort(() => Math.random() - 0.5)
  return { hanzi: h.hanzi, correct: h.pinyin, choices }
}

function start(n) {
  const pool = [...new Set(props.hanziList.map(h => h.pinyin))]
  isInfinite.value = n === 0
  if (isInfinite.value) {
    questions.value = Array.from({ length: 20 }, () => generateOne(pool))
  } else {
    const shuffled = [...props.hanziList].sort(() => Math.random() - 0.5)
    questions.value = shuffled.slice(0, n).map(h => {
      const dist = generateDistractors(h.pinyin, pool)
      const choices = [h.pinyin, ...dist].sort(() => Math.random() - 0.5)
      return { hanzi: h.hanzi, correct: h.pinyin, choices }
    })
  }
  current.value = 0
  correct.value = 0
  totalAnswered.value = 0
  selected.value = -1
  answered.value = false
  started.value = true
  done.value = false
}

function select(i) {
  selected.value = i
  answered.value = true
  totalAnswered.value++
  if (questions.value[current.value].choices[i] === questions.value[current.value].correct) {
    correct.value++
  }
}

function next() {
  if (isInfinite.value) {
    const pool = [...new Set(props.hanziList.map(h => h.pinyin))]
    questions.value[current.value] = generateOne(pool)
    current.value++
    selected.value = -1
    answered.value = false
  } else if (current.value < questions.value.length - 1) {
    current.value++
    selected.value = -1
    answered.value = false
  } else {
    done.value = true
  }
}

function reset() {
  started.value = false
  done.value = false
}

const rate = computed(() => totalAnswered.value > 0 ? Math.round((correct.value / totalAnswered.value) * 100) : 0)
</script>

<style scoped>
.title { font-size: 1.3rem; color: #e91e63; text-align: center; margin-bottom: 16px; }
.start-screen, .result { text-align: center; padding: 40px 0; }
.start-screen p { color: #888; margin-bottom: 20px; }
.options { display: flex; gap: 12px; justify-content: center; }
.btn {
  padding: 10px 28px; border: none; border-radius: 24px; background: linear-gradient(135deg, #e91e63, #f06292);
  color: white; font-size: 1rem; cursor: pointer; transition: all 0.2s;
}
.btn:hover { transform: scale(1.05); box-shadow: 0 4px 12px rgba(233,30,99,0.3); }
.score { font-size: 3rem; margin-bottom: 10px; }
.score .num { color: #e91e63; font-weight: bold; }
.score .slash { color: #ccc; margin: 0 4px; }
.msg { color: #888; margin-top: 8px; }
.result .btn { margin-top: 20px; }

.quiz-area { text-align: center; }
.progress { color: #999; font-size: 0.9rem; margin-bottom: 20px; }
.question { margin-bottom: 20px; }
.char { font-size: 5rem; font-weight: bold; color: #333; margin-bottom: 8px; transition: all 0.2s; }
.hint { color: #aaa; font-size: 0.9rem; }
.choices { display: flex; gap: 10px; justify-content: center; flex-wrap: wrap; margin-bottom: 20px; }
.choice {
  min-width: 90px; padding: 12px 20px; border: 2px solid #e0e0e0; border-radius: 12px;
  background: white; font-size: 1.2rem; cursor: pointer; transition: all 0.15s;
}
.choice:hover:not(:disabled) { border-color: #42a5f5; background: #e3f2fd; }
.choice.chosen:not(.correct):not(.wrong) { border-color: #42a5f5; background: #e3f2fd; }
.choice.correct { border-color: #4caf50; background: #e8f5e9; color: #2e7d32; }
.choice.wrong { border-color: #f44336; background: #ffebee; color: #c62828; }
.next-btn { margin-top: 10px; }
</style>