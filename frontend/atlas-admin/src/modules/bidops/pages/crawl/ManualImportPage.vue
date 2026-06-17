<script setup lang="ts">
import { computed, onBeforeUnmount, reactive, ref } from 'vue'
import type { FormInstance, FormRules } from 'element-plus'
import { ElMessage } from 'element-plus'
import { ArrowRight, CirclePlus, Link, Refresh, View } from '@element-plus/icons-vue'
import { useRouter } from 'vue-router'
import { rawNoticesApi } from '@/api/bidops/rawNotices.api'
import { backgroundJobsApi } from '@/api/operations/backgroundJobs.api'
import PageContainer from '@/shared/components/PageContainer.vue'
import { formatDateTime } from '@/shared/utils/date'
import JobStatusTag from '@/modules/operations/components/JobStatusTag.vue'
import type { BackgroundJobDetailDto } from '@/modules/operations/types'
import BidOpsStatusTag from '../../components/BidOpsStatusTag.vue'
import RawNoticePipelinePanel from '../../components/RawNoticePipelinePanel.vue'
import type { EnqueueJobDto, RawNoticeDto, RawNoticePipelineDto } from '../../types'
import { noticeTypeOptions, formatNoticeType } from '../../utils/display'

interface ManualImportForm {
  detailUrl: string
  noticeType: string
  title: string
  textContent: string
  sourceId: number | null
  channelId: number | null
}

const router = useRouter()
const formRef = ref<FormInstance>()
const submitting = ref(false)
const refreshing = ref(false)
const advancedPanels = ref<string[]>([])
const enqueuedJob = ref<EnqueueJobDto | null>(null)
const job = ref<BackgroundJobDetailDto | null>(null)
const rawNotice = ref<RawNoticeDto | null>(null)
const pipeline = ref<RawNoticePipelineDto | null>(null)
const pollTimer = ref<number | null>(null)
const pollCount = ref(0)

const form = reactive<ManualImportForm>({
  detailUrl: '',
  noticeType: 'ProcurementAnnouncement',
  title: '',
  textContent: '',
  sourceId: null,
  channelId: null,
})

const rules: FormRules<ManualImportForm> = {
  detailUrl: [
    { required: true, message: '请输入采购公告地址', trigger: 'blur' },
    {
      validator: (_rule, value, callback) => {
        try {
          const url = new URL(String(value || '').trim())
          if (url.protocol !== 'http:' && url.protocol !== 'https:') {
            callback(new Error('仅支持公开 http/https 地址'))
            return
          }
          callback()
        } catch {
          callback(new Error('请输入有效 URL'))
        }
      },
      trigger: 'blur',
    },
  ],
}

const jobTitle = computed(() => {
  if (!enqueuedJob.value) return '未提交'
  return enqueuedJob.value.alreadyExists ? '已有相同导入任务' : '导入任务已提交'
})

const advancedOpen = computed(() => advancedPanels.value.includes('advanced'))
const rawNoticeId = computed(() => rawNotice.value?.id || extractRawNoticeId(job.value?.result || job.value?.resultPreview || ''))

const canOpenRawNotice = computed(() => Boolean(rawNoticeId.value))
const canOpenReviewTask = computed(() => Boolean(pipeline.value?.reviewTaskId))
const shouldShowResult = computed(() => Boolean(enqueuedJob.value || job.value || rawNotice.value || pipeline.value))

async function submitImport() {
  try {
    await formRef.value?.validate()
  } catch {
    return
  }

  stopPolling()
  submitting.value = true
  enqueuedJob.value = null
  job.value = null
  rawNotice.value = null
  pipeline.value = null
  pollCount.value = 0

  try {
    const detailUrl = form.detailUrl.trim()
    const result = await rawNoticesApi.importUrl({
      detailUrl,
      noticeType: form.noticeType || null,
      title: form.title.trim() || null,
      textContent: form.textContent.trim() || null,
      sourceId: advancedOpen.value ? form.sourceId : null,
      channelId: advancedOpen.value ? form.channelId : null,
    })

    enqueuedJob.value = result
    ElMessage.success(result.alreadyExists ? '相同公告导入任务已存在，正在刷新状态' : '已提交导入任务')
    await refreshImportState()
    startPolling()
  } finally {
    submitting.value = false
  }
}

