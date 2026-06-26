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
})
