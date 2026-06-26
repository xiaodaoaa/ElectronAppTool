import { useState, useCallback } from 'react'
import { Input, Button, Space, Card, Tag } from 'antd'
import LogPanel from './LogPanel'
import { useWebSocket } from '../hooks/useWebSocket'
import { LogEntry } from '../types'

const ClientTab = () => {
  const [logs, setLogs] = useState<LogEntry[]>([])
  const [message, setMessage] = useState('')

  const addLog = useCallback((entry: LogEntry) => {
    setLogs((prev) => [...prev, entry])
  }, [])

  const { url, setUrl, connected, connecting, connect, disconnect, send } =
    useWebSocket(addLog)

  const handleSend = () => {
    const trimmed = message.trim()
    if (!trimmed) return
    const ok = send(trimmed)
    if (ok) setMessage('')
  }

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      handleSend()
    }
  }

  return (
    <div className="tab-layout">
      <div className="tab-left">
        <Card title="连接配置" size="small">
          <Space direction="vertical" style={{ width: '100%' }}>
            <Input
              placeholder="ws://localhost:8080"
              value={url}
              onChange={(e) => setUrl(e.target.value)}
              disabled={connected || connecting}
            />
            <Space>
              {!connected ? (
                <Button type="primary" onClick={connect} loading={connecting}>
                  连接
                </Button>
              ) : (
                <Button danger onClick={disconnect}>
                  断开
                </Button>
              )}
              <Tag color={connected ? 'green' : connecting ? 'processing' : 'default'}>
                {connected ? '已连接' : connecting ? '连接中...' : '已断开'}
              </Tag>
            </Space>
          </Space>
        </Card>

        <Card title="发送消息" size="small">
          <Space direction="vertical" style={{ width: '100%' }}>
            <Input.TextArea
              rows={3}
              value={message}
              onChange={(e) => setMessage(e.target.value)}
              onKeyDown={handleKeyDown}
              placeholder="输入消息内容，Enter 发送"
              disabled={!connected}
            />
            <Button
              type="primary"
              onClick={handleSend}
              disabled={!connected || !message.trim()}
              block
            >
              发送
            </Button>
          </Space>
        </Card>
      </div>

      <div className="tab-right">
        <LogPanel logs={logs} onClear={() => setLogs([])} />
      </div>
    </div>
  )
}

export default ClientTab