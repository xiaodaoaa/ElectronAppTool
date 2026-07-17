import { ipcMain, BrowserWindow } from 'electron'
import { IPC_CHANNELS } from './channels'
import { kafkaClientManager } from '../kafka/KafkaClientManager'
import { ProducerService } from '../kafka/ProducerService'
import { ConsumerService } from '../kafka/ConsumerService'
import { DemoScenarioService } from '../kafka/DemoScenarioService'
import { loadConnections, saveConnections } from '../store/connectionStore'
import type { ConnectionConfig, ProduceRequest } from '../kafka/types'
import { logger } from '../util/logger'

function getMainWindow(): BrowserWindow | null {
  const wins = BrowserWindow.getAllWindows()
  return wins.length > 0 ? wins[0] : null
}

function pushToRenderer(channel: string, payload: unknown): void {
  const wins = BrowserWindow.getAllWindows()
  const win = wins.length > 0 ? wins[0] : null
  if (win && !win.isDestroyed()) {
    win.webContents.send(channel, payload)
  } else {
    logger.warn('pushToRenderer 失败: 窗口不可用')
  }
}

function wrapHandler<T extends (...args: unknown[]) => unknown>(fn: T): T {
  return (async (...args: unknown[]) => {
    try {
      return await fn(...args)
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : String(err)
      logger.error('IPC Handler 错误:', message)
      throw new Error(message)
    }
  }) as T
}

