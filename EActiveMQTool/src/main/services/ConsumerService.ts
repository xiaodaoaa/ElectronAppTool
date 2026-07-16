import { StompSubscription } from '@stomp/stompjs'
import { connectionManager } from './ConnectionManager'
import { logger } from '../utils/logger'
import dayjs from 'dayjs'
import type { ConsumerConfig, ConsumedMessage, IpcResult } from '../../shared/types'

type MessageListener = (msgs: ConsumedMessage[]) => void
type StopListener = () => void

const BATCH_INTERVAL_MS = 100
const BATCH_MAX_SIZE = 200

class ConsumerService {
  private subscription: StompSubscription | null = null
  private seq = 0
  private receivedCount = 0
  private config: ConsumerConfig | null = null
  private messageListeners: Set<MessageListener> = new Set()
  private stopListeners: Set<StopListener> = new Set()
  private paused = false
  private batchBuffer: ConsumedMessage[] = []
  private batchTimer: ReturnType<typeof setInterval> | null = null

  onMessage(cb: MessageListener): void {
    this.messageListeners.add(cb)
  }

  offMessage(cb: MessageListener): void {
    this.messageListeners.delete(cb)
  }

  onStop(cb: StopListener): void {
    this.stopListeners.add(cb)
  }

  offStop(cb: StopListener): void {
    this.stopListeners.delete(cb)
  }

  private emitMessage(msgs: ConsumedMessage[]): void {
    this.messageListeners.forEach((cb) => cb(msgs))
  }

  private emitStop(): void {
    this.stopListeners.forEach((cb) => cb())
  }

  private startBatchTimer(): void {
    if (this.batchTimer) return
    this.batchTimer = setInterval(() => {
      this.flushBatch()
    }, BATCH_INTERVAL_MS)
  }

  private stopBatchTimer(): void {
    if (this.batchTimer) {
      clearInterval(this.batchTimer)
      this.batchTimer = null
    }
  }

  private flushBatch(): void {
    if (this.batchBuffer.length === 0) return
    const batch = this.batchBuffer.splice(0, this.batchBuffer.length)
    this.emitMessage(batch)
  }

  async start(config: ConsumerConfig): Promise<IpcResult> {
    if (!connectionManager.isConnected()) {
      return { success: false, error: '请先建立连接' }
    }
    if (this.subscription) {
      return { success: false, error: '已有消费进行中，请先停止' }
    }

    const client = connectionManager.getClient()!

    this.config = config
    this.seq = 0
    this.receivedCount = 0
    this.paused = false
    this.batchBuffer = []
    this.startBatchTimer()

    const subscribeHeaders: Record<string, string> = {
      ack: config.ackMode || 'auto'
    }

    if (config.selector) {
      subscribeHeaders['selector'] = config.selector
    }

    if (config.ackMode === 'client-individual' && config.prefetchCount > 0) {
      subscribeHeaders['activemq.prefetchSize'] = String(config.prefetchCount)
    }

    try {
      this.subscription = client.subscribe(
        config.destination,
        (message) => this.handleMessage(message),
        subscribeHeaders
      )
      logger.info(`开始消费：${config.destination}（ack=${config.ackMode}）`)
      return { success: true }
    } catch (err: any) {
      const msg = err?.message || String(err)
      logger.error('启动消费失败：' + msg)
      this.stopBatchTimer()
      return { success: false, error: msg }
    }
  }

  private handleMessage(message: { body: string; headers: Record<string, string>; ack: () => void; nack: () => void }): void {
    if (this.paused) {
      return
    }

    const config = this.config!
    const headers = message.headers || {}

    const matched = this.matchFilter(headers, config)
    if (!matched) {
      if (config.ackMode !== 'auto') {
        try {
          message.ack()
        } catch {
          // ignore
        }
      }
      return
    }

    const consumed = this.buildConsumedMessage(message)

    this.batchBuffer.push(consumed)
    if (this.batchBuffer.length >= BATCH_MAX_SIZE) {
      this.flushBatch()
    }

    logger.info(`收到消息 #${consumed.seq}：${config.destination}，内容：${consumed.body.slice(0, 100)}`)

    if (config.maxReceive > 0 && this.receivedCount >= config.maxReceive) {
      logger.info(`达到最大接收数 ${config.maxReceive}，自动停止`)
      this.stop()
      this.emitStop()
    }
  }

