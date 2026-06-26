const { PathConfigManager } = require('./electron/modules/path-config')
const fs = require('fs')
const path = require('path')
const os = require('os')

describe('PathConfigManager', () => {
  let manager
  let tempDir

  beforeEach(() => {
    tempDir = fs.mkdtempSync(path.join(os.tmpdir(), 'path-config-test-'))
    manager = new PathConfigManager(tempDir)
  })

  afterEach(() => {
    fs.rmSync(tempDir, { recursive: true, force: true })
  })

  test('should add a valid path config', () => {
    const config = {
      path: '/api/test',
      methods: ['GET'],
      echoEnabled: false,
      responseType: 'json',
      responseContent: '{"status":"ok"}',
    }

    const result = manager.add(config)

    expect(result.success).toBe(true)
    expect(result.id).toBeDefined()

    const all = manager.getAll()
    expect(all).toHaveLength(1)
    expect(all[0].path).toBe('/api/test')
    expect(all[0].id).toBe(result.id)
  })

  test('should reject path not starting with /', () => {
    const config = {
      path: 'api/test',
      methods: ['GET'],
      echoEnabled: false,
      responseType: 'json',
      responseContent: '',
    }

    const result = manager.add(config)

    expect(result.success).toBe(false)
    expect(result.error).toBe('路径必须以 / 开头')
    expect(manager.getAll()).toHaveLength(0)
  })

  test('should reject duplicate path', () => {
    manager.add({
      path: '/api/test',
      methods: ['GET'],
      echoEnabled: false,
      responseType: 'json',
      responseContent: '',
    })

    const result = manager.add({
      path: '/api/test',
      methods: ['POST'],
      echoEnabled: false,
      responseType: 'json',
      responseContent: '',
    })

    expect(result.success).toBe(false)
    expect(result.error).toBe('该路径已存在')
    expect(manager.getAll()).toHaveLength(1)
  })

  test('should update existing config', () => {
    const addResult = manager.add({
      path: '/api/test',
      methods: ['GET'],
      echoEnabled: false,
      responseType: 'json',
      responseContent: '',
    })
    const id = addResult.id

    const updateResult = manager.update({
      id,
      path: '/api/test',
      methods: ['GET', 'POST'],
      echoEnabled: true,
      responseType: 'text',
      responseContent: 'updated',
    })

    expect(updateResult.success).toBe(true)
    const all = manager.getAll()
    expect(all[0].methods).toEqual(['GET', 'POST'])
    expect(all[0].echoEnabled).toBe(true)
    expect(all[0].responseContent).toBe('updated')
  })

  test('should reject update with duplicate path', () => {
    manager.add({
      path: '/api/a',
      methods: ['GET'],
      echoEnabled: false,
      responseType: 'json',
      responseContent: '',
    })
    const resultB = manager.add({
      path: '/api/b',
      methods: ['GET'],
      echoEnabled: false,
      responseType: 'json',
      responseContent: '',
    })

    const updateResult = manager.update({
      id: resultB.id,
      path: '/api/a',
      methods: ['GET'],
      echoEnabled: false,
      responseType: 'json',
      responseContent: '',
    })

    expect(updateResult.success).toBe(false)
    expect(updateResult.error).toBe('该路径已存在')
  })

  test('should remove config by id', () => {
    const result = manager.add({
      path: '/api/test',
      methods: ['GET'],
      echoEnabled: false,
      responseType: 'json',
      responseContent: '',
    })

    manager.remove(result.id)
    expect(manager.getAll()).toHaveLength(0)
  })

  test('should find config by path', () => {
    manager.add({
      path: '/api/test',
      methods: ['GET'],
      echoEnabled: false,
      responseType: 'json',
      responseContent: '',
    })

    const found = manager.getByPath('/api/test')
    expect(found).not.toBeNull()
    expect(found.path).toBe('/api/test')

    const notFound = manager.getByPath('/api/missing')
    expect(notFound).toBeNull()
  })
})
