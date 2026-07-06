const { contextBridge, ipcRenderer } = require('electron')

contextBridge.exposeInMainWorld('electronAPI', {
  // Server control
  startServer: (port) => ipcRenderer.invoke('start-server', port),
  stopServer: () => ipcRenderer.invoke('stop-server'),

  // Path config
  addPath: (config) => ipcRenderer.invoke('add-path', config),
  updatePath: (config) => ipcRenderer.invoke('update-path', config),
  deletePath: (id) => ipcRenderer.invoke('delete-path', id),
  listPaths: () => ipcRenderer.invoke('list-paths'),

  // Logs
  getLogs: () => ipcRenderer.invoke('get-logs'),
  clearLogs: () => ipcRenderer.invoke('clear-logs'),

  // Context menu
  showContextMenu: () => ipcRenderer.invoke('show-context-menu'),

  // Event listeners
  onServerStarted: (callback) => {
    const handler = (_event, data) => callback(data)
    ipcRenderer.on('server-started', handler)
    return () => ipcRenderer.removeListener('server-started', handler)
  },
  onServerStopped: (callback) => {
    const handler = () => callback()
    ipcRenderer.on('server-stopped', handler)
    return () => ipcRenderer.removeListener('server-stopped', handler)
  },
  onServerError: (callback) => {
    const handler = (_event, data) => callback(data)
    ipcRenderer.on('server-error', handler)
    return () => ipcRenderer.removeListener('server-error', handler)
  },
  onNewRequest: (callback) => {
    const handler = (_event, data) => callback(data)
    ipcRenderer.on('new-request', handler)
    return () => ipcRenderer.removeListener('new-request', handler)
  },

  // DevTools 日志转发
  onDevLog: (callback) => {
    const handler = (_event, data) => callback(data)
    ipcRenderer.on('dev-log', handler)
    return () => ipcRenderer.removeListener('dev-log', handler)
  },

  removeAllListeners: (channel) => ipcRenderer.removeAllListeners(channel),
})
