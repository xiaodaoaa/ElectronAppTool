import Store from 'electron-store'
import { app } from 'electron'
import * as crypto from 'crypto'
import type { ConnectionConfig, SendParams, ConsumerConfig } from '../../shared/types'

const ENCRYPTION_KEY = crypto
  .createHash('sha256')
  .update(app.getName() + ':erabbitmq-store-key')
  .digest()

function encrypt(text: string): string {
  const iv = crypto.randomBytes(16)
  const cipher = crypto.createCipheriv('aes-256-cbc', ENCRYPTION_KEY, iv)
  const encrypted = Buffer.concat([cipher.update(text, 'utf8'), cipher.final()])
  return iv.toString('hex') + ':' + encrypted.toString('hex')
}

function decrypt(payload: string): string {
  const [ivHex, dataHex] = payload.split(':')
  const iv = Buffer.from(ivHex, 'hex')
  const data = Buffer.from(dataHex, 'hex')
  const decipher = crypto.createDecipheriv('aes-256-cbc', ENCRYPTION_KEY, iv)
  return decipher.update(data, undefined, 'utf8') + decipher.final('utf8')
}

interface StoreSchema {
  lastConnection?: ConnectionConfig
  lastProducer?: SendParams
  lastConsumer?: ConsumerConfig
  settings?: {
    theme: 'light' | 'dark'
    maxMessageCache: number
    maxDisplayLength: number
  }
}

const store = new Store<StoreSchema>({
  defaults: {
    settings: {
      theme: 'light',
      maxMessageCache: 1000,
      maxDisplayLength: 1000
    }
  }
})

export function saveConnection(config: ConnectionConfig): void {
  const safe = { ...config }
  if (safe.password) {
    safe.password = encrypt(safe.password)
  }
  if (safe.passphrase) {
    safe.passphrase = encrypt(safe.passphrase)
  }
  store.set('lastConnection', safe)
}

export function loadConnection(): ConnectionConfig | undefined {
  const saved = store.get('lastConnection')
  if (!saved) return undefined
  const restored = { ...saved }
  if (restored.password) {
    try {
      restored.password = decrypt(restored.password)
    } catch {
      restored.password = ''
    }
  }
  if (restored.passphrase) {
    try {
      restored.passphrase = decrypt(restored.passphrase)
    } catch {
      restored.passphrase = ''
    }
  }
  return restored
}

export function getSettings() {
  return store.get('settings')!
}

export function saveSettings(settings: StoreSchema['settings']): void {
  store.set('settings', settings)
}

export function saveProducerConfig(config: SendParams): void {
  store.set('lastProducer', config)
}

export function loadProducerConfig(): SendParams | undefined {
  return store.get('lastProducer')
}

export function saveConsumerConfig(config: ConsumerConfig): void {
  store.set('lastConsumer', config)
}

export function loadConsumerConfig(): ConsumerConfig | undefined {
  return store.get('lastConsumer')
}
