import { useEffect, useRef } from 'react'
import type { LogEntry } from '../types'

interface LogPanelProps {
  logs: LogEntry[]
}

const typeLabel: Record<LogEntry['type'], string> = {
  connect: '[连接]',
  disconnect: '[断开]',
  send: '[发送]',
  receive: '[接收]',
  subscribe: '[订阅]',
  error: '[错误]',
}

const LogPanel: React.FC<LogPanelProps> = ({ logs }) => {
  const endRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    endRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [logs])

  return (
    <div className="log-panel">
      <div className="log-panel-header">消息日志</div>
      <div className="log-panel-content">
        {logs.length === 0 ? (
          <div className="log-empty">暂无日志</div>
        ) : (
          logs.map((entry, i) => (
            <div key={i} className={`log-entry log-${entry.type}`}>
              <span className="log-time">{entry.time}</span>
              <span className="log-direction">{typeLabel[entry.type]}</span>
              <span className="log-message">{entry.detail}</span>
            </div>
          ))
        )}
        <div ref={endRef} />
      </div>
    </div>
  )
}

export default LogPanel