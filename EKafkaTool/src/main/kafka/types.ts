export interface ConnectionConfig {
  id: string
  name: string
  brokers: string[]
  clientId: string
  sasl?: {
    mechanism: 'plain' | 'scram-sha-256' | 'scram-sha-512'
    username: string
    password: string
  }
  ssl?: {
    enabled: boolean
    caFile?: string
    rejectUnauthorized?: boolean
  }
}

export interface TestResult {
  success: boolean
  brokerCount?: number
  controllerId?: number
  error?: string
}

export interface ClusterSummary {
  brokers: BrokerInfo[]
  topics: TopicSummary[]
}

export interface BrokerInfo {
  nodeId: number
  host: string
  port: number
  isController: boolean
}

export interface TopicSummary {
  name: string
  partitionCount: number
  replicationFactor: number
  totalMessages: number
}

export interface TopicDetail {
  name: string
  partitions: PartitionDetail[]
}

export interface PartitionDetail {
  partition: number
  leader: number
  replicas: number[]
  isr: number[]
  earliestOffset: string
  latestOffset: string
}

export interface ProduceRequest {
  topic: string
  partition?: number
  key?: string
  value: string
  headers?: Record<string, string>
  acks?: -1 | 0 | 1
}

export interface ProduceResult {
  topic: string
  partition: number
  offset: string
  timestamp: string
  latencyMs: number
}

export interface ConsumerOptions {
  alias: string
  groupId: string
  topics: string[]
  fromBeginning: boolean
  autoCommit: boolean
}

export interface ConsumerInstanceState {
  instanceId: string
  alias: string
  groupId: string
  topics: string[]
  status: 'created' | 'running' | 'paused' | 'stopped' | 'error'
  assignments: Array<{ topic: string; partitions: number[] }>
  consumedCount: number
  lag?: number
  error?: string
}

export interface MessageRecord {
  seq: number
  instanceId: string
  topic: string
  partition: number
  offset: string
  key: string | null
  value: string
  headers?: Record<string, string>
  timestamp: string
  receivedAt: number
}

export interface RebalanceEvent {
  groupId: string
  generation: number
  assignments: Record<string, number[]>
  ts: number
}

export interface ScenarioMeta {
  id: string
  title: string
  description: string
  duration: string
}

export interface ScenarioStep {
  type: 'createTopic' | 'note' | 'createConsumer' | 'produce' | 'sleep'
  [key: string]: unknown
}