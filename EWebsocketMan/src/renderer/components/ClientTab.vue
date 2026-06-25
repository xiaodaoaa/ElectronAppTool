<template>
  <div style="display:flex;flex-direction:column;height:100%;">
    <!-- URL Bar -->
    <div class="url-bar">
      <input type="text" v-model="clientUrl" placeholder="ws://127.0.0.1:20012" />
      <button :class="isConnected ? 'danger' : 'success'" @click="toggleConnection">
        {{ isConnected ? '关闭连接' : '开始连接' }}
      </button>
      <label style="font-size:12px;display:flex;align-items:center;gap:3px;">
        <input type="checkbox" v-model="shareUrl" /> 共享URL
      </label>
      <label style="font-size:12px;display:flex;align-items:center;gap:3px;">
        <input type="checkbox" v-model="shareHead" /> 共享head
      </label>
    </div>

    <!-- Main Layout -->
    <div class="main-layout">
      <!-- Left Panel - Request List -->
      <div class="left-panel">
        <div class="panel-label">请求列表：</div>
        <div class="connection-list">
          <div
            v-for="(req, idx) in requestList"
            :key="idx"
            class="conn-item"
            :class="{ selected: selectedRequest === idx }"
            @click="selectRequest(idx)"
          >
            <span class="conn-addr">{{ req.url }}</span>
          </div>
        </div>
      </div>

      <!-- Right Panel -->
      <div class="right-panel">
        <!-- Headers Section -->
        <div class="headers-section">
          <div class="headers-label">Headers：</div>
          <div class="header-row" v-for="(header, idx) in headers" :key="idx">
            <input type="text" v-model="header.key" placeholder="Header名" />
            <input type="text" v-model="header.value" placeholder="Header值" />
          </div>
        </div>

        <!-- Send Area -->
        <div class="send-area">
          <div class="area-label">
            <span class="label-left">
              发送区：
              <input
                type="text"
                v-model="saveRequestName"
                placeholder="保存当前请求为文件，输入文件名"
                style="flex:1;max-width:300px;"
              />
              <button @click="saveRequest" style="font-size:12px;">保存文件</button>
              <label><input type="checkbox" v-model="sendIsHex" /> 16进制</label>
            </span>
            <button class="primary" @click="sendMessage" :disabled="!isConnected">发送</button>
          </div>
          <div class="send-row">
            <label style="font-size:12px;display:flex;align-items:center;gap:3px;">
              <input type="checkbox" v-model="sendFileMode" /> 发送文件...
            </label>
            <input
              v-if="sendFileMode"
              type="text"
              v-model="sendFilePath"
              placeholder="输入文件路径"
              style="flex:1;"
            />
            <button v-if="sendFileMode" @click="selectSendFile" style="font-size:12px;">选择文件</button>
            <span style="font-size:11px;color:#888;">优先级高，文件是按16进制发送</span>
          </div>
          <textarea
            class="send-textarea"
            v-model="sendText"
            placeholder="输入要发送的消息..."
          ></textarea>
        </div>

        <!-- Display Area -->
        <div class="display-area">
          <div class="area-label">
            <span class="label-left">
              显示区：
            </span>
            <div class="clear-btns">
              <button @click="clearUp">↑ 清空</button>
              <button @click="clearDown">↓ 清空</button>
            </div>
          </div>
          <div class="save-to-file-row">
            <label><input type="checkbox" v-model="saveToFile" /> 保存到文件...</label>
            <input v-if="saveToFile" type="text" v-model="saveFilePath" placeholder="输入保存路径" style="width:160px;" />
            <button v-if="saveToFile" @click="selectSaveFile" style="font-size:12px;">选择文件</button>
            <label style="font-size:12px;display:flex;align-items:center;gap:3px;margin-left:auto;">
              <input type="checkbox" v-model="autoScroll" /> 自动滚动到底部
            </label>
          </div>
          <div class="display-box" ref="displayBox" :key="displayKey">
            <div v-for="(msg, idx) in displayMessages" :key="idx">
              <template v-if="msg.type === 'connected'">
                <span class="msg-arrow-in">&lt;——</span>
                <span class="msg-time"> 接收: {{ msg.time }}</span><br>
                <span>{{ msg.content }}</span>
              </template>
              <template v-else-if="msg.type === 'disconnected'">
                <span class="msg-arrow-in">&lt;——</span>
                <span class="msg-time"> 接收: {{ msg.time }}</span><br>
                <span>{{ msg.content }}</span>
              </template>
              <template v-else-if="msg.type === 'received'">
                <span class="msg-arrow-in">&lt;——</span>
                <span class="msg-time"> 接收: {{ msg.time }}</span><br>
                <span>{{ msg.content }}</span>
              </template>
              <template v-else-if="msg.type === 'sent'">
                <span class="msg-arrow-out">——&gt;</span>
                <span class="msg-time"> 发送: {{ msg.time }}</span><br>
                <span>{{ msg.content }}</span>
              </template>
              <template v-else-if="msg.type === 'error'">
                <span style="color:red;">错误: {{ msg.content }}</span>
              </template>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Status Bar -->
    <div class="status-bar">
      <span>{{ isConnected ? '已连接: ' + clientUrl : '未连接' }}</span>
    </div>
  </div>
