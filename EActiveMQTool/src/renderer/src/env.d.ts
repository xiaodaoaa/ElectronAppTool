/// <reference types="vite/client" />

import type { api } from '../../preload'

declare global {
  interface Window {
    api: typeof api
  }
}

declare module '*.vue' {
  import type { DefineComponent } from 'vue'
  const component: DefineComponent<{}, {}, any>
  export default component
}