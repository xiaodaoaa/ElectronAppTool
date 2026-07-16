import * as fs from 'fs'
import * as path from 'path'
import type { ConnectionConfig } from '../../shared/types'
import { logger } from './logger'

export interface AmqpConnectOptions {
  protocol: 'amqp' | 'amqps'
  hostname: string
  port: number
  username: string
  password: string
  vhost: string
  timeout?: number
  tls?: {
    rejectUnauthorized?: boolean
    ca?: Buffer[]
    cert?: Buffer
    key?: Buffer
    passphrase?: string
  }
}

export function validateConnectionConfig(config: ConnectionConfig): string | null {
  if (config.sslEnabled && !config.caPath && config.rejectUnauthorized) {
    return '请上传 CA 证书或关闭证书认证'
  }
  if (config.certPath && !config.keyPath) {
    return '上传客户端证书时需同时填写私钥'
  }
  if (config.sslEnabled && config.rejectUnauthorized && config.caPath) {
    if (!fs.existsSync(config.caPath)) {
      return `CA 证书路径无效：文件不存在 (${config.caPath})`
    }
  }
  if (config.certPath && !fs.existsSync(config.certPath)) {
    return `客户端证书路径无效：文件不存在 (${config.certPath})`
  }
  if (config.keyPath && !fs.existsSync(config.keyPath)) {
    return `客户端私钥路径无效：文件不存在 (${config.keyPath})`
  }
  return null
}

export function buildConnectOptions(config: ConnectionConfig): AmqpConnectOptions {
  const options: AmqpConnectOptions = {
    protocol: config.sslEnabled ? 'amqps' : 'amqp',
    hostname: config.host,
    port: config.port,
    username: config.username,
    password: config.password,
    vhost: config.vhost || '/',
    timeout: config.timeout || 5000
  }

  if (config.sslEnabled) {
    const tls: AmqpConnectOptions['tls'] = {}
    if (!config.rejectUnauthorized) {
      tls.rejectUnauthorized = false
      process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0'
      logger.warn('SSL 连接已关闭证书认证，存在中间人风险')
    } else {
      process.env.NODE_TLS_REJECT_UNAUTHORIZED = '1'
      tls.rejectUnauthorized = true
      if (config.caPath) {
        tls.ca = [fs.readFileSync(path.resolve(config.caPath))]
      }
      if (config.certPath && config.keyPath) {
        tls.cert = fs.readFileSync(path.resolve(config.certPath))
        tls.key = fs.readFileSync(path.resolve(config.keyPath))
      }
      if (config.passphrase) {
        tls.passphrase = config.passphrase
      }
    }
    options.tls = tls
  }

  return options
}
