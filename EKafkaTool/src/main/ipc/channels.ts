export const IPC_CHANNELS = {
  CONN_LIST: 'conn:list',
  CONN_SAVE: 'conn:save',
  CONN_DELETE: 'conn:delete',
  CONN_TEST: 'conn:test',
  CONN_CONNECT: 'conn:connect',
  CONN_DISCONNECT: 'conn:disconnect',

  ADMIN_LIST_TOPICS: 'admin:listTopics',
  ADMIN_TOPIC_DETAIL: 'admin:topicDetail',
  ADMIN_CREATE_TOPIC: 'admin:createTopic',
  ADMIN_DELETE_TOPIC: 'admin:deleteTopic',

  PRODUCER_SEND: 'producer:send',
  PRODUCER_SEND_BATCH: 'producer:sendBatch',
  PRODUCER_STOP_BATCH: 'producer:stopBatch',

  CONSUMER_CREATE: 'consumer:create',
  CONSUMER_START: 'consumer:start',
  CONSUMER_STOP: 'consumer:stop',
  CONSUMER_PAUSE: 'consumer:pause',
  CONSUMER_RESUME: 'consumer:resume',
  CONSUMER_SEEK: 'consumer:seek',
  CONSUMER_COMMIT: 'consumer:commit',
  CONSUMER_LIST_INSTANCES: 'consumer:listInstances',

  SCENARIO_RUN: 'scenario:run',
  SCENARIO_STOP: 'scenario:stop',
  SCENARIO_LIST: 'scenario:list',

  EVENT_CONN_STATUS: 'event:connStatus',
  EVENT_CONSUMER_MESSAGE: 'event:consumerMessage',
  EVENT_CONSUMER_STATE: 'event:consumerState',
  EVENT_REBALANCE: 'event:rebalance',
  EVENT_PRODUCE_ACK: 'event:produceAck',
  EVENT_SCENARIO_STEP: 'event:scenarioStep',
} as const