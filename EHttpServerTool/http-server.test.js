const { HttpServerManager } = require('./electron/modules/http-server')
const { PathConfigManager } = require('./electron/modules/path-config')
const { RequestLogger } = require('./electron/modules/request-logger')
const fs = require('fs')
const path = require('path')
const os = require('os')
const http = require('http')

describe('HttpServerManager', () => {
  let serverManager
  let pathConfig
  let logger
  let tempDir
  let actualPort

  function makeRequest(method, urlPath, body = '', headers = {}) {
    return new Promise((resolve, reject) => {
      const options = {
        hostname: '127.0.0.1',
        port: actualPort,
        path: urlPath,
        method,
        headers,
      }
      const req = http.request(options, (res) => {
        let data = ''
        res.on('data', (chunk) => (data += chunk))
        res.on('end', () =>
          resolve({
            statusCode: res.statusCode,
            headers: res.headers,
            body: data,
          })
        )
      })
      req.on('error', reject)
      if (body) req.write(body)
      req.end()
    })
  }

  beforeEach(() => {
    tempDir = fs.mkdtempSync(path.join(os.tmpdir(), 'http-server-test-'))
    pathConfig = new PathConfigManager(tempDir)
    logger = new RequestLogger()
    serverManager = new HttpServerManager(pathConfig, logger)
    actualPort = 0 // will be assigned by OS
  })

  afterEach(async () => {
    await serverManager.stop()
    fs.rmSync(tempDir, { recursive: true, force: true })
  })

  async function startWithPort0() {
    // Use port 0 to get a random available port
    await serverManager.start(0, () => {})
    actualPort = serverManager.server.address().port
  }

  test('should return custom JSON response for matched path', async () => {
    pathConfig.add({
      path: '/api/test',
      methods: ['GET'],
      echoEnabled: false,
      responseType: 'json',
      responseContent: '{"status":"ok"}',
    })

    await startWithPort0()
    const res = await makeRequest('GET', '/api/test')

    expect(res.statusCode).toBe(200)
    expect(res.headers['content-type']).toBe('application/json; charset=utf-8')
    expect(res.body).toBe('{"status":"ok"}')
  })

  test('should return 404 for unmatched path', async () => {
    await startWithPort0()
    const res = await makeRequest('GET', '/api/missing')

    expect(res.statusCode).toBe(404)
    expect(res.body).toBe('404 Not Found')
  })

  test('should return 405 for method not allowed', async () => {
    pathConfig.add({
      path: '/api/test',
      methods: ['GET'],
      echoEnabled: false,
      responseType: 'json',
      responseContent: '{}',
    })

    await startWithPort0()
    const res = await makeRequest('POST', '/api/test', 'data')

    expect(res.statusCode).toBe(405)
    expect(res.body).toBe('405 Method Not Allowed')
  })

  test('should return echo response when echo is enabled', async () => {
    pathConfig.add({
      path: '/api/echo',
      methods: ['POST'],
      echoEnabled: true,
      responseType: 'json',
      responseContent: '',
    })

    await startWithPort0()
    const res = await makeRequest(
      'POST',
      '/api/echo?foo=bar',
      '{"name":"test"}',
      { 'content-type': 'application/json' }
    )

    expect(res.statusCode).toBe(200)
    const echoData = JSON.parse(res.body)
    expect(echoData.echo).toBe(true)
    expect(echoData.method).toBe('POST')
    expect(echoData.path).toBe('/api/echo')
    expect(echoData.query).toEqual({ foo: 'bar' })
    expect(echoData.body).toBe('{"name":"test"}')
  })

  test('should log all requests', async () => {
    pathConfig.add({
      path: '/api/test',
      methods: ['GET'],
      echoEnabled: false,
      responseType: 'json',
      responseContent: '{}',
    })

    await startWithPort0()
    await makeRequest('GET', '/api/test?x=1', '', { 'user-agent': 'test-agent' })
    await makeRequest('GET', '/api/missing')

    const logs = logger.getAll()
    expect(logs.length).toBe(2)
    expect(logs[0].path).toBe('/api/test')
    expect(logs[0].query).toEqual({ x: '1' })
    expect(logs[0].headers['user-agent']).toBe('test-agent')
    expect(logs[1].path).toBe('/api/missing')
  })
})
