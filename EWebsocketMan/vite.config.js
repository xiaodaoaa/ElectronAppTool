import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import electron from 'vite-plugin-electron'
import renderer from 'vite-plugin-electron-renderer'

export default defineConfig({
  plugins: [
    vue(),
    electron([
      {
        entry: 'src/main/main.js',
        vite: {
          build: {
            outDir: 'dist-electron',
            rollupOptions: {
              external: ['ws', 'electron']
            }
          }
        }
      },
      {
        entry: 'src/preload/preload.js',
        onstart(options) {
          options.reload()
        },
        vite: {
          build: {
            outDir: 'dist-electron'
          }
        }
      }
    ]),
    renderer()
  ],
  server: {
    port: 5173
  }
})
