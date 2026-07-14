// src/hooks/useConfig.ts
import { useState, useCallback, useRef, useEffect } from 'react'
import type { ConnectionConfig } from '../types'

const DEFAULT_CONFIG: ConnectionConfig = {
  host: '127.0.0.1',
  port: 5672,
  vhost: '/',
  username: 'guest',
  password: 'guest',
}

export function useConfig() {
  const [config, setConfig] = useState<ConnectionConfig>(DEFAULT_CONFIG)
  const [loaded, setLoaded] = useState(false)
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  useEffect(() => {
    const api = (window as any).electronAPI
    api.loadConfig().then((result: any) => {
      if (result.success && result.config) {
        setConfig((prev) => ({ ...prev, ...result.config }))
      }
      setLoaded(true)
    })
  }, [])

  const saveConfig = useCallback((newConfig: ConnectionConfig) => {
    if (debounceRef.current) clearTimeout(debounceRef.current)
    debounceRef.current = setTimeout(() => {
      const api = (window as any).electronAPI
      api.saveConfig(newConfig)
    }, 300)
  }, [])

  const updateConfig = useCallback((partial: Partial<ConnectionConfig>) => {
    setConfig((prev) => {
      const next = { ...prev, ...partial }
      saveConfig(next)
      return next
    })
  }, [saveConfig])

  return { config, updateConfig, loaded }
}