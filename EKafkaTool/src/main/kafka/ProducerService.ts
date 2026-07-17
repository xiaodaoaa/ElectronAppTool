import type { Kafka, Producer } from 'kafkajs'
import type { ProduceRequest, ProduceResult } from './types'
import { logger } from '../util/logger'

export class ProducerService {
  private producer: Producer | null = null
  private batchTasks = new Map<string, AbortController>()
  private sendSeq = 0

  constructor(
    private getKafka: () => Kafka | null,
    private push: (channel: string, payload: unknown) => void,
  ) {}

  private async getProducer(): Promise<Producer> {
    if (this.producer) return this.producer
    const kafka = this.getKafka()
    if (!kafka) throw new Error('未连接 Kafka')
    this.producer = kafka.producer({ allowAutoTopicCreation: false })
    await this.producer.connect()
    logger.info('Producer 已连接')
    return this.producer
  }

  private renderTemplate(value: string, seq: number): string {
    return value
      .replace(/\{\{seq\}\}/g, String(seq))
      .replace(/\{\{ts\}\}/g, String(Date.now()))
      .replace(/\{\{rand\}\}/g, String(Math.floor(Math.random() * 10000)))
  }

  private buildMessages(req: ProduceRequest, seq: number) {
    const value = this.renderTemplate(req.value, seq)
    const messages: Array<{ key?: string; value: string; partition?: number; headers?: Record<string, string> }> = []
    messages.push({
      key: req.key ? this.renderTemplate(req.key, seq) : undefined,
      value,
      partition: req.partition,
      headers: req.headers,
    })
    return messages
  }

  async send(req: ProduceRequest): Promise<ProduceResult> {
    const start = Date.now()
    const producer = await this.getProducer()
    this.sendSeq++
    const messages = this.buildMessages(req, this.sendSeq)
    const result = await producer.send({
      topic: req.topic,
      messages,
      acks: req.acks ?? -1,
    })
    const latencyMs = Date.now() - start
    const meta = result[0]
    return {
      topic: req.topic,
      partition: meta.partition,
      offset: (meta as Record<string, unknown>).baseOffset as string ?? '0',
      timestamp: String(Date.now()),
      latencyMs,
    }
  }

  async sendBatch(
    req: ProduceRequest,
    count: number,
    intervalMs: number,
    keyStrategy: string,
  ): Promise<string> {
    const taskId = crypto.randomUUID()
    const controller = new AbortController()
    this.batchTasks.set(taskId, controller)

    const producer = await this.getProducer()

    const run = async () => {
      for (let i = 0; i < count; i++) {
        if (controller.signal.aborted) break

        const start = Date.now()
        let key = req.key
        if (keyStrategy === 'random') {
          key = `user${Math.floor(Math.random() * 100)}`
        } else if (keyStrategy === 'round-robin') {
          key = `user${i % 5}`
        }

        const batchReq: ProduceRequest = { ...req, key }
        const messages = this.buildMessages(batchReq, i + 1)

        try {
          const result = await producer.send({
            topic: req.topic,
            messages,
            acks: req.acks ?? -1,
          })
          const meta = result[0]
          this.push('event:produceAck', {
            topic: req.topic,
            partition: meta.partition,
            offset: (meta as Record<string, unknown>).baseOffset as string ?? '0',
            timestamp: String(Date.now()),
            latencyMs: Date.now() - start,
          })
        } catch (err) {
          logger.error('批量发送失败:', err)
        }

        if (intervalMs > 0 && i < count - 1) {
          await new Promise(resolve => setTimeout(resolve, intervalMs))
        }
      }
      this.batchTasks.delete(taskId)
    }

    run().catch(err => logger.error('批量任务异常:', err))
    return taskId
  }

  stopBatch(taskId: string): void {
    const controller = this.batchTasks.get(taskId)
    if (controller) {
      controller.abort()
      this.batchTasks.delete(taskId)
      logger.info(`批量任务已停止: ${taskId}`)
    }
  }

  async disconnect(): Promise<void> {
    for (const [, ctrl] of this.batchTasks) {
      ctrl.abort()
    }
    this.batchTasks.clear()
    if (this.producer) {
      await this.producer.disconnect().catch(() => {})
      this.producer = null
    }
  }
}