import { app, BrowserWindow, shell } from 'electron'
import { join } from 'path'
import { initLogger } from './util/logger'
import { registerAllHandlers } from './ipc/registerHandlers'
import { kafkaClientManager } from './kafka/KafkaClientManager'

const logger = initLogger()

process.on('uncaughtException', (error) => {
  logger.error('未捕获异常:', error)
})

process.on('unhandledRejection', (reason) => {
  logger.error('未处理的 Promise 拒绝:', reason)
})

app.on('before-quit', async () => {
  logger.info('应用即将退出，清理资源...')
  await kafkaClientManager.disconnect().catch(() => {})
})

function createWindow(): void {
  const mainWindow = new BrowserWindow({
    width: 1440,
    height: 900,
    title: 'KafkaTeach',
    webPreferences: {
      preload: join(__dirname, '../preload/index.mjs'),
      nodeIntegration: false,
      contextIsolation: true,
      sandbox: false,
    },
  })

  mainWindow.webContents.setWindowOpenHandler(({ url }) => {
    if (url.startsWith('https:')) shell.openExternal(url)
    return { action: 'deny' }
  })

  if (process.env.ELECTRON_RENDERER_URL) {
    mainWindow.loadURL(process.env.ELECTRON_RENDERER_URL)
  } else {
    mainWindow.loadFile(join(__dirname, '../renderer/index.html'))
  }

  logger.info('主窗口已创建')
}

app.whenReady().then(() => {
  registerAllHandlers()
  createWindow()
})

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit()
})

app.on('activate', () => {
  if (BrowserWindow.getAllWindows().length === 0) createWindow()
})