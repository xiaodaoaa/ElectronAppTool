import { contextBridge, ipcRenderer } from 'electron'

/**
 * Helper: register an ipcRenderer listener and return an unsubscribe function.
 * This prevents listener leaks when Vue components mount/unmount repeatedly.
 */
function createListener(channel, callback) {
  const handler = (event, data) => callback(data)
  ipcRenderer.on(channel, handler)
  return () => {
    ipcRenderer.removeListener(channel, handler)
  }
}

contextBridge.exposeInMainWorld('electronAPI', {
  // ── Invoke methods (request/response) ──
  startServer: (data) => ipcRenderer.invoke('start-server', data),
  stopServer: () => ipcRenderer.invoke('stop-server'),
  sendServerMessage: (data) => ipcRenderer.invoke('send-server-message', data),

  connectClient: (data) => ipcRenderer.invoke('connect-client', data),
  disconnectClient: () => ipcRenderer.invoke('disconnect-client'),
  sendClientMessage: (data) => ipcRenderer.invoke('send-client-message', data),

  readFile: (data) => ipcRenderer.invoke('read-file', data),
  readFileBinary: (data) => ipcRenderer.invoke('read-file-binary', data),

  saveConfig: (data) => ipcRenderer.invoke('save-config', data),
  loadConfig: () => ipcRenderer.invoke('load-config'),
  appendToFile: (data) => ipcRenderer.invoke('append-to-file', data),
  appendBinaryToFile: (data) => ipcRenderer.invoke('append-binary-to-file', data),
  writeFile: (data) => ipcRenderer.invoke('write-file', data),
  writeBinaryFile: (data) => ipcRenderer.invoke('write-binary-file', data),
  selectSaveFile: () => ipcRenderer.invoke('select-save-file'),
  selectOpenFile: () => ipcRenderer.invoke('select-open-file'),

  // ── Event listeners (each returns an unsubscribe function) ──

  // Server events
  onServerStarted: (cb) => createListener('server-started', cb),
  onServerStopped: (cb) => createListener('server-stopped', cb),
  onServerClientConnected: (cb) => createListener('server-client-connected', cb),
  onServerClientDisconnected: (cb) => createListener('server-client-disconnected', cb),
  onServerHandshake: (cb) => createListener('server-handshake', cb),
  onServerMessageReceived: (cb) => createListener('server-message-received', cb),
  onServerMessageSent: (cb) => createListener('server-message-sent', cb),
  onServerError: (cb) => createListener('server-error', cb),

  // Client events
  onClientConnected: (cb) => createListener('client-connected', cb),
  onClientDisconnected: (cb) => createListener('client-disconnected', cb),
  onClientMessageReceived: (cb) => createListener('client-message-received', cb),
  onClientMessageSent: (cb) => createListener('client-message-sent', cb),
  onClientError: (cb) => createListener('client-error', cb),

  // ── Bulk cleanup ──
  removeAllListeners: (channel) => {
    ipcRenderer.removeAllListeners(channel)
  }
})
