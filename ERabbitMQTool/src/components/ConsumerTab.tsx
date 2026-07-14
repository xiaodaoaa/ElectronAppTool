import { useState, useCallback, useEffect } from 'react'
import { Input, Button, Tag, Typography } from 'antd'
import type { ReceivedMessage } from '../types'

const { Text } = Typography

interface ConsumerTabProps {
  connected: boolean
  messages: ReceivedMessage[]
  onSubscribe: (queue: string) => Promise<{ success: boolean; consumerTag?: string }>
  onUnsubscribe: (consumerTag: string) => Promise<{ success: boolean }>
  defaultQueue?: string
  onQueueChange?: (queue: string) => void
}

const ConsumerTab: React.FC<ConsumerTabProps> = ({ connected, messages, onSubscribe, onUnsubscribe, defaultQueue, onQueueChange }) => {
  const [queue, setQueue] = useState(defaultQueue ?? '')
  const [consumerTag, setConsumerTag] = useState<string | null>(null)
  const [subscribing, setSubscribing] = useState(false)

  useEffect(() => {
    if (defaultQueue) setQueue(defaultQueue)
  }, [defaultQueue])

  const handleQueueChange = useCallback((value: string) => {
    setQueue(value)
    onQueueChange?.(value)
  }, [onQueueChange])

  const handleSubscribe = useCallback(async () => {
    if (!queue.trim()) return
    setSubscribing(true)
    const result = await onSubscribe(queue.trim())
    if (result.success && result.consumerTag) {
      setConsumerTag(result.consumerTag)
    }
    setSubscribing(false)
  }, [queue, onSubscribe])

  const handleUnsubscribe = useCallback(async () => {
    if (!consumerTag) return
    await onUnsubscribe(consumerTag)
    setConsumerTag(null)
  }, [consumerTag, onUnsubscribe])

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12, height: '100%' }}>
      <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
        <Input
          placeholder="Queue 名"
          value={queue}
          onChange={(e) => handleQueueChange(e.target.value)}
          disabled={!connected || !!consumerTag}
          style={{ flex: 1 }}
        />
        {!consumerTag ? (
          <Button
            type="primary"
            onClick={handleSubscribe}
            loading={subscribing}
            disabled={!connected || !queue.trim()}
          >
            订阅
          </Button>
        ) : (
          <Button danger onClick={handleUnsubscribe}>
            取消订阅
          </Button>
        )}
      </div>

      {consumerTag && (
        <Tag color="blue">已订阅: {consumerTag}</Tag>
      )}

      <div style={{ flex: 1, overflow: 'auto', border: '1px solid #d9d9d9', borderRadius: 8, padding: 8 }}>
        {messages.length === 0 ? (
          <div style={{ color: '#999', textAlign: 'center', padding: 40 }}>暂无消息</div>
        ) : (
          messages.map((msg, i) => (
            <div
              key={i}
              style={{
                padding: '6px 8px',
                borderBottom: '1px solid #f0f0f0',
                fontFamily: 'Consolas, Monaco, monospace',
                fontSize: 13,
              }}
            >
              <div>
                <Text type="secondary" style={{ fontSize: 12 }}>[{msg.timestamp}]</Text>
                <Tag style={{ marginLeft: 6 }} color="green">{msg.queue}</Tag>
              </div>
              <div style={{ marginTop: 2 }}>{msg.message}</div>
              <details style={{ marginTop: 4 }}>
                <summary style={{ fontSize: 12, color: '#999', cursor: 'pointer' }}>Properties</summary>
                <pre style={{ fontSize: 11, margin: 4, whiteSpace: 'pre-wrap' }}>
                  {JSON.stringify(msg.properties, null, 2)}
                </pre>
              </details>
            </div>
          ))
        )}
      </div>
    </div>
  )
}

export default ConsumerTab