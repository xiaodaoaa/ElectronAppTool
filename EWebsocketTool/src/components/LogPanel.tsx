import { useEffect, useRef } from 'react'
import { LogEntry } from '../types'

interface LogPanelProps {
  logs: LogEntry[]
}

const DIRECTION_LABELS: Record<string, string> = {
  send: '发送',
  receive: '接收',
  info: '信息',
  error: '错误',
}

const LogPanel = ({ logs }: LogPanelProps) => {
  const containerRef = useRef<HTMLDivElement>(null)
  const isAtBottomRef = useRef(true)

  const handleScroll = () => {
    if (containerRef.current) {
      const { scrollTop, scrollHeight, clientHeight } = containerRef.current
      isAtBottomRef.current = scrollHeight - scrollTop - clientHeight < 40
    }
  }

  useEffect(() => {
    if (containerRef.current && isAtBottomRef.current) {
      containerRef.current.scrollTop = containerRef.current.scrollHeight
    }
  }, [logs])

  return (
    <div className="log-panel">
      <div className="log-panel-header">消息日志</div>
      <div className="log-panel-content" ref={containerRef} onScroll={handleScroll}>
        {logs.map((log) => (
          <div key={log.id} className={`log-entry log-${log.type}`}>
            <span className="log-time">[{log.timestamp}]</span>
            <span className="log-direction">
              [{DIRECTION_LABELS[log.type] || log.type}]
            </span>
            <span className="log-message">{log.content}</span>
          </div>
        ))}
        {logs.length === 0 && (
          <div className="log-empty">暂无日志</div>
        )}
      </div>
    </div>
  )
}

export default LogPanel