// electron/modules/logger.js
const fs = require('fs')
const path = require('path')
const { EventEmitter } = require('events')

const LOG_LEVELS = {
  TRACE: 100,
  DEBUG: 200,
  INFO: 300,
  WARN: 400,
  ERROR: 500,
  FATAL: 600,
}

const LEVEL_NAMES = {
  100: 'TRACE',
  200: 'DEBUG',
  300: 'INFO',
  400: 'WARN',
  500: 'ERROR',
  600: 'FATAL',
}

// ANSI 颜色代码
const COLORS = {
  TRACE: '\x1b[90m',   // 灰色
  DEBUG: '\x1b[36m',   // 青色
  INFO: '\x1b[32m',    // 绿色
  WARN: '\x1b[33m',    // 黄色
  ERROR: '\x1b[31m',   // 红色
  FATAL: '\x1b[35m',   // 紫色
  RESET: '\x1b[0m',
}

const MAX_FILE_SIZE = 5 * 1024 * 1024 // 5MB
const MAX_BACKUPS = 3

class Logger extends EventEmitter {
  constructor(options = {}) {
    super()
    this.moduleName = options.name || 'app'
    // 程序所在目录下的 logs/
    const appDir = options.appDir || path.dirname(process.execPath || __dirname)
    this.logDir = path.join(appDir, 'logs')
    this.logFile = path.join(this.logDir, 'app.log')
    this.level = options.level || LOG_LEVELS.DEBUG

    // 确保日志目录存在
    if (!fs.existsSync(this.logDir)) {
      fs.mkdirSync(this.logDir, { recursive: true })
    }
  }

  _formatTimestamp() {
    const now = new Date()
    const year = now.getFullYear()
    const month = String(now.getMonth() + 1).padStart(2, '0')
    const day = String(now.getDate()).padStart(2, '0')
    const hours = String(now.getHours()).padStart(2, '0')
    const minutes = String(now.getMinutes()).padStart(2, '0')
    const seconds = String(now.getSeconds()).padStart(2, '0')
    const ms = String(now.getMilliseconds()).padStart(3, '0')
    return `${year}-${month}-${day} ${hours}:${minutes}:${seconds}.${ms}`
  }

  _formatMessage(level, message) {
    const timestamp = this._formatTimestamp()
    return `[${level}] [${timestamp}] [${this.moduleName}] ${message}`
  }

  _rotateFile() {
    try {
      if (!fs.existsSync(this.logFile)) return

      const stats = fs.statSync(this.logFile)
      if (stats.size < MAX_FILE_SIZE) return

      // 生成备份文件名
      const now = new Date()
      const timestamp = now.getFullYear().toString() +
        String(now.getMonth() + 1).padStart(2, '0') +
        String(now.getDate()).padStart(2, '0') +
        String(now.getHours()).padStart(2, '0') +
        String(now.getMinutes()).padStart(2, '0') +
        String(now.getSeconds()).padStart(2, '0')

      // 找到下一个序号
      let seq = 1
      while (fs.existsSync(path.join(this.logDir, `app-${timestamp}-${seq}.log`))) {
        seq++
      }

      const backupFile = path.join(this.logDir, `app-${timestamp}-${seq}.log`)
      fs.renameSync(this.logFile, backupFile)

      // 清理旧备份
      this._cleanupBackups()
    } catch (err) {
      console.error('日志轮转失败:', err.message)
    }
  }

  _cleanupBackups() {
    try {
      const files = fs.readdirSync(this.logDir)
        .filter(f => f.startsWith('app-') && f.endsWith('.log') && f !== 'app.log')
        .sort()

      while (files.length > MAX_BACKUPS) {
        const oldest = files.shift()
        fs.unlinkSync(path.join(this.logDir, oldest))
      }
    } catch (err) {
      console.error('清理备份失败:', err.message)
    }
  }

  _writeToFile(formattedMessage) {
    try {
      this._rotateFile()
      fs.appendFileSync(this.logFile, formattedMessage + '\n', 'utf-8')
    } catch (err) {
      // 文件写入失败时只输出到控制台
      console.error('日志文件写入失败:', err.message)
    }
  }

  _log(level, message, ...args) {
    const levelNum = LOG_LEVELS[level]
    if (levelNum < this.level) return

    // 处理消息格式化
    let formattedMessage = message
    if (args.length > 0) {
      formattedMessage = message.replace(/\{(\d+)\}/g, (match, index) => {
        const i = parseInt(index, 10)
        return i < args.length ? String(args[i]) : match
      })
      // 追加未格式化的参数
      const usedIndices = new Set()
      message.replace(/\{(\d+)\}/g, (_, index) => usedIndices.add(parseInt(index, 10)))
      const remaining = args.filter((_, i) => !usedIndices.has(i))
      if (remaining.length > 0) {
        formattedMessage += ' ' + remaining.map(a => {
          if (typeof a === 'object') {
            try { return JSON.stringify(a) } catch { return String(a) }
          }
          return String(a)
        }).join(' ')
      }
    }

    const fullMessage = this._formatMessage(level, formattedMessage)

    // 控制台输出（带颜色）
    const color = COLORS[level] || ''
    const consoleMessage = `${color}${fullMessage}${COLORS.RESET}`

    if (levelNum >= LOG_LEVELS.ERROR) {
      console.error(consoleMessage)
    } else {
      console.log(consoleMessage)
    }

    // 文件输出（不带颜色）
    this._writeToFile(fullMessage)

    // 发送事件（用于 DevTools 转发）
    this.emit('log', {
      level,
      levelNum,
      message: formattedMessage,
      timestamp: Date.now(),
      module: this.moduleName,
    })
  }

  trace(message, ...args) {
    this._log('TRACE', message, ...args)
  }

  debug(message, ...args) {
    this._log('DEBUG', message, ...args)
  }

  info(message, ...args) {
    this._log('INFO', message, ...args)
  }

  warn(message, ...args) {
    this._log('WARN', message, ...args)
  }

  error(message, ...args) {
    this._log('ERROR', message, ...args)
  }

  fatal(message, ...args) {
    this._log('FATAL', message, ...args)
  }
}

function createLogger(options) {
  return new Logger(options)
}

module.exports = { Logger, createLogger, LOG_LEVELS }
