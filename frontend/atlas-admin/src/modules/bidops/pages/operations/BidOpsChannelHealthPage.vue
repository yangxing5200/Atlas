<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Refresh, Search, Tickets, VideoPause, VideoPlay, RefreshLeft } from '@element-plus/icons-vue'
import { useRouter } from 'vue-router'
import { crawlChannelsApi } from '@/api/bidops/crawlChannels.api'
import { bidOpsOperationsApi } from '@/api/bidops/operations.api'
import DataTable from '@/shared/components/DataTable.vue'
import PageContainer from '@/shared/components/PageContainer.vue'
import SearchForm from '@/shared/components/SearchForm.vue'
import { formatDateTime } from '@/shared/utils/date'
import type { BidOpsChannelHealthDto } from '@/modules/operations/types'
import { formatHealthStatus, healthStatusTagType } from '@/modules/operations/utils/display'
import { formatNoticeType, formatSourceType } from '../../utils/display'

const router = useRouter()
const loading = ref(false)
const actionLoading = ref(false)
const keyword = ref('')
const status = ref('')
const items = ref<BidOpsChannelHealthDto[]>([])

const healthOptions = [
  { label: '健康', value: 'Healthy' },
  { label: '待扫描', value: 'Due' },
  { label: '超期未成功', value: 'Stale' },
  { label: '最近失败', value: 'Failed' },
  { label: '需登录已跳过', value: 'SkippedNeedLogin' },
  { label: '从未成功', value: 'NeverSucceeded' },
  { label: '停用', value: 'Disabled' },
]

const filteredItems = computed(() => {
  const text = keyword.value.trim().toLowerCase()
  return items.value.filter((item) => {
    const matchedText =
      !text ||
      item.sourceName.toLowerCase().includes(text) ||
      item.channelName.toLowerCase().includes(text) ||
      String(item.channelId).includes(text)
    const matchedStatus = !status.value || item.healthStatus === status.value
    return matchedText && matchedStatus
  })
})

async function loadData() {
  loading.value = true
  try {
    items.value = await bidOpsOperationsApi.channelsHealth()
  } finally {
    loading.value = false
  }
}

function checkpointStatusLabel(value: string) {
  const labels: Record<string, string> = {
    Idle: '待继续',
    Running: '运行中',
    Paused: '已暂停',
    Completed: '已完成',
    Failed: '失败',
  }
  return labels[value] || (value ? value : '未开始')
}

function checkpointTagType(value: string) {
  if (value === 'Completed') return 'success'
  if (value === 'Running') return 'primary'
  if (value === 'Paused') return 'info'
  if (value === 'Failed') return 'danger'
  return 'warning'
}

function alertTagType(value: string) {
  if (value === 'Error') return 'danger'
  if (value === 'Warning') return 'warning'
  if (value === 'Info') return 'info'
  return 'success'
}

async function continueBackfill(row: BidOpsChannelHealthDto) {
  await runChannelAction(async () => {
    const job = await crawlChannelsApi.continueCheckpoint(row.channelId, { mode: 'Backfill', maxPages: 3 })
    ElMessage.success(`继续扫描已入队：JobId=${job.jobId}`)
  })
}

async function pauseBackfill(row: BidOpsChannelHealthDto) {
  await runChannelAction(async () => {
    await crawlChannelsApi.pauseCheckpoint(row.channelId, { mode: 'Backfill', reason: 'Paused from operations page' })
    ElMessage.success('补采游标已暂停')
  })
}

async function resumeBackfill(row: BidOpsChannelHealthDto) {
  await runChannelAction(async () => {
    const job = await crawlChannelsApi.resumeCheckpoint(row.channelId, { mode: 'Backfill', maxPages: 3 })
    ElMessage.success(`补采已恢复：JobId=${job.jobId}`)
  })
}

async function resetBackfill(row: BidOpsChannelHealthDto) {
  try {
    await ElMessageBox.confirm('重置后补采游标会回到第 1 页，累计进度也会清零。', '重置补采游标', {
      confirmButtonText: '重置',
      cancelButtonText: '取消',
      type: 'warning',
    })
    await runChannelAction(async () => {
      await crawlChannelsApi.resetCheckpoint(row.channelId, { mode: 'Backfill', nextPage: 1 })
      ElMessage.success('补采游标已重置')
    })
  } catch {
    return
  }
}

async function runChannelAction(action: () => Promise<void>) {
  actionLoading.value = true
  try {
    await action()
    await loadData()
  } finally {
    actionLoading.value = false
  }
}

function reset() {
  keyword.value = ''
  status.value = ''
}

onMounted(loadData)
</script>

