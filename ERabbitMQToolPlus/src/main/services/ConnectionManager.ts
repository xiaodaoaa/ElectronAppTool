import * as amqp from 'amqplib'
import type { ConnectionConfig, ConnectionStatus, ConnectionStatusInfo } from '../../shared/types'
import { buildConnectOptions, validateConnectionConfig } from '../utils/ssl'
import { logger } from '../utils/logger'

type StatusListener = (info: ConnectionStatusInfo) => void

class ConnectionManager {
  private connection: amqp.ChannelModel | null = null
  private status: ConnectionStatus = 'disconnected'
  private currentConfig: ConnectionConfig | null = null
  private listeners: Set<StatusListener> = new Set()

  private buildStatusInfo(): ConnectionStatusInfo {
    if (this.status === 'connected' && this.currentConfig) {
      const proto = this.currentConfig.sslEnabled ? 'amqps' : 'amqp'
      const display = `${proto}://${this.currentConfig.username}@${this.currentConfig.host}:${this.currentConfig.port}/${this.currentConfig.vhost || '/'}`
      return {
        status: this.status,
        display,
        sslEnabled: this.currentConfig.sslEnabled,
        rejectUnauthorized: this.currentConfig.rejectUnauthorized
      }
    }
    return {
      status: this.status,
      display: this.status === 'connecting' ? '连接中...' : '未连接',
      sslEnabled: false,
      rejectUnauthorized: false
    }
  }

  private notifyListeners(): void {
    const info = this.buildStatusInfo()
    this.listeners.forEach((cb) => cb(info))
  }

  private setStatus(status: ConnectionStatus): void {
    this.status = status
    this.notifyListeners()
  }

  onStatusChange(cb: StatusListener): void {
    this.listeners.add(cb)
  }

  offStatusChange(cb: StatusListener): void {
    this.listeners.delete(cb)
  }

  async connect(config: ConnectionConfig): Promise<{ success: boolean; error?: string }> {
    const validationError = validateConnectionConfig(config)
    if (validationError) {
      return { success: false, error: validationError }
    }
    if (this.connection) {
      await this.disconnect()
    }
    this.currentConfig = config
    this.setStatus('connecting')
    try {
      const options = buildConnectOptions(config)
      this.connection = await amqp.connect(options as any)
      this.connection.on('close', () => {
        if (this.status === 'error') return
        logger.warn('连接已关闭')
        this.connection = null
        this.setStatus('disconnected')
      })
      this.connection.on('error', (err: Error) => {
        logger.error('连接错误：' + err.message)
        this.connection = null
        this.setStatus('error')
      })
      this.setStatus('connected')
      logger.info(`已连接 ${config.host}:${config.port}`)
      return { success: true }
    } catch (err: any) {
      this.connection = null
      this.setStatus('error')
      const msg = this.humanizeError(err)
      logger.error('连接失败：' + msg)
      return { success: false, error: msg }
    }
  }

  async test(config: ConnectionConfig): Promise<{ success: boolean; error?: string }> {
    const validationError = validateConnectionConfig(config)
    if (validationError) {
      return { success: false, error: validationError }
    }
    try {
      const options = buildConnectOptions(config)
      const conn = await amqp.connect(options as any)
      await conn.close()
      logger.info(`测试连接成功 ${config.host}:${config.port}`)
      return { success: true }
    } catch (err: any) {
      const msg = this.humanizeError(err, config)
      logger.error('测试连接失败：' + msg)
      return { success: false, error: msg }
    }
  }

  async disconnect(): Promise<{ success: boolean; error?: string }> {
    if (!this.connection) {
      return { success: true }
    }
    try {
      await this.connection.close()
    } catch (err: any) {
      logger.warn('关闭连接时出错：' + err.message)
    }
    this.connection = null
    this.currentConfig = null
    this.setStatus('disconnected')
    logger.info('已断开连接')
    return { success: true }
  }

  getConnection(): amqp.ChannelModel | null {
    return this.connection
  }

  isConnected(): boolean {
    return this.status === 'connected' && this.connection !== null
  }

  private humanizeError(err: any, config?: ConnectionConfig | null): string {
    const code = err?.code || ''
    const msg = err?.message || String(err)
    const host = config?.host || this.currentConfig?.host || ''
    if (code === 'ECONNREFUSED' || msg.includes('ECONNREFUSED')) {
      return `无法连接到服务器：${host} 拒绝连接`
    }
    if (code === 'ENOTFOUND' || msg.includes('ENOTFOUND')) {
      return `主机名无法解析：${host}`
    }
    if (code === 'ETIMEDOUT' || msg.includes('ETIMEDOUT')) {
      return '连接超时，请检查网络或主机地址'
    }
    if (msg.includes('ACCESS_REFUSED') || msg.includes('Handshake')) {
      return '认证失败：用户名/密码错误或无权限'
    }
    if (msg.includes('SSL') || msg.includes('certificate') || msg.includes('CERT')) {
      return 'SSL/证书错误：' + msg
    }
    return msg
  }
}

export const connectionManager = new ConnectionManager()
