const { app, BrowserWindow, ipcMain } = require('electron')
const path = require('path')
const fs = require('fs')
const amqplib = require('amqplib')

let mainWindow = null
let connection = null
let channel = null
const consumerTags = new Map()
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
  for (const [tag] of consumerTags) {
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
      const protocol = config.sslEnabled ? 'amqps' : 'amqp'
      const url = `${protocol}://${config.username}:${config.password}@${config.host}:${config.port}${config.vhost}`

      const connectOptions = {}
      if (config.sslEnabled) {
        connectOptions.rejectUnauthorized = config.sslValidateServerCert !== false
      }

      connection = await amqplib.connect(url, connectOptions)

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

      channel.on('error', (err) => {
        sendToRenderer('log-event', { type: 'error', detail: `Channel error: ${err.message}` })
      })

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

  ipcMain.handle('publish', async (_event, target) => {
    if (!channel) {
      sendToRenderer('publish-confirmed', { success: false, message: '未连接' })
      return { success: false }
    }

    try {
      const props = {
        persistent: target.properties.persistent,
        contentType: target.properties.contentType || 'text/plain',
        priority: target.properties.priority || 0,
        headers: target.properties.headers || {},
      }
      if (target.properties.messageId) props.messageId = target.properties.messageId
      if (target.properties.replyTo) props.replyTo = target.properties.replyTo

      let exchange = ''
      let routingKey = ''
      let displayTarget = ''

      if (target.target === 'exchange') {
        exchange = target.exchange || ''
        routingKey = target.routingKey || ''
        displayTarget = `exchange ${exchange}`
      } else {
        exchange = ''
        routingKey = target.queue || ''
        displayTarget = `queue ${target.queue}`
        await channel.assertQueue(target.queue, { durable: true })
      }

      const buf = Buffer.from(target.message || '', 'utf-8')
      const pubResult = channel.publish(exchange, routingKey, buf, props)

      if (!pubResult) {
        sendToRenderer('publish-confirmed', { success: false, message: '消息未入队列（缓冲区满或通道关闭）' })
        return { success: false }
      }

      sendToRenderer('publish-confirmed', { success: true })
      const summary = target.message.length > 50 ? target.message.slice(0, 50) + '...' : target.message
      sendToRenderer('log-event', { type: 'send', detail: `[→${displayTarget}] ${summary}` })

      return { success: true }
    } catch (err) {
      sendToRenderer('publish-confirmed', { success: false, message: err.message })
      return { success: false }
    }
  })

  ipcMain.handle('subscribe', async (_event, params) => {
    if (!channel) return { success: false }

    const mode = typeof params === 'string' ? 'queue' : params.mode
    const target = typeof params === 'string' ? params : params.target
    if (!target || !target.trim()) return { success: false }

    try {
      let consumeQueue = target
      let displayLabel = target

      if (mode === 'exchange') {
        const tmp = await channel.assertQueue('', { exclusive: true })
        consumeQueue = tmp.queue
        await channel.bindQueue(consumeQueue, target, '')
        displayLabel = `exchange ${target}`
        sendToRenderer('log-event', { type: 'subscribe', detail: `已创建临时队列 ${consumeQueue} 并绑定到 ${displayLabel}` })
      } else {
        await channel.assertQueue(target, { durable: true })
        const qInfo = await channel.checkQueue(target)
        if (qInfo.consumerCount > 0) {
          sendToRenderer('log-event', { type: 'error', detail: `队列 ${target} 上已有 ${qInfo.consumerCount} 个消费者，消息将 round-robin 分配，可能丢失。建议使用 Exchange 模式订阅。` })
        }
      }

      for (const [existingTag, existingQueue] of consumerTags) {
        if (existingQueue === target) {
          try {
            await channel.cancel(existingTag)
          } catch (_) {}
          consumerTags.delete(existingTag)
        }
      }

      const consumeResult = await channel.consume(consumeQueue, (msg) => {
        if (!msg) return
        const received = {
          queue: target,
          message: msg.content.toString('utf-8'),
          properties: {
            contentType: msg.properties.contentType,
            priority: msg.properties.priority,
            messageId: msg.properties.messageId,
            replyTo: msg.properties.replyTo,
            headers: msg.properties.headers,
            deliveryTag: msg.fields.deliveryTag,
          },
          consumerTag: msg.fields.consumerTag,
          timestamp: new Date().toISOString(),
        }
        sendToRenderer('message-received', received)
        const summary = received.message.length > 50 ? received.message.slice(0, 50) + '...' : received.message
        sendToRenderer('log-event', { type: 'receive', detail: `[←${displayLabel}] ${summary}` })
      }, { noAck: true })

      consumerTags.set(consumeResult.consumerTag, target)
      sendToRenderer('log-event', { type: 'subscribe', detail: `${displayLabel} consumerTag=${consumeResult.consumerTag}` })

      return { success: true, consumerTag: consumeResult.consumerTag }
    } catch (err) {
      sendToRenderer('log-event', { type: 'error', detail: `订阅失败: ${err.message}` })
      return { success: false }
    }
  })

  ipcMain.handle('unsubscribe', async (_event, consumerTag) => {
    if (!channel) return { success: false }

    try {
      await channel.cancel(consumerTag)
      consumerTags.delete(consumerTag)
      sendToRenderer('log-event', { type: 'subscribe', detail: `已取消 consumerTag=${consumerTag}` })
      return { success: true }
    } catch (err) {
      return { success: false }
    }
  })

  ipcMain.handle('save-config', async (_event, config) => {
    try {
      const configPath = path.join(app.getPath('userData'), 'config.json')
      fs.writeFileSync(configPath, JSON.stringify(config, null, 2), 'utf-8')
      return { success: true }
    } catch (err) {
      return { success: false }
    }
  })

  ipcMain.handle('load-config', async () => {
    try {
      const configPath = path.join(app.getPath('userData'), 'config.json')
      if (!fs.existsSync(configPath)) {
        return { success: true, config: null }
      }
      const data = fs.readFileSync(configPath, 'utf-8')
      return { success: true, config: JSON.parse(data) }
    } catch (err) {
      return { success: true, config: null }
    }
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