</template>

<script>
import { ref, onMounted, onUnmounted, nextTick, watch } from 'vue'

export default {
  name: 'ClientTab',
  setup() {
    const clientUrl = ref('ws://127.0.0.1:20012')
    const shareUrl = ref(false)
    const shareHead = ref(false)
    const isConnected = ref(false)
    const requestList = ref([])
    const selectedRequest = ref(-1)
    const headers = ref([
      { key: '', value: '' },
      { key: '', value: '' },
      { key: '', value: '' },
      { key: '', value: '' }
    ])
    const sendText = ref('测试消息001')
    const sendIsHex = ref(false)
    const sendFileMode = ref(false)
    const sendFilePath = ref('')
    const saveRequestName = ref('')
    const displayMessages = ref([])
    const displayKey = ref(0)  // 用于强制重新渲染 display-box
    const saveToFile = ref(false)
    const saveFilePath = ref('')
    const displayBox = ref(null)
    const autoScroll = ref(true)

    // Collect unsubscribe functions for cleanup
    const unsubscribers = []

    function subscribe(channel, cb) {
      const unsub = window.electronAPI[channel](cb)
      unsubscribers.push(unsub)
    }

    function toggleConnection() {
      if (isConnected.value) {
        window.electronAPI.disconnectClient()
      } else {
        const headerObj = {}
        headers.value.forEach(h => {
          if (h.key && h.value) headerObj[h.key] = h.value
        })

        window.electronAPI.connectClient({
          url: clientUrl.value,
          headers: headerObj
        })

        requestList.value.push({ url: clientUrl.value })
        selectedRequest.value = requestList.value.length - 1
      }
    }

    function selectRequest(idx) {
      selectedRequest.value = idx
      if (requestList.value[idx]) {
        clientUrl.value = requestList.value[idx].url
      }
    }

    function sendMessage() {
      if (!isConnected.value) return
      let message = sendText.value
      if (sendFileMode.value && sendFilePath.value) {
        // Read file as hex and send
        window.electronAPI.readFileBinary({ filePath: sendFilePath.value })
          .then(result => {
            if (result?.success) {
              window.electronAPI.sendClientMessage({
                message: result.hex,
                isHex: true
              })
              // Don't push locally - the onClientMessageSent event will handle display
            } else {
              displayMessages.value.push({
                type: 'error',
                content: `读取文件失败: ${result?.error || '未知错误'}`
              })
              scrollToBottom()
            }
          })
          .catch(err => {
            displayMessages.value.push({
              type: 'error',
              content: `读取文件失败: ${err.message || '未知错误'}`
            })
            scrollToBottom()
          })
        return
      }
      window.electronAPI.sendClientMessage({
        message,
        isHex: sendIsHex.value
      })
    }

    function saveRequest() {
      // Pick save location, then write current request (URL + headers + body) to file
      window.electronAPI.selectSaveFile().then(result => {
        if (!result?.success || !result?.filePath) return
        // Update the input field to show selected file path
        saveRequestName.value = result.filePath
        const lines = []
        lines.push(`URL: ${clientUrl.value}`)
        const activeHeaders = headers.value.filter(h => h.key && h.value)
        if (activeHeaders.length > 0) {
          lines.push('')
          lines.push('Headers:')
          activeHeaders.forEach(h => lines.push(`${h.key}: ${h.value}`))
        }
        lines.push('')
        lines.push('Body:')
        lines.push(sendText.value)
        return window.electronAPI.writeFile({
          filePath: result.filePath,
          content: lines.join('\n')
        })
      }).then(writeResult => {
        if (!writeResult) return  // dialog cancelled
        if (writeResult.success) {
          displayMessages.value.push({
            type: 'system',
            content: `请求已保存`
          })
        } else {
          displayMessages.value.push({
            type: 'error',
            content: `保存失败: ${writeResult.error || '未知错误'}`
          })
        }
        scrollToBottom()
      })
    }

    function clearUp() {
      displayMessages.value.splice(0, displayMessages.value.length)
      sendText.value = ''
      displayKey.value++
    }
    function clearDown() {
      displayMessages.value.splice(0, displayMessages.value.length)
      sendText.value = ''
      displayKey.value++
    }

    function scrollToBottom() {
      if (!autoScroll.value) return
      nextTick(() => {
        if (displayBox.value) {
          displayBox.value.scrollTop = displayBox.value.scrollHeight
        }
      })
    }

    function formatTime(isoString) {
      const d = new Date(isoString)
      const pad = n => String(n).padStart(2, '0')
      return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`
    }

    function saveReceivedMessage(message, isBinary) {
      if (saveToFile.value && saveFilePath.value) {
        if (isBinary) {
          window.electronAPI.appendBinaryToFile({ filePath: saveFilePath.value, hex: message })
        } else {
          window.electronAPI.appendToFile({ filePath: saveFilePath.value, content: message })
        }
      }
    }

    function selectSaveFile() {
      window.electronAPI.selectSaveFile().then(result => {
        if (result?.success && result?.filePath) {
          saveFilePath.value = result.filePath
        }
      })
    }

    function selectSendFile() {
      window.electronAPI.selectOpenFile().then(result => {
        if (result?.success && result?.filePath) {
          sendFilePath.value = result.filePath
        }
      })
    }

    // ─ Config persistence ─
    let saveTimer = null
    function persistConfig() {
      clearTimeout(saveTimer)
      saveTimer = setTimeout(() => {
        window.electronAPI.loadConfig().then(result => {
          const existing = (result?.success && result?.config) ? result.config : {}
          existing.client = {
            clientUrl: clientUrl.value,
            shareUrl: shareUrl.value,
            shareHead: shareHead.value,
            sendText: sendText.value,
            sendIsHex: sendIsHex.value,
            sendFileMode: sendFileMode.value,
            saveToFile: saveToFile.value,
            saveFilePath: saveFilePath.value,
            autoScroll: autoScroll.value,
            headers: headers.value.map(h => ({ key: h.key, value: h.value }))
          }
          window.electronAPI.saveConfig(existing)
        })
      }, 300)
    }

    watch([clientUrl, shareUrl, shareHead, sendText, sendIsHex, sendFileMode, saveToFile, saveFilePath, autoScroll, headers], persistConfig, { deep: true })

    onMounted(() => {
      // Load saved config
      window.electronAPI.loadConfig().then(result => {
        if (result?.success && result?.config?.client) {
          const c = result.config.client
          if (c.clientUrl !== undefined) clientUrl.value = c.clientUrl
          if (c.shareUrl !== undefined) shareUrl.value = c.shareUrl
          if (c.shareHead !== undefined) shareHead.value = c.shareHead
          if (c.sendText !== undefined) sendText.value = c.sendText
          if (c.sendIsHex !== undefined) sendIsHex.value = c.sendIsHex
          if (c.sendFileMode !== undefined) sendFileMode.value = c.sendFileMode
          if (c.saveToFile !== undefined) saveToFile.value = c.saveToFile
          if (c.saveFilePath !== undefined) saveFilePath.value = c.saveFilePath
          if (c.autoScroll !== undefined) autoScroll.value = c.autoScroll
          if (c.headers && Array.isArray(c.headers)) {
            c.headers.forEach((h, i) => {
              if (headers.value[i]) {
                headers.value[i].key = h.key || ''
                headers.value[i].value = h.value || ''
              }
            })
          }
        }
      })

      subscribe('onClientConnected', (data) => {
        isConnected.value = true
        displayMessages.value.push({
          type: 'connected',
          content: 'connected',
          time: formatTime(data.timestamp)
        })
        scrollToBottom()
      })

      subscribe('onClientDisconnected', (data) => {
        isConnected.value = false
        displayMessages.value.push({
          type: 'disconnected',
          content: 'disconnected',
          time: formatTime(data.timestamp)
        })
        scrollToBottom()
      })

      subscribe('onClientMessageReceived', (data) => {
        const content = data.isBinary ? '[文件]' : data.message
        displayMessages.value.push({
          type: 'received',
          content,
          time: formatTime(data.timestamp)
        })
        saveReceivedMessage(data.message, data.isBinary)
        scrollToBottom()
      })

      subscribe('onClientMessageSent', (data) => {
        const content = data.isBinary ? '[文件]' : data.message
        displayMessages.value.push({
          type: 'sent',
          content,
          time: formatTime(data.timestamp)
        })
        scrollToBottom()
      })

      subscribe('onClientError', (data) => {
        displayMessages.value.push({
          type: 'error',
          content: data.error,
          time: data.timestamp ? formatTime(data.timestamp) : ''
        })
        scrollToBottom()
      })
    })

    onUnmounted(() => {
      unsubscribers.forEach(fn => fn())
      unsubscribers.length = 0
    })

    return {
      clientUrl, shareUrl, shareHead, isConnected,
      requestList, selectedRequest, headers,
      sendText, sendIsHex, sendFileMode, sendFilePath, saveRequestName,
      displayMessages, displayKey, saveToFile, saveFilePath, displayBox, autoScroll,
      toggleConnection, selectRequest, sendMessage, saveRequest,
      clearUp, clearDown, selectSaveFile, selectSendFile
    }
  }
}
</script>
