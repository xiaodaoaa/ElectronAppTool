/** Path 配置 */
export interface PathConfig {
  id: string
  path: string
  methods: string[]
  echoEnabled: boolean
  responseType: 'text' | 'json' | 'xml' | 'html'
  responseContent: string
}

/** 请求日志 */
export interface RequestLog {
  id: string
  timestamp: number
  clientIp: string
  method: string
  path: string
  headers: Record<string, string>
  query: Record<string, string>
  body: string
}

/** Electron 主进程暴露的 API */
export interface ElectronAPI {
  // Server control
  startServer: (port: number) => Promise<{ success: boolean }>
  stopServer: () => Promise<{ success: boolean }>

  // Path config
  addPath: (config: Omit<PathConfig, 'id'>) => Promise<{ success: boolean; id?: string; error?: string }>
  updatePath: (config: PathConfig) => Promise<{ success: boolean; error?: string }>
  deletePath: (id: string) => Promise<{ success: boolean }>
  listPaths: () => Promise<PathConfig[]>

  // Logs
  getLogs: () => Promise<RequestLog[]>
  clearLogs: () => Promise<{ success: boolean }>

  // Context menu
  showContextMenu: () => Promise<string | null>

  // Event listeners (each returns cleanup function)
  onServerStarted: (callback: (data: { port: number }) => void) => () => void
  onServerStopped: (callback: () => void) => () => void
  onServerError: (callback: (data: { message: string }) => void) => () => void
  onNewRequest: (callback: (data: RequestLog) => void) => () => void

  removeAllListeners: (channel: string) => void
}

declare global {
  interface Window {
    electronAPI?: ElectronAPI
  }
}

export {}
