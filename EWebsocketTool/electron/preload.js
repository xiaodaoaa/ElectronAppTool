// electron/preload.js
const { contextBridge, ipcRenderer } = require('electron')

contextBridge.exposeInMainWorld('electronAPI', {
  startServer: (port) => ipcRenderer.invoke('start-server', port),
  stopServer: () => ipcRenderer.invoke('stop-server'),
  sendToClients: (clientIds, message) =>
    ipcRenderer.invoke('send-to-clients', { clientIds, message }),
  broadcast: (message) => ipcRenderer.invoke('broadcast', message),

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
  onClientConnected: (callback) => {
    const handler = (_event, data) => callback(data)
    ipcRenderer.on('client-connected', handler)
    return () => ipcRenderer.removeListener('client-connected', handler)
  },
  onClientDisconnected: (callback) => {
    const handler = (_event, data) => callback(data)
    ipcRenderer.on('client-disconnected', handler)
    return () => ipcRenderer.removeListener('client-disconnected', handler)
  },
  onMessageReceived: (callback) => {
    const handler = (_event, data) => callback(data)
    ipcRenderer.on('message-received', handler)
    return () => ipcRenderer.removeListener('message-received', handler)
  },

  removeAllListeners: (channel) => ipcRenderer.removeAllListeners(channel),
})