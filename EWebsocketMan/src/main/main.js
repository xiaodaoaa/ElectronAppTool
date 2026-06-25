import { app, BrowserWindow, ipcMain } from 'electron'
import path from 'path'
import { fileURLToPath } from 'url'
import { WebSocketServer, WebSocket } from 'ws'
import fs from 'fs'

const __dirname = path.dirname(fileURLToPath(import.meta.url))

let mainWindow = null
let wss = null
let wsc = null
let connectedClients = new Map()

// Runtime-modifiable server options (not captured in closure)
let serverOptions = { echo: false, sendall: false, tls13: false }

// Connection state flags
let isServerRunning = false
let isClientConnected = false
let isConnecting = false  // prevents double-connect race

/**
 * Safe IPC send — guards against mainWindow being null (window closed).
 */
function sendToRenderer(channel, data) {
  if (mainWindow && !mainWindow.isDestroyed()) {
    mainWindow.webContents.send(channel, data)
  }
}

function createWindow() {
  mainWindow = new BrowserWindow({
    width: 900,
    height: 700,
    title: 'EWebsocketMan - WebSocket 测试工具 v1.0.0',
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      nodeIntegration: false,
      contextIsolation: true
    }
  })

  if (process.env.VITE_DEV_SERVER_URL) {
    mainWindow.loadURL(process.env.VITE_DEV_SERVER_URL)
    mainWindow.webContents.openDevTools()
  } else {
    mainWindow.loadFile(path.join(__dirname, '../dist/index.html'))
  }

  mainWindow.on('closed', () => {
    mainWindow = null
  })
}

// ==================== WebSocket Server IPC ====================

ipcMain.handle('start-server', async (event, { url, options }) => {
  if (isServerRunning) {
    return { success: false, error: 'Server already running' }
  }

  try {
    const urlObj = new URL(url)
    const port = parseInt(urlObj.port) || 8080

    // Store options at module level so runtime changes take effect
    serverOptions = {
      echo: options?.echo ?? false,
      sendall: options?.sendall ?? false,
      tls13: options?.tls13 ?? false
    }

    wss = new WebSocketServer({ port })

    wss.on('connection', (ws, req) => {
      // Strip IPv6-mapped IPv4 prefix (e.g. ::ffff:127.0.0.1 → 127.0.0.1)
      let addr = req.socket.remoteAddress
      if (addr.startsWith('::ffff:')) addr = addr.slice(7)
      const clientAddr = addr + ':' + req.socket.remotePort
      connectedClients.set(clientAddr, ws)

      sendToRenderer('server-client-connected', clientAddr)

      const handshakeInfo = Object.entries(req.headers)
        .map(([key, value]) => `${key}: ${value}`)
        .join('\n')
      sendToRenderer('server-handshake', { clientAddr, handshakeInfo })

      ws.on('message', (data, isBinary) => {
        const message = isBinary ? data.toString('hex') : data.toString()
        sendToRenderer('server-message-received', {
          clientAddr,
          message,
          isBinary,
          timestamp: new Date().toISOString()
        })

        // Read options from module-level variable (not closure)
        if (serverOptions.echo) {
          if (ws.readyState === WebSocket.OPEN) {
            ws.send(data)
            sendToRenderer('server-message-sent', {
              clientAddr,
              message,
              isBinary,
              timestamp: new Date().toISOString()
            })
          }
        }

        if (serverOptions.sendall) {
          connectedClients.forEach((client, addr) => {
            if (addr !== clientAddr && client.readyState === WebSocket.OPEN) {
              client.send(message)
            }
          })
        }
      })

      ws.on('close', () => {
        connectedClients.delete(clientAddr)
        sendToRenderer('server-client-disconnected', clientAddr)
      })

      ws.on('error', (err) => {
        sendToRenderer('server-error', { clientAddr, error: err.message })
      })
    })

    wss.on('error', (err) => {
      sendToRenderer('server-error', { error: err.message })
    })

    isServerRunning = true
    sendToRenderer('server-started', { port })
    return { success: true, port }
  } catch (err) {
    sendToRenderer('server-error', { error: err.message })
    return { success: false, error: err.message }
  }
})

