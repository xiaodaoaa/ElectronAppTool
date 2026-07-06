const { app, BrowserWindow, ipcMain, Menu } = require('electron')
const path = require('path')
const { PathConfigManager } = require('./modules/path-config')
const { RequestLogger } = require('./modules/request-logger')
const { HttpServerManager } = require('./modules/http-server')
const { createLogger } = require('./modules/logger')

let mainWindow = null
let pathConfigManager = null
let requestLogger = null
let httpServerManager = null
let rootLogger = null

function createWindow() {
  mainWindow = new BrowserWindow({
    width: 1200,
    height: 800,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
    },
  })

  const devUrl = process.env.VITE_DEV_SERVER_URL
  if (devUrl) {
    mainWindow.loadURL(devUrl)
  } else {
    mainWindow.loadFile(path.join(__dirname, '..', 'dist', 'index.html'))
  }
}

function sendToRenderer(channel, data) {
  if (mainWindow && !mainWindow.isDestroyed()) {
    mainWindow.webContents.send(channel, data)
  }
}

function setupModules() {
  const userDataPath = app.getPath('userData')
  // 日志文件位置：程序所在目录下的 logs/
  // 开发模式：项目根目录；打包后：exe 所在目录
  const appDir = app.isPackaged
    ? path.dirname(app.getPath('exe'))
    : path.resolve(__dirname, '..')

  // 创建根日志记录器
  rootLogger = createLogger({ name: 'main', appDir })

  // 转发日志事件到渲染进程（DevTools）
  rootLogger.on('log', (logData) => {
    sendToRenderer('dev-log', logData)
  })

  pathConfigManager = new PathConfigManager(userDataPath)
  requestLogger = new RequestLogger()
  httpServerManager = new HttpServerManager(pathConfigManager, requestLogger, rootLogger)

  rootLogger.info('模块初始化完成')
}

function setupIPC() {
  // Server control
  ipcMain.handle('start-server', async (_event, port) => {
    return httpServerManager.start(port, sendToRenderer)
  })

  ipcMain.handle('stop-server', async () => {
    return httpServerManager.stop()
  })

  // Path config
  ipcMain.handle('add-path', async (_event, config) => {
    return pathConfigManager.add(config)
  })

  ipcMain.handle('update-path', async (_event, config) => {
    return pathConfigManager.update(config)
  })

  ipcMain.handle('delete-path', async (_event, id) => {
    return pathConfigManager.remove(id)
  })

  ipcMain.handle('list-paths', async () => {
    return pathConfigManager.getAll()
  })

  // Logs
  ipcMain.handle('get-logs', async () => {
    return requestLogger.getAll()
  })

  ipcMain.handle('clear-logs', async () => {
    return requestLogger.clear()
  })

  // Context menu
  ipcMain.handle('show-context-menu', async () => {
    return new Promise((resolve) => {
      const menu = Menu.buildFromTemplate([
        {
          label: '清空',
          click: () => resolve('clear'),
        },
      ])
      menu.popup({
        callback: () => resolve(null),
      })
    })
  })
}

app.whenReady().then(() => {
  setupModules()
  setupIPC()
  createWindow()
})

app.on('window-all-closed', () => {
  if (httpServerManager) {
    httpServerManager.stop()
  }
  app.quit()
})

app.on('activate', () => {
  if (BrowserWindow.getAllWindows().length === 0) {
    createWindow()
  }
})
