import { useState, useEffect, useCallback } from 'react'
import { Input, Button, Space, Card, Tag, Table, InputNumber } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import LogPanel from './LogPanel'
import { LogEntry, ConnectedClient } from '../types'

const PORT_KEY = 'ws-server-port'

let idCounter = 0
const generateId = () => `${Date.now()}-${++idCounter}`

const columns: ColumnsType<ConnectedClient> = [
  { title: '客户端 ID', dataIndex: 'clientId', key: 'clientId', ellipsis: true },
  { title: 'IP 地址', dataIndex: 'ip', key: 'ip', width: 130 },
  { title: '连接时间', dataIndex: 'connectTime', key: 'connectTime', width: 90 },
]

const ServerTab = () => {
  const [logs, setLogs] = useState<LogEntry[]>([])
  const [message, setMessage] = useState('')
  const [running, setRunning] = useState(false)
  const [port, setPort] = useState<number>(() => {
    const saved = localStorage.getItem(PORT_KEY)
    return saved ? parseInt(saved, 10) : 8080
  })
  const [clients, setClients] = useState<ConnectedClient[]>([])
  const [selectedRowKeys, setSelectedRowKeys] = useState<React.Key[]>([])
  const [starting, setStarting] = useState(false)

  const addLog = useCallback((entry: LogEntry) => {
    setLogs((prev) => [...prev, entry])
  }, [])

  // 设置 IPC 监听器
  useEffect(() => {
    const api = window.electronAPI
    if (!api) return

    const cleanups: (() => void)[] = []

    cleanups.push(
      api.onServerStarted((data) => {
        setRunning(true)
        addLog({
          id: generateId(),
          timestamp: new Date().toLocaleTimeString(),
          type: 'info',
          content: `服务已启动，监听端口: ${data.port}`,
        })
      })
    )

    cleanups.push(
      api.onServerStopped(() => {
        setRunning(false)
        setClients([])
        setSelectedRowKeys([])
        addLog({
          id: generateId(),
          timestamp: new Date().toLocaleTimeString(),
          type: 'info',
          content: '服务已停止',
        })
      })
    )

    cleanups.push(
      api.onServerError((data) => {
        setRunning(false)
        setClients([])
        addLog({
          id: generateId(),
          timestamp: new Date().toLocaleTimeString(),
          type: 'error',
          content: `服务错误: ${data.message}`,
        })
      })
    )

    cleanups.push(
      api.onClientConnected((data) => {
        setClients((prev) => [...prev, data])
        addLog({
          id: generateId(),
          timestamp: new Date().toLocaleTimeString(),
          type: 'info',
          content: `客户端已连接: ${data.clientId}`,
        })
      })
    )

    cleanups.push(
      api.onClientDisconnected((data) => {
        setClients((prev) => prev.filter((c) => c.clientId !== data.clientId))
        setSelectedRowKeys((prev) => prev.filter((k) => k !== data.clientId))
        addLog({
          id: generateId(),
          timestamp: new Date().toLocaleTimeString(),
          type: 'info',
          content: `客户端已断开: ${data.clientId}`,
        })
      })
    )

    cleanups.push(
      api.onMessageReceived((data) => {
        addLog({
          id: generateId(),
          timestamp: new Date().toLocaleTimeString(),
          type: 'receive',
          content: `[${data.clientId}] ${data.message}`,
        })
      })
    )

    return () => {
      cleanups.forEach((fn) => { try { fn() } catch (_) { /* ignore */ } })
    }
  }, [addLog])

  const handleStart = async () => {
    setStarting(true)
    localStorage.setItem(PORT_KEY, port.toString())
    const api = window.electronAPI
    if (!api) { setStarting(false); return }
    try {
      await api.startServer(port)
    } catch (err: any) {
      addLog({
        id: generateId(),
        timestamp: new Date().toLocaleTimeString(),
        type: 'error',
        content: `启动失败: ${err.message}`,
      })
    } finally {
      setStarting(false)
    }
  }

  const handleStop = async () => {
    try {
      await window.electronAPI?.stopServer()
    } catch (err: any) {
      addLog({
        id: generateId(),
        timestamp: new Date().toLocaleTimeString(),
        type: 'error',
        content: `停止失败: ${err.message}`,
      })
    }
  }

  const handleSend = async () => {
    const trimmed = message.trim()
    if (!trimmed || selectedRowKeys.length === 0) return
    const api = window.electronAPI
    if (!api) return
    try {
      const result = await api.sendToClients(selectedRowKeys as string[], trimmed)
      addLog({
        id: generateId(),
        timestamp: new Date().toLocaleTimeString(),
        type: 'send',
        content: `[→ 选中 ${result.sent}/${result.total}] ${trimmed}`,
      })
      setMessage('')
    } catch (err: any) {
      addLog({
        id: generateId(),
        timestamp: new Date().toLocaleTimeString(),
        type: 'error',
        content: `发送失败: ${err.message}`,
      })
    }
  }

  const handleBroadcast = async () => {
    const trimmed = message.trim()
    if (!trimmed) return
    const api = window.electronAPI
    if (!api) return
    try {
      const result = await api.broadcast(trimmed)
      addLog({
        id: generateId(),
        timestamp: new Date().toLocaleTimeString(),
        type: 'send',
        content: `[广播 → ${result.sent}/${result.total}] ${trimmed}`,
      })
      setMessage('')
    } catch (err: any) {
      addLog({
        id: generateId(),
        timestamp: new Date().toLocaleTimeString(),
        type: 'error',
        content: `广播失败: ${err.message}`,
      })
    }
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
        <Card title="服务配置" size="small">
          <Space direction="vertical" style={{ width: '100%' }}>
            <Space>
              <span>端口:</span>
              <InputNumber
                min={1}
                max={65535}
                value={port}
                onChange={(v) => setPort(v || 8080)}
                disabled={running}
                style={{ width: 120 }}
              />
            </Space>
            <Space>
              {!running ? (
                <Button type="primary" onClick={handleStart} loading={starting}>
                  启动
                </Button>
              ) : (
                <Button danger onClick={handleStop}>
                  停止
                </Button>
              )}
              <Tag color={running ? 'green' : 'default'}>
                {running ? '运行中' : '已停止'}
              </Tag>
            </Space>
          </Space>
        </Card>

        <Card title="已连接客户端" size="small">
          <Table
            dataSource={clients}
            columns={columns}
            rowKey="clientId"
            size="small"
            rowSelection={{
              selectedRowKeys,
              onChange: (keys) => setSelectedRowKeys(keys),
              selections: [
                {
                  key: 'all',
                  text: '全选',
                  onSelect: () =>
                    setSelectedRowKeys(clients.map((c) => c.clientId)),
                },
                {
                  key: 'none',
                  text: '取消全选',
                  onSelect: () => setSelectedRowKeys([]),
                },
              ],
            }}
            locale={{ emptyText: '暂无连接' }}
            pagination={false}
            scroll={{ y: 200 }}
          />
        </Card>

        <Card title="发送消息" size="small">
          <Space direction="vertical" style={{ width: '100%' }}>
            <Input.TextArea
              rows={3}
              value={message}
              onChange={(e) => setMessage(e.target.value)}
              onKeyDown={handleKeyDown}
              placeholder="输入消息内容"
              disabled={!running}
            />
            <Space>
              <Button
                type="primary"
                onClick={handleSend}
                disabled={!running || selectedRowKeys.length === 0 || !message.trim()}
              >
                发送
              </Button>
              <Button
                onClick={handleBroadcast}
                disabled={!running || clients.length === 0 || !message.trim()}
              >
                广播
              </Button>
            </Space>
          </Space>
        </Card>
      </div>

      <div className="tab-right">
        <LogPanel logs={logs} />
      </div>
    </div>
  )
}

export default ServerTab