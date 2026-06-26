/** 日志条目 */
export interface LogEntry {
  id: string
  timestamp: string
  type: 'send' | 'receive' | 'info' | 'error'
  content: string
}

/** 已连接的客户端信息 */
export interface ConnectedClient {
  clientId: string
  ip: string
  connectTime: string
}

/** Electron 主进程暴露的 API */
export interface ElectronAPI {
  startServer: (port: number) => Promise<{ success: boolean }>
  stopServer: () => Promise<{ success: boolean }>
  sendToClients: (clientIds: string[], message: string) => Promise<{ sent: number; total: number }>
  broadcast: (message: string) => Promise<{ sent: number; total: number }>
  showContextMenu: () => Promise<string | null>
  onServerStarted: (callback: (data: { port: number }) => void) => () => void
  onServerStopped: (callback: () => void) => () => void
  onServerError: (callback: (data: { message: string }) => void) => () => void
  onClientConnected: (callback: (data: ConnectedClient) => void) => () => void
  onClientDisconnected: (callback: (data: { clientId: string }) => void) => () => void
  onMessageReceived: (callback: (data: { clientId: string; message: string }) => void) => () => void
  removeAllListeners: (channel: string) => void
}

declare global {
  interface Window {
    electronAPI?: ElectronAPI
  }
}

export {}