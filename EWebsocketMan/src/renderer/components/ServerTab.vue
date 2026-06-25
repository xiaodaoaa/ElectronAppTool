<template>
  <div style="display:flex;flex-direction:column;height:100%;">
    <!-- URL Bar -->
    <div class="url-bar">
      <input type="text" v-model="serverUrl" placeholder="ws://127.0.0.1:20012" />
      <button :class="isRunning ? 'danger' : 'success'" @click="toggleServer">
        {{ isRunning ? '停止监听' : '开始监听' }}
      </button>
    </div>

    <!-- Options Row -->
    <div class="options-row">
      <label>
        ssl key password
        <input type="text" v-model="sslPassword" placeholder="" />
      </label>
      <label><input type="checkbox" v-model="optTls13" /> TLS1.3</label>
      <label><input type="checkbox" v-model="optSendall" /> sendall</label>
      <label><input type="checkbox" v-model="optEcho" /> echo</label>
    </div>

    <!-- Main Layout -->
    <div class="main-layout">
      <!-- Left Panel - Connection List -->
      <div class="left-panel">
        <div class="panel-label">连接列表：</div>
        <div class="connection-list">
          <div
            v-for="addr in connectedClients"
            :key="addr"
            class="conn-item"
            :class="{ selected: selectedClient === addr }"
            @click="selectedClient = addr"
          >
            <span class="conn-addr">{{ addr }}</span>
          </div>
        </div>
      </div>

      <!-- Right Panel -->
      <div class="right-panel">
        <!-- Send Area -->
        <div class="send-area">
          <div class="area-label">
            <span class="label-left">
              发送区：
              <label><input type="checkbox" v-model="sendIsHex" /> 16进制</label>
            </span>
            <button class="primary" @click="sendToClient" :disabled="!selectedClient">发送</button>
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
              <label><input type="checkbox" v-model="saveToFile" /> 保存到文件...</label>
              <input v-if="saveToFile" type="text" v-model="saveFilePath" placeholder="输入保存路径" style="width:160px;" />
              <button v-if="saveToFile" @click="selectSaveFile" style="font-size:12px;">选择文件</button>
              <label><input type="checkbox" v-model="autoScroll" /> 自动滚动到底部</label>
            </span>
            <div class="clear-btns">
              <button @click="clearUp">↑ 清空</button>
              <button @click="clearDown">↓ 清空</button>
            </div>
          </div>
          <div class="display-box" ref="displayBox">
            <div v-for="(msg, idx) in displayMessages" :key="idx">
              <template v-if="msg.type === 'handshake'">
                <span class="msg-handshake">{{ msg.content }}</span>
              </template>
              <template v-else-if="msg.type === 'received'">
                <span class="msg-arrow-in">&lt;——</span>
                <span class="msg-time"> 接收: {{ msg.time }}</span><br>
                <span>{{ msg.clientAddr }}: {{ msg.content }}</span>
              </template>
              <template v-else-if="msg.type === 'sent'">
                <span class="msg-arrow-out">——&gt;</span>
                <span class="msg-time"> 发送: {{ msg.time }}</span><br>
                <span>{{ msg.clientAddr }}: {{ msg.content }}</span>
              </template>
              <template v-else-if="msg.type === 'system'">
                <span style="color:#888;">{{ msg.content }}</span>
              </template>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Status Bar -->
    <div class="status-bar">
      <span>{{ selectedClient || '未选择客户端' }}</span>
    </div>
  </div>
</template>

<script>
import { ref, onMounted, onUnmounted, nextTick, watch } from 'vue'

