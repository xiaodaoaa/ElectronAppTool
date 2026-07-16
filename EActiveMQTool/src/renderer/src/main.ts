import { createApp } from 'vue'
import { createPinia } from 'pinia'
import ElementPlus from 'element-plus'
import 'element-plus/dist/index.css'
import 'element-plus/theme-chalk/dark/css-vars.css'
import './styles/global.css'
import App from './App.vue'
import { useConnectionStore } from './stores/connection'
import { useProducerStore } from './stores/producer'
import { useConsumerStore } from './stores/consumer'
import { useLogStore } from './stores/log'

const app = createApp(App)
app.use(createPinia())
app.use(ElementPlus)

const connectionStore = useConnectionStore()
const producerStore = useProducerStore()
const consumerStore = useConsumerStore()
const logStore = useLogStore()
connectionStore.bindIpc()
producerStore.bindIpc()
consumerStore.bindIpc()
logStore.bindIpc()

app.mount('#app')