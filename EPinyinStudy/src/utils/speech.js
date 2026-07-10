// Web Speech API 汉语发音工具。
// 只设置 u.lang = 'zh-CN' 不够：浏览器可能默认选 en-US voice，
// 把拼音字母 "b/p/m/f" 当成英文字母读。必须显式选 zh-CN 的 voice。

let cachedZhVoice = null
let voicesReady = null // Promise，voices 加载完成后 resolve

function getVoices() {
  return window.speechSynthesis.getVoices() || []
}

// Chromium 的 voices 是异步加载的，首次启动时 getVoices() 可能返回空数组。
// 监听 voiceschanged 事件并缓存 Promise，避免每次发音都重新查找。
function ensureVoicesReady() {
  if (voicesReady) return voicesReady
  const synth = window.speechSynthesis
  if (!synth) return Promise.resolve()
  voicesReady = new Promise((resolve) => {
    const done = () => {
      cachedZhVoice = pickZhVoice(getVoices())
      resolve()
    }
    // Chromium: voices 异步填充，触发 voiceschanged。
    synth.addEventListener('voiceschanged', done, { once: true })
    // 兜底：若已同步可用，或事件不触发（部分平台），立即解析一次。
    done()
  })
  return voicesReady
}

function pickZhVoice(voices) {
  if (!voices || !voices.length) return null
  // 优先精确匹配 zh-CN；其次任意 zh-*（zh-TW/zh-HK 也可接受，至少是中文音）。
  return (
    voices.find((v) => v.lang === 'zh-CN') ||
    voices.find((v) => v.lang && v.lang.toLowerCase().startsWith('zh')) ||
    null
  )
}

/**
 * 用汉语 voice 朗读文本。
 * @param {string} text 要朗读的内容
 * @param {number} rate 语速，默认 0.8
 */
export async function speakZh(text, rate = 0.8) {
  const synth = window.speechSynthesis
  if (!synth || !text) return
  await ensureVoicesReady()
  const voice = cachedZhVoice || pickZhVoice(getVoices())
  const u = new SpeechSynthesisUtterance(text)
  u.lang = 'zh-CN'
  u.rate = rate
  if (voice) u.voice = voice // 显式选 zh-CN voice，覆盖默认的 en-US
  synth.speak(u)
}

// 拼音声母/韵母本身是拉丁字母（b/p/m/a/o）。Windows 中文 TTS 引擎
// (Microsoft Huihui) 会把孤立的拉丁字母当英文字母名读（"biː"/"piː"），
// 而不是读拼音的"玻/坡/摸/佛"。汉字才是中文 TTS 能正确发音的内容。
// 声母优先读 soundChar（标准呼读音示范字，如 b→"播" bō），确保听到的是
// 该声母的标准读音；无 soundChar 时退回 examples[0]（韵母无 soundChar）。
export async function speakPinyin(item, rate = 0.8) {
  const soundChar = item?.soundChar
  const example = item?.examples?.[0]
  const text = soundChar || example
  if (text) {
    await speakZh(text, rate)
  } else {
    // 兜底：朗读拼音文本本身（可能仍被当字母读）
    await speakZh(item?.pinyin, rate)
  }
}
