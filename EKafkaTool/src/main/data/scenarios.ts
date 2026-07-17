export interface ScenarioDefinition {
  id: string
  title: string
  description: string
  duration: string
  steps: ScenarioStepDef[]
}

export interface ScenarioStepDef {
  type: 'createTopic' | 'note' | 'createConsumer' | 'produce' | 'sleep' | 'stopConsumer'
  [key: string]: unknown
}

export const SCENARIOS: ScenarioDefinition[] = [
  {
    id: 'S1',
    title: '第一条消息',
    description: '创建 1 分区 Topic，起 1 个消费者，发 3 条消息，观察基本收发与 Offset 递增',
    duration: '约 1 分钟',
    steps: [
      { type: 'createTopic', name: 'demo-hello', partitions: 1 },
      { type: 'note', text: '已创建 1 分区 Topic "demo-hello"，现在启动消费者' },
      { type: 'createConsumer', alias: 'Consumer-Hello', groupId: 'demo-g0', topics: ['demo-hello'], fromBeginning: true },
      { type: 'note', text: '消费者已启动，从 earliest 开始消费。现在发送 3 条消息' },
      { type: 'produce', topic: 'demo-hello', count: 3, intervalMs: 1000, keyStrategy: 'fixed', value: '{"msg": "hello {{seq}}"}' },
      { type: 'note', text: '观察：每条消息 Offset 从 0 递增，消息持久化后可重放' },
      { type: 'sleep', seconds: 5 },
      { type: 'note', text: '场景结束。可尝试手动重置 Offset 到 0 重放消息' },
    ],
  },
  {
    id: 'S2',
    title: '分区与 Key',
    description: '创建 3 分区 Topic，分别以无 Key 和固定 Key 各发 12 条，观察分区分布',
    duration: '约 2 分钟',
    steps: [
      { type: 'createTopic', name: 'demo-partition', partitions: 3 },
      { type: 'createConsumer', alias: 'Consumer-P', groupId: 'demo-g1', topics: ['demo-partition'], fromBeginning: true },
      { type: 'note', text: '已创建 3 分区 Topic。先发送 12 条无 Key 消息，观察轮询分布' },
      { type: 'produce', topic: 'demo-partition', count: 12, intervalMs: 500, keyStrategy: 'fixed', value: '{"msg": "no-key {{seq}}"}' },
      { type: 'sleep', seconds: 3 },
      { type: 'note', text: '无 Key 消息均匀分布到 3 个分区。现在发送 12 条固定 Key="user1" 的消息' },
      { type: 'produce', topic: 'demo-partition', count: 12, intervalMs: 500, keyStrategy: 'fixed', key: 'user1', value: '{"msg": "user1-{{seq}}"}' },
      { type: 'sleep', seconds: 3 },
      { type: 'note', text: '同 Key 消息全部落入同一分区（哈希取模）。这就是分区策略的核心' },
    ],
  },
  {
    id: 'S3',
    title: '消费组负载均衡',
    description: '3 分区 Topic + Consumer-A 持续消费，60 秒后自动加入 Consumer-B，观察再均衡',
    duration: '约 3 分钟',
    steps: [
      { type: 'createTopic', name: 'demo-group', partitions: 3 },
      { type: 'note', text: '先启动 Consumer-A，独占 3 个分区' },
      { type: 'createConsumer', alias: 'Consumer-A', groupId: 'demo-g2', topics: ['demo-group'], fromBeginning: true },
      { type: 'produce', topic: 'demo-group', count: 0, intervalMs: 2000, keyStrategy: 'random', value: '{"msg": "data-{{seq}}"}' },
      { type: 'sleep', seconds: 15 },
      { type: 'note', text: 'Consumer-A 持续消费中。现在加入 Consumer-B，观察再均衡' },
      { type: 'createConsumer', alias: 'Consumer-B', groupId: 'demo-g2', topics: ['demo-group'], fromBeginning: false },
      { type: 'note', text: '再均衡触发！分区从 A 独占变为 A(2) + B(1) 分配。观察分配图变化' },
      { type: 'sleep', seconds: 30 },
      { type: 'note', text: '观察两张卡片的分区色块与各自消费速率。这就是消费组负载均衡' },
    ],
  },
  {
    id: 'S4',
    title: '再均衡（手动）',
    description: '手动新增/停止消费者，观察分区分配图动画迁移与 lag 变化',
    duration: '约 2 分钟',
    steps: [
      { type: 'createTopic', name: 'demo-rebalance', partitions: 4 },
      { type: 'createConsumer', alias: 'Consumer-X', groupId: 'demo-g3', topics: ['demo-rebalance'], fromBeginning: true },
      { type: 'produce', topic: 'demo-rebalance', count: 0, intervalMs: 1000, keyStrategy: 'random', value: '{"msg": "{{seq}}"}' },
      { type: 'note', text: 'Consumer-X 当前独占 4 个分区。请手动点击"新增消费者实例"创建 Consumer-Y（同 groupId demo-g3）' },
      { type: 'sleep', seconds: 30 },
      { type: 'note', text: '观察分配图动画迁移。暂停 Consumer-X 期间消息积压 lag 增长' },
      { type: 'sleep', seconds: 30 },
      { type: 'note', text: '场景结束。可继续手动操作观察再均衡行为' },
    ],
  },
  {
    id: 'S5',
    title: '消息重放',
    description: '消费者消费完 20 条后，执行 seek 重置到 Offset 0 再消费，演示消息不删除特性',
    duration: '约 2 分钟',
    steps: [
      { type: 'createTopic', name: 'demo-replay', partitions: 1 },
      { type: 'note', text: '先发送 20 条消息，消费者消费完毕' },
      { type: 'createConsumer', alias: 'Consumer-R', groupId: 'demo-g4', topics: ['demo-replay'], fromBeginning: true },
      { type: 'produce', topic: 'demo-replay', count: 20, intervalMs: 300, keyStrategy: 'fixed', value: '{"msg": "replay-{{seq}}"}' },
      { type: 'sleep', seconds: 10 },
      { type: 'note', text: '20 条消息已消费完毕。现在执行 Offset 重置到 0，重新消费' },
      { type: 'sleep', seconds: 3 },
      { type: 'note', text: '观察：Kafka 消息不删除，Offset 回拨即可重放。对比传统 MQ 的删除机制' },
      { type: 'sleep', seconds: 10 },
      { type: 'note', text: '场景结束。这就是 Kafka 消息持久化与重放的核心优势' },
    ],
  },
  {
    id: 'S6',
    title: '顺序性与 acks',
    description: '1 分区 Topic 发 20 条带序号消息，展示严格顺序；切换 acks=0 演示无 Offset',
    duration: '约 2 分钟',
    steps: [
      { type: 'createTopic', name: 'demo-order', partitions: 1 },
      { type: 'createConsumer', alias: 'Consumer-O', groupId: 'demo-g5', topics: ['demo-order'], fromBeginning: true },
      { type: 'note', text: '在 1 分区 Topic 中发送 20 条带序号消息（acks=all），观察严格顺序' },
      { type: 'produce', topic: 'demo-order', count: 20, intervalMs: 300, keyStrategy: 'fixed', value: '{"seq": {{seq}}}' },
      { type: 'sleep', seconds: 8 },
      { type: 'note', text: '观察：分区内消息严格按 Offset 递增顺序消费。顺序性只在分区内成立' },
      { type: 'sleep', seconds: 5 },
      { type: 'note', text: '现在切换到生产者页面，将 acks 设为 0 发送消息，观察"发送即返回"无 Offset 确认' },
      { type: 'sleep', seconds: 15 },
      { type: 'note', text: '场景结束。acks=0 不等待 Broker 确认，吞吐最高但可能丢消息' },
    ],
  },
]