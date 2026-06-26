import React from 'react'
import { List, Button, Typography } from 'antd'
import { PlusOutlined } from '@ant-design/icons'
import type { PathConfig } from '../types'

const { Text } = Typography

interface PathListProps {
  paths: PathConfig[]
  selectedId: string | null
  onSelect: (id: string) => void
  onAdd: () => void
}

const PathList: React.FC<PathListProps> = ({ paths, selectedId, onSelect, onAdd }) => {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      <div style={{ padding: '8px 12px', borderBottom: '1px solid #f0f0f0' }}>
        <Button type="primary" icon={<PlusOutlined />} onClick={onAdd} block>
          添加 Path
        </Button>
      </div>
      <div style={{ flex: 1, overflow: 'auto' }}>
        <List
          dataSource={paths}
          locale={{ emptyText: '暂无配置，请添加 Path' }}
          renderItem={(item) => (
            <List.Item
              onClick={() => onSelect(item.id)}
              style={{
                cursor: 'pointer',
                padding: '8px 12px',
                backgroundColor: selectedId === item.id ? '#e6f4ff' : undefined,
                borderLeft: selectedId === item.id ? '3px solid #1677ff' : '3px solid transparent',
              }}
            >
              <Text strong={selectedId === item.id}>{item.path}</Text>
              <Text type="secondary" style={{ fontSize: 12 }}>
                {item.methods.join(', ')}
              </Text>
            </List.Item>
          )}
        />
      </div>
    </div>
  )
}

export default PathList