<template>
  <div class="tab-bar">
    <button
      class="tab-btn"
      :class="{ active: activeTab === 'server' }"
      @click="activeTab = 'server'"
    >
      服务器
    </button>
    <button
      class="tab-btn"
      :class="{ active: activeTab === 'client' }"
      @click="activeTab = 'client'"
    >
      客户端
    </button>
    <button
      class="tab-btn"
      :class="{ active: activeTab === 'about' }"
      @click="activeTab = 'about'"
    >
      关于
    </button>
  </div>

  <div class="tab-content">
    <ServerTab v-show="activeTab === 'server'" />
    <ClientTab v-show="activeTab === 'client'" />
    <AboutTab v-show="activeTab === 'about'" />
  </div>
</template>

<script>
import { ref } from 'vue'
import ServerTab from './components/ServerTab.vue'
import ClientTab from './components/ClientTab.vue'
import AboutTab from './components/AboutTab.vue'

export default {
  name: 'App',
  components: { ServerTab, ClientTab, AboutTab },
  setup() {
    const activeTab = ref('server')
    return { activeTab }
  }
}
</script>
