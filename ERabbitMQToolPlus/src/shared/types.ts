export interface ConnectionConfig {
  host: string
  port: number
  vhost: string
  username: string
  password: string
  timeout: number
  sslEnabled: boolean
  caPath?: string
  certPath?: string
  keyPath?: string
  passphrase?: string
  rejectUnauthorized: boolean
}

export type ExchangeType = 'direct' | 'topic' | 'fanout' | 'headers'
export type MessageFormat = 'json' | 'text' | 'xml'

export interface MessageProperties {
  contentType?: string
  contentEncoding?: string
  deliveryMode?: 1 | 2
  priority?: number
  expiration?: string
  headers?: Record<string, any>
}

export interface BatchConfig {
  count: number
  intervalMs: number
}

export interface SendParams {
  exchange: string
  exchangeType?: ExchangeType
  routingKey: string
  autoDeclare: boolean
  durable: boolean
  message: string
  format: MessageFormat
  properties: MessageProperties
  batch: BatchConfig
}

export interface SendResult {
  success: boolean
  messageId?: string
  error?: string
}

export interface HistoryItem {
  time: string
  exchange: string
  routingKey: string
  messageSummary: string
}

export interface ConsumedMessage {
  seq: number
  receivedAt: string
  exchange: string
  routingKey: string
  messageId?: string
  deliveryTag: number
  content: string
  properties: MessageProperties
}

export interface ConsumerConfig {
  queue: string
  autoDeclareQueue: boolean
  durable: boolean
  exclusive: boolean
  autoDelete: boolean
  exchange: string
  bindingKey: string
  mode: 'push' | 'pull'
  prefetch: number
  autoAck: boolean
  filterByRoutingKey?: string
  filterByHeaderKey?: string
  filterByHeaderValue?: string
  maxReceive: number
  purgeOnStart: boolean
}

export interface IpcResult {
  success: boolean
  error?: string
}

export interface ProgressInfo {
  current: number
  total: number
}

export type LogLevel = 'INFO' | 'WARN' | 'ERROR'

export interface LogEntry {
  time: string
  level: LogLevel
  message: string
}

export type ConnectionStatus = 'disconnected' | 'connecting' | 'connected' | 'error'

export interface ConnectionStatusInfo {
  status: ConnectionStatus
  display: string
  sslEnabled: boolean
  rejectUnauthorized: boolean
}
