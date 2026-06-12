<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { ArrowLeft, Close, Refresh } from '@element-plus/icons-vue'
import { useRoute, useRouter } from 'vue-router'
import { backgroundJobsApi } from '@/api/operations/backgroundJobs.api'
import PageContainer from '@/shared/components/PageContainer.vue'
import { formatDateTime } from '@/shared/utils/date'
import JobStatusTag from '../components/JobStatusTag.vue'
import type { BackgroundJobDetailDto } from '../types'
import { formatSeconds } from '../utils/display'

const route = useRoute()
const router = useRouter()
const loading = ref(false)
const job = ref<BackgroundJobDetailDto | null>(null)
const jobId = computed(() => String(route.params.id || ''))

async function loadData() {
  loading.value = true
  try {
    job.value = await backgroundJobsApi.get(jobId.value)
  } catch {
    job.value = null
  } finally {
    loading.value = false
  }
}

async function retryJob() {
  if (!job.value) return
  await ElMessageBox.confirm('将创建新的后台任务，原任务历史不会被覆盖。', '确认重试', { type: 'warning' })
  const result = await backgroundJobsApi.retry(String(job.value.id))
  ElMessage.success(`已创建重试任务：${result.newJobId}`)
  await loadData()
}

async function cancelJob() {
  if (!job.value) return
  await ElMessageBox.confirm('Running 任务不会被强制终止，仅 Pending / Failed / Dead 可取消。', '确认取消', {
    type: 'warning',
  })
  const result = await backgroundJobsApi.cancel(String(job.value.id))
  ElMessage.success(result.message)
  await loadData()
}

onMounted(loadData)
</script>

<template>
  <PageContainer title="后台任务详情" :description="`JobId ${jobId}`">
    <template #actions>
      <el-button :icon="ArrowLeft" @click="router.back()">返回</el-button>
      <el-button :icon="Refresh" :loading="loading" @click="loadData">刷新</el-button>
      <el-button :icon="Refresh" :disabled="!job" @click="retryJob">重试</el-button>
      <el-button :icon="Close" :disabled="!job" @click="cancelJob">取消</el-button>
    </template>

    <el-skeleton v-if="loading" :rows="10" animated />
    <el-empty v-else-if="!job" description="未找到任务" />
    <template v-else>
      <section class="summary-band">
        <div>
          <span>状态</span>
          <JobStatusTag :status="job.status" :status-name="job.statusName" />
        </div>
        <div>
          <span>队列</span>
          <strong>{{ job.queue }}</strong>
        </div>
        <div>
          <span>重试</span>
          <strong>{{ job.attemptCount }} / {{ job.maxAttempts }}</strong>
        </div>
        <div>
          <span>运行时长</span>
          <strong>{{ formatSeconds(job.runSeconds) }}</strong>
        </div>
      </section>

      <el-tabs class="detail-tabs">
        <el-tab-pane label="基本信息">
          <el-descriptions :column="2" border>
            <el-descriptions-item label="JobId">{{ job.id }}</el-descriptions-item>
            <el-descriptions-item label="任务类型">{{ job.jobType }}</el-descriptions-item>
            <el-descriptions-item label="任务名称">{{ job.jobName || '-' }}</el-descriptions-item>
            <el-descriptions-item label="幂等键">{{ job.deduplicationKey || '-' }}</el-descriptions-item>
            <el-descriptions-item label="TenantId">{{ job.tenantId || '-' }}</el-descriptions-item>
            <el-descriptions-item label="StoreId">{{ job.storeId || '-' }}</el-descriptions-item>
            <el-descriptions-item label="优先级">{{ job.priority }}</el-descriptions-item>
            <el-descriptions-item label="超时锁定">{{ job.isStaleRunning ? '是' : '否' }}</el-descriptions-item>
            <el-descriptions-item label="创建时间">{{ formatDateTime(job.createdAt) }}</el-descriptions-item>
            <el-descriptions-item label="可执行时间">{{ formatDateTime(job.availableAtUtc) }}</el-descriptions-item>
            <el-descriptions-item label="开始时间">{{ formatDateTime(job.startedAtUtc) }}</el-descriptions-item>
            <el-descriptions-item label="完成时间">{{ formatDateTime(job.completedAtUtc) }}</el-descriptions-item>
            <el-descriptions-item label="锁定时间">{{ formatDateTime(job.lockedAtUtc) }}</el-descriptions-item>
            <el-descriptions-item label="锁定节点">{{ job.lockedBy || '-' }}</el-descriptions-item>
            <el-descriptions-item label="下次重试">{{ formatDateTime(job.nextAttemptAtUtc) }}</el-descriptions-item>
          </el-descriptions>
        </el-tab-pane>

        <el-tab-pane label="Payload">
          <pre class="code-panel">{{ job.payload || '-' }}</pre>
        </el-tab-pane>

        <el-tab-pane label="结果">
          <pre class="code-panel">{{ job.result || '-' }}</pre>
        </el-tab-pane>

        <el-tab-pane label="错误">
          <pre class="code-panel error-panel">{{ job.lastError || '-' }}</pre>
        </el-tab-pane>
      </el-tabs>
    </template>
  </PageContainer>
</template>

<style scoped>
.summary-band {
  display: grid;
  grid-template-columns: repeat(4, minmax(140px, 1fr));
  gap: 10px;
  margin-bottom: 16px;
}

.summary-band > div {
  display: grid;
  gap: 6px;
  min-height: 70px;
  padding: 12px;
  border: 1px solid #dce3ee;
  border-radius: 8px;
  background: #fff;
}

.summary-band span {
  color: #687385;
  font-size: 13px;
}

.summary-band strong {
  color: #17202a;
  font-size: 17px;
}

.detail-tabs {
  padding: 14px;
  border: 1px solid #dce3ee;
  border-radius: 8px;
  background: #fff;
}

.code-panel {
  min-height: 260px;
  max-height: 560px;
  overflow: auto;
  padding: 14px;
  border: 1px solid #dce3ee;
  border-radius: 6px;
  background: #0f172a;
  color: #dbeafe;
  font-size: 12px;
  line-height: 1.55;
  white-space: pre-wrap;
  word-break: break-word;
}

.error-panel {
  color: #fecaca;
}

@media (max-width: 900px) {
  .summary-band {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
</style>
