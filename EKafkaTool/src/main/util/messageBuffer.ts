import type { MessageRecord } from '../kafka/types'
import { logger } from './logger'

export class MessageBuffer {
  private pending: MessageRecord[] = []
  private timer: ReturnType<typeof setInterval> | null = null

  constructor(
    private flush: (batch: MessageRecord[]) => void,
    private maxSize = 100,
    private intervalMs = 200,
  ) {
    this.timer = setInterval(() => this.tick(), intervalMs)
  }

  push(record: MessageRecord): void {
    this.pending.push(record)
    if (this.pending.length >= this.maxSize) {
      this.tick()
    }
  }

  private tick(): void {
    if (this.pending.length > 0) {
      const batch = this.pending.splice(0)
      logger.info(`MessageBuffer flush: ${batch.length} 条消息`)
      this.flush(batch)
    }
  }

  destroy(): void {
    if (this.timer) {
      clearInterval(this.timer)
      this.timer = null
    }
    this.tick()
  }
}