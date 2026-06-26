// electron/modules/request-logger.js

const MAX_LOGS = 1000
let logIdCounter = 0

class RequestLogger {
  constructor() {
    this.logs = []
    this.listeners = []
  }

  _nextId() {
    return `log-${++logIdCounter}`
  }

  log(entry) {
    const logEntry = {
      id: this._nextId(),
      timestamp: Date.now(),
      clientIp: entry.clientIp || 'unknown',
      method: entry.method,
      path: entry.path,
      headers: entry.headers || {},
      query: entry.query || {},
      body: entry.body || '',
    }
    this.logs.push(logEntry)
    // Cap at MAX_LOGS
    if (this.logs.length > MAX_LOGS) {
      this.logs = this.logs.slice(this.logs.length - MAX_LOGS)
    }
    // Notify listeners
    for (const listener of this.listeners) {
      try {
        listener(logEntry)
      } catch (_) {
        // Ignore listener errors
      }
    }
    return logEntry
  }

  getAll() {
    return [...this.logs]
  }

  clear() {
    this.logs = []
    return { success: true }
  }

  onNewRequest(callback) {
    this.listeners.push(callback)
    return () => {
      const index = this.listeners.indexOf(callback)
      if (index !== -1) this.listeners.splice(index, 1)
    }
  }
}

module.exports = { RequestLogger }
