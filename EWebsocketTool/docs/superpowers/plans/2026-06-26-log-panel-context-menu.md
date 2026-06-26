# LogPanel 右键菜单清空功能 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a native right-click context menu to the LogPanel with a "清空" option that clears displayed log entries.

**Architecture:** LogPanel listens for `contextmenu` events and calls the Electron main process via IPC to show a native `Menu.popup()`. The main process returns the user's selection. LogPanel calls an `onClear` callback prop so the parent tab (ClientTab/ServerTab) clears its `logs` state.

**Tech Stack:** Electron `Menu` API, `ipcMain.handle` / `ipcRenderer.invoke`, React `onContextMenu`

## Global Constraints

- Use native Electron `Menu.popup()` — no custom HTML menus
- LogPanel stays a pure display component — it does not own log state
- All changes must work in both ClientTab and ServerTab

## File Structure

| Action | File | Responsibility |
|--------|------|---------------|
| Modify | `electron/main.js` | Add `show-context-menu` IPC handler using `Menu.popup()` |
| Modify | `electron/preload.js` | Expose `showContextMenu()` to renderer |
| Modify | `src/types/index.ts` | Add `showContextMenu` to `ElectronAPI` interface |
| Modify | `src/components/LogPanel.tsx` | Add `onContextMenu` handler + `onClear` prop |
| Modify | `src/components/ClientTab.tsx` | Pass `onClear={() => setLogs([])}` to LogPanel |
| Modify | `src/components/ServerTab.tsx` | Pass `onClear={() => setLogs([])}` to LogPanel |

---

### Task 1: Main Process — Add Context Menu IPC Handler

**Files:**
- Modify: `electron/main.js`

- [ ] **Step 1: Import Menu from electron**

At the top of `electron/main.js`, add `Menu` to the existing require:

```js
// Change line 2 from:
const { app, BrowserWindow, ipcMain } = require('electron')
// To:
const { app, BrowserWindow, ipcMain, Menu } = require('electron')
```

- [ ] **Step 2: Add the `show-context-menu` IPC handler**

Inside the `setupIPC()` function (after the existing handlers, before the closing `}`), add:

```js
  ipcMain.handle('show-context-menu', async () => {
    return new Promise((resolve) => {
      const menu = Menu.buildFromTemplate([
        {
          label: '清空',
          click: () => resolve('clear'),
        },
      ])
      menu.popup({
        callback: () => resolve(null),
      })
    })
  })
```

When the user clicks "清空", the promise resolves with `'clear'`. When the menu is dismissed without selection, the callback fires and resolves with `null`.

- [ ] **Step 3: Commit**

```bash
git add electron/main.js
git commit -m "feat: add show-context-menu IPC handler with native Menu"
```

---

### Task 2: Preload — Expose showContextMenu API

**Files:**
- Modify: `electron/preload.js`

- [ ] **Step 1: Add showContextMenu to contextBridge**

Inside the `contextBridge.exposeInMainWorld('electronAPI', { ... })` call, add a new method. Place it after the `broadcast` line (line 9) and before the event listener methods:

```js
  showContextMenu: () => ipcRenderer.invoke('show-context-menu'),
```

- [ ] **Step 2: Commit**

```bash
git add electron/preload.js
git commit -m "feat: expose showContextMenu via preload"
```

---

### Task 3: TypeScript Types — Update ElectronAPI Interface

**Files:**
- Modify: `src/types/index.ts`

- [ ] **Step 1: Add showContextMenu to ElectronAPI**

Add this line inside the `ElectronAPI` interface, after the `broadcast` line (line 21):

```ts
  showContextMenu: () => Promise<string | null>
```

- [ ] **Step 2: Commit**

```bash
git add src/types/index.ts
git commit -m "feat: add showContextMenu to ElectronAPI type"
```

---

### Task 4: LogPanel — Add Right-Click Handler and onClear Prop

**Files:**
- Modify: `src/components/LogPanel.tsx`

- [ ] **Step 1: Update the component**

Replace the full content of `src/components/LogPanel.tsx` with:

```tsx
import { useEffect, useRef } from 'react'
import { LogEntry } from '../types'

interface LogPanelProps {
  logs: LogEntry[]
  onClear?: () => void
}

const DIRECTION_LABELS: Record<string, string> = {
  send: '发送',
  receive: '接收',
  info: '信息',
  error: '错误',
}

const LogPanel = ({ logs, onClear }: LogPanelProps) => {
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
        onContextMenu={handleContextMenu}
      >
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
```

Changes summary:
- Added `onClear?: () => void` to `LogPanelProps`
- Added `handleContextMenu` async handler that calls `showContextMenu()` and invokes `onClear` when result is `'clear'`
- Added `onContextMenu={handleContextMenu}` to the `.log-panel-content` div

- [ ] **Step 2: Commit**

```bash
git add src/components/LogPanel.tsx
git commit -m "feat: add right-click context menu with clear option to LogPanel"
```

---

### Task 5: Parent Tabs — Pass onClear Callback

**Files:**
- Modify: `src/components/ClientTab.tsx`
- Modify: `src/components/ServerTab.tsx`

- [ ] **Step 1: Update ClientTab**

In `src/components/ClientTab.tsx`, change the LogPanel usage (line 83):

```tsx
// Change from:
        <LogPanel logs={logs} />
// To:
        <LogPanel logs={logs} onClear={() => setLogs([])} />
```

- [ ] **Step 2: Update ServerTab**

In `src/components/ServerTab.tsx`, change the LogPanel usage (line 300):

```tsx
// Change from:
        <LogPanel logs={logs} />
// To:
        <LogPanel logs={logs} onClear={() => setLogs([])} />
```

- [ ] **Step 3: Commit**

```bash
git add src/components/ClientTab.tsx src/components/ServerTab.tsx
git commit -m "feat: wire up onClear callback in ClientTab and ServerTab"
```

---

### Task 6: Manual Verification

- [ ] **Step 1: Run the app in dev mode**

```bash
npm run dev
```

- [ ] **Step 2: Verify ClientTab context menu**

1. Switch to the "客户端" (Client) tab
2. Right-click on the "消息日志" area
3. Verify a native context menu appears with "清空" option
4. Click "清空" — verify all log entries disappear
5. Right-click again, click outside the menu to dismiss — verify nothing happens

- [ ] **Step 3: Verify ServerTab context menu**

1. Switch to the "服务端" (Server) tab
2. Repeat the same steps as Step 2
3. Verify the same behavior

- [ ] **Step 4: Verify empty state**

1. With no logs displayed, right-click the log area
2. Verify the menu still appears
3. Click "清空" — verify no errors occur

- [ ] **Step 5: Build verification**

```bash
npm run build
```

Expected: Build completes without TypeScript errors.
