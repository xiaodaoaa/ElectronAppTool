import { useState, useCallback, useEffect } from 'react'
import { ConfigProvider, Tabs } from 'antd'
import type { TabsProps } from 'antd'
import ConnectionPanel from './components/ConnectionPanel'
import ProducerTab from './components/ProducerTab'
import ConsumerTab from './components/ConsumerTab'
import LogPanel from './components/LogPanel'
import { useRabbitMQ } from './hooks/useRabbitMQ'
import { useConfig } from './hooks/useConfig'
import type { ServerInfo, ReceivedMessage, LogEntry } from './types'

const App: React.FC = () => {
  const { config, updateConfig, loaded } = useConfig()
  const [connected, setConnected] = useState(false)
  const [connecting, setConnecting] = useState(false)
  const [serverInfo, setServerInfo] = useState<ServerInfo | null>(null)
  const [logs, setLogs] = useState<LogEntry[]>([])
  const [messages, setMessages] = useState<ReceivedMessage[]>([])
  const [activeTab, setActiveTab] = useState('producer')

  const addLog = useCallback((entry: LogEntry) => {
    setLogs((prev) => [...prev, entry])
  }, [])

  const onConnected = useCallback((info: ServerInfo) => {
    setConnected(true)
    setConnecting(false)
    setServerInfo(info)
    addLog({ time: new Date().toLocaleTimeString(), type: 'connect', detail: `已连接到 amqp://${info.host}:${info.port}${info.vhost}` })
  }, [addLog])

  const onDisconnected = useCallback((reason: string) => {
    setConnected(false)
    setConnecting(false)
    setServerInfo(null)
    setMessages([])
    addLog({ time: new Date().toLocaleTimeString(), type: 'disconnect', detail: reason })
  }, [addLog])

  const onConnectionError = useCallback((message: string) => {
    setConnecting(false)
    addLog({ time: new Date().toLocaleTimeString(), type: 'error', detail: `连接失败: ${message}` })
  }, [addLog])

  const onMessageReceived = useCallback((msg: ReceivedMessage) => {
    setMessages((prev) => [...prev, msg])
  }, [])

  const onPublishConfirmed = useCallback((result: { success: boolean; message?: string }) => {
    if (!result.success) {
      addLog({ time: new Date().toLocaleTimeString(), type: 'error', detail: `发送失败: ${result.message || '未知错误'}` })
    }
  }, [addLog])

  const onLogEvent = useCallback((entry: LogEntry) => {
    addLog(entry)
  }, [addLog])

  const { connect, disconnect, publish, subscribe, unsubscribe } = useRabbitMQ({
    onConnected, onDisconnected, onConnectionError, onMessageReceived, onPublishConfirmed, onLogEvent,
  })

  const handleConnect = useCallback(async () => {
    setConnecting(true)
    const result = await connect(config)
    if (!result.success) setConnecting(false)
  }, [connect, config])

  const handleDisconnect = useCallback(async () => {
    await disconnect()
  }, [disconnect])

  const serverLabel = serverInfo ? `${serverInfo.host}:${serverInfo.port}` : ''

  const tabItems: TabsProps['items'] = [
    {
      key: 'producer',
      label: '生产者',
      children: (
        <ProducerTab
          connected={connected}
          onPublish={publish}
        />
      ),
    },
    {
      key: 'consumer',
      label: '消费者',
      children: (
        <ConsumerTab
          connected={connected}
          messages={messages}
          onSubscribe={subscribe}
          onUnsubscribe={unsubscribe}
        />
      ),
    },
  ]

  return (
    <ConfigProvider theme={{ token: { colorPrimary: '#1677ff' } }}>
      <div className="app-container">
        <div style={{ borderBottom: '1px solid #d9d9d9', paddingBottom: 4 }}>
          <Tabs
            activeKey={activeTab}
            onChange={setActiveTab}
            items={[
              { key: 'producer', label: '生产者' },
              { key: 'consumer', label: '消费者' },
            ]}
            className="app-tabs"
          />
          {loaded && (
            <ConnectionPanel
              config={config}
              onConfigChange={updateConfig}
              connected={connected}
              connecting={connecting}
              onConnect={handleConnect}
              onDisconnect={handleDisconnect}
              serverLabel={serverLabel}
            />
          )}
        </div>
        <div className="tab-content">
          <div className="tab-layout">
            <div className="tab-left">
              {activeTab === 'producer' ? (
                <ProducerTab connected={connected} onPublish={publish} />
              ) : (
                <ConsumerTab
                  connected={connected}
                  messages={messages}
                  onSubscribe={subscribe}
                  onUnsubscribe={unsubscribe}
                />
              )}
            </div>
            <div className="tab-right">
              <LogPanel logs={logs} />
            </div>
          </div>
        </div>
      </div>
    </ConfigProvider>
  )
}

export default App