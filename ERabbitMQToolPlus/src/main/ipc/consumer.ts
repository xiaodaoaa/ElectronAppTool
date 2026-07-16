import { ipcMain } from 'electron'
import { consumerService } from '../services/ConsumerService'
import { logger } from '../utils/logger'
import type { ConsumedMessage } from '../../shared/types'

export function registerConsumerIpc(
  mainWindow: { webContents: { send: (ch: string, ...args: any[]) => void } }
): void {
  consumerService.onMessage((msg: ConsumedMessage) => {
    mainWindow.webContents.send('consumer:message', msg)
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
  ipcMain.handle('consumer:pull', async (_evt, count: number) => {
    return await consumerService.pull(count)
  })
  ipcMain.handle('consumer:ack', async (_evt, deliveryTag: number) => {
    return await consumerService.ack(deliveryTag)
  })
  ipcMain.handle('consumer:nack', async (_evt, deliveryTag: number, requeue: boolean) => {
    return await consumerService.nack(deliveryTag, requeue)
  })

  logger.info('消费者 IPC 已注册')
}