  private buildConsumedMessage(message: { body: string; headers: Record<string, string>; ack: () => void; nack: () => void }): ConsumedMessage {
    const seq = ++this.seq
    this.receivedCount++

    return {
      seq,
      receivedAt: dayjs().format('YYYY-MM-DD HH:mm:ss.SSS'),
      destination: message.headers['destination'] || this.config?.destination || '',
      messageId: message.headers['message-id'] || '',
      body: message.body,
      headers: message.headers,
      acked: this.config?.ackMode === 'auto'
    }
  }

  private matchFilter(headers: Record<string, string>, config: ConsumerConfig): boolean {
    if (config.filterRoutingKey) {
      const destination = headers['destination'] || ''
      if (!this.routingKeyMatch(destination, config.filterRoutingKey)) {
        return false
      }
    }
    if (config.filterByHeaderKey && config.filterByHeaderValue !== undefined) {
      if (headers[config.filterByHeaderKey] !== config.filterByHeaderValue) {
        return false
      }
    }
    return true
  }

  private routingKeyMatch(actual: string, pattern: string): boolean {
    if (!pattern.includes('*') && !pattern.includes('#')) {
      return actual === pattern
    }
    const patternParts = pattern.split('.')
    const actualParts = actual.split('.')
    return this.amqpMatch(actualParts, 0, patternParts, 0)
  }

  private amqpMatch(actual: string[], ai: number, pattern: string[], pi: number): boolean {
    if (pi >= pattern.length) {
      return ai >= actual.length
    }
    if (pattern[pi] === '#') {
      if (pi === pattern.length - 1) return true
      for (let i = ai; i <= actual.length; i++) {
        if (this.amqpMatch(actual, i, pattern, pi + 1)) return true
      }
      return false
    }
    if (ai >= actual.length) return false
    if (pattern[pi] === '*' || pattern[pi] === actual[ai]) {
      return this.amqpMatch(actual, ai + 1, pattern, pi + 1)
    }
    return false
  }

  async pause(): Promise<IpcResult> {
    if (!this.subscription) {
      return { success: false, error: '无活跃消费者' }
    }
    try {
      this.subscription.unsubscribe()
      this.subscription = null
      this.paused = true
      logger.info('消费已暂停')
      return { success: true }
    } catch (err: any) {
      return { success: false, error: err?.message || String(err) }
    }
  }

  async resume(): Promise<IpcResult> {
    if (!this.config) {
      return { success: false, error: '无活跃消费者' }
    }
    if (!this.paused) {
      return { success: false, error: '消费者未暂停' }
    }

    const client = connectionManager.getClient()!
    const config = this.config

    const subscribeHeaders: Record<string, string> = {
      ack: config.ackMode || 'auto'
    }
    if (config.selector) {
      subscribeHeaders['selector'] = config.selector
    }

    try {
      this.subscription = client.subscribe(
        config.destination,
        (message) => this.handleMessage(message),
        subscribeHeaders
      )
      this.paused = false
      logger.info('消费已恢复')
      return { success: true }
    } catch (err: any) {
      return { success: false, error: err?.message || String(err) }
    }
  }

  async stop(): Promise<IpcResult> {
    this.stopBatchTimer()
    this.flushBatch()
    if (this.subscription) {
      try {
        this.subscription.unsubscribe()
      } catch {
        // ignore
      }
      this.subscription = null
    }
    this.paused = false
    logger.info('消费已停止')
    return { success: true }
  }

  async ack(messageId: string): Promise<IpcResult> {
    if (!connectionManager.isConnected()) {
      return { success: false, error: '未连接' }
    }
    const client = connectionManager.getClient()!
    try {
      client.ack({ 'message-id': messageId })
      return { success: true }
    } catch (err: any) {
      return { success: false, error: err?.message || String(err) }
    }
  }

  async nack(messageId: string): Promise<IpcResult> {
    if (!connectionManager.isConnected()) {
      return { success: false, error: '未连接' }
    }
    const client = connectionManager.getClient()!
    try {
      client.nack({ 'message-id': messageId })
      return { success: true }
    } catch (err: any) {
      return { success: false, error: err?.message || String(err) }
    }
  }

  clearOnDisconnect(): void {
    this.stopBatchTimer()
    this.flushBatch()
    if (this.subscription) {
      try {
        this.subscription.unsubscribe()
      } catch {
        // ignore
      }
      this.subscription = null
    }
    this.config = null
    this.paused = false
  }
}

export const consumerService = new ConsumerService()