async function refreshImportState() {
  const jobId = enqueuedJob.value?.jobId || job.value?.id
  if (!jobId) return

  refreshing.value = true
  try {
    job.value = await backgroundJobsApi.get(String(jobId))

    const parsedId = extractRawNoticeId(job.value.result || job.value.resultPreview)
    if (parsedId) {
      await loadRawNotice(parsedId)
      return
    }

    if (isSucceededJob(job.value)) {
      await findRawNoticeByUrl()
    }
  } finally {
    refreshing.value = false
  }
}

async function loadRawNotice(id: string) {
  rawNotice.value = await rawNoticesApi.get(id).catch(() => null)
  pipeline.value = await rawNoticesApi.pipeline(id).catch(() => null)
}

async function findRawNoticeByUrl() {
  const result = await rawNoticesApi.search({
    keyword: form.detailUrl.trim(),
    pageIndex: 1,
    pageSize: 5,
  })
  const matched = result.items.find((item) => item.detailUrl === form.detailUrl.trim()) || result.items[0]
  if (matched) {
    await loadRawNotice(matched.id)
  }
}

function startPolling() {
  stopPolling()
  pollTimer.value = window.setInterval(async () => {
    pollCount.value += 1
    await refreshImportState().catch(() => undefined)
    if (shouldStopPolling()) {
      stopPolling()
    }
  }, 3000)
}

function stopPolling() {
  if (pollTimer.value !== null) {
    window.clearInterval(pollTimer.value)
    pollTimer.value = null
  }
}

function shouldStopPolling() {
  if (pollCount.value >= 60) return true
  if (!job.value || !isTerminalJob(job.value)) return false
  if (isFailedJob(job.value)) return true
  if (!pipeline.value) return false
  return pipeline.value.steps.every((step) => step.status !== 'Pending')
}

function extractRawNoticeId(value?: string | null) {
  const match = String(value || '').match(/rawNoticeId=(\d+)/i)
  return match?.[1] || ''
}

function isSucceededJob(value: BackgroundJobDetailDto) {
  return value.statusName === 'Succeeded' || String(value.status) === '2'
}

function isFailedJob(value: BackgroundJobDetailDto) {
  return ['Failed', 'Dead', 'Canceled'].includes(value.statusName) || ['3', '4', '5'].includes(String(value.status))
}

function isTerminalJob(value: BackgroundJobDetailDto) {
  return isSucceededJob(value) || isFailedJob(value)
}

function openRawNotice() {
  if (rawNoticeId.value) {
    router.push(`/bidops/crawl/raw-notices/${rawNoticeId.value}`)
  }
}

function openReviewTask() {
  const reviewTaskId = pipeline.value?.reviewTaskId
  if (reviewTaskId) {
    router.push(`/bidops/review/tasks/${reviewTaskId}`)
  }
}

onBeforeUnmount(stopPolling)
</script>

