import { useEffect, useRef, useState, useCallback } from 'react'
import type { LogEntry } from '../types'

interface LogPanelProps {
  logs: LogEntry[]
  onClear?: () => void
}

const typeLabel: Record<LogEntry['type'], string> = {
  connect: '[连接]',
  disconnect: '[断开]',
  send: '[发送]',
  receive: '[接收]',
  subscribe: '[订阅]',
  error: '[错误]',
}

interface MenuState {
  visible: boolean
  x: number
  y: number
}

const LogPanel: React.FC<LogPanelProps> = ({ logs, onClear }) => {
  const endRef = useRef<HTMLDivElement>(null)
  const [menu, setMenu] = useState<MenuState>({ visible: false, x: 0, y: 0 })

  useEffect(() => {
    endRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [logs])

  // 右键弹出自定义菜单
  const handleContextMenu = useCallback((e: React.MouseEvent) => {
    e.preventDefault()
    setMenu({ visible: true, x: e.clientX, y: e.clientY })
  }, [])

  const closeMenu = useCallback(() => setMenu((m) => (m.visible ? { ...m, visible: false } : m)), [])

  // 点击外部 / ESC 关闭菜单
  useEffect(() => {
    if (!menu.visible) return
    const handleClick = () => closeMenu()
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') closeMenu()
    }
    window.addEventListener('click', handleClick)
    window.addEventListener('keydown', handleKeyDown)
    return () => {
      window.removeEventListener('click', handleClick)
      window.removeEventListener('keydown', handleKeyDown)
    }
  }, [menu.visible, closeMenu])

  const handleClear = useCallback(() => {
    onClear?.()
    closeMenu()
  }, [onClear, closeMenu])

  return (
    <div className="log-panel">
      <div className="log-panel-header">消息日志</div>
      <div className="log-panel-content" onContextMenu={handleContextMenu}>
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

      {menu.visible && (
        <div
          className="log-context-menu"
          style={{ left: menu.x, top: menu.y }}
          onClick={(e) => e.stopPropagation()}
        >
          <div className="log-context-item" onClick={handleClear}>
            清空日志
          </div>
        </div>
      )}
    </div>
  )
}

export default LogPanel
