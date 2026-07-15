// src/components/ConnectionPanel.tsx
import { Input, Button, Tag, Space, Form, Switch, Tooltip } from 'antd'
import type { ConnectionConfig } from '../types'

interface ConnectionPanelProps {
  config: ConnectionConfig
  onConfigChange: (partial: Partial<ConnectionConfig>) => void
  connected: boolean
  connecting: boolean
  onConnect: () => void
  onDisconnect: () => void
  serverLabel: string
}

const formItemLayout = {
  labelCol: { style: { width: 50 } },
  wrapperCol: { style: { flex: 1 } },
}

const ConnectionPanel: React.FC<ConnectionPanelProps> = ({
  config, onConfigChange, connected, connecting, onConnect, onDisconnect, serverLabel,
}) => {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap', padding: '8px 0' }}>
      <Space size={4} wrap>
        <Form.Item label="Host" {...formItemLayout} style={{ marginBottom: 0 }}>
          <Input
            size="small"
            style={{ width: 120 }}
            value={config.host}
            onChange={(e) => onConfigChange({ host: e.target.value })}
            disabled={connected || connecting}
          />
        </Form.Item>
        <Form.Item label="Port" {...formItemLayout} style={{ marginBottom: 0 }}>
          <Input
            size="small"
            style={{ width: 80 }}
            value={config.port}
            type="number"
            onChange={(e) => onConfigChange({ port: Number(e.target.value) })}
            disabled={connected || connecting}
          />
        </Form.Item>
        <Form.Item label="VHost" {...formItemLayout} style={{ marginBottom: 0 }}>
          <Input
            size="small"
            style={{ width: 100 }}
            value={config.vhost}
            onChange={(e) => onConfigChange({ vhost: e.target.value })}
            disabled={connected || connecting}
          />
        </Form.Item>
        <Form.Item label="用户" {...formItemLayout} style={{ marginBottom: 0 }}>
          <Input
            size="small"
            style={{ width: 90 }}
            value={config.username}
            onChange={(e) => onConfigChange({ username: e.target.value })}
            disabled={connected || connecting}
          />
        </Form.Item>
        <Input.Password
          size="small"
          style={{ width: 90 }}
          value={config.password}
          onChange={(e) => onConfigChange({ password: e.target.value })}
          disabled={connected || connecting}
          placeholder="密码"
        />
        <Form.Item label="SSL" {...formItemLayout} style={{ marginBottom: 0 }}>
          <Switch
            size="small"
            checked={config.sslEnabled}
            onChange={(v) => onConfigChange({ sslEnabled: v })}
            disabled={connected || connecting}
          />
        </Form.Item>
        {config.sslEnabled && (
          <Tooltip title="关闭则不验证服务器证书">
            <Form.Item label="验证证书" {...formItemLayout} style={{ marginBottom: 0 }}>
              <Switch
                size="small"
                checked={config.sslValidateServerCert}
                onChange={(v) => onConfigChange({ sslValidateServerCert: v })}
                disabled={connected || connecting}
              />
            </Form.Item>
          </Tooltip>
        )}
      </Space>

      <Space size={4}>
        {!connected ? (
          <Button type="primary" size="small" loading={connecting} onClick={onConnect} disabled={connecting}>
            连接
          </Button>
        ) : (
          <Button danger size="small" onClick={onDisconnect}>
            断开
          </Button>
        )}
        {connecting && <Tag color="processing">连接中</Tag>}
        {connected && <Tag color="success">已连接 {serverLabel}</Tag>}
        {!connected && !connecting && <Tag>未连接</Tag>}
      </Space>
    </div>
  )
}

export default ConnectionPanel