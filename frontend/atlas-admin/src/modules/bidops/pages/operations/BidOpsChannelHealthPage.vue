<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Refresh, Search, Tickets } from '@element-plus/icons-vue'
import { useRouter } from 'vue-router'
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
        <template #default="{ row }">{{ row.crawlIntervalMinutes }} 分钟</template>
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
      <el-table-column prop="lastError" label="错误信息" min-width="230" show-overflow-tooltip />
      <el-table-column label="操作" width="120" fixed="right">
        <template #default="{ row }">
          <el-button size="small" @click="router.push(`/bidops/operations/jobs?businessId=${row.channelId}`)">任务</el-button>
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
</style>
