import type { Kafka, Consumer } from 'kafkajs'
import type { ConsumerOptions, ConsumerInstanceState, MessageRecord } from './types'
import { MessageBuffer } from '../util/messageBuffer'
import { logger } from '../util/logger'

interface InstanceEntry {
  consumer: Consumer
  opts: ConsumerOptions
  consumed: number
  buffer: MessageBuffer
  status: ConsumerInstanceState['status']
  assignments: Array<{ topic: string; partitions: number[] }>
  error?: string
}

export class ConsumerService {
  private instances = new Map<string, InstanceEntry>()

  constructor(
    private getKafka: () => Kafka | null,
    private push: (channel: string, payload: unknown) => void,
  ) {}

  create(opts: ConsumerOptions): string {
    const kafka = this.getKafka()
    if (!kafka) throw new Error('未连接 Kafka')

    const id = crypto.randomUUID()
    logger.info('ConsumerService.create 开始:', { alias: opts.alias, groupId: opts.groupId, topics: opts.topics })
    const consumer = kafka.consumer({
      groupId: opts.groupId,
      sessionTimeout: 10000,
      heartbeatInterval: 3000,
    })

    const entry: InstanceEntry = {
      consumer,
      opts,
      consumed: 0,
      buffer: new MessageBuffer((batch) => {
        this.push('event:consumerMessage', batch)
      }),
      status: 'created',
      assignments: [],
    }

    this.instances.set(id, entry)
    logger.info(`消费者实例已创建: ${opts.alias} (${id})`)
    return id
  }

  async start(id: string): Promise<void> {
    const e = this.mustGet(id)
    await e.consumer.connect()
    await e.consumer.subscribe({
      topics: e.opts.topics,
      fromBeginning: e.opts.fromBeginning,
    })

    e.consumer.on(e.consumer.events.GROUP_JOIN, (ev: { payload: { groupId: string; generationId: number; memberAssignment: Record<string, number[]> } }) => {
      const assignments = Object.entries(ev.payload.memberAssignment).map(([topic, partitions]) => ({
        topic,
        partitions,
      }))
      e.assignments = assignments
      this.push('event:rebalance', {
        groupId: ev.payload.groupId,
        generation: ev.payload.generationId,
        assignments: ev.payload.memberAssignment,
        ts: Date.now(),
      })
      this.pushState(id)
    })

    await e.consumer.run({
      autoCommit: e.opts.autoCommit,
      eachMessage: async ({ topic, partition, message }) => {
        e.consumed++
        logger.info(`收到消息: ${topic} P${partition} offset=${message.offset}`)
        e.buffer.push({
          seq: e.consumed,
          instanceId: id,
          topic,
          partition,
          offset: message.offset,
          key: message.key?.toString() ?? null,
          value: message.value?.toString() ?? '',
          headers: message.headers
            ? Object.fromEntries(Object.entries(message.headers).map(([k, v]) => [k, v?.toString() ?? '']))
            : undefined,
          timestamp: message.timestamp,
          receivedAt: Date.now(),
        })
      },
    })

    e.status = 'running'
    this.pushState(id)
    logger.info(`消费者实例已启动: ${e.opts.alias}`)
  }

  pause(id: string): void {
    const e = this.mustGet(id)
    e.consumer.pause(e.opts.topics.map(t => ({ topic: t })))
    e.status = 'paused'
    this.pushState(id)
  }

  resume(id: string): void {
    const e = this.mustGet(id)
    e.consumer.resume(e.opts.topics.map(t => ({ topic: t })))
    e.status = 'running'
    this.pushState(id)
  }

  async seek(id: string, topic: string, partition: number, offset: string): Promise<void> {
    const e = this.mustGet(id)
    e.consumer.seek({ topic, partition, offset })
    logger.info(`Offset 已重置: ${e.opts.alias} ${topic}:${partition} → ${offset}`)
  }

  async commit(id: string): Promise<void> {
    const e = this.mustGet(id)
    await e.consumer.commitOffsets()
    logger.info(`Offset 已提交: ${e.opts.alias}`)
  }

  async stop(id: string): Promise<void> {
    const e = this.mustGet(id)
    e.buffer.destroy()
    try {
      await e.consumer.stop()
      await e.consumer.disconnect()
    } catch (err) {
      logger.error('停止消费者失败:', err)
    }
    e.status = 'stopped'
    this.pushState(id)
    this.instances.delete(id)
    logger.info(`消费者实例已停止: ${e.opts.alias}`)
  }

  listInstances(): ConsumerInstanceState[] {
    return Array.from(this.instances.entries()).map(([id, e]) => ({
      instanceId: id,
      alias: e.opts.alias,
      groupId: e.opts.groupId,
      topics: e.opts.topics,
      status: e.status,
      assignments: e.assignments,
      consumedCount: e.consumed,
      error: e.error,
    }))
  }

  async disconnectAll(): Promise<void> {
    for (const [id] of this.instances) {
      await this.stop(id).catch(() => {})
    }
  }

  private pushState(id: string): void {
    const e = this.instances.get(id)
    if (!e) return
    this.push('event:consumerState', {
      instanceId: id,
      alias: e.opts.alias,
      groupId: e.opts.groupId,
      topics: e.opts.topics,
      status: e.status,
      assignments: e.assignments,
      consumedCount: e.consumed,
      error: e.error,
    })
  }

  private mustGet(id: string): InstanceEntry {
    const e = this.instances.get(id)
    if (!e) throw new Error(`消费者实例不存在: ${id}`)
    return e
  }
}