export default {
  name: 'ServerTab',
  setup() {
    const serverUrl = ref('ws://127.0.0.1:20012')
    const sslPassword = ref('')
    const optTls13 = ref(false)
    const optSendall = ref(false)
    const optEcho = ref(false)
    const isRunning = ref(false)
    const connectedClients = ref([])
    const selectedClient = ref('')
    const sendText = ref('这是服务端消息')
    const sendIsHex = ref(false)
    const sendFileMode = ref(false)
    const sendFilePath = ref('')
    const displayMessages = ref([])
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

    function toggleServer() {
      if (isRunning.value) {
        window.electronAPI.stopServer()
      } else {
        window.electronAPI.startServer({
          url: serverUrl.value,
          options: {
            tls13: optTls13.value,
            sendall: optSendall.value,
            echo: optEcho.value
          }
        })
      }
    }

    function sendToClient() {
      if (!selectedClient.value) return
      let message = sendText.value
      if (sendFileMode.value && sendFilePath.value) {
        message = `[文件: ${sendFilePath.value}]`
      }
      window.electronAPI.sendServerMessage({
        clientAddr: selectedClient.value,
        message,
        isHex: sendIsHex.value
      })
    }

    function clearUp() { displayMessages.value = [] }
    function clearDown() { displayMessages.value = [] }

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

    function logToFile(line) {
      if (saveToFile.value && saveFilePath.value) {
        window.electronAPI.appendToFile({ filePath: saveFilePath.value, content: line })
      }
    }

    function selectSaveFile() {
      window.electronAPI.selectSaveFile().then(result => {
        if (result?.success && result?.filePath) {
          saveFilePath.value = result.filePath
        }
      })
    }

    // ─ Config persistence ──
    let saveTimer = null
    function persistConfig() {
      clearTimeout(saveTimer)
      saveTimer = setTimeout(() => {
        window.electronAPI.loadConfig().then(result => {
          const existing = (result?.success && result?.config) ? result.config : {}
          existing.server = {
            serverUrl: serverUrl.value,
            sslPassword: sslPassword.value,
            optTls13: optTls13.value,
            optSendall: optSendall.value,
            optEcho: optEcho.value,
            sendText: sendText.value,
            sendIsHex: sendIsHex.value,
            sendFileMode: sendFileMode.value,
            sendFilePath: sendFilePath.value,
            saveToFile: saveToFile.value,
            saveFilePath: saveFilePath.value,
            autoScroll: autoScroll.value
          }
          window.electronAPI.saveConfig(existing)
        })
      }, 300)
    }

    watch([serverUrl, sslPassword, optTls13, optSendall, optEcho, sendText, sendIsHex, sendFileMode, sendFilePath, saveToFile, saveFilePath, autoScroll], persistConfig)

    onMounted(() => {
      // Load saved config
      window.electronAPI.loadConfig().then(result => {
        if (result?.success && result?.config?.server) {
          const c = result.config.server
          if (c.serverUrl !== undefined) serverUrl.value = c.serverUrl
          if (c.sslPassword !== undefined) sslPassword.value = c.sslPassword
          if (c.optTls13 !== undefined) optTls13.value = c.optTls13
          if (c.optSendall !== undefined) optSendall.value = c.optSendall
          if (c.optEcho !== undefined) optEcho.value = c.optEcho
          if (c.sendText !== undefined) sendText.value = c.sendText
          if (c.sendIsHex !== undefined) sendIsHex.value = c.sendIsHex
          if (c.sendFileMode !== undefined) sendFileMode.value = c.sendFileMode
          if (c.sendFilePath !== undefined) sendFilePath.value = c.sendFilePath
          if (c.saveToFile !== undefined) saveToFile.value = c.saveToFile
          if (c.saveFilePath !== undefined) saveFilePath.value = c.saveFilePath
          if (c.autoScroll !== undefined) autoScroll.value = c.autoScroll
        }
      })

      subscribe('onServerStarted', (data) => {
        isRunning.value = true
        displayMessages.value.push({
          type: 'system',
          content: `服务器已启动，监听端口: ${data.port}`
        })
        logToFile(`服务器已启动，监听端口: ${data.port}`)
        scrollToBottom()
      })

      subscribe('onServerStopped', () => {
        isRunning.value = false
        connectedClients.value = []
        selectedClient.value = ''
        displayMessages.value.push({ type: 'system', content: '服务器已停止' })
        logToFile('服务器已停止')
        scrollToBottom()
      })

      subscribe('onServerClientConnected', (addr) => {
        if (!connectedClients.value.includes(addr)) {
          connectedClients.value.push(addr)
          selectedClient.value = addr
        }
        logToFile(`客户端连接: ${addr}`)
        scrollToBottom()
      })

      subscribe('onServerClientDisconnected', (addr) => {
        connectedClients.value = connectedClients.value.filter(a => a !== addr)
        if (selectedClient.value === addr) {
          selectedClient.value = connectedClients.value[0] || ''
        }
        displayMessages.value.push({
          type: 'system',
          content: `客户端断开: ${addr}`
        })
        logToFile(`客户端断开: ${addr}`)
        scrollToBottom()
      })

      subscribe('onServerHandshake', (data) => {
        displayMessages.value.push({ type: 'handshake', content: data.handshakeInfo })
        logToFile(`握手信息 [${data.clientAddr}]:\n${data.handshakeInfo}`)
        scrollToBottom()
      })

      subscribe('onServerMessageReceived', (data) => {
        displayMessages.value.push({
          type: 'received',
          clientAddr: data.clientAddr,
          content: data.message,
          time: formatTime(data.timestamp)
        })
        logToFile(`[${formatTime(data.timestamp)}] 接收 [${data.clientAddr}]: ${data.message}`)
        scrollToBottom()
      })

      subscribe('onServerMessageSent', (data) => {
        displayMessages.value.push({
          type: 'sent',
          clientAddr: data.clientAddr,
          content: data.message,
          time: formatTime(data.timestamp)
        })
        logToFile(`[${formatTime(data.timestamp)}] 发送 [${data.clientAddr}]: ${data.message}`)
        scrollToBottom()
      })

      subscribe('onServerError', (data) => {
        displayMessages.value.push({
          type: 'system',
          content: `错误: ${data.error}`
        })
        scrollToBottom()
      })
    })

    onUnmounted(() => {
      unsubscribers.forEach(fn => fn())
      unsubscribers.length = 0
    })

    return {
      serverUrl, sslPassword, optTls13, optSendall, optEcho,
      isRunning, connectedClients, selectedClient,
      sendText, sendIsHex, sendFileMode, sendFilePath,
      displayMessages, saveToFile, saveFilePath, displayBox, autoScroll,
      toggleServer, sendToClient, clearUp, clearDown, selectSaveFile
    }
  }
}
</script>
