import { contextBridge, ipcRenderer } from 'electron'
import type {
  ConnectionConfig, SendParams, SendResult, HistoryItem,
  ConsumedMessage, ConsumerConfig, IpcResult, ProgressInfo,
  ConnectionStatusInfo, LogEntry
} from '../shared/types'

export const api = {
  connection: {
    connect: (config: ConnectionConfig): Promise<IpcResult> =>
      ipcRenderer.invoke('connection:connect', config),
    test: (config: ConnectionConfig): Promise<IpcResult> =>
      ipcRenderer.invoke('connection:test', config),
    disconnect: (): Promise<IpcResult> =>
      ipcRenderer.invoke('connection:disconnect'),
    onStatusChange: (cb: (info: ConnectionStatusInfo) => void): void => {
      const handler = (_: unknown, info: ConnectionStatusInfo) => cb(info)
      ipcRenderer.on('connection:statusChanged', handler)
    },
    onLastConfigLoaded: (cb: (config: ConnectionConfig) => void): void => {
      const handler = (_: unknown, config: ConnectionConfig) => cb(config)
      ipcRenderer.on('connection:lastConfigLoaded', handler)
    }
  },
  producer: {
    send: (params: SendParams): Promise<SendResult> =>
      ipcRenderer.invoke('producer:send', params),
    getHistory: (): Promise<HistoryItem[]> =>
      ipcRenderer.invoke('producer:getHistory'),
    onProgress: (cb: (p: ProgressInfo) => void): void => {
      const handler = (_: unknown, p: ProgressInfo) => cb(p)
      ipcRenderer.on('producer:progress', handler)
    }
  },
  consumer: {
    start: (config: ConsumerConfig): Promise<IpcResult> =>
      ipcRenderer.invoke('consumer:start', config),
    pause: (): Promise<IpcResult> => ipcRenderer.invoke('consumer:pause'),
    resume: (): Promise<IpcResult> => ipcRenderer.invoke('consumer:resume'),
    stop: (): Promise<IpcResult> => ipcRenderer.invoke('consumer:stop'),
    ack: (messageId: string): Promise<IpcResult> =>
      ipcRenderer.invoke('consumer:ack', messageId),
    nack: (messageId: string): Promise<IpcResult> =>
      ipcRenderer.invoke('consumer:nack', messageId),
    onMessage: (cb: (msgs: ConsumedMessage[]) => void): void => {
      const handler = (_: unknown, msgs: ConsumedMessage[]) => cb(msgs)
      ipcRenderer.on('consumer:message', handler)
    },
    onStop: (cb: () => void): void => {
      const handler = () => cb()
      ipcRenderer.on('consumer:stopped', handler)
    }
  },
  log: {
    onLog: (cb: (entry: LogEntry) => void): void => {
      const handler = (_: unknown, entry: LogEntry) => cb(entry)
      ipcRenderer.on('log:entry', handler)
    }
  },
  dialog: {
    selectFile: (): Promise<string | null> => ipcRenderer.invoke('dialog:selectFile')
  },
  config: {
    saveProducer: (config: SendParams): Promise<{ success: boolean }> =>
      ipcRenderer.invoke('config:saveProducer', config),
    saveConsumer: (config: ConsumerConfig): Promise<{ success: boolean }> =>
      ipcRenderer.invoke('config:saveConsumer', config),
    loadProducer: (): Promise<SendParams | null> =>
      ipcRenderer.invoke('config:loadProducer'),
    loadConsumer: (): Promise<ConsumerConfig | null> =>
      ipcRenderer.invoke('config:loadConsumer'),
    onProducerLoaded: (cb: (config: SendParams) => void): void => {
      const handler = (_: unknown, config: SendParams) => cb(config)
      ipcRenderer.on('config:producerLoaded', handler)
    },
    onConsumerLoaded: (cb: (config: ConsumerConfig) => void): void => {
      const handler = (_: unknown, config: ConsumerConfig) => cb(config)
      ipcRenderer.on('config:consumerLoaded', handler)
    }
  }
}

if (process.contextIsolated) {
  try {
    contextBridge.exposeInMainWorld('api', api)
  } catch (error) {
    console.error(error)
  }
} else {
  // @ts-ignore
  window.api = api
}