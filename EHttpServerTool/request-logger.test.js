const { RequestLogger } = require('./electron/modules/request-logger')

describe('RequestLogger', () => {
  let logger

  beforeEach(() => {
    logger = new RequestLogger()
  })

  test('should log a request entry', () => {
    const entry = logger.log({
      clientIp: '127.0.0.1',
      method: 'GET',
      path: '/api/test',
      headers: { 'user-agent': 'test' },
      query: { page: '1' },
      body: '',
    })

    expect(entry.id).toBeDefined()
    expect(entry.timestamp).toBeDefined()
    expect(entry.clientIp).toBe('127.0.0.1')
    expect(entry.method).toBe('GET')
    expect(entry.path).toBe('/api/test')

    const all = logger.getAll()
    expect(all).toHaveLength(1)
    expect(all[0].id).toBe(entry.id)
  })

  test('should cap logs at 1000 entries', () => {
    for (let i = 0; i < 1005; i++) {
      logger.log({
        clientIp: '127.0.0.1',
        method: 'GET',
        path: `/api/test-${i}`,
        headers: {},
        query: {},
        body: '',
      })
    }

    const all = logger.getAll()
    expect(all).toHaveLength(1000)
    // Oldest entries should be removed
    expect(all[0].path).toBe('/api/test-5')
    expect(all[999].path).toBe('/api/test-1004')
  })

  test('should clear all logs', () => {
    logger.log({
      clientIp: '127.0.0.1',
      method: 'GET',
      path: '/api/test',
      headers: {},
      query: {},
      body: '',
    })

    const result = logger.clear()
    expect(result.success).toBe(true)
    expect(logger.getAll()).toHaveLength(0)
  })

  test('should notify listener on new request', () => {
    const callback = jest.fn()
    logger.onNewRequest(callback)

    logger.log({
      clientIp: '127.0.0.1',
      method: 'POST',
      path: '/api/test',
      headers: {},
      query: {},
      body: '{"data":"test"}',
    })

    expect(callback).toHaveBeenCalledTimes(1)
    expect(callback).toHaveBeenCalledWith(
      expect.objectContaining({
        method: 'POST',
        path: '/api/test',
        body: '{"data":"test"}',
      })
    )
  })

  test('should unsubscribe listener', () => {
    const callback = jest.fn()
    const unsubscribe = logger.onNewRequest(callback)

    logger.log({
      clientIp: '127.0.0.1',
      method: 'GET',
      path: '/api/test-1',
      headers: {},
      query: {},
      body: '',
    })

    expect(callback).toHaveBeenCalledTimes(1)

    unsubscribe()

    logger.log({
      clientIp: '127.0.0.1',
      method: 'GET',
      path: '/api/test-2',
      headers: {},
      query: {},
      body: '',
    })

    expect(callback).toHaveBeenCalledTimes(1) // Still 1, not called again
  })
})
