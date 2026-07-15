import { useState, useCallback } from 'react'
import { Radio, Input, Button, Switch, Select, Space, Divider, Typography } from 'antd'
import { MinusCircleOutlined, PlusOutlined } from '@ant-design/icons'
import type { PublishTarget, MessageProperties, ProducerState } from '../types'

const { TextArea } = Input
const { Text } = Typography

interface ProducerTabProps {
  connected: boolean
  onPublish: (target: PublishTarget) => Promise<{ success: boolean }>
  defaults?: ProducerState
  onChange?: (state: ProducerState) => void
}

const defaultProperties: MessageProperties = {
  persistent: true,
  contentType: 'text/plain',
  priority: 0,
  headers: {},
}

const ProducerTab: React.FC<ProducerTabProps> = ({ connected, onPublish, defaults, onChange }) => {
  // 受控字段：由父组件 useConfig.producer 单一持有，加载的值天然同步，避免本地副本与 initializedRef 守卫导致的回写竞态
  const targetMode = defaults?.targetMode ?? 'exchange'
  const exchange = defaults?.exchange ?? ''
  const routingKey = defaults?.routingKey ?? ''
  const queue = defaults?.queue ?? ''
  const properties = defaults?.properties ?? defaultProperties

  // 非持久化字段：消息内容与 headers 仅本地维护
  const [message, setMessage] = useState('')
  const [headers, setHeaders] = useState<{ key: string; value: string }[]>([])
  const [sending, setSending] = useState(false)

  const update = useCallback((partial: Partial<ProducerState>) => {
    onChange?.({ targetMode, exchange, routingKey, queue, properties, ...partial })
  }, [onChange, targetMode, exchange, routingKey, queue, properties])

  const handleSend = useCallback(async (msg?: string) => {
    const content = msg ?? message
    if (!content || !content.trim()) return
    setSending(true)

    const headersObj: Record<string, string> = {}
    headers.forEach((h) => {
      if (h.key) headersObj[h.key] = h.value
    })

    const target: PublishTarget = {
      target: targetMode,
      exchange: targetMode === 'exchange' ? exchange : undefined,
      routingKey: targetMode === 'exchange' ? routingKey : undefined,
      queue: targetMode === 'queue' ? queue : undefined,
      message: content,
      properties: { ...properties, headers: headersObj },
    }

    await onPublish(target)
    setSending(false)
  }, [message, targetMode, exchange, routingKey, queue, properties, headers, onPublish])

  const handleKeyDown = useCallback((e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      handleSend()
    }
  }, [handleSend])

  const addHeader = useCallback(() => {
    setHeaders((prev) => [...prev, { key: '', value: '' }])
  }, [])

  const removeHeader = useCallback((index: number) => {
    setHeaders((prev) => prev.filter((_, i) => i !== index))
  }, [])

  const updateHeader = useCallback((index: number, field: 'key' | 'value', val: string) => {
    setHeaders((prev) => prev.map((h, i) => i === index ? { ...h, [field]: val } : h))
  }, [])

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
      <div>
        <Text strong>发送目标</Text>
        <Radio.Group
          value={targetMode}
          onChange={(e) => update({ targetMode: e.target.value })}
          style={{ marginLeft: 12 }}
        >
          <Radio value="exchange">Exchange</Radio>
          <Radio value="queue">Queue</Radio>
        </Radio.Group>
      </div>

      {targetMode === 'exchange' ? (
        <Space direction="vertical" style={{ width: '100%' }}>
          <Input
            placeholder="Exchange 名"
            value={exchange}
            onChange={(e) => update({ exchange: e.target.value })}
            disabled={!connected}
          />
          <Input
            placeholder="Routing Key"
            value={routingKey}
            onChange={(e) => update({ routingKey: e.target.value })}
            disabled={!connected}
          />
        </Space>
      ) : (
        <Input
          placeholder="Queue 名"
          value={queue}
          onChange={(e) => update({ queue: e.target.value })}
          disabled={!connected}
        />
      )}

      <Divider style={{ margin: '4px 0' }} />

      <div>
        <Text strong>消息属性</Text>
        <Space direction="vertical" style={{ width: '100%', marginTop: 8 }}>
          <Space>
            <Text type="secondary">Persistent:</Text>
            <Switch
              size="small"
              checked={properties.persistent}
              onChange={(v) => update({ properties: { ...properties, persistent: v } })}
            />
          </Space>
          <Space>
            <Text type="secondary">Content-Type:</Text>
            <Select
              size="small"
              style={{ width: 140 }}
              value={properties.contentType}
              onChange={(v) => update({ properties: { ...properties, contentType: v } })}
              options={[
                { value: 'text/plain', label: 'text/plain' },
                { value: 'application/json', label: 'application/json' },
                { value: 'text/xml', label: 'text/xml' },
                { value: 'application/octet-stream', label: 'application/octet-stream' },
              ]}
            />
          </Space>
          <Space>
            <Text type="secondary">Priority:</Text>
            <Input
              size="small"
              style={{ width: 60 }}
              type="number"
              min={0}
              max={9}
              value={properties.priority}
              onChange={(e) => update({ properties: { ...properties, priority: Math.min(9, Math.max(0, Number(e.target.value))) } })}
            />
          </Space>
          <Space>
            <Text type="secondary">Message ID:</Text>
            <Input
              size="small"
              style={{ width: 180 }}
              value={properties.messageId || ''}
              onChange={(e) => update({ properties: { ...properties, messageId: e.target.value || undefined } })}
            />
          </Space>
          <Space>
            <Text type="secondary">Reply-To:</Text>
            <Input
              size="small"
              style={{ width: 180 }}
              value={properties.replyTo || ''}
              onChange={(e) => update({ properties: { ...properties, replyTo: e.target.value || undefined } })}
            />
          </Space>
        </Space>
      </div>

      <div>
        <Space>
          <Text strong>Headers</Text>
          <Button type="link" icon={<PlusOutlined />} size="small" onClick={addHeader} />
        </Space>
        {headers.map((h, i) => (
          <Space key={i} style={{ display: 'flex', marginTop: 4 }}>
            <Input
              size="small"
              placeholder="Key"
              style={{ width: 120 }}
              value={h.key}
              onChange={(e) => updateHeader(i, 'key', e.target.value)}
            />
            <Input
              size="small"
              placeholder="Value"
              style={{ width: 120 }}
              value={h.value}
              onChange={(e) => updateHeader(i, 'value', e.target.value)}
            />
            <Button
              type="link"
              danger
              icon={<MinusCircleOutlined />}
              size="small"
              onClick={() => removeHeader(i)}
            />
          </Space>
        ))}
      </div>

      <Divider style={{ margin: '4px 0' }} />

      <TextArea
        rows={4}
        placeholder="消息内容 (Enter 发送，Shift+Enter 换行)"
        value={message}
        onChange={(e) => setMessage(e.target.value)}
        onKeyDown={handleKeyDown}
        disabled={!connected}
      />

      <Button
        type="primary"
        onClick={() => handleSend()}
        loading={sending}
        disabled={!connected || !message.trim()}
      >
        发送
      </Button>
    </div>
  )
}

export default ProducerTab