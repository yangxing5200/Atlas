<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Check, Refresh, Tickets, VideoPause, VideoPlay, Warning } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useRouter } from 'vue-router'
import { bidOpsOperationsApi } from '@/api/bidops/operations.api'
import DataTable from '@/shared/components/DataTable.vue'
import PageContainer from '@/shared/components/PageContainer.vue'
import { formatDateTime } from '@/shared/utils/date'
import { usePermission } from '@/shared/composables/usePermission'
import JobStatusTag from '@/modules/operations/components/JobStatusTag.vue'
import type { BidOpsOperationsDashboardDto } from '@/modules/operations/types'
import { formatJobType, severityType } from '@/modules/operations/utils/display'
import { BIDOPS_PERMISSIONS } from '../../constants'

const router = useRouter()
const loading = ref(false)
const aiProviderSaving = ref(false)
const runtimeSaving = ref(false)
const aiProvider = ref('')
const codexScenarioForms = ref<Record<string, { model: string; reasoningEffort: string }>>({})
const codexScenarioSaving = ref<Record<string, boolean>>({})
const dashboard = ref<BidOpsOperationsDashboardDto | null>(null)
const { visible: canManageOps } = usePermission(BIDOPS_PERMISSIONS.OPS_MANAGE)

const codexReasoningEffortOptions = [
  { label: 'Minimal', value: 'minimal' },
  { label: 'Low', value: 'low' },
  { label: 'Medium', value: 'medium' },
  { label: 'High', value: 'high' },
  { label: 'XHigh', value: 'xhigh' },
]

const metrics = computed(() => {
  const data = dashboard.value
  return [
    { label: 'Pending', value: data?.jobs.pending ?? 0 },
    { label: 'Running', value: data?.jobs.running ?? 0 },
    { label: 'Failed', value: data?.jobs.failed ?? 0 },
    { label: 'Dead', value: data?.jobs.dead ?? 0 },
    { label: '今日 RawNotice', value: data?.rawNoticeCreatedToday ?? 0 },
    { label: '今日审核任务', value: data?.reviewTaskCreatedToday ?? 0 },
    { label: '待解析公告', value: data?.parseQueuedRawNotices ?? 0 },
    { label: '失败附件', value: data?.failedAttachments ?? 0 },
  ]
})

const aiSettings = computed(() => dashboard.value?.aiSettings ?? null)
const runtimeStatus = computed(() => dashboard.value?.runtimeStatus ?? null)
const aiProviderOptions = computed(() =>
  (aiSettings.value?.options || []).map((item) => ({
    label: item.label,
    value: item.provider,
    disabled: aiProviderSaving.value,
  })),
)
const aiProviderLabel = computed(() => {
  const provider = aiSettings.value?.effectiveProvider || ''
  return aiSettings.value?.options.find((item) => item.provider === provider)?.label || provider || '-'
})
const aiReasoningLabel = computed(() => aiSettings.value?.reasoningEffort || '-')
const codexScenarios = computed(() => aiSettings.value?.codexCliScenarios || [])
const runtimeStatusLabel = computed(() => (runtimeStatus.value?.taskPaused ? '已暂停' : '运行中'))
const runtimeSummary = computed(() =>
  runtimeStatus.value?.taskPaused ? '新任务暂停执行' : '后台任务正常接收与执行',
)
const runtimeAlertDescription = computed(() => {
  const status = runtimeStatus.value
  if (!status?.taskPaused) return 'BidOps 后台任务正在按配置执行。'

  const reason = status.pauseReason || '未填写原因'
  const updatedBy = status.pauseUpdatedByUserName || '-'
  const updatedAt = formatDateTime(status.pauseUpdatedAt)
  return `原因：${reason}；操作人：${updatedBy}；时间：${updatedAt}`
})

async function loadData() {
  loading.value = true
  try {
    dashboard.value = await bidOpsOperationsApi.dashboard()
    syncAiSettingsForm(dashboard.value.aiSettings)
  } finally {
    loading.value = false
  }
}

function syncAiSettingsForm(settings = aiSettings.value) {
  if (!settings) return

  aiProvider.value = settings.effectiveProvider
  codexScenarioForms.value = Object.fromEntries(
    (settings.codexCliScenarios || []).map((item) => [
      item.scenario,
      {
        model: item.model || 'gpt-5.5',
        reasoningEffort: item.reasoningEffort || 'low',
      },
    ]),
  )
}

async function switchAiProvider(value: string | number | boolean) {
  const provider = String(value || '')
  if (!provider || provider === aiSettings.value?.effectiveProvider) return

  aiProviderSaving.value = true
  try {
    const updated = await bidOpsOperationsApi.updateAiProvider({ provider })
    if (dashboard.value) dashboard.value.aiSettings = updated
    syncAiSettingsForm(updated)
    ElMessage.success(`AI 模型已切换为 ${aiProviderLabel.value}`)
  } finally {
    aiProviderSaving.value = false
  }
}

