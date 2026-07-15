// src/types/index.ts

export interface ConnectionConfig {
  host: string
  port: number
  vhost: string
  username: string
  password: string
  sslEnabled: boolean
  sslValidateServerCert: boolean
}

export interface ServerInfo {
  rabbitmqVersion?: string
  platform?: string
  host: string
  port: number
  vhost: string
}

export interface PublishTarget {
  target: 'exchange' | 'queue'
  exchange?: string
  routingKey?: string
  queue?: string
  message: string
  properties: MessageProperties
}

export interface MessageProperties {
  persistent: boolean
  contentType: string
  priority: number
  messageId?: string
  replyTo?: string
  headers: Record<string, string>
}

export interface ReceivedMessage {
  queue: string
  message: string
  properties: Record<string, unknown>
  consumerTag: string
  timestamp: string
}

export interface LogEntry {
  time: string
  type: 'connect' | 'disconnect' | 'send' | 'receive' | 'subscribe' | 'error'
  detail: string
}

export interface ProducerState {
  targetMode: 'exchange' | 'queue'
  exchange: string
  routingKey: string
  queue: string
  properties: MessageProperties
}

export interface AppConfig {
  host: string
  port: number
  vhost: string
  username: string
  password: string
  producer?: ProducerState
  consumerQueue?: string
  consumerBindingKey?: string
}

export interface ElectronAPI {
  connect: (config: ConnectionConfig) => Promise<{ success: boolean; serverInfo?: ServerInfo }>
  disconnect: () => Promise<{ success: boolean }>
  publish: (target: PublishTarget) => Promise<{ success: boolean }>
  subscribe: (params: { mode: 'queue' | 'exchange'; target: string; bindingKey?: string }) => Promise<{ success: boolean; consumerTag?: string }>,
  unsubscribe: (consumerTag: string) => Promise<{ success: boolean }>
  saveConfig: (config: ConnectionConfig & { producer?: Record<string, unknown>; consumerQueue?: string; consumerBindingKey?: string }) => Promise<{ success: boolean }>
  loadConfig: () => Promise<{ success: boolean; config?: Record<string, unknown> | null }>

  onConnected: (callback: (data: { serverInfo: ServerInfo }) => void) => () => void
  onDisconnected: (callback: (data: { reason: string }) => void) => () => void
  onConnectionError: (callback: (data: { message: string }) => void) => () => void
  onMessageReceived: (callback: (data: ReceivedMessage) => void) => () => void
  onPublishConfirmed: (callback: (data: { success: boolean; message?: string }) => void) => () => void
  onLogEvent: (callback: (data: LogEntry) => void) => () => void

  removeAllListeners: (channel: string) => void
}