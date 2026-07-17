import type {
  ConnectionConfig, TestResult, ClusterSummary, TopicSummary,
  TopicDetail, ProduceRequest, ProduceResult, ConsumerOptions,
  ConsumerInstanceState, MessageRecord, RebalanceEvent, ScenarioMeta,
} from '../../main/kafka/types'

interface KafkaApi {
  connList(): Promise<ConnectionConfig[]>
  connSave(config: ConnectionConfig): Promise<void>
  connDelete(id: string): Promise<void>
  connTest(config: ConnectionConfig): Promise<TestResult>
  connConnect(id: string): Promise<ClusterSummary>
  connDisconnect(): Promise<void>
  listTopics(): Promise<TopicSummary[]>
  topicDetail(topic: string): Promise<TopicDetail>
  createTopic(args: { name: string; numPartitions: number; replicationFactor: number }): Promise<void>
  deleteTopic(name: string): Promise<void>
  produce(req: ProduceRequest): Promise<ProduceResult>
  produceBatch(args: { req: ProduceRequest; count: number; intervalMs: number; keyStrategy: string }): Promise<string>
  stopBatch(taskId: string): Promise<void>
  createConsumer(opts: ConsumerOptions): Promise<string>
  startConsumer(id: string): Promise<void>
  stopConsumer(id: string): Promise<void>
  pauseConsumer(id: string): Promise<void>
  resumeConsumer(id: string): Promise<void>
  seekConsumer(args: { instanceId: string; topic: string; partition: number; offset: string }): Promise<void>
  commitConsumer(id: string): Promise<void>
  listConsumerInstances(): Promise<ConsumerInstanceState[]>
  runScenario(scenarioId: string): Promise<string>
  stopScenario(runId: string): Promise<void>
  listScenarios(): Promise<ScenarioMeta[]>
  onConnStatus(cb: (data: { status: string; detail?: string }) => void): () => void
  onConsumerMessage(cb: (batch: MessageRecord[]) => void): () => void
  onConsumerState(cb: (state: ConsumerInstanceState) => void): () => void
  onRebalance(cb: (e: RebalanceEvent) => void): () => void
  onProduceAck(cb: (result: ProduceResult) => void): () => void
  onScenarioStep(cb: (data: { runId: string; stepIndex: number; message: string }) => void): () => void
}

declare global {
  interface Window {
    kafkaApi: KafkaApi
  }
}

export function getKafkaApi(): KafkaApi {
  if (!window.kafkaApi) {
    throw new Error('kafkaApi 不可用，请确保 preload 脚本已加载')
  }
  return window.kafkaApi
}