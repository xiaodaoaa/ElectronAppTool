# LogPanel 右键菜单设计

## 概述

为消息日志显示区域添加右键菜单，提供"清空"选项，可以清空当前显示区域的日志内容。

## 需求

- 在 LogPanel 区域右键点击时显示原生上下文菜单
- 菜单包含"清空"选项
- 点击"清空"后清除当前 tab 的所有日志
- 使用 Electron 原生 Menu API，保持系统一致性

## 架构设计

### 组件职责

**LogPanel（显示组件）**
- 监听 `contextmenu` 事件
- 通过 IPC 调用 main process 显示原生菜单
- 接收 `onClear` 回调 prop，用户选择清空时调用
- 不持有日志状态，只负责交互

**ClientTab / ServerTab（状态拥有者）**
- 持有各自的 `logs` state
- 传入 `onClear={() => setLogs([])}` 给 LogPanel

### 数据流

```
用户右键点击 LogPanel
  ↓
LogPanel 监听 contextmenu 事件
  ↓
调用 window.electronAPI.showContextMenu()
  ↓
preload.js 通过 ipcRenderer.invoke 转发到 main process
  ↓
main.js 使用 Menu.popup() 显示菜单，等待用户选择
  ↓
返回用户选择（'clear' 或 null）
  ↓
LogPanel 收到结果，如果是 'clear' 则调用 onClear()
  ↓
父组件 setLogs([]) 清空日志
  ↓
LogPanel 重新渲染，显示空列表
```

## 实现细节

### 1. LogPanel.tsx

```typescript
interface LogPanelProps {
  logs: LogEntry[]
  onClear?: () => void  // 新增
}

const LogPanel = ({ logs, onClear }: LogPanelProps) => {
  const containerRef = useRef<HTMLDivElement>(null)
  const isAtBottomRef = useRef(true)

  // 新增：右键菜单处理
  const handleContextMenu = async (e: React.MouseEvent) => {
    e.preventDefault()
    if (!window.electronAPI?.showContextMenu) return
    
    const action = await window.electronAPI.showContextMenu()
    if (action === 'clear' && onClear) {
      onClear()
    }
  }

  return (
    <div className="log-panel">
      <div className="log-panel-header">消息日志</div>
      <div 
        className="log-panel-content" 
        ref={containerRef} 
        onScroll={handleScroll}
        onContextMenu={handleContextMenu}  // 新增
      >
        {/* ... existing code ... */}
      </div>
    </div>
  )
}
```

### 2. preload.js

```javascript
showContextMenu: () => {
  return ipcRenderer.invoke('show-context-menu')
}
```

### 3. main.js

```javascript
const { Menu } = require('electron')

ipcMain.handle('show-context-menu', async () => {
  const menu = Menu.buildFromTemplate([
    {
      label: '清空',
      click: () => 'clear'
    }
  ])
  
  const result = await menu.popup({
    callback: (menuItem) => {
      return menuItem.label === '清空' ? 'clear' : null
    }
  })
  
  return result
})
```

注意：`Menu.popup()` 是异步的，需要正确处理回调。实际实现时可能需要调整 IPC handler 的返回方式。

### 4. ClientTab.tsx / ServerTab.tsx

```typescript
<LogPanel logs={logs} onClear={() => setLogs([])} />
```

## 错误处理

- 如果 `window.electronAPI` 不存在（非 Electron 环境），静默失败
- 如果用户取消菜单（点击外部），不执行任何操作
- 如果 `onClear` 未提供，右键菜单仍可显示但不执行清空

## 测试要点

- [ ] 右键点击 LogPanel 显示原生菜单
- [ ] 点击"清空"清除所有日志
- [ ] 点击菜单外部取消操作
- [ ] ClientTab 和 ServerTab 都能正常工作
- [ ] 日志为空时仍可右键（菜单正常显示）

## 未来扩展

如果需要更多菜单项（如"复制"、"导出日志"等），可以：
- 扩展 `showContextMenu` 返回更多 action 类型
- 在 LogPanel 添加更多回调 props
- 或重构为通用的 ContextMenu 组件
