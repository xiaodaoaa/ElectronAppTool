# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
npm run dev            # Vite dev server (browser only, port 5173)
npm run electron:dev   # Vite + Electron with hot reload
npm run build          # Vite production build → dist/
npm run pack           # Build + package Windows NSIS installer → release/
npm run electron:linux # Build + package Linux AppImage → release/
```

No test runner, linter, or formatter is configured.

## Architecture

**Electron + Vue 3** desktop app for first-grade Chinese pinyin education. No backend, no API calls, no external data — everything is static and bundled at build time.

### Navigation

No Vue Router. `App.vue` owns a single `currentTab` ref and swaps views via `v-if`. Six tabs: home, initials, finals, whole syllables, hanzi, quiz.

### Component tree

```
App.vue  (owns currentTab, imports all data from src/data/pinyin.js)
├── HomePage.vue       — landing page, emits navigate events
├── PinyinTable.vue    — reusable grid (props: title, items, columns), used 3x
│                        clicks trigger Web Speech API pronunciation
├── HanziReader.vue    — character grid with category filter
└── QuizGame.vue       — multiple-choice quiz, self-contained state machine
```

### State management

No Vuex/Pinia. All state is local refs. Data flows one-way via props; child-to-parent via `$emit`. `App.vue` passes pinyin data down as props.

### Electron specifics

- `electron/main.js` — uses `app.isPackaged` to branch between dev (localhost:5173 + DevTools) and production (loadFile dist/index.html).
- `electron/preload.js` — exposes `window.electronAPI.platform` via contextBridge. Currently unused by Vue components.
- `contextIsolation: true`, `nodeIntegration: false`.

### Data

All pinyin/hanzi data lives in `src/data/pinyin.js`: `initials` (23), `finals` (24), `wholeSyllables` (16), `hanziList` (~100 chars with categories). Adding new content means editing this file.

### Build pipeline

Vite builds to `dist/` with `base: './'` (relative paths for Electron `file://` loading). electron-builder bundles `dist/**/*`, `electron/**/*`, and `package.json` into the installer. Windows icon: `public/app.ico`, Linux icon: `public/app.png`.

### Audio

Pronunciation uses the Web Speech API (`SpeechSynthesisUtterance`, `lang: 'zh-CN'`). Rate is 0.8x for pinyin, 0.7x for hanzi.
