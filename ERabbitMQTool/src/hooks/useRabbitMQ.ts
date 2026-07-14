import { useEffect, useRef, useCallback } from 'react'
import type { ConnectionConfig, ServerInfo, ReceivedMessage, LogEntry, PublishTarget } from '../types'

interface UseRabbitMQCallbacks {
  onConnected?: (info: ServerInfo) => void
  onDisconnected?: (reason: string) => void
  onConnectionError?: (message: string) => void
  onMessageReceived?: (msg: ReceivedMessage) => void
  onPublishConfirmed?: (result: { success: boolean; message?: string }) => void
  onLogEvent?: (entry: LogEntry) => void
}

export function useRabbitMQ(callbacks: UseRabbitMQCallbacks) {
  const callbacksRef = useRef(callbacks)
  callbacksRef.current = callbacks
  const unsubsRef = useRef<(() => void)[]>([])

  useEffect(() => {
    const api = (window as any).electronAPI
    if (!api) return

    const unsubs: (() => void)[] = [
      api.onConnected((data: { serverInfo: ServerInfo }) => {
        callbacksRef.current.onConnected?.(data.serverInfo)
      }),
      api.onDisconnected((data: { reason: string }) => {
        callbacksRef.current.onDisconnected?.(data.reason)
      }),
      api.onConnectionError((data: { message: string }) => {
        callbacksRef.current.onConnectionError?.(data.message)
      }),
      api.onMessageReceived((msg: ReceivedMessage) => {
        callbacksRef.current.onMessageReceived?.(msg)
      }),
      api.onPublishConfirmed((result: { success: boolean; message?: string }) => {
        callbacksRef.current.onPublishConfirmed?.(result)
      }),
      api.onLogEvent((entry: LogEntry) => {
        callbacksRef.current.onLogEvent?.(entry)
      }),
    ]

    unsubsRef.current = unsubs

    return () => {
      unsubs.forEach((fn) => fn())
      unsubsRef.current = []
    }
  }, [])

  const connect = useCallback(async (config: ConnectionConfig) => {
    const api = (window as any).electronAPI
    return api.connect(config)
  }, [])

  const disconnect = useCallback(async () => {
    const api = (window as any).electronAPI
    return api.disconnect()
  }, [])

  const publish = useCallback(async (target: PublishTarget) => {
    const api = (window as any).electronAPI
    return api.publish(target)
  }, [])

  const subscribe = useCallback(async (queue: string) => {
    const api = (window as any).electronAPI
    return api.subscribe(queue)
  }, [])

  const unsubscribe = useCallback(async (consumerTag: string) => {
    const api = (window as any).electronAPI
    return api.unsubscribe(consumerTag)
  }, [])

  return { connect, disconnect, publish, subscribe, unsubscribe }
}