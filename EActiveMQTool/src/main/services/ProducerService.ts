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
    const client = connectionManager.getClient()!

    if (params.format === 'json') {
      try {
        JSON.parse(params.body)
      } catch {
        return { success: false, error: '消息体不是合法 JSON' }
      }
    }

    const headers: Record<string, string> = {
      'content-type': params.contentType || 'text/plain',
      ...params.headers
    }

    if (params.persistent) {
      headers['persistent'] = 'true'
    }
    if (params.priority > 0) {
      headers['priority'] = String(params.priority)
    }
    if (params.expires > 0) {
      headers['expires'] = String(params.expires)
    }

    const total = Math.max(1, params.batch.count)
    let lastMessageId = ''

    try {
      for (let i = 0; i < total; i++) {
        const messageId = uuidv4()
        lastMessageId = messageId
        client.publish({
          destination: params.destination,
          body: params.body,
          headers: { ...headers, 'message-id': messageId }
        })
        this.emitProgress({ current: i + 1, total })
        if (i < total - 1 && params.batch.intervalMs > 0) {
          await new Promise((r) => setTimeout(r, params.batch.intervalMs))
        }
      }

      const historyItem: HistoryItem = {
        time: dayjs().format('YYYY-MM-DD HH:mm:ss'),
        destination: params.destination,
        messageSummary: params.body.slice(0, 50)
      }
      this.history.unshift(historyItem)
      if (this.history.length > 10) {
        this.history = this.history.slice(0, 10)
      }

      logger.info(`发送成功：${params.destination}，共 ${total} 条`)
      return { success: true, messageId: lastMessageId }
    } catch (err: any) {
      const msg = err?.message || String(err)
      logger.error('发送失败：' + msg)
      return { success: false, error: msg }
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