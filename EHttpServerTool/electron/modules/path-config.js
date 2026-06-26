// electron/modules/path-config.js
const fs = require('fs')
const path = require('path')

let idCounter = 0

class PathConfigManager {
  constructor(dataDir) {
    this.configs = []
    this.filePath = path.join(dataDir, 'paths.json')
    this._load()
  }

  _load() {
    try {
      if (fs.existsSync(this.filePath)) {
        const data = fs.readFileSync(this.filePath, 'utf-8')
        this.configs = JSON.parse(data)
        for (const c of this.configs) {
          const num = parseInt(c.id.replace('path-', ''), 10)
          if (num >= idCounter) idCounter = num
        }
      }
    } catch (_) {
      this.configs = []
    }
  }

  _save() {
    try {
      const dir = path.dirname(this.filePath)
      if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true })
      fs.writeFileSync(this.filePath, JSON.stringify(this.configs, null, 2), 'utf-8')
    } catch (_) {
      // Silently fail
    }
  }

  _nextId() {
    return `path-${++idCounter}`
  }

  add(config) {
    if (!config.path || !config.path.startsWith('/')) {
      return { success: false, error: '路径必须以 / 开头' }
    }
    if (this.configs.some(c => c.path === config.path)) {
      return { success: false, error: '该路径已存在' }
    }
    const id = this._nextId()
    const newConfig = { id, ...config }
    this.configs.push(newConfig)
    this._save()
    return { success: true, id }
  }

  update(config) {
    const index = this.configs.findIndex(c => c.id === config.id)
    if (index === -1) {
      return { success: false, error: '配置不存在' }
    }
    if (this.configs.some(c => c.path === config.path && c.id !== config.id)) {
      return { success: false, error: '该路径已存在' }
    }
    this.configs[index] = config
    this._save()
    return { success: true }
  }

  remove(id) {
    const index = this.configs.findIndex(c => c.id === id)
    if (index !== -1) {
      this.configs.splice(index, 1)
      this._save()
    }
    return { success: true }
  }

  getAll() {
    return [...this.configs]
  }

  getByPath(reqPath) {
    return this.configs.find(c => c.path === reqPath) || null
  }
}

module.exports = { PathConfigManager }
