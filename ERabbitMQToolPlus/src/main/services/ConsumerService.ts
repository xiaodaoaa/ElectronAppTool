import * as amqp from 'amqplib'
import { connectionManager } from './ConnectionManager'
import { logger } from '../utils/logger'
import dayjs from 'dayjs'
import type { ConsumerConfig, ConsumedMessage, IpcResult } from '../../shared/types'

type MessageListener = (msg: ConsumedMessage) => void
type StopListener = () => void

class ConsumerService {
  private channel: amqp.Channel | null = null
  private consumerTag: string | null = null
  private seq = 0
  private receivedCount = 0
  private config: ConsumerConfig | null = null
  private queueName: string | null = null
  private pendingMessages: Map<number, amqp.Message> = new Map()
  private messageListeners: Set<MessageListener> = new Set()
  private stopListeners: Set<StopListener> = new Set()
  private paused = false

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

  private emitMessage(msg: ConsumedMessage): void {
    this.messageListeners.forEach((cb) => cb(msg))
  }

  private emitStop(): void {
    this.stopListeners.forEach((cb) => cb())
  }

  async start(config: ConsumerConfig): Promise<IpcResult> {
    if (!connectionManager.isConnected()) {
      return { success: false, error: '请先建立连接' }
    }
    if (this.channel) {
      return { success: false, error: '已有消费进行中，请先停止' }
    }
    const conn = connectionManager.getConnection()!
    try {
      this.channel = await conn.createChannel()
      this.channel.on('error', (err: Error) => {
        logger.error('消费 channel 错误：' + err.message)
      })
      this.channel.on('close', () => {
        if (!this.channel) return
        logger.warn('消费 channel 已关闭')
        this.channel = null
        this.consumerTag = null
        this.pendingMessages.clear()
        this.emitStop()
      })
      this.config = config
      this.queueName = null
      this.seq = 0
      this.receivedCount = 0
      this.paused = false
      this.pendingMessages.clear()

      let queueName = config.queue
      if (config.autoDeclareQueue) {
        const q = await this.channel.assertQueue(config.queue, {
          durable: config.durable,
          exclusive: config.exclusive,
          autoDelete: config.autoDelete
        })
        queueName = q.queue
        if (config.exchange) {
          await this.channel.bindQueue(queueName, config.exchange, config.bindingKey || '')
        }
      } else {
        try {
          await this.channel.checkQueue(config.queue)
        } catch {
          return {
            success: false,
            error: `队列不存在：${config.queue}，请检查名称或开启自动声明`
          }
        }
      }

      await this.channel.prefetch(config.prefetch || 0)

      this.queueName = queueName

      if (config.purgeOnStart) {
        await this.channel.purgeQueue(queueName)
        logger.info(`已清空队列：${queueName}`)
      }

      if (config.mode === 'push') {
        logger.info(`autoAck=${config.autoAck} (${typeof config.autoAck}), noAck=${config.autoAck}`)
        const consumeResult = await this.channel.consume(
          queueName,
          (msg) => this.handleMessage(msg),
          { noAck: config.autoAck }
        )
        this.consumerTag = consumeResult.consumerTag
        logger.info(`开始消费队列：${queueName}（推模式）`)
      }

      return { success: true }
    } catch (err: any) {
      const msg = err?.message || String(err)
      logger.error('启动消费失败：' + msg)
      await this.cleanupChannel()
      return { success: false, error: msg }
    }
  }

  private async handleMessage(msg: amqp.ConsumeMessage | null): Promise<void> {
    if (!msg) {
      logger.warn('消费者被服务器取消')
      return
    }
    if (this.paused) {
      return
    }
    const config = this.config!
    const routingKey = msg.fields.routingKey
    const headers = (msg.properties.headers || {}) as Record<string, any>

    const matched = this.matchFilter(routingKey, headers, config)
    if (!matched) {
      if (!config.autoAck && this.channel) {
        try {
          this.channel.ack(msg)
        } catch (e) {
          logger.warn('过滤消息 ack 失败：' + (e as Error).message)
        }
      }
      return
    }

    const consumed = this.buildConsumedMessage(msg)

    this.emitMessage(consumed)
    logger.info(`收到消息 #${consumed.seq}：${routingKey}，内容：${consumed.content.slice(0, 100)}`)

    if (config.maxReceive > 0 && this.receivedCount >= config.maxReceive) {
      logger.info(`达到最大接收数 ${config.maxReceive}，自动停止`)
      await this.stop()
      this.emitStop()
    }
  }

