import * as amqp from 'amqplib'
import { connectionManager } from './ConnectionManager'
import { logger } from '../utils/logger'
import { v4 as uuidv4 } from 'uuid'
import dayjs from 'dayjs'
import type { SendParams, SendResult, HistoryItem, ProgressInfo } from '../../shared/types'

type ProgressListener = (p: ProgressInfo) => void

class ProducerService {
  private history: HistoryItem[] = []
  private progressListeners: Set<ProgressListener> = new Set()

  onProgress(cb: ProgressListener): void {
    this.progressListeners.add(cb)
  }

  offProgress(cb: ProgressListener): void {
    this.progressListeners.delete(cb)
  }

  private emitProgress(p: ProgressInfo): void {
    this.progressListeners.forEach((cb) => cb(p))
  }

  async send(params: SendParams): Promise<SendResult> {
    if (!connectionManager.isConnected()) {
      return { success: false, error: '请先建立连接' }
    }
    const conn = connectionManager.getConnection()!
    let channel: amqp.Channel | null = null
    try {
      channel = await conn.createChannel()

      if (params.autoDeclare && params.exchange) {
        await channel.assertExchange(params.exchange, params.exchangeType || 'direct', {
          durable: params.durable
        })
      } else if (params.exchange) {
        try {
          await channel.checkExchange(params.exchange)
        } catch {
          return {
            success: false,
            error: `交换机不存在：${params.exchange}，请检查名称或开启自动声明`
          }
        }
      }

      if (params.format === 'json') {
        try {
          JSON.parse(params.message)
        } catch {
          return { success: false, error: '消息体不是合法 JSON' }
        }
      }

      const content = Buffer.from(params.message, 'utf8')
      const options: amqp.Options.Publish = {
        contentType: params.properties.contentType,
        contentEncoding: params.properties.contentEncoding,
        deliveryMode: params.properties.deliveryMode,
        priority: params.properties.priority,
        expiration: params.properties.expiration,
        headers: params.properties.headers
      }

      const total = Math.max(1, params.batch.count)
      for (let i = 0; i < total; i++) {
        options.messageId = uuidv4()
        channel.publish(params.exchange || '', params.routingKey, content, options)
        this.emitProgress({ current: i + 1, total })
        if (i < total - 1 && params.batch.intervalMs > 0) {
          await new Promise((r) => setTimeout(r, params.batch.intervalMs))
        }
      }

      const historyItem: HistoryItem = {
        time: dayjs().format('YYYY-MM-DD HH:mm:ss'),
        exchange: params.exchange || '(默认)',
        routingKey: params.routingKey,
        messageSummary: params.message.slice(0, 50)
      }
      this.history.unshift(historyItem)
      if (this.history.length > 10) {
        this.history = this.history.slice(0, 10)
      }

      logger.info(`发送成功：${params.exchange || '(默认)'} / ${params.routingKey}，共 ${total} 条`)
      return { success: true, messageId: options.messageId }
    } catch (err: any) {
      const msg = err?.message || String(err)
      logger.error('发送失败：' + msg)
      return { success: false, error: msg }
    } finally {
      if (channel) {
        try {
          await channel.close()
        } catch {
          // ignore
        }
      }
    }
  }

  getHistory(): HistoryItem[] {
    return this.history
  }

  clearHistoryOnDisconnect(): void {
    this.history = []
  }
}

export const producerService = new ProducerService()
