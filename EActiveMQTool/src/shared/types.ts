export interface ConnectionConfig {
  host: string
  port: number
  username: string
  password: string
  sslEnabled: boolean
  heartbeatOutgoing: number
  heartbeatIncoming: number
}

export type MessageFormat = 'json' | 'text' | 'xml'

export interface BatchConfig {
  count: number
  intervalMs: number
}

export interface SendParams {
  destination: string
  body: string
  format: MessageFormat
  contentType: string
  persistent: boolean
  priority: number
  expires: number
  headers: Record<string, string>
  batch: BatchConfig
}

export interface SendResult {
  success: boolean
  messageId?: string
  error?: string
}

export interface HistoryItem {
  time: string
  destination: string
  messageSummary: string
}

export interface ConsumedMessage {
  seq: number
  receivedAt: string
  destination: string
  messageId: string
  body: string
  headers: Record<string, string>
  acked: boolean
}

export interface ConsumerConfig {
  destination: string
  ackMode: 'auto' | 'client' | 'client-individual'
  selector: string
  prefetchCount: number
  maxReceive: number
  filterRoutingKey: string
  filterByHeaderKey: string
  filterByHeaderValue: string
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
}