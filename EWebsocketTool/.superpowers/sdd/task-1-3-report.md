# Task 1-3 Report: Right-Click Context Menu IPC Layer

## Status: DONE

## Commits

1. `8f6323b` - feat: add show-context-menu IPC handler with native Menu (electron/main.js)
2. `02a920e` - feat: expose showContextMenu via preload (electron/preload.js)
3. `915a50c` - feat: add showContextMenu to ElectronAPI type (src/types/index.ts)

## Changes Summary

### electron/main.js
- Added `Menu` to the electron require on line 2
- Added `show-context-menu` IPC handler inside `setupIPC()` that creates a native context menu with a "清空" (clear) option. Returns `'clear'` when clicked, or `null` when dismissed without selection.

### electron/preload.js
- Exposed `showContextMenu` method in the `electronAPI` bridge, mapping to `ipcRenderer.invoke('show-context-menu')`

### src/types/index.ts
- Added `showContextMenu: () => Promise<string | null>` to the `ElectronAPI` interface

## Concerns

None. All three files compile cleanly and commits are clean. The IPC handler follows the same pattern as existing handlers (`broadcast`, `send-to-clients`). The context menu currently only has one option ("清空"); additional options can be added to the template array in future tasks.
