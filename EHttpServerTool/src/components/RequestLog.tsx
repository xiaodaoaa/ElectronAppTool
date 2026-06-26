import React, { useRef, useEffect, useState, useCallback } from 'react'
import { Button, Checkbox, Collapse, Tag, Typography, Empty } from 'antd'
import { ClearOutlined } from '@ant-design/icons'
import type { RequestLog } from '../types'

const { Text } = Typography

interface RequestLogPanelProps {
  logs: RequestLog[]
  onClear: () => void
}

const METHOD_COLORS: Record<string, string> = {
  GET: 'green',
  POST: 'blue',
  PUT: 'orange',
  DELETE: 'red',
  PATCH: 'purple',
}

function formatTime(timestamp: number): string {
  const d = new Date(timestamp)
  return d.toLocaleTimeString('zh-CN', { hour12: false })
}

const RequestLogPanel: React.FC<RequestLogPanelProps> = ({ logs, onClear }) => {
  const scrollRef = useRef<HTMLDivElement>(null)
  const [autoScroll, setAutoScroll] = useState(true)

  const scrollToBottom = useCallback(() => {
    if (scrollRef.current && autoScroll) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight
    }
  }, [autoScroll])

  useEffect(() => {
    scrollToBottom()
  }, [logs, scrollToBottom])

  const handleContextMenu = async (e: React.MouseEvent) => {
    e.preventDefault()
    if (window.electronAPI) {
      const result = await window.electronAPI.showContextMenu()
      if (result === 'clear') {
        onClear()
      }
    }
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      <div
        style={{
          padding: '8px 12px',
          borderBottom: '1px solid #f0f0f0',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
        }}
      >
        <Text strong>请求日志 ({logs.length})</Text>
        <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
          <Checkbox checked={autoScroll} onChange={(e) => setAutoScroll(e.target.checked)}>
            自动滚动
          </Checkbox>
          <Button size="small" icon={<ClearOutlined />} onClick={onClear}>
            清空
          </Button>
        </div>
      </div>
      <div
        ref={scrollRef}
        onContextMenu={handleContextMenu}
        style={{ flex: 1, overflow: 'auto', padding: '4px 0' }}
      >
        {logs.length === 0 ? (
          <Empty description="暂无请求" style={{ marginTop: 40 }} />
        ) : (
          <Collapse
            ghost
            items={logs.map((log) => ({
              key: log.id,
              label: (
                <div style={{ display: 'flex', gap: 8, alignItems: 'center', fontFamily: 'monospace' }}>
                  <Text type="secondary" style={{ fontSize: 12, minWidth: 70 }}>
                    {formatTime(log.timestamp)}
                  </Text>
                  <Tag color={METHOD_COLORS[log.method] || 'default'} style={{ margin: 0 }}>
                    {log.method}
                  </Tag>
                  <Text style={{ fontSize: 13 }}>{log.path}</Text>
                </div>
              ),
              children: (
                <div style={{ fontFamily: 'monospace', fontSize: 12, lineHeight: 1.8 }}>
                  <div><Text strong>Client:</Text> {log.clientIp}</div>
                  <div style={{ marginTop: 4 }}>
                    <Text strong>Headers:</Text>
                    {Object.keys(log.headers).length === 0 ? (
                      <div style={{ color: '#999', paddingLeft: 12 }}>无</div>
                    ) : (
                      Object.entries(log.headers).map(([k, v]) => (
                        <div key={k} style={{ paddingLeft: 12 }}>
                          <Text type="secondary">{k}:</Text> {v}
                        </div>
                      ))
                    )}
                  </div>
                  <div style={{ marginTop: 4 }}>
                    <Text strong>Query:</Text>
                    {Object.keys(log.query).length === 0 ? (
                      <div style={{ color: '#999', paddingLeft: 12 }}>无</div>
                    ) : (
                      Object.entries(log.query).map(([k, v]) => (
                        <div key={k} style={{ paddingLeft: 12 }}>
                          {k}={v}
                        </div>
                      ))
                    )}
                  </div>
                  <div style={{ marginTop: 4 }}>
                    <Text strong>Body:</Text>
                    <pre style={{
                      paddingLeft: 12,
                      margin: 0,
                      whiteSpace: 'pre-wrap',
                      wordBreak: 'break-all',
                      color: log.body ? undefined : '#999',
                    }}>
                      {log.body || '无'}
                    </pre>
                  </div>
                </div>
              ),
            }))}
          />
        )}
      </div>
    </div>
  )
}

export default RequestLogPanel