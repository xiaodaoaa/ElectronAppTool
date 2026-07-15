import { useState, useCallback } from 'react'
import { Input, Button, Tag, Typography, Radio } from 'antd'
import type { ReceivedMessage } from '../types'

const { Text } = Typography

interface ConsumerTabProps {
  connected: boolean
  messages: ReceivedMessage[]
  onSubscribe: (params: { mode: 'queue' | 'exchange'; target: string }) => Promise<{ success: boolean; consumerTag?: string }>
  onUnsubscribe: (consumerTag: string) => Promise<{ success: boolean }>
  defaultQueue?: string
  onQueueChange?: (queue: string) => void
}

const ConsumerTab: React.FC<ConsumerTabProps> = ({ connected, messages, onSubscribe, onUnsubscribe, defaultQueue, onQueueChange }) => {
  // target 受控：由父组件 useConfig.consumerQueue 单一持有，避免本地副本与 initializedRef 守卫导致的加载值丢失
  const target = defaultQueue ?? ''
  const [mode, setMode] = useState<'queue' | 'exchange'>('queue')
  const [consumerTag, setConsumerTag] = useState<string | null>(null)
  const [subscribing, setSubscribing] = useState(false)

  const handleTargetChange = useCallback((value: string) => {
    onQueueChange?.(value)
  }, [onQueueChange])

  const handleSubscribe = useCallback(async () => {
    if (!target.trim()) return
    setSubscribing(true)
    const result = await onSubscribe({ mode, target: target.trim() })
    if (result.success && result.consumerTag) {
      setConsumerTag(result.consumerTag)
    }
    setSubscribing(false)
  }, [mode, target, onSubscribe])

  const handleUnsubscribe = useCallback(async () => {
    if (!consumerTag) return
    await onUnsubscribe(consumerTag)
    setConsumerTag(null)
  }, [consumerTag, onUnsubscribe])

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12, height: '100%' }}>
      <div>
        <Radio.Group
          value={mode}
          onChange={(e) => setMode(e.target.value)}
          size="small"
          disabled={!!consumerTag}
        >
          <Radio value="queue">Queue</Radio>
          <Radio value="exchange">Exchange</Radio>
        </Radio.Group>
      </div>

      {mode === 'exchange' && (
        <Text type="secondary" style={{ fontSize: 12 }}>
          将创建临时独占队列并绑定到 exchange，不会与其他消费者竞争消息
        </Text>
      )}

      <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
        <Input
          placeholder={mode === 'queue' ? 'Queue 名' : 'Exchange 名'}
          value={target}
          onChange={(e) => handleTargetChange(e.target.value)}
          disabled={!connected || !!consumerTag}
          style={{ flex: 1 }}
        />
        {!consumerTag ? (
          <Button
            type="primary"
            onClick={handleSubscribe}
            loading={subscribing}
            disabled={!connected || !target.trim()}
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