import { useRef, useState, useCallback, useEffect } from 'react'
import { LogEntry } from '../types'

const WS_URL_KEY = 'ws-client-url'

let idCounter = 0
const generateId = () => `${Date.now()}-${++idCounter}`

export const useWebSocket = (addLog: (entry: LogEntry) => void) => {
  const wsRef = useRef<WebSocket | null>(null)
  const addLogRef = useRef(addLog)
  addLogRef.current = addLog

  const [connected, setConnected] = useState(false)
  const [connecting, setConnecting] = useState(false)
  const [url, setUrl] = useState(() => {
    return localStorage.getItem(WS_URL_KEY) || 'ws://localhost:8080'
  })

  const doAddLog = useCallback((entry: Omit<LogEntry, 'id' | 'timestamp'>) => {
    addLogRef.current({
      ...entry,
      id: generateId(),
      timestamp: new Date().toLocaleTimeString(),
    })
  }, [])

  const connect = useCallback(() => {
    if (connected || connecting) return

    if (!url.startsWith('ws://') && !url.startsWith('wss://')) {
      doAddLog({
        type: 'error',
        content: 'URL 格式不正确，需以 ws:// 或 wss:// 开头',
      })
      return
    }

    // 清理旧连接
    if (wsRef.current) {
      wsRef.current.onclose = null
      wsRef.current.onerror = null
      wsRef.current.onmessage = null
      wsRef.current.onopen = null
      wsRef.current.close()
      wsRef.current = null
    }

    setConnecting(true)
    localStorage.setItem(WS_URL_KEY, url)

    try {
      const ws = new WebSocket(url)
      wsRef.current = ws

      ws.onopen = () => {
        setConnected(true)
        setConnecting(false)
        doAddLog({
          type: 'info',
          content: `已连接到 ${url}`,
        })
      }

      ws.onmessage = (event) => {
        doAddLog({
          type: 'receive',
          content: event.data,
        })
      }

      ws.onclose = (event) => {
        setConnected(false)
        setConnecting(false)
        wsRef.current = null
        doAddLog({
          type: 'info',
          content: `连接已关闭: code=${event.code}${event.reason ? ', reason=' + event.reason : ''}`,
        })
      }

      ws.onerror = () => {
        doAddLog({
          type: 'error',
          content: '连接发生错误',
        })
      }
    } catch (err: any) {
      setConnecting(false)
      doAddLog({
        type: 'error',
        content: `连接失败: ${err.message}`,
      })
    }
  }, [url, connected, connecting, doAddLog])

  const disconnect = useCallback(() => {
    if (wsRef.current) {
      wsRef.current.close(1000, '主动断开')
      wsRef.current = null
    }
  }, [])

  // 组件卸载时清理
  useEffect(() => {
    return () => {
      if (wsRef.current) {
        wsRef.current.onclose = null
        wsRef.current.close()
        wsRef.current = null
      }
    }
  }, [])

  const send = useCallback(
    (message: string): boolean => {
      if (!message.trim()) return false
      if (wsRef.current && wsRef.current.readyState === WebSocket.OPEN) {
        wsRef.current.send(message)
        doAddLog({
          type: 'send',
          content: message,
        })
        return true
      }
      doAddLog({
        type: 'error',
        content: '未连接，无法发送消息',
      })
      return false
    },
    [doAddLog]
  )

  return { url, setUrl, connected, connecting, connect, disconnect, send }
}