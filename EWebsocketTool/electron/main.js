// electron/main.js
const { app, BrowserWindow, ipcMain, Menu } = require('electron')
const path = require('path')
const { WebSocketServer } = require('ws')

let mainWindow = null
let wss = null
let clientIdCounter = 0
const clients = new Map()

// 从 remoteAddress 中提取纯 IPv4 地址
// ::ffff:x.x.x.x → x.x.x.x
// ::1            → 127.0.0.1
// x.x.x.x        → x.x.x.x
// 其他纯 IPv6     → null
function getIPv4(remoteAddress) {
  if (!remoteAddress) return null
  // IPv4-mapped IPv6
  if (remoteAddress.startsWith('::ffff:')) {
    return remoteAddress.slice(7)
  }
  // IPv6 环回地址
  if (remoteAddress === '::1') {
    return '127.0.0.1'
  }
  // 纯 IPv4
  if (/^\d+\.\d+\.\d+\.\d+$/.test(remoteAddress)) {
    return remoteAddress
  }
  // 其他纯 IPv6 地址，不显示
  return null
}

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

function setupIPC() {
  ipcMain.handle('start-server', async (_event, port) => {
    if (wss) {
      throw new Error('服务已在运行中')
    }
    return new Promise((resolve, reject) => {
      try {
        wss = new WebSocketServer({ port })
        wss.on('listening', () => {
          sendToRenderer('server-started', { port })
          resolve({ success: true })
        })
        wss.on('error', (err) => {
          wss = null
          sendToRenderer('server-error', { message: err.message })
          reject(err)
        })
        wss.on('connection', (ws, req) => {
          const ipv4 = getIPv4(req.socket.remoteAddress)
          const displayIp = ipv4 || 'unknown'
          const shortId = displayIp !== 'unknown' ? `${displayIp}:${req.socket.remotePort}` : `#${clientIdCounter + 1}`
          const clientId = `${shortId}-${++clientIdCounter}`
          const clientInfo = {
            ws,
            ip: displayIp,
            connectTime: new Date().toLocaleTimeString(),
          }
          clients.set(clientId, clientInfo)

          sendToRenderer('client-connected', {
            clientId,
            ip: displayIp,
            connectTime: clientInfo.connectTime,
          })

          ws.on('message', (data) => {
            sendToRenderer('message-received', {
              clientId,
              message: data.toString(),
            })
          })

          ws.on('close', () => {
            clients.delete(clientId)
            sendToRenderer('client-disconnected', { clientId })
          })

          ws.on('error', (err) => {
            sendToRenderer('server-error', { message: `客户端 ${clientId} 错误: ${err.message}` })
          })
        })
      } catch (err) {
        reject(err)
      }
    })
  })

  ipcMain.handle('stop-server', async () => {
    if (wss) {
      // 关闭所有客户端连接
      for (const [, client] of clients) {
        try {
          client.ws.close()
        } catch (_) {
          // 忽略已关闭的连接
        }
      }
      clients.clear()
      wss.close()
      wss = null
      sendToRenderer('server-stopped')
    }
    return { success: true }
  })

  ipcMain.handle('send-to-clients', async (_event, { clientIds, message }) => {
    let sent = 0
    for (const clientId of clientIds) {
      const client = clients.get(clientId)
      if (client && client.ws.readyState === 1) {
        client.ws.send(message)
        sent++
      }
    }
    return { sent, total: clientIds.length }
  })

  ipcMain.handle('broadcast', async (_event, message) => {
    let sent = 0
    const total = clients.size
    for (const [, client] of clients) {
      if (client.ws.readyState === 1) {
        client.ws.send(message)
        sent++
      }
    }
    return { sent, total }
  })

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
  setupIPC()
  createWindow()
})

app.on('window-all-closed', () => {
  if (wss) {
    for (const [, client] of clients) {
      try {
        client.ws.close()
      } catch (_) {
        // 忽略已关闭的连接
      }
    }
    clients.clear()
    wss.close()
  }
  app.quit()
})

app.on('activate', () => {
  if (BrowserWindow.getAllWindows().length === 0) {
    createWindow()
  }
})