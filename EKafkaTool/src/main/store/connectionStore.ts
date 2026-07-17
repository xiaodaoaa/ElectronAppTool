import { app } from 'electron'
import { join } from 'path'
import { readFileSync, writeFileSync, existsSync, mkdirSync } from 'fs'
import { safeStorage } from 'electron'
import type { ConnectionConfig } from '../kafka/types'
import { logger } from '../util/logger'

const STORE_FILE = join(app.getPath('userData'), 'connections.json')

function ensureDir(): void {
  const dir = app.getPath('userData')
  if (!existsSync(dir)) mkdirSync(dir, { recursive: true })
}

function encryptPassword(password: string): string {
  if (safeStorage.isEncryptionAvailable()) {
    return safeStorage.encryptString(password).toString('base64')
  }
  return Buffer.from(password).toString('base64')
}

function decryptPassword(encrypted: string): string {
  if (safeStorage.isEncryptionAvailable()) {
    return safeStorage.decryptString(Buffer.from(encrypted, 'base64'))
  }
  return Buffer.from(encrypted, 'base64').toString('utf-8')
}

export function loadConnections(): ConnectionConfig[] {
  try {
    ensureDir()
    if (!existsSync(STORE_FILE)) return []
    const raw = readFileSync(STORE_FILE, 'utf-8')
    const configs: ConnectionConfig[] = JSON.parse(raw)
    for (const c of configs) {
      if (c.sasl?.password) {
        c.sasl.password = decryptPassword(c.sasl.password)
      }
    }
    return configs
  } catch (err) {
    logger.error('加载连接配置失败:', err)
    return []
  }
}

export function saveConnections(configs: ConnectionConfig[]): void {
  ensureDir()
  const toSave = configs.map(c => {
    const copy = { ...c, sasl: c.sasl ? { ...c.sasl } : undefined }
    if (copy.sasl?.password) {
      copy.sasl.password = encryptPassword(copy.sasl.password)
    }
    return copy
  })
  writeFileSync(STORE_FILE, JSON.stringify(toSave, null, 2), 'utf-8')
  logger.info(`已保存 ${configs.length} 条连接配置`)
}