function codexScenarioForm(scenario: string) {
  if (!codexScenarioForms.value[scenario]) {
    codexScenarioForms.value[scenario] = {
      model: 'gpt-5.5',
      reasoningEffort: scenario === 'reviewer-prompt'
        ? 'xhigh'
        : (scenario === 'complex' || scenario === 'manual-reparse' ? 'medium' : 'low'),
    }
  }

  return codexScenarioForms.value[scenario]
}

function codexScenarioChanged(scenario: string) {
  const settings = codexScenarios.value.find((item) => item.scenario === scenario)
  const form = codexScenarioForm(scenario)
  return !!settings &&
    (form.model.trim() !== settings.model || form.reasoningEffort !== settings.reasoningEffort)
}

function updateCodexScenarioModel(scenario: string, value: string) {
  codexScenarioForm(scenario).model = value
}

function updateCodexScenarioReasoningEffort(scenario: string, value: string | number) {
  codexScenarioForm(scenario).reasoningEffort = String(value)
}

async function saveCodexScenarioSettings(scenario: string) {
  const form = codexScenarioForm(scenario)
  const model = form.model.trim()
  const reasoningEffort = form.reasoningEffort
  if (!model) {
    ElMessage.warning('请填写 Codex CLI 模型')
    return
  }

  codexScenarioSaving.value = { ...codexScenarioSaving.value, [scenario]: true }
  try {
    const updated = await bidOpsOperationsApi.updateCodexCliScenarioSettings({ scenario, model, reasoningEffort })
    if (dashboard.value) dashboard.value.aiSettings = updated
    syncAiSettingsForm(updated)
    ElMessage.success('Codex CLI 场景设置已应用到 Worker')
  } finally {
    codexScenarioSaving.value = { ...codexScenarioSaving.value, [scenario]: false }
  }
}

async function updateTaskPause(paused: boolean, reason?: string) {
  runtimeSaving.value = true
  try {
    const updated = await bidOpsOperationsApi.updateTaskPause({ paused, reason })
    if (dashboard.value) dashboard.value.runtimeStatus = updated
    ElMessage.success(paused ? 'BidOps 全局任务已暂停' : 'BidOps 全局任务已恢复')
  } finally {
    runtimeSaving.value = false
  }
}

async function toggleTaskPause(value: string | number | boolean) {
  const paused = Boolean(value)
  if (paused === runtimeStatus.value?.taskPaused) return

  if (paused) {
    let reason = 'Operator paused all BidOps tasks.'
    try {
      const result = await ElMessageBox.prompt(
        '暂停后不会启动新的 BidOps 后台任务，已排队任务会延后等待恢复。',
        '暂停所有任务',
        {
          confirmButtonText: '暂停',
          cancelButtonText: '取消',
          inputPlaceholder: '填写暂停原因',
          inputValue: reason,
          inputValidator: (input: string) => input.length <= 300 || '暂停原因不能超过 300 个字符',
          type: 'warning',
        },
      )
      reason = String(result.value || '').trim() || reason
    } catch {
      return
    }

    await updateTaskPause(true, reason)
    return
  }

  try {
    await ElMessageBox.confirm('恢复后，已排队的 BidOps 后台任务会继续执行。', '恢复所有任务', {
      confirmButtonText: '恢复',
      cancelButtonText: '取消',
      type: 'info',
    })
  } catch {
    return
  }

  await updateTaskPause(false)
}

onMounted(loadData)
</script>