  private buildConsumedMessage(msg: amqp.Message): ConsumedMessage {
    const seq = ++this.seq
    this.receivedCount++
    if (this.config && !this.config.autoAck) {
      this.pendingMessages.set(msg.fields.deliveryTag, msg)
    }
    return {
      seq,
      receivedAt: dayjs().format('YYYY-MM-DD HH:mm:ss.SSS'),
      exchange: msg.fields.exchange || '',
      routingKey: msg.fields.routingKey,
      messageId: msg.properties.messageId as string | undefined,
      deliveryTag: msg.fields.deliveryTag,
      content: msg.content.toString('utf8'),
      properties: {
        contentType: msg.properties.contentType as string | undefined,
        contentEncoding: msg.properties.contentEncoding as string | undefined,
        deliveryMode: msg.properties.deliveryMode as 1 | 2 | undefined,
        priority: msg.properties.priority as number | undefined,
        expiration: msg.properties.expiration as string | undefined,
        headers: msg.properties.headers as Record<string, any> | undefined
      }
    }
  }

  private matchFilter(routingKey: string, headers: Record<string, any>, config: ConsumerConfig): boolean {
    if (config.filterByRoutingKey) {
      if (!this.routingKeyMatch(routingKey, config.filterByRoutingKey)) {
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
    if (!this.channel || !this.consumerTag) {
      return { success: false, error: '无活跃的推模式消费者' }
    }
    try {
      await this.channel.cancel(this.consumerTag)
      this.consumerTag = null
      this.paused = true
      logger.info('消费已暂停')
      return { success: true }
    } catch (err: any) {
      return { success: false, error: err?.message || String(err) }
    }
  }

  async resume(): Promise<IpcResult> {
    if (!this.channel || !this.config) {
      return { success: false, error: '无活跃消费者' }
    }
    if (!this.paused) {
      return { success: false, error: '消费者未暂停' }
    }
    try {
      const config = this.config
      const queueName = this.queueName || config.queue
      const consumeResult = await this.channel.consume(
        queueName,
        (msg) => this.handleMessage(msg),
        { noAck: config.autoAck }
      )
      this.consumerTag = consumeResult.consumerTag
      this.paused = false
      logger.info('消费已恢复')
      return { success: true }
    } catch (err: any) {
      return { success: false, error: err?.message || String(err) }
    }
  }

  async stop(): Promise<IpcResult> {
    await this.cleanupChannel()
    this.pendingMessages.clear()
    this.paused = false
    logger.info('消费已停止，清空未确认列表')
    return { success: true }
  }

  private async cleanupChannel(): Promise<void> {
    if (this.consumerTag && this.channel) {
      try {
        await this.channel.cancel(this.consumerTag)
      } catch {
        // ignore
      }
      this.consumerTag = null
    }
    if (this.channel) {
      try {
        await this.channel.close()
      } catch {
        // ignore
      }
      this.channel = null
    }
  }

  async pull(count: number): Promise<ConsumedMessage[]> {
    if (!this.channel || !this.config) {
      return []
    }
    if (this.config.mode !== 'pull') {
      return []
    }
    const results: ConsumedMessage[] = []
    for (let i = 0; i < count; i++) {
      const msg = await this.channel.get(this.config.queue, { noAck: this.config.autoAck })
      if (!msg) break
      results.push(this.buildConsumedMessage(msg))
    }
    if (results.length > 0) {
      results.forEach((m) => this.emitMessage(m))
      logger.info(`拉取 ${results.length} 条消息`)
    }
    if (this.config.maxReceive > 0 && this.receivedCount >= this.config.maxReceive) {
      await this.stop()
      this.emitStop()
    }
    return results
  }

  async ack(deliveryTag: number): Promise<IpcResult> {
    if (!this.channel) {
      return { success: false, error: '无活跃 channel' }
    }
    const msg = this.pendingMessages.get(deliveryTag)
    if (!msg) {
      return { success: false, error: '消息不在未确认列表中' }
    }
    try {
      this.channel.ack(msg)
      this.pendingMessages.delete(deliveryTag)
      return { success: true }
    } catch (err: any) {
      return { success: false, error: err?.message || String(err) }
    }
  }

  async nack(deliveryTag: number, requeue: boolean): Promise<IpcResult> {
    if (!this.channel) {
      return { success: false, error: '无活跃 channel' }
    }
    const msg = this.pendingMessages.get(deliveryTag)
    if (!msg) {
      return { success: false, error: '消息不在未确认列表中' }
    }
    try {
      this.channel.nack(msg, false, requeue)
      this.pendingMessages.delete(deliveryTag)
      logger.info(`消息 #${deliveryTag} 已 nack，requeue=${requeue}`)
      return { success: true }
    } catch (err: any) {
      return { success: false, error: err?.message || String(err) }
    }
  }

  clearOnDisconnect(): void {
    this.cleanupChannel()
    this.pendingMessages.clear()
    this.config = null
    this.queueName = null
    this.paused = false
  }
}

export const consumerService = new ConsumerService()