<template>
  <PageContainer title="采集健康" description="按来源和栏目查看最近扫描状态、积压任务和 24 小时成功失败数。">
    <template #actions>
      <el-button :icon="Tickets" @click="router.push('/bidops/operations/jobs')">任务监控</el-button>
      <el-button :icon="Refresh" :loading="loading" @click="loadData">刷新</el-button>
    </template>

    <SearchForm @search="loadData" @reset="reset">
      <el-form-item label="关键词">
        <el-input v-model="keyword" clearable placeholder="来源 / 栏目 / ChannelId" />
      </el-form-item>
      <el-form-item label="健康状态">
        <el-select v-model="status" clearable placeholder="全部" style="width: 180px">
          <el-option v-for="item in healthOptions" :key="item.value" :label="item.label" :value="item.value" />
        </el-select>
      </el-form-item>
      <el-button type="primary" :icon="Search" @click="loadData">查询</el-button>
    </SearchForm>

    <DataTable :data="filteredItems" :loading="loading">
      <el-table-column label="健康状态" width="140">
        <template #default="{ row }">
          <el-tag :type="healthStatusTagType(row.healthStatus)" effect="light">
            {{ formatHealthStatus(row.healthStatus) }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="sourceName" label="来源" min-width="180" show-overflow-tooltip>
        <template #default="{ row }">
          <div class="stacked">
            <strong>{{ row.sourceName }}</strong>
            <span>{{ formatSourceType(row.sourceType) }}</span>
          </div>
        </template>
      </el-table-column>
      <el-table-column prop="channelName" label="栏目" min-width="190" show-overflow-tooltip>
        <template #default="{ row }">
          <div class="stacked">
            <strong>{{ row.channelName }}</strong>
            <span>ChannelId {{ row.channelId }}</span>
          </div>
        </template>
      </el-table-column>
      <el-table-column label="公告类型" width="150">
        <template #default="{ row }">{{ formatNoticeType(row.noticeType) }}</template>
      </el-table-column>
      <el-table-column label="启用" width="110">
        <template #default="{ row }">
          <el-tag :type="row.enabled ? 'success' : 'info'" effect="light">{{ row.enabled ? '启用' : '停用' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="登录要求" width="120">
        <template #default="{ row }">
          <el-tag :type="row.needLogin ? 'warning' : 'info'" effect="light">{{ row.needLogin ? '需登录' : '公开' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="间隔" width="100">
        <template #default="{ row }">{{ row.scheduleMode === 'Daily' ? `每天 ${row.dailyScanTime || '-'}` : `${row.crawlIntervalMinutes} 分钟` }}</template>
      </el-table-column>
      <el-table-column label="最近扫描" width="170">
        <template #default="{ row }">{{ formatDateTime(row.lastScanTime) }}</template>
      </el-table-column>
      <el-table-column label="最近成功" width="170">
        <template #default="{ row }">{{ formatDateTime(row.lastSuccessTime) }}</template>
      </el-table-column>
      <el-table-column label="积压" width="110">
        <template #default="{ row }">{{ row.pendingJobs }} / {{ row.runningJobs }}</template>
      </el-table-column>
      <el-table-column label="24h 成败" width="120">
        <template #default="{ row }">{{ row.succeededJobs24h }} / {{ row.failedJobs24h }}</template>
      </el-table-column>
      <el-table-column label="补采进度" width="190">
        <template #default="{ row }">
          <div class="stacked">
            <strong>{{ row.backfillScannedItemCount }} 条 / 剩 {{ row.backfillRemainingEstimate ?? '-' }}</strong>
            <span>新 {{ row.backfillCreatedCount }} · 变 {{ row.backfillChangedCount }} · 重 {{ row.backfillDuplicateCount }}</span>
          </div>
        </template>
      </el-table-column>
      <el-table-column label="补采状态" width="130">
        <template #default="{ row }">
          <el-tag :type="checkpointTagType(row.backfillStatus)" effect="light">
            {{ checkpointStatusLabel(row.backfillStatus) }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="游标" width="100">
        <template #default="{ row }">{{ row.backfillNextCursor || '-' }}</template>
      </el-table-column>
      <el-table-column label="告警" min-width="190" show-overflow-tooltip>
        <template #default="{ row }">
          <el-tag v-if="row.alertLevel" :type="alertTagType(row.alertLevel)" effect="light">
            {{ row.alertMessage || row.alertLevel }}
          </el-tag>
          <span v-else>-</span>
        </template>
      </el-table-column>
      <el-table-column prop="lastError" label="错误信息" min-width="230" show-overflow-tooltip />
      <el-table-column label="操作" width="260" fixed="right">
        <template #default="{ row }">
          <div class="table-actions">
            <el-button size="small" @click="router.push(`/bidops/operations/jobs?businessId=${row.channelId}`)">任务</el-button>
            <el-button size="small" type="primary" plain :icon="VideoPlay" :loading="actionLoading" @click="continueBackfill(row)">
              继续
            </el-button>
            <el-button
              v-if="row.backfillStatus === 'Paused'"
              size="small"
              plain
              :icon="VideoPlay"
              :loading="actionLoading"
              @click="resumeBackfill(row)"
            >
              恢复
            </el-button>
            <el-button v-else size="small" plain :icon="VideoPause" :loading="actionLoading" @click="pauseBackfill(row)">
              暂停
            </el-button>
            <el-button size="small" plain :icon="RefreshLeft" :loading="actionLoading" @click="resetBackfill(row)">重置</el-button>
          </div>
        </template>
      </el-table-column>
    </DataTable>
  </PageContainer>
</template>

<style scoped>
.stacked {
  display: grid;
  gap: 3px;
  min-width: 0;
}

.stacked strong {
  overflow: hidden;
  color: #17202a;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.stacked span {
  color: #687385;
  font-size: 12px;
}

.table-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}
</style>