ipcMain.handle('stop-server', async () => {
  if (!isServerRunning || !wss) {
    return { success: false, error: 'Server not running' }
  }

  // Close all client connections first
  connectedClients.forEach((ws) => {
    try { ws.close() } catch (_) {}
  })
  connectedClients.clear()

  const server = wss
  wss = null
  isServerRunning = false

  // Close the underlying HTTP server
  if (server.close) {
    server.close(() => {
      sendToRenderer('server-stopped', null)
    })
    // Fallback: if close callback doesn't fire, send after timeout
    setTimeout(() => {
      if (!isServerRunning) {
        sendToRenderer('server-stopped', null)
      }
    }, 500)
  } else {
    sendToRenderer('server-stopped', null)
  }

  return { success: true }
})

ipcMain.handle('send-server-message', async (event, { clientAddr, message, isHex }) => {
  const client = connectedClients.get(clientAddr)
  if (!client || client.readyState !== WebSocket.OPEN) {
    return { success: false, error: 'Client not connected' }
  }
  try {
    let data = message
    if (isHex) {
      const hexStr = message.replace(/\s/g, '')
      data = Buffer.from(hexStr, 'hex')
    }
    client.send(data)
    sendToRenderer('server-message-sent', {
      clientAddr,
      message,
      isBinary: isHex,
      timestamp: new Date().toISOString()
    })
    return { success: true }
  } catch (err) {
    return { success: false, error: err.message }
  }
})

ipcMain.handle('broadcast-server-message', async (event, { message, isHex }) => {
  try {
    let data = message
    if (isHex) {
      const hexStr = message.replace(/\s/g, '')
      data = Buffer.from(hexStr, 'hex')
    }
    let sentCount = 0
    connectedClients.forEach((client, addr) => {
      if (client.readyState === WebSocket.OPEN) {
        client.send(data)
        sendToRenderer('server-message-sent', {
          clientAddr: addr,
          message,
          isBinary: isHex,
          timestamp: new Date().toISOString()
        })
        sentCount++
      }
    })
    return { success: true, sentCount }
  } catch (err) {
    return { success: false, error: err.message }
  }
})

ipcMain.handle('update-server-options', async (event, options) => {
  serverOptions = {
    echo: options?.echo ?? serverOptions.echo,
    sendall: options?.sendall ?? serverOptions.sendall,
    tls13: options?.tls13 ?? serverOptions.tls13
  }
  return { success: true }
})

// ==================== WebSocket Client IPC ====================

ipcMain.handle('connect-client', async (event, { url, headers }) => {
  // Prevent double-connect race
  if (isConnecting || isClientConnected) {
    return { success: false, error: 'Already connected or connecting' }
  }

  isConnecting = true

  try {
    const extraHeaders = {}
    if (headers && typeof headers === 'object') {
      Object.entries(headers).forEach(([key, value]) => {
        if (key && value) extraHeaders[key] = value
      })
    }

    wsc = new WebSocket(url, { headers: extraHeaders })

    wsc.on('open', () => {
      isClientConnected = true
      isConnecting = false
      sendToRenderer('client-connected', {
        timestamp: new Date().toISOString()
      })
    })

    wsc.on('message', (data, isBinary) => {
      const message = isBinary ? data.toString('hex') : data.toString()
      sendToRenderer('client-message-received', {
        message,
        isBinary,
        timestamp: new Date().toISOString()
      })
    })

    wsc.on('close', () => {
      isClientConnected = false
      isConnecting = false
      wsc = null
      sendToRenderer('client-disconnected', {
        timestamp: new Date().toISOString()
      })
    })

    wsc.on('error', (err) => {
      isConnecting = false
      // Don't set isClientConnected=false here — close event will fire after
      sendToRenderer('client-error', {
        error: err.message,
        timestamp: new Date().toISOString()
      })
    })

    return { success: true }
  } catch (err) {
    isConnecting = false
    sendToRenderer('client-error', {
      error: err.message,
      timestamp: new Date().toISOString()
    })
    return { success: false, error: err.message }
  }
})

ipcMain.handle('disconnect-client', async () => {
  if (!wsc) {
    isClientConnected = false
    isConnecting = false
    return { success: false, error: 'Client not connected' }
  }

  // Mark as disconnecting immediately to prevent race
  isClientConnected = false
  isConnecting = false

  try {
    wsc.close()
  } catch (_) {}

  wsc = null
  return { success: true }
})

