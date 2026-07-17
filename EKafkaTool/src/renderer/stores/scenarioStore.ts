import { defineStore } from 'pinia'
import { ref } from 'vue'
import { getKafkaApi } from '@/api/kafkaApi'
import type { ScenarioMeta } from '../../main/kafka/types'

export const useScenarioStore = defineStore('scenario', () => {
  const scenarios = ref<ScenarioMeta[]>([])
  const runningId = ref<string | null>(null)
  const currentStep = ref<{ runId: string; stepIndex: number; message: string } | null>(null)

  async function loadScenarios() {
    scenarios.value = await getKafkaApi().listScenarios()
  }

  async function runScenario(id: string) {
    runningId.value = await getKafkaApi().runScenario(id)
  }

  async function stopScenario() {
    if (runningId.value) {
      await getKafkaApi().stopScenario(runningId.value)
      runningId.value = null
      currentStep.value = null
    }
  }

  function setupListener() {
    getKafkaApi().onScenarioStep((data) => {
      currentStep.value = data
    })
  }

  return {
    scenarios, runningId, currentStep,
    loadScenarios, runScenario, stopScenario, setupListener,
  }
})