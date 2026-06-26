import React, { useState, useEffect, useCallback } from 'react'
import { ConfigProvider, InputNumber, Button, Space, Tag, message } from 'antd'
import { PlayCircleOutlined, StopOutlined } from '@ant-design/icons'
import PathList from './components/PathList'
import PathDetail from './components/PathDetail'
import RequestLogPanel from './components/RequestLog'
import type { PathConfig, RequestLog } from './types'

const EMPTY_CONFIG: Omit<PathConfig, 'id'> = {
  path: '',
  methods: ['GET'],
  echoEnabled: false,
  responseType: 'json',
  responseContent: '',
}

const App: React.FC = () => {
  const [port, setPort] = useState<number>(8080)
  const [running, setRunning] = useState(false)
  const [paths, setPaths] = useState<PathConfig[]>([])
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [logs, setLogs] = useState<RequestLog[]>([])

  // Load paths on mount
  useEffect(() => {
    const loadPaths = async () => {
      if (window.electronAPI) {
        const list = await window.electronAPI.listPaths()
        setPaths(list)
      }
    }
    loadPaths()
  }, [])

  // Event listeners
  useEffect(() => {
    if (!window.electronAPI) return
    const cleanups: (() => void)[] = []

    cleanups.push(window.electronAPI.onServerStarted(() => {
      setRunning(true)
      message.success('服务器已启动')
    }))

    cleanups.push(window.electronAPI.onServerStopped(() => {
      setRunning(false)
      message.info('服务器已停止')
    }))

    cleanups.push(window.electronAPI.onServerError((data) => {
      message.error(data.message)
    }))

    cleanups.push(window.electronAPI.onNewRequest((log) => {
      setLogs((prev) => [...prev, log])
    }))

    return () => {
      for (const cleanup of cleanups) cleanup()
    }
  }, [])

  const handleStart = useCallback(async () => {
    if (!window.electronAPI || !port) return
    try {
      await window.electronAPI.startServer(port)
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : '启动失败'
      message.error(msg)
    }
  }, [port])

  const handleStop = useCallback(async () => {
    if (!window.electronAPI) return
    await window.electronAPI.stopServer()
  }, [])

  const handleAddPath = useCallback(async () => {
    if (!window.electronAPI) return
    const result = await window.electronAPI.addPath(EMPTY_CONFIG)
    if (result.success && result.id) {
      const list = await window.electronAPI.listPaths()
      setPaths(list)
      setSelectedId(result.id)
    } else if (result.error) {
      message.error(result.error)
    }
  }, [])

  const handleSavePath = useCallback(async (config: PathConfig) => {
    if (!window.electronAPI) return
    const result = await window.electronAPI.updatePath(config)
    if (result.success) {
      const list = await window.electronAPI.listPaths()
      setPaths(list)
      message.success('保存成功')
    } else if (result.error) {
      message.error(result.error)
    }
  }, [])

  const handleDeletePath = useCallback(async (id: string) => {
    if (!window.electronAPI) return
    await window.electronAPI.deletePath(id)
    const list = await window.electronAPI.listPaths()
    setPaths(list)
    if (selectedId === id) setSelectedId(null)
    message.success('已删除')
  }, [selectedId])

  const handleClearLogs = useCallback(async () => {
    if (!window.electronAPI) return
    await window.electronAPI.clearLogs()
    setLogs([])
  }, [])

  const selectedConfig = paths.find((p) => p.id === selectedId) || null

  return (
    <ConfigProvider>
      <div className="app-container">
        {/* Top toolbar */}
        <div className="toolbar">
          <Space>
            <span>端口:</span>
            <InputNumber
              min={1}
              max={65535}
              value={port}
              onChange={(v) => setPort(v || 8080)}
              disabled={running}
              style={{ width: 100 }}
            />
            {running ? (
              <Button danger icon={<StopOutlined />} onClick={handleStop}>
                停止
              </Button>
            ) : (
              <Button type="primary" icon={<PlayCircleOutlined />} onClick={handleStart}>
                启动
              </Button>
            )}
            <Tag color={running ? 'green' : 'default'}>
              {running ? '运行中' : '已停止'}
            </Tag>
          </Space>
        </div>

        {/* Main content */}
        <div className="main-content">
          {/* Left panel: path list + detail */}
          <div className="left-panel">
            <div className="path-list-area">
              <PathList
                paths={paths}
                selectedId={selectedId}
                onSelect={setSelectedId}
                onAdd={handleAddPath}
              />
            </div>
            <div className="path-detail-area">
              <PathDetail
                config={selectedConfig}
                onSave={handleSavePath}
                onDelete={handleDeletePath}
              />
            </div>
          </div>

          {/* Right panel: request log */}
          <div className="right-panel">
            <RequestLogPanel logs={logs} onClear={handleClearLogs} />
          </div>
        </div>
      </div>
    </ConfigProvider>
  )
}

export default App