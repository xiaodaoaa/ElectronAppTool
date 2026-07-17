import log from 'electron-log'

export function initLogger() {
  log.transports.file.level = 'info'
  log.transports.console.level = 'debug'
  log.transports.file.maxSize = 10 * 1024 * 1024
  log.info('===== KafkaTeach 启动 =====')
  return log
}

export { log as logger }