<template>
  <PageContainer title="BidOps 运营看板" description="查看 BidOps 后台任务、配置告警和今日处理概况。">
    <template #actions>
      <el-button :icon="Tickets" @click="router.push('/bidops/operations/jobs')">任务监控</el-button>
      <el-button :icon="Warning" @click="router.push('/bidops/operations/channels')">采集健康</el-button>
      <el-button :icon="Refresh" :loading="loading" @click="loadData">刷新</el-button>
    </template>

    <el-skeleton v-if="loading && !dashboard" :rows="10" animated />
    <template v-else>
      <section class="warning-panel">
        <el-alert
          v-if="dashboard?.configWarnings.length === 0"
          title="后台配置检查通过"
          type="success"
          show-icon
          :closable="false"
        />
        <el-alert
          v-for="item in dashboard?.configWarnings || []"
          :key="item.code"
          :title="item.title"
          :description="item.message"
          :type="severityType(item.severity)"
          show-icon
          :closable="false"
        />
      </section>

      <section v-if="runtimeStatus" class="runtime-control-panel">
        <div class="runtime-control-header">
          <div>
            <h2>任务总开关</h2>
            <p>{{ runtimeStatusLabel }} · {{ runtimeSummary }}</p>
          </div>
          <div class="runtime-control-actions">
            <el-tag :type="runtimeStatus.taskPaused ? 'danger' : 'success'" effect="light">
              {{ runtimeStatusLabel }}
            </el-tag>
            <el-switch
              class="task-pause-switch"
              :model-value="runtimeStatus.taskPaused"
              :disabled="!canManageOps || runtimeSaving"
              :loading="runtimeSaving"
              :active-icon="VideoPause"
              :inactive-icon="VideoPlay"
              inline-prompt
              @change="toggleTaskPause"
            />
          </div>
        </div>
        <el-alert
          :title="runtimeStatus.taskPaused ? 'BidOps 后台任务已暂停' : 'BidOps 后台任务运行中'"
          :description="runtimeAlertDescription"
          :type="runtimeStatus.taskPaused ? 'warning' : 'success'"
          show-icon
          :closable="false"
        />
      </section>

      <section v-if="aiSettings" class="ai-panel">
        <div class="ai-panel-header">
          <div>
            <h2>AI 模型</h2>
            <p>{{ aiProviderLabel }} · {{ aiSettings.providerSource === 'Runtime' ? '运行时' : '配置文件' }}</p>
          </div>
          <el-segmented
            v-model="aiProvider"
            :options="aiProviderOptions"
            :disabled="!canManageOps || aiProviderSaving"
            @change="switchAiProvider"
          />
        </div>
        <div class="ai-meta-grid">
          <div class="ai-meta-cell">
            <span>Provider</span>
            <strong>{{ aiSettings.effectiveProvider || '-' }}</strong>
          </div>
          <div class="ai-meta-cell">
            <span>Model</span>
            <strong>{{ aiSettings.effectiveModel || '-' }}</strong>
          </div>
          <div class="ai-meta-cell">
            <span>Reasoning</span>
            <strong>{{ aiReasoningLabel }}</strong>
          </div>
          <div class="ai-meta-cell">
            <span>更新人</span>
            <strong>{{ aiSettings.updatedByUserName || '-' }}</strong>
          </div>
        </div>
        <el-alert
          v-if="aiSettings.effectiveProvider === 'CodexCli'"
          :title="`Codex CLI 当前使用 ${aiSettings.codexCliModel || '-'} / ${aiSettings.codexCliReasoningEffort || '-'}`"
          description="普通识别默认 low；复杂件和重解析默认 medium；人工提示默认 xhigh。保存后 Worker 下次执行 Codex CLI 抽取时生效。"
          type="info"
          show-icon
          :closable="false"
        />
        <div v-if="aiSettings.effectiveProvider === 'CodexCli'" class="codex-scenarios">
          <div v-for="scenario in codexScenarios" :key="scenario.scenario" class="codex-scenario-row">
            <div class="codex-scenario-copy">
              <strong>{{ scenario.label }}</strong>
              <span>{{ scenario.description }}</span>
            </div>
            <div class="codex-setting-field">
              <span>Codex Model</span>
              <el-input
                :model-value="codexScenarioForm(scenario.scenario).model"
                :disabled="!canManageOps || codexScenarioSaving[scenario.scenario]"
                placeholder="gpt-5.5"
                maxlength="100"
                show-word-limit
                @update:model-value="(value: string) => updateCodexScenarioModel(scenario.scenario, value)"
              />
              <small>{{ scenario.modelSource === 'Runtime' ? '运行时设置' : '默认配置' }}</small>
            </div>
            <div class="codex-setting-field">
              <span>Reasoning</span>
              <el-segmented
                :model-value="codexScenarioForm(scenario.scenario).reasoningEffort"
                :options="codexReasoningEffortOptions"
                :disabled="!canManageOps || codexScenarioSaving[scenario.scenario]"
                @update:model-value="(value: string | number) => updateCodexScenarioReasoningEffort(scenario.scenario, value)"
              />
              <small>{{ scenario.reasoningEffortSource === 'Runtime' ? '运行时设置' : '默认配置' }}</small>
            </div>
            <el-button
              type="primary"
              :icon="Check"
              :loading="codexScenarioSaving[scenario.scenario]"
              :disabled="!canManageOps || !codexScenarioChanged(scenario.scenario)"
              @click="saveCodexScenarioSettings(scenario.scenario)"
            >
              应用到 Worker
            </el-button>
          </div>
        </div>
      </section>

      <section class="runtime-grid">
        <div class="runtime-cell">
          <span>OneTime Worker</span>
          <strong>{{ dashboard?.backgroundJobWorkerEnabled ? '启用' : '未启用' }}</strong>
        </div>
        <div class="runtime-cell">
          <span>Recurring Runner</span>
          <strong>{{ dashboard?.recurringTaskRunnerEnabled ? '启用' : '未启用' }}</strong>
        </div>
        <div class="runtime-cell">
          <span>bidops 队列</span>
          <strong>{{ dashboard?.bidOpsQueueConfigured ? '已配置' : '缺失' }}</strong>
        </div>
      </section>

      <section class="metric-grid">
        <div v-for="item in metrics" :key="item.label" class="metric-cell">
          <span>{{ item.label }}</span>
          <strong>{{ item.value }}</strong>
        </div>
      </section>

      <section class="content-panel">
        <h2>最近失败任务</h2>
        <DataTable :data="dashboard?.recentFailedJobs || []" :loading="loading" empty-text="暂无失败任务">
          <el-table-column label="状态" width="130">
            <template #default="{ row }">
              <JobStatusTag :status="row.status" :status-name="row.statusName" />
            </template>
          </el-table-column>
          <el-table-column label="任务类型" min-width="260" show-overflow-tooltip>
            <template #default="{ row }">{{ formatJobType(row.jobType, row.jobTypeName) }}</template>
          </el-table-column>
          <el-table-column prop="queue" label="队列" width="100" />
          <el-table-column label="创建时间" width="170">
            <template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template>
          </el-table-column>
          <el-table-column prop="lastErrorPreview" label="错误信息" min-width="260" show-overflow-tooltip />
          <el-table-column label="操作" width="90" fixed="right">
            <template #default="{ row }">
              <el-button size="small" @click="router.push(`/ops/jobs/${row.id}`)">详情</el-button>
            </template>
          </el-table-column>
        </DataTable>
      </section>
    </template>
  </PageContainer>
