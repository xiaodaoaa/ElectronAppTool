const { contextBridge, ipcRenderer } = require('electron')

contextBridge.exposeInMainWorld('electronAPI', {
  removeAllListeners: (channel) => ipcRenderer.removeAllListeners(channel),
})