ipcMain.handle('send-client-message', async (event, { message, isHex }) => {
  if (!wsc || wsc.readyState !== WebSocket.OPEN) {
    return { success: false, error: 'Not connected' }
  }
  try {
    let data = message
    if (isHex) {
      const hexStr = message.replace(/\s/g, '')
      data = Buffer.from(hexStr, 'hex')
    }
    wsc.send(data)
    sendToRenderer('client-message-sent', {
      message,
      isBinary: isHex,
      timestamp: new Date().toISOString()
    })
    return { success: true }
  } catch (err) {
    return { success: false, error: err.message }
  }
})

// ==================== File Operations ====================

ipcMain.handle('read-file', async (event, { filePath }) => {
  try {
    const content = fs.readFileSync(filePath, 'utf-8')
    return { success: true, content }
  } catch (err) {
    return { success: false, error: err.message }
  }
})

ipcMain.handle('read-file-binary', async (event, { filePath }) => {
  try {
    const buffer = fs.readFileSync(filePath)
    return { success: true, hex: buffer.toString('hex') }
  } catch (err) {
    return { success: false, error: err.message }
  }
})

// ==================== Config Persistence ====================

const CONFIG_PATH = path.join(app.getPath('userData'), 'config.json')

ipcMain.handle('save-config', async (event, config) => {
  try {
    fs.writeFileSync(CONFIG_PATH, JSON.stringify(config, null, 2), 'utf-8')
    return { success: true }
  } catch (err) {
    return { success: false, error: err.message }
  }
})

ipcMain.handle('load-config', async () => {
  try {
    if (fs.existsSync(CONFIG_PATH)) {
      const data = fs.readFileSync(CONFIG_PATH, 'utf-8')
      return { success: true, config: JSON.parse(data) }
    }
    return { success: false, config: null }
  } catch (err) {
    return { success: false, config: null, error: err.message }
  }
})

// ==================== Message File Logging ====================

ipcMain.handle('append-to-file', async (event, { filePath, content }) => {
  try {
    fs.appendFileSync(filePath, content + '\n', 'utf-8')
    return { success: true }
  } catch (err) {
    return { success: false, error: err.message }
  }
})

ipcMain.handle('append-binary-to-file', async (event, { filePath, hex }) => {
  try {
    const buffer = Buffer.from(hex, 'hex')
    fs.appendFileSync(filePath, buffer)
    return { success: true }
  } catch (err) {
    return { success: false, error: err.message }
  }
})

ipcMain.handle('write-binary-file', async (event, { filePath, hex }) => {
  try {
    const buffer = Buffer.from(hex, 'hex')
    fs.writeFileSync(filePath, buffer)
    return { success: true }
  } catch (err) {
    return { success: false, error: err.message }
  }
})

ipcMain.handle('write-file', async (event, { filePath, content }) => {
  try {
    fs.writeFileSync(filePath, content, 'utf-8')
    return { success: true }
  } catch (err) {
    return { success: false, error: err.message }
  }
})

// ==================== File Dialog ====================

ipcMain.handle('select-save-file', async () => {
  const { dialog } = require('electron')
  const result = await dialog.showSaveDialog(mainWindow, {
    title: '选择保存文件路径',
    defaultPath: 'websocket-log.txt',
    filters: [
      { name: 'Text Files', extensions: ['txt', 'log'] },
      { name: 'All Files', extensions: ['*'] }
    ]
  })
  if (result.canceled) {
    return { success: false, filePath: null }
  }
  return { success: true, filePath: result.filePath }
})

ipcMain.handle('select-open-file', async () => {
  const { dialog } = require('electron')
  const result = await dialog.showOpenDialog(mainWindow, {
    title: '选择要发送的文件',
    properties: ['openFile'],
    filters: [
      { name: 'All Files', extensions: ['*'] }
    ]
  })
  if (result.canceled) {
    return { success: false, filePath: null }
  }
  return { success: true, filePath: result.filePaths[0] }
})

// ==================== App Lifecycle ====================

app.whenReady().then(() => {
  createWindow()

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      createWindow()
    }
  })
})

app.on('window-all-closed', () => {
  if (wss) { try { wss.close() } catch (_) {} }
  if (wsc) { try { wsc.close() } catch (_) {} }
  if (process.platform !== 'darwin') {
    app.quit()
  }
})
