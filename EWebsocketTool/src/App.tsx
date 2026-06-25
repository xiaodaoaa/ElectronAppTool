import { ConfigProvider, Tabs } from 'antd'
import type { TabsProps } from 'antd'
import ClientTab from './components/ClientTab'
import ServerTab from './components/ServerTab'

const tabItems: TabsProps['items'] = [
  { key: 'client', label: '客户端', children: <ClientTab /> },
  { key: 'server', label: '服务端', children: <ServerTab /> },
]

const App = () => {
  return (
    <ConfigProvider
      theme={{
        token: {
          colorPrimary: '#1677ff',
        },
      }}
    >
      <div className="app-container">
        <Tabs
          defaultActiveKey="client"
          items={tabItems}
          className="app-tabs"
        />
      </div>
    </ConfigProvider>
  )
}

export default App