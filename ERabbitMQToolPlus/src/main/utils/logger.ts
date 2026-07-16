import type { BrowserWindow } from 'electron'
import type { LogEntry, LogLevel } from '../../shared/types'
import dayjs from 'dayjs'

let mainWindow: BrowserWindow | null = null

export function setMainWindow(win: BrowserWindow): void {
  mainWindow = win
}

export function log(level: LogLevel, message: string): void {
  const entry: LogEntry = {
    time: dayjs().format('YYYY-MM-DD HH:mm:ss.SSS'),
    level,
    message
  }
  if (mainWindow && !mainWindow.isDestroyed()) {
    mainWindow.webContents.send('log:entry', entry)
  }
}

export const logger = {
  info: (msg: string) => log('INFO', msg),
  warn: (msg: string) => log('WARN', msg),
  error: (msg: string) => log('ERROR', msg)
}
