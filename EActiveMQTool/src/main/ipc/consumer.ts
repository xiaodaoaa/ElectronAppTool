import { ipcMain } from 'electron'
import { consumerService } from '../services/ConsumerService'
import { logger } from '../utils/logger'
import type { ConsumedMessage } from '../../shared/types'

export function registerConsumerIpc(
  mainWindow: { webContents: { send: (ch: string, ...args: any[]) => void } }
): void {
  consumerService.onMessage((msgs: ConsumedMessage[]) => {
    mainWindow.webContents.send('consumer:message', msgs)
  })

  consumerService.onStop(() => {
    mainWindow.webContents.send('consumer:stopped')
  })

  ipcMain.handle('consumer:start', async (_evt, config) => {
    return await consumerService.start(config)
  })
  ipcMain.handle('consumer:pause', async () => {
    return await consumerService.pause()
  })
  ipcMain.handle('consumer:resume', async () => {
    return await consumerService.resume()
  })
  ipcMain.handle('consumer:stop', async () => {
    return await consumerService.stop()
  })
  ipcMain.handle('consumer:ack', async (_evt, messageId: string) => {
    return await consumerService.ack(messageId)
  })
  ipcMain.handle('consumer:nack', async (_evt, messageId: string) => {
    return await consumerService.nack(messageId)
  })

  logger.info('消费者 IPC 已注册')
}