import { Kafka, Admin, type ITopicMetadata, type BrokerMetadata } from 'kafkajs'
import type { ConnectionConfig, ClusterSummary, BrokerInfo, TopicSummary } from './types'
import { logger } from '../util/logger'

export class KafkaClientManager {
  private kafka: Kafka | null = null
  private admin: Admin | null = null
  private onStatusChange?: (status: string, detail?: string) => void

  setStatusCallback(cb: (status: string, detail?: string) => void): void {
    this.onStatusChange = cb
  }

  async connect(config: ConnectionConfig): Promise<ClusterSummary> {
    await this.disconnect()

    this.kafka = new Kafka({
      clientId: config.clientId,
      brokers: config.brokers,
      ssl: config.ssl?.enabled
        ? { rejectUnauthorized: config.ssl.rejectUnauthorized ?? true }
        : undefined,
      sasl: config.sasl
        ? { mechanism: config.sasl.mechanism, username: config.sasl.username, password: config.sasl.password }
        : undefined,
      retry: { retries: 3 },
    })

    this.admin = this.kafka.admin()
    await this.admin.connect()

    const cluster = await this.admin.describeCluster()
    const brokers: BrokerInfo[] = cluster.brokers.map((b: BrokerMetadata) => ({
      nodeId: b.nodeId,
      host: b.host,
      port: b.port,
      isController: b.nodeId === cluster.controller,
    }))

    const topicMetas = await this.admin.listTopics()
    const topics: TopicSummary[] = []

    for (const topicName of topicMetas) {
      const meta = await this.admin.fetchTopicMetadata({ topics: [topicName] })
      const t = meta.topics[0]
      topics.push({
        name: t.name,
        partitionCount: t.partitions.length,
        replicationFactor: t.partitions[0]?.replicas.length ?? 0,
        totalMessages: 0,
      })
    }

    this.onStatusChange?.('connected')
    logger.info(`已连接 Kafka: ${config.brokers.join(',')}`)

    return { brokers, topics }
  }

  async disconnect(): Promise<void> {
    try {
      if (this.admin) {
        await this.admin.disconnect()
        this.admin = null
      }
    } catch (err) {
      logger.error('断开 Admin 连接失败:', err)
    }
    this.kafka = null
    this.onStatusChange?.('disconnected')
    logger.info('已断开 Kafka 连接')
  }

  getKafka(): Kafka | null {
    return this.kafka
  }

  getAdmin(): Admin | null {
    return this.admin
  }

  isConnected(): boolean {
    return this.admin !== null
  }

  async testConnection(config: ConnectionConfig): Promise<{ success: boolean; brokerCount?: number; controllerId?: number; error?: string }> {
    const testKafka = new Kafka({
      clientId: config.clientId + '-test',
      brokers: config.brokers,
      ssl: config.ssl?.enabled
        ? { rejectUnauthorized: config.ssl.rejectUnauthorized ?? true }
        : undefined,
      sasl: config.sasl
        ? { mechanism: config.sasl.mechanism, username: config.sasl.username, password: config.sasl.password }
        : undefined,
      connectionTimeout: 3000,
      retry: { retries: 0 },
    })
    const testAdmin = testKafka.admin()
    try {
      await testAdmin.connect()
      const cluster = await testAdmin.describeCluster()
      return {
        success: true,
        brokerCount: cluster.brokers.length,
        controllerId: cluster.controller,
      }
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : String(err)
      return { success: false, error: msg }
    } finally {
      await testAdmin.disconnect().catch(() => {})
    }
  }
}

export const kafkaClientManager = new KafkaClientManager()