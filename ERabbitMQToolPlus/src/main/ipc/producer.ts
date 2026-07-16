import { ipcMain } from 'electron'
import { producerService } from '../services/ProducerService'
import { logger } from '../utils/logger'
import type { ProgressInfo } from '../../shared/types'

export function registerProducerIpc(
  mainWindow: { webContents: { send: (ch: string, ...args: any[]) => void } }
): void {
  producerService.onProgress((p: ProgressInfo) => {
    mainWindow.webContents.send('producer:progress', p)
  })

  ipcMain.handle('producer:send', async (_evt, params) => {
    return await producerService.send(params)
  })

  ipcMain.handle('producer:getHistory', async () => {
    return producerService.getHistory()
  })

  logger.info('生产者 IPC 已注册')
}
