import { app, shell, BrowserWindow } from 'electron'
import { join } from 'path'
import { electronApp, optimizer, is } from '@electron-toolkit/utils'
import { setMainWindow } from './utils/logger'
import { registerConnectionIpc, getLastSavedConnection, getLastProducerConfig, getLastConsumerConfig } from './ipc/connection'
import { registerProducerIpc } from './ipc/producer'
import { registerConsumerIpc } from './ipc/consumer'
import { registerDialogIpc } from './ipc/dialog'
import { producerService } from './services/ProducerService'
import { consumerService } from './services/ConsumerService'
import { connectionManager } from './services/ConnectionManager'

connectionManager.onStatusChange((info) => {
  if (info.status === 'disconnected' || info.status === 'error') {
    producerService.clearHistoryOnDisconnect()
    consumerService.clearOnDisconnect()
  }
})

function createWindow(): void {
  const mainWindow = new BrowserWindow({
    width: 900,
    height: 670,
    show: false,
    autoHideMenuBar: true,
    webPreferences: {
      preload: join(__dirname, '../preload/index.mjs'),
      sandbox: false
    }
  })

  mainWindow.on('ready-to-show', () => {
    mainWindow.show()
  })

  mainWindow.webContents.setWindowOpenHandler((details) => {
    shell.openExternal(details.url)
    return { action: 'deny' }
  })

  setMainWindow(mainWindow)

  registerConnectionIpc(mainWindow)

  registerProducerIpc(mainWindow)

  registerConsumerIpc(mainWindow)

  registerDialogIpc()

  mainWindow.webContents.on('did-finish-load', () => {
    const saved = getLastSavedConnection()
    if (saved) {
      mainWindow.webContents.send('connection:lastConfigLoaded', saved)
    }
    const producer = getLastProducerConfig()
    if (producer) {
      mainWindow.webContents.send('config:producerLoaded', producer)
    }
    const consumer = getLastConsumerConfig()
    if (consumer) {
      mainWindow.webContents.send('config:consumerLoaded', consumer)
    }
  })

  if (is.dev && process.env['ELECTRON_RENDERER_URL']) {
    mainWindow.loadURL(process.env['ELECTRON_RENDERER_URL'])
  } else {
    mainWindow.loadFile(join(__dirname, '../renderer/index.html'))
  }
}

app.whenReady().then(() => {
  electronApp.setAppUserModelId('com.fengchao12.erabbitmqtool')

  app.on('browser-window-created', (_, window) => {
    optimizer.watchWindowShortcuts(window)
  })

  createWindow()

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow()
  })
})

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit()
  }
})
