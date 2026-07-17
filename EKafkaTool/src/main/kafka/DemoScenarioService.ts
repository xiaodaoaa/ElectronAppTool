import { SCENARIOS, type ScenarioDefinition, type ScenarioStepDef } from '../data/scenarios'
import { logger } from '../util/logger'

interface RunContext {
  runId: string
  scenarioId: string
  currentStep: number
  aborted: boolean
  consumers: string[]
  topics: string[]
}

export class DemoScenarioService {
  private runs = new Map<string, RunContext>()

  constructor(
    private push: (channel: string, payload: unknown) => void,
    private createTopic: (name: string, partitions: number) => Promise<void>,
    private createConsumer: (opts: { alias: string; groupId: string; topics: string[]; fromBeginning: boolean }) => Promise<string>,
    private startConsumer: (id: string) => Promise<void>,
    private stopConsumer: (id: string) => Promise<void>,
    private produce: (topic: string, count: number, intervalMs: number, keyStrategy: string, key?: string, value?: string) => Promise<string>,
    private stopBatch: (taskId: string) => Promise<void>,
  ) {}

  listScenarios() {
    return SCENARIOS.map(s => ({
      id: s.id,
      title: s.title,
      description: s.description,
      duration: s.duration,
    }))
  }

  async run(scenarioId: string): Promise<string> {
    const scenario = SCENARIOS.find(s => s.id === scenarioId)
    if (!scenario) throw new Error(`场景不存在: ${scenarioId}`)

    const runId = crypto.randomUUID()
    const ctx: RunContext = {
      runId,
      scenarioId,
      currentStep: 0,
      aborted: false,
      consumers: [],
      topics: [],
    }
    this.runs.set(runId, ctx)

    this.execute(scenario, ctx).catch(err => {
      logger.error(`场景执行异常 [${scenarioId}]:`, err)
    })

    return runId
  }

  stop(runId: string): void {
    const ctx = this.runs.get(runId)
    if (ctx) {
      ctx.aborted = true
      this.runs.delete(runId)
    }
  }

  private async execute(scenario: ScenarioDefinition, ctx: RunContext): Promise<void> {
    let batchTaskId: string | null = null

    for (let i = 0; i < scenario.steps.length; i++) {
      if (ctx.aborted) break

      const step = scenario.steps[i]
      ctx.currentStep = i

      try {
        switch (step.type) {
          case 'createTopic': {
            const name = step.name as string
            const partitions = (step.partitions as number) ?? 3
            await this.createTopic(name, partitions)
            ctx.topics.push(name)
            this.pushNote(ctx, `已创建 Topic "${name}"（${partitions} 分区）`)
            break
          }

          case 'createConsumer': {
            const alias = step.alias as string
            const groupId = step.groupId as string
            const topics = step.topics as string[]
            const fromBeginning = (step.fromBeginning as boolean) ?? true
            const id = await this.createConsumer({ alias, groupId, topics, fromBeginning })
            await this.startConsumer(id)
            ctx.consumers.push(id)
            this.pushNote(ctx, `消费者 "${alias}" 已启动（groupId: ${groupId}）`)
            break
          }

          case 'produce': {
            if (batchTaskId) {
              await this.stopBatch(batchTaskId)
            }
            const topic = step.topic as string
            const count = step.count as number
            const intervalMs = (step.intervalMs as number) ?? 500
            const keyStrategy = (step.keyStrategy as string) ?? 'fixed'
            const key = step.key as string | undefined
            const value = step.value as string | undefined

            if (count > 0) {
              batchTaskId = await this.produce(topic, count, intervalMs, keyStrategy, key, value)
              this.pushNote(ctx, `开始发送 ${count} 条消息到 "${topic}"`)
            } else {
              batchTaskId = await this.produce(topic, 999999, intervalMs, keyStrategy, key, value)
              this.pushNote(ctx, `开始持续发送消息到 "${topic}"（速率: ${intervalMs}ms/条）`)
            }
            break
          }

          case 'sleep': {
            const seconds = step.seconds as number
            await this.sleep(seconds * 1000, ctx)
            break
          }

          case 'note': {
            this.pushNote(ctx, step.text as string)
            break
          }

          case 'stopConsumer': {
            const alias = step.alias as string
            this.pushNote(ctx, `停止消费者 "${alias}"`)
            break
          }
        }
      } catch (err) {
        logger.error(`场景步骤执行失败 [${scenario.id}:${i}]:`, err)
        this.pushNote(ctx, `步骤执行出错: ${err instanceof Error ? err.message : String(err)}`)
      }
    }

    if (batchTaskId) {
      await this.stopBatch(batchTaskId).catch(() => {})
    }

    this.pushNote(ctx, '场景执行完毕')
    this.runs.delete(ctx.runId)
  }

  private pushNote(ctx: RunContext, message: string): void {
    this.push('event:scenarioStep', {
      runId: ctx.runId,
      stepIndex: ctx.currentStep,
      message,
    })
  }

  private sleep(ms: number, ctx: RunContext): Promise<void> {
    return new Promise(resolve => {
      const check = () => {
        if (ctx.aborted) { resolve(); return }
        setTimeout(() => { resolve() }, ms)
      }
      check()
    })
  }
}