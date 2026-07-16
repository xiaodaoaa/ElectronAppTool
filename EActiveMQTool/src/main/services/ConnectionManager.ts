import { Client, StompConfig } from '@stomp/stompjs'
import type { ConnectionConfig, ConnectionStatus, ConnectionStatusInfo } from '../../shared/types'
import { logger } from '../utils/logger'

type StatusListener = (info: ConnectionStatusInfo) => void

class ConnectionManager {
  private client: Client | null = null
  private status: ConnectionStatus = 'disconnected'
  private currentConfig: ConnectionConfig | null = null
  private listeners: Set<StatusListener> = new Set()

  private buildStatusInfo(): ConnectionStatusInfo {
    if (this.status === 'connected' && this.currentConfig) {
      const proto = this.currentConfig.sslEnabled ? 'wss' : 'ws'
      const display = `${proto}://${this.currentConfig.username}@${this.currentConfig.host}:${this.currentConfig.port}`
      return {
        status: this.status,
        display,
        sslEnabled: this.currentConfig.sslEnabled
      }
    }
    return {
      status: this.status,
      display: this.status === 'connecting' ? '连接中...' : '未连接',
      sslEnabled: false
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
    if (this.client) {
      await this.disconnect()
    }
    this.currentConfig = config
    this.setStatus('connecting')

    return new Promise((resolve) => {
      const proto = config.sslEnabled ? 'wss' : 'ws'
      const brokerURL = `${proto}://${config.host}:${config.port}/ws`

      const stompConfig: StompConfig = {
        brokerURL,
        connectHeaders: {
          login: config.username,
          passcode: config.password
        },
        heartbeatOutgoing: config.heartbeatOutgoing || 10000,
        heartbeatIncoming: config.heartbeatIncoming || 10000,
        reconnectDelay: 0,
        debug: (msg: string) => {
          logger.info('[STOMP] ' + msg)
        },

        onConnect: () => {
          this.setStatus('connected')
          logger.info(`已连接 ${config.host}:${config.port}`)
          resolve({ success: true })
        },

        onDisconnect: () => {
          logger.warn('连接已关闭')
          this.client = null
          this.setStatus('disconnected')
        },

        onStompError: (frame) => {
          const msg = frame.headers['message'] || 'STOMP 错误'
          logger.error('STOMP 错误：' + msg)
          this.client = null
          this.setStatus('error')
          resolve({ success: false, error: this.humanizeError(new Error(msg)) })
        },

        onWebSocketClose: (evt) => {
          if (this.status === 'error') return
          logger.warn('WebSocket 已关闭')
          this.client = null
          this.setStatus('disconnected')
        }
      }

      this.client = new Client(stompConfig)
      this.client.activate()
    })
  }

  async test(config: ConnectionConfig): Promise<{ success: boolean; error?: string }> {
    return new Promise((resolve) => {
      const proto = config.sslEnabled ? 'wss' : 'ws'
      const brokerURL = `${proto}://${config.host}:${config.port}/ws`

      const testClient = new Client({
        brokerURL,
        connectHeaders: {
          login: config.username,
          passcode: config.password
        },
        heartbeatOutgoing: config.heartbeatOutgoing || 10000,
        heartbeatIncoming: config.heartbeatIncoming || 10000,
        reconnectDelay: 0,

        onConnect: () => {
          testClient.deactivate()
          logger.info(`测试连接成功 ${config.host}:${config.port}`)
          resolve({ success: true })
        },

        onStompError: (frame) => {
          const msg = frame.headers['message'] || 'STOMP 错误'
          testClient.deactivate()
          logger.error('测试连接失败：' + msg)
          resolve({ success: false, error: this.humanizeError(new Error(msg), config) })
        },

        onWebSocketClose: () => {
          resolve({ success: false, error: 'WebSocket 连接关闭' })
        }
      })

      testClient.activate()
    })
  }

  async disconnect(): Promise<{ success: boolean; error?: string }> {
    if (!this.client) {
      return { success: true }
    }
    try {
      this.client.deactivate()
    } catch (err: any) {
      logger.warn('关闭连接时出错：' + err.message)
    }
    this.client = null
    this.currentConfig = null
    this.setStatus('disconnected')
    logger.info('已断开连接')
    return { success: true }
  }

  getClient(): Client | null {
    return this.client
  }

  isConnected(): boolean {
    return this.status === 'connected' && this.client !== null
  }

  private humanizeError(err: any, config?: ConnectionConfig | null): string {
    const msg = err?.message || String(err)
    const host = config?.host || this.currentConfig?.host || ''

    if (msg.includes('ECONNREFUSED')) {
      return `无法连接到服务器：${host} 拒绝连接`
    }
    if (msg.includes('ENOTFOUND')) {
      return `主机名无法解析：${host}`
    }
    if (msg.includes('ETIMEDOUT') || msg.includes('timeout')) {
      return '连接超时，请检查网络或主机地址'
    }
    if (msg.includes('401') || msg.includes('Unauthorized') || msg.includes('auth')) {
      return '认证失败：用户名/密码错误或无权限'
    }
    if (msg.includes('SSL') || msg.includes('certificate') || msg.includes('TLS')) {
      return 'SSL/TLS 错误：' + msg
    }
    return msg
  }
}

export const connectionManager = new ConnectionManager()