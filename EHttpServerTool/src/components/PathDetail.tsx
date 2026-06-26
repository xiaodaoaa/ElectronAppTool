import React, { useEffect, useState } from 'react'
import { Form, Input, Checkbox, Switch, Select, Button, Space, Typography, message } from 'antd'
import { DeleteOutlined, SaveOutlined } from '@ant-design/icons'
import type { PathConfig } from '../types'

const { TextArea } = Input
const { Title } = Typography

const ALL_METHODS = ['GET', 'POST', 'PUT', 'DELETE', 'PATCH']

const RESPONSE_TYPE_OPTIONS = [
  { label: 'Text', value: 'text' },
  { label: 'JSON', value: 'json' },
  { label: 'XML', value: 'xml' },
  { label: 'HTML', value: 'html' },
]

interface PathDetailProps {
  config: PathConfig | null
  onSave: (config: PathConfig) => void
  onDelete: (id: string) => void
}

const PathDetail: React.FC<PathDetailProps> = ({ config, onSave, onDelete }) => {
  const [form] = Form.useForm()
  const [isEditing, setIsEditing] = useState(false)

  useEffect(() => {
    if (config) {
      form.setFieldsValue({
        path: config.path,
        methods: config.methods,
        echoEnabled: config.echoEnabled,
        responseType: config.responseType,
        responseContent: config.responseContent,
      })
      setIsEditing(false)
    }
  }, [config, form])

  if (!config) {
    return (
      <div style={{ padding: 24, textAlign: 'center', color: '#999' }}>
        请选择或添加一个 Path
      </div>
    )
  }

  const handleSave = () => {
    form.validateFields().then((values) => {
      if (!values.path.startsWith('/')) {
        message.error('路径必须以 / 开头')
        return
      }
      onSave({
        id: config.id,
        path: values.path,
        methods: values.methods,
        echoEnabled: values.echoEnabled,
        responseType: values.responseType,
        responseContent: values.responseContent,
      })
    })
  }

  const handleDelete = () => {
    onDelete(config.id)
  }

  return (
    <div style={{ padding: 16 }}>
      <Title level={5}>Path 配置</Title>
      <Form form={form} layout="vertical" onValuesChange={() => setIsEditing(true)}>
        <Form.Item
          name="path"
          label="路径"
          rules={[
            { required: true, message: '请输入路径' },
            { pattern: /^\//, message: '路径必须以 / 开头' },
          ]}
        >
          <Input placeholder="/api/users" />
        </Form.Item>

        <Form.Item
          name="methods"
          label="请求方法"
          rules={[{ required: true, message: '请选择至少一个方法', type: 'array', min: 1 }]}
        >
          <Checkbox.Group options={ALL_METHODS} />
        </Form.Item>

        <Form.Item name="echoEnabled" label="Echo 模式" valuePropName="checked">
          <Switch checkedChildren="开" unCheckedChildren="关" />
        </Form.Item>

        <Form.Item name="responseType" label="回复类型">
          <Select options={RESPONSE_TYPE_OPTIONS} />
        </Form.Item>

        <Form.Item name="responseContent" label="回复内容">
          <TextArea rows={6} placeholder="输入自定义回复内容..." />
        </Form.Item>

        <Form.Item>
          <Space>
            <Button
              type="primary"
              icon={<SaveOutlined />}
              onClick={handleSave}
              disabled={!isEditing}
            >
              保存
            </Button>
            <Button
              danger
              icon={<DeleteOutlined />}
              onClick={handleDelete}
            >
              删除
            </Button>
          </Space>
        </Form.Item>
      </Form>
    </div>
  )
}

export default PathDetail