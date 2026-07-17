import { contextBridge, ipcRenderer } from 'electron'
import { IPC_CHANNELS } from '../main/ipc/channels'
import type {
  ConnectionConfig, TestResult, ClusterSummary, TopicSummary,
  TopicDetail, ProduceRequest, ProduceResult, ConsumerOptions,
  ConsumerInstanceState, MessageRecord, RebalanceEvent, ScenarioMeta,
} from '../main/kafka/types'

console.log('[preload] 脚本开始加载')

const api = {
  connList: (): Promise<ConnectionConfig[]> =>
    ipcRenderer.invoke(IPC_CHANNELS.CONN_LIST),

  connSave: (config: ConnectionConfig): Promise<void> =>
    ipcRenderer.invoke(IPC_CHANNELS.CONN_SAVE, config),

  connDelete: (id: string): Promise<void> =>
    ipcRenderer.invoke(IPC_CHANNELS.CONN_DELETE, id),

  connTest: (config: ConnectionConfig): Promise<TestResult> =>
    ipcRenderer.invoke(IPC_CHANNELS.CONN_TEST, config),

  connConnect: (id: string): Promise<ClusterSummary> =>
    ipcRenderer.invoke(IPC_CHANNELS.CONN_CONNECT, id),

  connDisconnect: (): Promise<void> =>
    ipcRenderer.invoke(IPC_CHANNELS.CONN_DISCONNECT),

  listTopics: (): Promise<TopicSummary[]> =>
    ipcRenderer.invoke(IPC_CHANNELS.ADMIN_LIST_TOPICS),

  topicDetail: (topic: string): Promise<TopicDetail> =>
    ipcRenderer.invoke(IPC_CHANNELS.ADMIN_TOPIC_DETAIL, topic),

  createTopic: (args: { name: string; numPartitions: number; replicationFactor: number }): Promise<void> =>
    ipcRenderer.invoke(IPC_CHANNELS.ADMIN_CREATE_TOPIC, args),

  deleteTopic: (name: string): Promise<void> =>
    ipcRenderer.invoke(IPC_CHANNELS.ADMIN_DELETE_TOPIC, name),

  produce: (req: ProduceRequest): Promise<ProduceResult> =>
    ipcRenderer.invoke(IPC_CHANNELS.PRODUCER_SEND, req),

  produceBatch: (args: { req: ProduceRequest; count: number; intervalMs: number; keyStrategy: string }): Promise<string> =>
    ipcRenderer.invoke(IPC_CHANNELS.PRODUCER_SEND_BATCH, args),

  stopBatch: (taskId: string): Promise<void> =>
    ipcRenderer.invoke(IPC_CHANNELS.PRODUCER_STOP_BATCH, taskId),

  createConsumer: (opts: ConsumerOptions): Promise<string> =>
    ipcRenderer.invoke(IPC_CHANNELS.CONSUMER_CREATE, opts),

  startConsumer: (id: string): Promise<void> =>
    ipcRenderer.invoke(IPC_CHANNELS.CONSUMER_START, id),

  stopConsumer: (id: string): Promise<void> =>
    ipcRenderer.invoke(IPC_CHANNELS.CONSUMER_STOP, id),

  pauseConsumer: (id: string): Promise<void> =>
    ipcRenderer.invoke(IPC_CHANNELS.CONSUMER_PAUSE, id),

  resumeConsumer: (id: string): Promise<void> =>
    ipcRenderer.invoke(IPC_CHANNELS.CONSUMER_RESUME, id),

  seekConsumer: (args: { instanceId: string; topic: string; partition: number; offset: string }): Promise<void> =>
    ipcRenderer.invoke(IPC_CHANNELS.CONSUMER_SEEK, args),

  commitConsumer: (id: string): Promise<void> =>
    ipcRenderer.invoke(IPC_CHANNELS.CONSUMER_COMMIT, id),

  listConsumerInstances: (): Promise<ConsumerInstanceState[]> =>
    ipcRenderer.invoke(IPC_CHANNELS.CONSUMER_LIST_INSTANCES),

  runScenario: (scenarioId: string): Promise<string> =>
    ipcRenderer.invoke(IPC_CHANNELS.SCENARIO_RUN, scenarioId),

  stopScenario: (runId: string): Promise<void> =>
    ipcRenderer.invoke(IPC_CHANNELS.SCENARIO_STOP, runId),

  listScenarios: (): Promise<ScenarioMeta[]> =>
    ipcRenderer.invoke(IPC_CHANNELS.SCENARIO_LIST),

  onConnStatus: (cb: (data: { status: string; detail?: string }) => void) => {
    console.log('[preload] onConnStatus 注册')
    const handler = (_event: Electron.IpcRendererEvent, data: { status: string; detail?: string }) => {
      console.log('[preload] onConnStatus 触发:', data.status)
      cb(data)
    }
    ipcRenderer.on(IPC_CHANNELS.EVENT_CONN_STATUS, handler)
    return () => {
      console.log('[preload] onConnStatus 移除')
      ipcRenderer.removeListener(IPC_CHANNELS.EVENT_CONN_STATUS, handler)
    }
  },

  onConsumerMessage: (cb: (batch: MessageRecord[]) => void) => {
    console.log('[preload] onConsumerMessage 注册')
    const handler = (_event: Electron.IpcRendererEvent, batch: MessageRecord[]) => {
      console.log('[preload] onConsumerMessage 触发:', batch.length)
      cb(batch)
    }
    ipcRenderer.on(IPC_CHANNELS.EVENT_CONSUMER_MESSAGE, handler)
    return () => {
      console.log('[preload] onConsumerMessage 移除')
      ipcRenderer.removeListener(IPC_CHANNELS.EVENT_CONSUMER_MESSAGE, handler)
    }
  },

  onConsumerState: (cb: (state: ConsumerInstanceState) => void) => {
    console.log('[preload] onConsumerState 注册')
    const handler = (_event: Electron.IpcRendererEvent, state: ConsumerInstanceState) => {
      console.log('[preload] onConsumerState 触发:', state.alias)
      cb(state)
    }
    ipcRenderer.on(IPC_CHANNELS.EVENT_CONSUMER_STATE, handler)
    return () => {
      console.log('[preload] onConsumerState 移除')
      ipcRenderer.removeListener(IPC_CHANNELS.EVENT_CONSUMER_STATE, handler)
    }
  },

  onRebalance: (cb: (e: RebalanceEvent) => void) => {
    console.log('[preload] onRebalance 注册')
    const handler = (_event: Electron.IpcRendererEvent, e: RebalanceEvent) => {
      console.log('[preload] onRebalance 触发:', e.groupId)
      cb(e)
    }
    ipcRenderer.on(IPC_CHANNELS.EVENT_REBALANCE, handler)
    return () => {
      console.log('[preload] onRebalance 移除')
      ipcRenderer.removeListener(IPC_CHANNELS.EVENT_REBALANCE, handler)
    }
  },

  onProduceAck: (cb: (result: ProduceResult) => void) => {
    console.log('[preload] onProduceAck 注册')
    const handler = (_event: Electron.IpcRendererEvent, result: ProduceResult) => {
      console.log('[preload] onProduceAck 触发:', result.partition)
      cb(result)
    }
    ipcRenderer.on(IPC_CHANNELS.EVENT_PRODUCE_ACK, handler)
    return () => {
      console.log('[preload] onProduceAck 移除')
      ipcRenderer.removeListener(IPC_CHANNELS.EVENT_PRODUCE_ACK, handler)
    }
  },

  onScenarioStep: (cb: (data: { runId: string; stepIndex: number; message: string }) => void) => {
    console.log('[preload] onScenarioStep 注册')
    const handler = (_event: Electron.IpcRendererEvent, data: { runId: string; stepIndex: number; message: string }) => {
      console.log('[preload] onScenarioStep 触发')
      cb(data)
    }
    ipcRenderer.on(IPC_CHANNELS.EVENT_SCENARIO_STEP, handler)
    return () => {
      console.log('[preload] onScenarioStep 移除')
      ipcRenderer.removeListener(IPC_CHANNELS.EVENT_SCENARIO_STEP, handler)
    }
  },
}

console.log('[preload] 准备暴露 kafkaApi')
contextBridge.exposeInMainWorld('kafkaApi', api)
console.log('[preload] kafkaApi 已暴露')

export type KafkaApi = typeof api