</template>

<style scoped>
.warning-panel {
  display: grid;
  gap: 10px;
  margin-bottom: 14px;
}

.runtime-grid,
.metric-grid,
.ai-meta-grid {
  display: grid;
  gap: 10px;
  margin-bottom: 14px;
}

.runtime-grid {
  grid-template-columns: repeat(3, minmax(160px, 1fr));
}

.metric-grid {
  grid-template-columns: repeat(4, minmax(130px, 1fr));
}

.ai-meta-grid {
  grid-template-columns: repeat(4, minmax(120px, 1fr));
}

.runtime-cell,
.metric-cell,
.ai-meta-cell,
.runtime-control-panel,
.ai-panel,
.content-panel {
  border: 1px solid #dce3ee;
  border-radius: 8px;
  background: #fff;
}

.runtime-cell,
.metric-cell,
.ai-meta-cell {
  display: grid;
  gap: 6px;
  min-height: 72px;
  padding: 12px;
}

.runtime-cell span,
.metric-cell span,
.ai-meta-cell span {
  color: #687385;
  font-size: 13px;
}

.runtime-cell strong,
.metric-cell strong,
.ai-meta-cell strong {
  color: #17202a;
  font-size: 22px;
  line-height: 1.2;
}

.runtime-control-panel,
.ai-panel {
  display: grid;
  gap: 12px;
  margin-bottom: 14px;
  padding: 14px;
}

.runtime-control-header,
.ai-panel-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
}

.runtime-control-header h2,
.ai-panel-header h2 {
  margin: 0;
  color: #17202a;
  font-size: 16px;
}

.runtime-control-header p,
.ai-panel-header p {
  margin: 4px 0 0;
  color: #687385;
  font-size: 13px;
}

.runtime-control-actions {
  display: flex;
  align-items: center;
  gap: 12px;
}

.task-pause-switch {
  --el-switch-on-color: #d03050;
  --el-switch-off-color: #1f8f5f;
}

.codex-scenarios {
  display: grid;
  gap: 12px;
}

.codex-scenario-row {
  display: grid;
  align-items: end;
  grid-template-columns: minmax(180px, 0.9fr) minmax(220px, 1fr) minmax(360px, 1.4fr) auto;
  gap: 12px;
  padding-top: 12px;
  border-top: 1px solid #edf1f7;
}

.codex-scenario-copy {
  display: grid;
  gap: 6px;
  align-self: center;
}

.codex-scenario-copy strong {
  color: #17202a;
  font-size: 14px;
}

.codex-scenario-copy span {
  color: #687385;
  font-size: 12px;
  line-height: 1.5;
}

.codex-setting-field {
  display: grid;
  gap: 6px;
}

.codex-setting-field span {
  color: #687385;
  font-size: 13px;
}

.codex-setting-field small {
  color: #8a96a8;
  font-size: 12px;
}

.content-panel {
  padding: 14px;
}

.content-panel h2 {
  margin: 0 0 12px;
  color: #17202a;
  font-size: 16px;
}

@media (max-width: 980px) {
  .runtime-grid,
  .metric-grid,
  .ai-meta-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .runtime-control-header,
  .ai-panel-header {
    align-items: flex-start;
    flex-direction: column;
  }

  .codex-scenario-row {
    align-items: stretch;
    grid-template-columns: 1fr;
  }
}
</style>
