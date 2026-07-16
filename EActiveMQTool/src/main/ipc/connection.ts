import { ipcMain } from 'electron'
import { connectionManager } from '../services/ConnectionManager'
import {
  loadConnection, saveConnection,
  loadProducerConfig, saveProducerConfig,
  loadConsumerConfig, saveConsumerConfig
} from '../utils/store'
import { logger } from '../utils/logger'
import type { ConnectionConfig, ConnectionStatusInfo, SendParams, ConsumerConfig } from '../../shared/types'

export function registerConnectionIpc(
  mainWindow: { webContents: { send: (ch: string, ...args: any[]) => void } }
): void {
  connectionManager.onStatusChange((info: ConnectionStatusInfo) => {
    mainWindow.webContents.send('connection:statusChanged', info)
  })

  ipcMain.handle('connection:connect', async (_evt, config: ConnectionConfig) => {
    const result = await connectionManager.connect(config)
    if (result.success) {
      saveConnection(config)
    }
    return result
  })

  ipcMain.handle('connection:test', async (_evt, config: ConnectionConfig) => {
    return await connectionManager.test(config)
  })

  ipcMain.handle('connection:disconnect', async () => {
    return await connectionManager.disconnect()
  })

  ipcMain.handle('config:saveProducer', async (_evt, config: SendParams) => {
    saveProducerConfig(config)
    return { success: true }
  })

  ipcMain.handle('config:saveConsumer', async (_evt, config: ConsumerConfig) => {
    saveConsumerConfig(config)
    return { success: true }
  })

  ipcMain.handle('config:loadProducer', async () => {
    return loadProducerConfig()
  })

  ipcMain.handle('config:loadConsumer', async () => {
    return loadConsumerConfig()
  })

  logger.info('连接 IPC 已注册')
}

export function getLastSavedConnection(): ConnectionConfig | undefined {
  return loadConnection()
}

export function getLastProducerConfig(): SendParams | undefined {
  return loadProducerConfig()
}

export function getLastConsumerConfig(): ConsumerConfig | undefined {
  return loadConsumerConfig()
}