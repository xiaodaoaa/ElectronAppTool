<template>
  <div class="home">
    <div class="hero">
      <div class="mascot">🐼</div>
      <h2 class="welcome">欢迎来到拼音学习乐园！</h2>
      <p class="desc">和熊猫一起快乐学拼音，认汉字吧！</p>
    </div>

    <div class="cards">
      <div class="card" @click="$emit('navigate', 'initials')">
        <span class="card-icon">🔤</span>
        <h3>声母学习</h3>
        <p>23个声母，分类记忆</p>
      </div>
      <div class="card" @click="$emit('navigate', 'finals')">
        <span class="card-icon">🔊</span>
        <h3>韵母学习</h3>
        <p>单韵母、复韵母、鼻韵母</p>
      </div>
      <div class="card" @click="$emit('navigate', 'whole')">
        <span class="card-icon">📖</span>
        <h3>整体认读音节</h3>
        <p>16个整体认读音节</p>
      </div>
      <div class="card" @click="$emit('navigate', 'hanzi')">
        <span class="card-icon">🀄</span>
        <h3>汉字认读</h3>
        <p>100+一年级常用汉字</p>
      </div>
      <div class="card" @click="$emit('navigate', 'quiz')">
        <span class="card-icon">✍️</span>
        <h3>练习测验</h3>
        <p>挑战自己，看看记住了多少</p>
      </div>
    </div>
  </div>
</template>

<script setup>
defineEmits(['navigate'])
</script>

<style scoped>
.home { padding: 10px 0; }
.hero { text-align: center; padding: 30px 0 20px; }
.mascot { font-size: 72px; }
.welcome { font-size: 1.5rem; color: #e91e63; margin: 10px 0; }
.desc { color: #888; font-size: 1rem; }
.cards { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: 14px; margin-top: 20px; }
.card {
  background: linear-gradient(135deg, #f8f9ff, #fff);
  border: 2px solid #e8e8f0; border-radius: 16px; padding: 24px 16px;
  text-align: center; cursor: pointer; transition: all 0.2s;
}
.card:hover { transform: translateY(-3px); box-shadow: 0 8px 24px rgba(0,0,0,0.1); border-color: #42a5f5; }
.card-icon { font-size: 40px; display: block; margin-bottom: 10px; }
.card h3 { font-size: 1.1rem; color: #333; }
.card p { font-size: 0.85rem; color: #999; margin-top: 4px; }
</style>