<template>
  <PageContainer title="手动导入采购公告" description="输入公开采购公告地址，Worker 自动抓取公告详情、附件并进入审核队列。">
    <div class="manual-import-layout">
      <section class="content-panel import-panel">
        <el-form ref="formRef" :model="form" :rules="rules" label-position="top" @submit.prevent>
          <el-alert
            type="warning"
            show-icon
            :closable="false"
            title="仅支持公开可访问地址；不使用登录态、Cookie、验证码或反爬绕过配置。"
            class="guardrail-alert"
          />

          <el-form-item label="采购公告地址" prop="detailUrl">
            <el-input
              v-model.trim="form.detailUrl"
              :prefix-icon="Link"
              clearable
              placeholder="https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-bid/..."
            />
          </el-form-item>

          <el-form-item label="公告类型">
            <el-select v-model="form.noticeType" filterable placeholder="请选择公告类型">
              <el-option v-for="item in noticeTypeOptions" :key="item.value" :label="item.label" :value="item.value" />
            </el-select>
          </el-form-item>

          <el-collapse v-model="advancedPanels" class="advanced-collapse">
            <el-collapse-item name="advanced" title="高级补充信息">
              <div class="form-grid">
                <el-form-item label="来源 ID">
                  <el-input-number v-model="form.sourceId" :min="0" :controls="false" />
                </el-form-item>
                <el-form-item label="栏目 ID">
                  <el-input-number v-model="form.channelId" :min="0" :controls="false" />
                </el-form-item>
                <el-form-item label="标题" class="full-row">
                  <el-input v-model.trim="form.title" clearable />
                </el-form-item>
                <el-form-item label="备用文本" class="full-row">
                  <el-input v-model="form.textContent" type="textarea" :rows="5" />
                </el-form-item>
              </div>
            </el-collapse-item>
          </el-collapse>

          <div class="form-actions">
            <el-button type="primary" :icon="CirclePlus" :loading="submitting" @click="submitImport">开始导入</el-button>
            <el-button :icon="Refresh" :loading="refreshing" :disabled="!enqueuedJob" @click="refreshImportState">刷新状态</el-button>
          </div>
        </el-form>
      </section>

      <section class="content-panel result-panel">
        <div class="panel-heading">
          <h2>导入状态</h2>
          <el-button text :icon="ArrowRight" @click="router.push('/bidops/crawl/raw-notices')">原始公告</el-button>
        </div>

        <el-empty v-if="!shouldShowResult" description="暂无导入任务" />
        <template v-else>
          <div class="status-strip">
            <div>
              <span>任务</span>
              <strong>{{ jobTitle }}</strong>
            </div>
            <div>
              <span>JobId</span>
              <strong>{{ enqueuedJob?.jobId || job?.id || '-' }}</strong>
            </div>
            <div>
              <span>Job 状态</span>
              <JobStatusTag
                :status="job?.status"
                :status-name="job?.statusName"
                :cancellation-requested="job?.isCancellationRequested"
              />
            </div>
            <div>
              <span>Raw 状态</span>
              <BidOpsStatusTag v-if="rawNotice" :value="rawNotice.status" kind="rawNotice" />
              <strong v-else>-</strong>
            </div>
          </div>

          <el-alert
            v-if="job?.lastError"
            type="error"
            show-icon
            :closable="false"
            :title="job.lastError"
            class="job-error"
          />

          <el-descriptions v-if="rawNotice" :column="2" border class="raw-summary">
            <el-descriptions-item label="标题">{{ rawNotice.title }}</el-descriptions-item>
            <el-descriptions-item label="公告类型">{{ formatNoticeType(rawNotice.noticeType) }}</el-descriptions-item>
            <el-descriptions-item label="发布时间">{{ formatDateTime(rawNotice.publishTime) }}</el-descriptions-item>
            <el-descriptions-item label="抓取时间">{{ formatDateTime(rawNotice.fetchTime) }}</el-descriptions-item>
            <el-descriptions-item label="详情 URL">
              <el-link :href="rawNotice.detailUrl" target="_blank" type="primary">{{ rawNotice.detailUrl }}</el-link>
            </el-descriptions-item>
            <el-descriptions-item label="错误信息">{{ rawNotice.lastError || '-' }}</el-descriptions-item>
          </el-descriptions>

          <div v-if="pipeline" class="pipeline-section">
            <RawNoticePipelinePanel :pipeline="pipeline" />
          </div>

          <div class="result-actions">
            <el-button :icon="View" :disabled="!canOpenRawNotice" @click="openRawNotice">查看公告详情</el-button>
            <el-button type="primary" :icon="ArrowRight" :disabled="!canOpenReviewTask" @click="openReviewTask">进入审核任务</el-button>
          </div>
        </template>
      </section>
    </div>
  </PageContainer>
</template>

<style scoped>
.manual-import-layout {
  display: grid;
  grid-template-columns: minmax(320px, 0.75fr) minmax(0, 1.25fr);
  gap: 16px;
  align-items: start;
}

.import-panel,
.result-panel {
  min-width: 0;
}

.guardrail-alert {
  margin-bottom: 16px;
}

.advanced-collapse {
  margin-top: 4px;
  border-top: 1px solid var(--el-border-color-lighter);
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.form-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 16px;
}

.panel-heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 14px;
}

.panel-heading h2 {
  margin: 0;
  font-size: 16px;
  line-height: 1.3;
}

.status-strip {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 10px;
}

.status-strip > div {
  display: grid;
  gap: 6px;
  min-height: 64px;
  padding: 10px 12px;
  border: 1px solid var(--el-border-color-light);
  border-radius: 6px;
  background: var(--el-fill-color-blank);
}

.status-strip span {
  color: var(--el-text-color-secondary);
  font-size: 13px;
}

.status-strip strong {
  min-width: 0;
  overflow-wrap: anywhere;
  color: var(--el-text-color-primary);
  font-size: 14px;
  font-weight: 650;
}

.job-error,
.raw-summary,
.pipeline-section,
.result-actions {
  margin-top: 14px;
}

.result-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

@media (max-width: 1100px) {
  .manual-import-layout,
  .status-strip {
    grid-template-columns: 1fr;
  }
}
</style>