export function registerAllHandlers(): void {
  kafkaClientManager.setStatusCallback((status, detail) => {
    pushToRenderer(IPC_CHANNELS.EVENT_CONN_STATUS, { status, detail })
  })

  const producerService = new ProducerService(
    () => kafkaClientManager.getKafka(),
    pushToRenderer,
  )

  const consumerService = new ConsumerService(
    () => kafkaClientManager.getKafka(),
    pushToRenderer,
  )

  const demoScenarioService = new DemoScenarioService(
    pushToRenderer,
    async (name, partitions) => {
      const admin = kafkaClientManager.getAdmin()
      if (!admin) throw new Error('未连接 Kafka')
      await admin.createTopics({ topics: [{ topic: name, numPartitions: partitions, replicationFactor: 1 }] })
    },
    async (opts) => consumerService.create(opts),
    async (id) => consumerService.start(id),
    async (id) => consumerService.stop(id),
    async (topic, count, intervalMs, keyStrategy, key, value) => {
      return producerService.sendBatch(
        { topic, key, value: value ?? '{"msg": "{{seq}}"}', acks: -1 },
        count,
        intervalMs,
        keyStrategy,
      )
    },
    async (taskId) => producerService.stopBatch(taskId),
  )

  ipcMain.handle(IPC_CHANNELS.CONN_LIST, wrapHandler(async () => {
    return loadConnections()
  }))

  ipcMain.handle(IPC_CHANNELS.CONN_SAVE, wrapHandler(async (_event, config: ConnectionConfig) => {
    const configs = loadConnections()
    const idx = configs.findIndex(c => c.id === config.id)
    if (idx >= 0) {
      configs[idx] = config
    } else {
      configs.push(config)
    }
    saveConnections(configs)
    logger.info(`连接配置已保存: ${config.name}`)
  }))

  ipcMain.handle(IPC_CHANNELS.CONN_DELETE, wrapHandler(async (_event, id: string) => {
    const configs = loadConnections().filter(c => c.id !== id)
    saveConnections(configs)
    logger.info(`连接配置已删除: ${id}`)
  }))

  ipcMain.handle(IPC_CHANNELS.CONN_TEST, wrapHandler(async (_event, config: ConnectionConfig) => {
    return kafkaClientManager.testConnection(config)
  }))

  ipcMain.handle(IPC_CHANNELS.CONN_CONNECT, wrapHandler(async (_event, id: string) => {
    const configs = loadConnections()
    const config = configs.find(c => c.id === id)
    if (!config) throw new Error(`连接配置不存在: ${id}`)
    return kafkaClientManager.connect(config)
  }))

  ipcMain.handle(IPC_CHANNELS.CONN_DISCONNECT, wrapHandler(async () => {
    await kafkaClientManager.disconnect()
  }))

  ipcMain.handle(IPC_CHANNELS.ADMIN_LIST_TOPICS, wrapHandler(async () => {
    const admin = kafkaClientManager.getAdmin()
    if (!admin) throw new Error('未连接 Kafka')
    const topicNames = await admin.listTopics()
    const topics = []
    for (const name of topicNames) {
      const meta = await admin.fetchTopicMetadata({ topics: [name] })
      const t = meta.topics[0]
      topics.push({
        name: t.name,
        partitionCount: t.partitions.length,
        replicationFactor: t.partitions[0]?.replicas.length ?? 0,
        totalMessages: 0,
      })
    }
    return topics
  }))

  ipcMain.handle(IPC_CHANNELS.ADMIN_TOPIC_DETAIL, wrapHandler(async (_event, topic: string) => {
    const admin = kafkaClientManager.getAdmin()
    if (!admin) throw new Error('未连接 Kafka')
    const meta = await admin.fetchTopicMetadata({ topics: [topic] })
    const t = meta.topics[0]
    if (!t) throw new Error(`Topic 不存在: ${topic}`)

    const offsets = await admin.fetchTopicOffsets(topic)
    return {
      name: t.name,
      partitions: t.partitions.map(p => {
        const offsetInfo = offsets.find(o => o.partition === p.partition)
        return {
          partition: p.partition,
          leader: p.leader,
          replicas: p.replicas,
          isr: p.isr,
          earliestOffset: offsetInfo?.low ?? '0',
          latestOffset: offsetInfo?.high ?? '0',
        }
      }),
    }
  }))

  ipcMain.handle(IPC_CHANNELS.ADMIN_CREATE_TOPIC, wrapHandler(async (_event, args: { name: string; numPartitions: number; replicationFactor: number }) => {
    const admin = kafkaClientManager.getAdmin()
    if (!admin) throw new Error('未连接 Kafka')
    await admin.createTopics({
      topics: [{ topic: args.name, numPartitions: args.numPartitions, replicationFactor: args.replicationFactor }],
    })
    logger.info(`Topic 已创建: ${args.name}`)
  }))

  ipcMain.handle(IPC_CHANNELS.ADMIN_DELETE_TOPIC, wrapHandler(async (_event, name: string) => {
    const admin = kafkaClientManager.getAdmin()
    if (!admin) throw new Error('未连接 Kafka')
    await admin.deleteTopics({ topics: [name] })
    logger.info(`Topic 已删除: ${name}`)
  }))

  ipcMain.handle(IPC_CHANNELS.PRODUCER_SEND, wrapHandler(async (_event, req: ProduceRequest) => {
    return producerService.send(req)
  }))

  ipcMain.handle(IPC_CHANNELS.PRODUCER_SEND_BATCH, wrapHandler(async (_event, args: {
    req: ProduceRequest; count: number; intervalMs: number; keyStrategy: string
  }) => {
    return producerService.sendBatch(args.req, args.count, args.intervalMs, args.keyStrategy)
  }))

  ipcMain.handle(IPC_CHANNELS.PRODUCER_STOP_BATCH, wrapHandler(async (_event, taskId: string) => {
    producerService.stopBatch(taskId)
  }))

  ipcMain.handle(IPC_CHANNELS.CONSUMER_CREATE, async (_event, opts) => {
    logger.info('CONSUMER_CREATE 收到:', JSON.stringify(opts))
    try {
      return consumerService.create(opts)
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : String(err)
      logger.error('CONSUMER_CREATE 错误:', msg)
      throw new Error(msg)
    }
  })

  ipcMain.handle(IPC_CHANNELS.CONSUMER_START, wrapHandler(async (_event, id: string) => {
    await consumerService.start(id)
  }))

  ipcMain.handle(IPC_CHANNELS.CONSUMER_STOP, wrapHandler(async (_event, id: string) => {
    await consumerService.stop(id)
  }))

  ipcMain.handle(IPC_CHANNELS.CONSUMER_PAUSE, wrapHandler(async (_event, id: string) => {
    consumerService.pause(id)
  }))

  ipcMain.handle(IPC_CHANNELS.CONSUMER_RESUME, wrapHandler(async (_event, id: string) => {
    consumerService.resume(id)
  }))

  ipcMain.handle(IPC_CHANNELS.CONSUMER_SEEK, wrapHandler(async (_event, args: { instanceId: string; topic: string; partition: number; offset: string }) => {
    await consumerService.seek(args.instanceId, args.topic, args.partition, args.offset)
  }))

  ipcMain.handle(IPC_CHANNELS.CONSUMER_COMMIT, wrapHandler(async (_event, id: string) => {
    await consumerService.commit(id)
  }))

  ipcMain.handle(IPC_CHANNELS.CONSUMER_LIST_INSTANCES, wrapHandler(async () => {
    return consumerService.listInstances()
  }))

  ipcMain.handle(IPC_CHANNELS.SCENARIO_LIST, wrapHandler(async () => {
    return demoScenarioService.listScenarios()
  }))

  ipcMain.handle(IPC_CHANNELS.SCENARIO_RUN, wrapHandler(async (_event, scenarioId: string) => {
    return demoScenarioService.run(scenarioId)
  }))

  ipcMain.handle(IPC_CHANNELS.SCENARIO_STOP, wrapHandler(async (_event, runId: string) => {
    demoScenarioService.stop(runId)
  }))

  logger.info('IPC Handlers 已注册')
}