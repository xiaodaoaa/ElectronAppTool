// electron/modules/http-server.js
const http = require('http')
const { URL } = require('url')

const RESPONSE_CONTENT_TYPES = {
  text: 'text/plain; charset=utf-8',
  json: 'application/json; charset=utf-8',
  xml: 'application/xml; charset=utf-8',
  html: 'text/html; charset=utf-8',
}

// 从 remoteAddress 中提取纯 IPv4 地址
function getIPv4(remoteAddress) {
  if (!remoteAddress) return null
  if (remoteAddress.startsWith('::ffff:')) {
    return remoteAddress.slice(7)
  }
  if (remoteAddress === '::1') {
    return '127.0.0.1'
  }
  if (/^\d+\.\d+\.\d+\.\d+$/.test(remoteAddress)) {
    return remoteAddress
  }
  return null
}

function parseBody(req) {
  return new Promise((resolve) => {
    const chunks = []
    req.on('data', (chunk) => chunks.push(chunk))
    req.on('end', () => resolve(Buffer.concat(chunks).toString('utf-8')))
    req.on('error', () => resolve(''))
  })
}

function parseQuery(urlString) {
  try {
    const url = new URL(urlString, 'http://localhost')
    const query = {}
    for (const [key, value] of url.searchParams) {
      query[key] = value
    }
    return query
  } catch (_) {
    return {}
  }
}

function buildEchoResponse(method, urlPath, headers, query, body) {
  return JSON.stringify({
    echo: true,
    method,
    path: urlPath,
    headers,
    query,
    body,
  }, null, 2)
}

class HttpServerManager {
  constructor(pathConfigManager, requestLogger, logger) {
    this.pathConfig = pathConfigManager
    this.logger = requestLogger
    this.log = logger || console
    this.server = null
    this.sendToRenderer = null
  }

  start(port, sendToRenderer) {
    if (this.server) {
      throw new Error('服务已在运行中')
    }
    this.sendToRenderer = sendToRenderer

    return new Promise((resolve, reject) => {
      this.server = http.createServer(async (req, res) => {
        await this._handleRequest(req, res)
      })

      this.server.on('listening', () => {
        this.log.info(`HTTP 服务器启动在端口 {0}`, port)
        this.sendToRenderer('server-started', { port })
        resolve({ success: true })
      })

      this.server.on('error', (err) => {
        this.log.error('服务器错误: {0}', err.message)
        this.server = null
        this.sendToRenderer('server-error', { message: err.message })
        reject(err)
      })

      try {
        this.server.listen(port)
      } catch (err) {
        this.server = null
        reject(err)
      }
    })
  }

  stop() {
    if (this.server) {
      return new Promise((resolve) => {
        this.server.close(() => {
          this.log.info('HTTP 服务器已停止')
          this.server = null
          if (this.sendToRenderer) {
            this.sendToRenderer('server-stopped')
          }
          resolve({ success: true })
        })
      })
    }
    return Promise.resolve({ success: true })
  }

  async _handleRequest(req, res) {
    const ipv4 = getIPv4(req.socket.remoteAddress)
    const clientIp = ipv4 || 'unknown'
    const method = req.method.toUpperCase()
    const urlObj = new URL(req.url, 'http://localhost')
    const reqPath = urlObj.pathname
    const query = parseQuery(req.url)
    const body = await parseBody(req)
    const headers = { ...req.headers }

    this.log.trace('收到请求: {0} {1} 来自 {2}', method, reqPath, clientIp)

    // Log the request
    this.logger.log({ clientIp, method, path: reqPath, headers, query, body })

    // Push log to renderer
    if (this.sendToRenderer) {
      const logs = this.logger.getAll()
      this.sendToRenderer('new-request', logs[logs.length - 1])
    }

    // Find matching path config
    const config = this.pathConfig.getByPath(reqPath)
    if (!config) {
      this.log.warn('路径未找到: {0}', reqPath)
      res.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' })
      res.end('404 Not Found')
      return
    }

    // Check method
    if (!config.methods.includes(method)) {
      this.log.warn('方法不允许: {0} {1}', method, reqPath)
      res.writeHead(405, { 'Content-Type': 'text/plain; charset=utf-8' })
      res.end('405 Method Not Allowed')
      return
    }

    // Echo mode
    if (config.echoEnabled) {
      this.log.debug('回显模式: {0} {1}', method, reqPath)
      const echoBody = buildEchoResponse(method, reqPath, headers, query, body)
      res.writeHead(200, { 'Content-Type': 'application/json; charset=utf-8' })
      res.end(echoBody)
      return
    }

    // Custom response
    this.log.debug('自定义响应: {0} {1}', method, reqPath)
    const contentType = RESPONSE_CONTENT_TYPES[config.responseType] || 'text/plain; charset=utf-8'
    res.writeHead(200, { 'Content-Type': contentType })
    res.end(config.responseContent || '')
  }
}

module.exports = { HttpServerManager }
