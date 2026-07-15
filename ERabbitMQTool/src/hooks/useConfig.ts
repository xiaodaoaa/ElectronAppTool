import { useState, useCallback, useRef, useEffect } from 'react'
import type { ConnectionConfig, AppConfig, ProducerState } from '../types'

const DEFAULT_CONFIG: ConnectionConfig = {
  host: '127.0.0.1',
  port: 5672,
  vhost: '/',
  username: 'guest',
  password: 'guest',
  sslEnabled: false,
  sslValidateServerCert: true,
}

const DEFAULT_PRODUCER: ProducerState = {
  targetMode: 'exchange',
  exchange: '',
  routingKey: '',
  queue: '',
  properties: {
    persistent: true,
    contentType: 'text/plain',
    priority: 0,
    headers: {},
  },
}

export function useConfig() {
  const [config, setConfig] = useState<ConnectionConfig>(DEFAULT_CONFIG)
  const [producer, setProducer] = useState<ProducerState>(DEFAULT_PRODUCER)
  const [consumerQueue, setConsumerQueue] = useState('')
  const [loaded, setLoaded] = useState(false)
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  useEffect(() => {
    const api = (window as any).electronAPI
    api.loadConfig().then((result: any) => {
      if (result.success && result.config) {
        const c = result.config
        setConfig((prev) => ({
          host: c.host ?? prev.host,
          port: c.port ?? prev.port,
          vhost: c.vhost ?? prev.vhost,
          username: c.username ?? prev.username,
          password: c.password ?? prev.password,
        }))
        if (c.producer) setProducer(c.producer)
        if (c.consumerQueue) setConsumerQueue(c.consumerQueue)
      }
      setLoaded(true)
    })
  }, [])

  const save = useCallback(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current)
    debounceRef.current = setTimeout(() => {
      const api = (window as any).electronAPI
      const full: AppConfig = { ...config, producer, consumerQueue }
      api.saveConfig(full)
    }, 300)
  }, [config, producer, consumerQueue])

  const updateConfig = useCallback((partial: Partial<ConnectionConfig>) => {
    setConfig((prev) => ({ ...prev, ...partial }))
  }, [])

  const updateProducer = useCallback((partial: Partial<ProducerState>) => {
    setProducer((prev) => ({ ...prev, ...partial }))
  }, [])

  const updateConsumerQueue = useCallback((queue: string) => {
    setConsumerQueue(queue)
  }, [])

  useEffect(() => {
    if (loaded) save()
  }, [config, producer, consumerQueue, loaded, save])

  return { config, updateConfig, producer, updateProducer, consumerQueue, updateConsumerQueue, loaded }
}