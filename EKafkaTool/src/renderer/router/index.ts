import { createRouter, createWebHashHistory } from 'vue-router'

const router = createRouter({
  history: createWebHashHistory(),
  routes: [
    { path: '/', redirect: '/connection' },
    { path: '/connection', name: 'connection', component: () => import('@/views/ConnectionView.vue') },
    { path: '/cluster', name: 'cluster', component: () => import('@/views/ClusterView.vue') },
    { path: '/producer', name: 'producer', component: () => import('@/views/ProducerView.vue') },
    { path: '/consumer', name: 'consumer', component: () => import('@/views/ConsumerView.vue') },
    { path: '/visual', name: 'visual', component: () => import('@/views/VisualView.vue') },
    { path: '/scenario', name: 'scenario', component: () => import('@/views/ScenarioView.vue') },
    { path: '/settings', name: 'settings', component: () => import('@/views/SettingsView.vue') },
  ],
})

export default router