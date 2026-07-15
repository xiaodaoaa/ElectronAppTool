const { contextBridge, ipcRenderer } = require('electron')

contextBridge.exposeInMainWorld('electronAPI', {
  connect: (config) => ipcRenderer.invoke('connect', config),
  disconnect: () => ipcRenderer.invoke('disconnect'),
  publish: (target) => ipcRenderer.invoke('publish', target),
  subscribe: (params) => ipcRenderer.invoke('subscribe', params),
  unsubscribe: (consumerTag) => ipcRenderer.invoke('unsubscribe', consumerTag),
  saveConfig: (config) => ipcRenderer.invoke('save-config', config),
  loadConfig: () => ipcRenderer.invoke('load-config'),

  onConnected: (callback) => {
    const handler = (_event, data) => callback(data)
    ipcRenderer.on('connected', handler)
    return () => ipcRenderer.removeListener('connected', handler)
  },
  onDisconnected: (callback) => {
    const handler = (_event, data) => callback(data)
    ipcRenderer.on('disconnected', handler)
    return () => ipcRenderer.removeListener('disconnected', handler)
  },
  onConnectionError: (callback) => {
    const handler = (_event, data) => callback(data)
    ipcRenderer.on('connection-error', handler)
    return () => ipcRenderer.removeListener('connection-error', handler)
  },
  onMessageReceived: (callback) => {
    const handler = (_event, data) => callback(data)
    ipcRenderer.on('message-received', handler)
    return () => ipcRenderer.removeListener('message-received', handler)
  },
  onPublishConfirmed: (callback) => {
    const handler = (_event, data) => callback(data)
    ipcRenderer.on('publish-confirmed', handler)
    return () => ipcRenderer.removeListener('publish-confirmed', handler)
  },
  onLogEvent: (callback) => {
    const handler = (_event, data) => callback(data)
    ipcRenderer.on('log-event', handler)
    return () => ipcRenderer.removeListener('log-event', handler)
  },

  removeAllListeners: (channel) => ipcRenderer.removeAllListeners(channel),
})