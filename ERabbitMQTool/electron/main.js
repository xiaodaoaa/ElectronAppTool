const { app, BrowserWindow, ipcMain } = require('electron')
const path = require('path')
const amqplib = require('amqplib')

let mainWindow = null
let connection = null
let channel = null
const consumerTags = new Set()
let isConnecting = false

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

async function cleanUp() {
  for (const tag of consumerTags) {
    try {
      if (channel) await channel.cancel(tag)
    } catch (_) {}
  }
  consumerTags.clear()
  try {
    if (channel) await channel.close()
  } catch (_) {}
  try {
    if (connection) await connection.close()
  } catch (_) {}
  channel = null
  connection = null
  isConnecting = false
}

function setupIPC() {
  ipcMain.handle('connect', async (_event, config) => {
    if (isConnecting) return { success: false }
    if (connection) await cleanUp()
    isConnecting = true

    try {
      const url = `amqp://${config.username}:${config.password}@${config.host}:${config.port}${config.vhost}`
      connection = await amqplib.connect(url)

      connection.on('error', (err) => {
        sendToRenderer('connection-error', { message: err.message })
        cleanUp().then(() => sendToRenderer('disconnected', { reason: err.message }))
      })

      connection.on('close', () => {
        if (connection) {
          cleanUp().then(() => sendToRenderer('disconnected', { reason: '连接已关闭' }))
        }
      })

      channel = await connection.createChannel()

      connection.on('error', () => {}) // prevent duplicate error
      channel.on('error', () => {})    // prevent duplicate error

      isConnecting = false

      const serverInfo = {
        host: config.host,
        port: config.port,
        vhost: config.vhost,
      }

      sendToRenderer('connected', { serverInfo })
      return { success: true, serverInfo }
    } catch (err) {
      isConnecting = false
      await cleanUp()
      const message = err.message || String(err)
      sendToRenderer('connection-error', { message })
      return { success: false }
    }
  })

  ipcMain.handle('disconnect', async () => {
    sendToRenderer('disconnected', { reason: '主动断开' })
    await cleanUp()
    return { success: true }
  })
}

app.whenReady().then(() => {
  setupIPC()
  createWindow()
})

app.on('window-all-closed', async () => {
  await cleanUp()
  app.quit()
})

app.on('activate', () => {
  if (BrowserWindow.getAllWindows().length === 0) {
    createWindow()
  }
})
