import { ipcMain, dialog } from 'electron'
import { logger } from '../utils/logger'

export function registerDialogIpc(): void {
  ipcMain.handle('dialog:selectFile', async () => {
    const r = await dialog.showOpenDialog({ properties: ['openFile'] })
    if (r.canceled) return null
    return r.filePaths[0]
  })
  logger.info('Dialog IPC 已注册')
}