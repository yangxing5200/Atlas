<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Refresh, View } from '@element-plus/icons-vue'
import { workersApi } from '@/api/operations/workers.api'
import DataTable from '@/shared/components/DataTable.vue'
import PageContainer from '@/shared/components/PageContainer.vue'
import SearchForm from '@/shared/components/SearchForm.vue'
import { useTableQuery } from '@/shared/composables/useTableQuery'
import { formatDateTime } from '@/shared/utils/date'
import type { BackgroundWorkerHeartbeatDto } from '../types'
import { formatSeconds } from '../utils/display'

interface WorkerListQuery {
  keyword: string
  queue: string
  onlineOnly: boolean
  pageIndex: number
  pageSize: number
}

const route = useRoute()
const router = useRouter()
const bidOpsMode = computed(() => route.path.startsWith('/bidops/operations/worker-heartbeats'))
const pageTitle = computed(() => (bidOpsMode.value ? 'Worker 心跳' : 'Worker 节点'))
const pageDescription = computed(() =>
  bidOpsMode.value
    ? '查看当前后台 Worker 是否在线，以及是否消费 bidops 队列。'
    : '查看后台 Worker 节点、队列、当前任务和最后心跳。',
)

const table = useTableQuery<BackgroundWorkerHeartbeatDto, WorkerListQuery>(
  (params) => workersApi.search(params),
  {
    keyword: '',
    queue: bidOpsMode.value ? 'bidops' : '',
    onlineOnly: false,
    pageIndex: 1,
    pageSize: 20,
  },
)

const hasOnlineBidOpsWorker = computed(() =>
  table.result.items.some((worker) => worker.isOnline && worker.queues.some((queue) => queue.toLowerCase() === 'bidops')),
)
</script>

<template>
  <PageContainer :title="pageTitle" :description="pageDescription">
    <template #actions>
      <el-button :icon="Refresh" :loading="table.loading" @click="table.loadData">刷新</el-button>
    </template>

    <el-alert
      v-if="bidOpsMode && !table.loading && !hasOnlineBidOpsWorker"
      type="error"
      show-icon
      :closable="false"
      title="当前没有在线 Worker 消费 bidops 队列，BidOps 后台任务会积压。"
      class="worker-alert"
    />

    <SearchForm @search="table.search" @reset="table.reset({ queue: bidOpsMode ? 'bidops' : '' })">
      <el-form-item label="关键词">
        <el-input v-model="table.query.keyword" clearable placeholder="Worker / Host / 当前任务" />
      </el-form-item>
      <el-form-item label="队列">
        <el-input v-model="table.query.queue" clearable placeholder="default / tenant / bidops" />
      </el-form-item>
      <el-form-item label="在线">
        <el-switch v-model="table.query.onlineOnly" active-text="只看在线" />
      </el-form-item>
    </SearchForm>

    <DataTable :data="table.result.items" :loading="table.loading" empty-text="暂无 Worker 心跳">
      <el-table-column label="状态" width="90">
        <template #default="{ row }">
          <el-tag :type="row.isOnline ? 'success' : 'danger'" effect="light">
            {{ row.isOnline ? '在线' : '离线' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="workerId" label="Worker" min-width="260" show-overflow-tooltip />
      <el-table-column label="主机" min-width="160">
        <template #default="{ row }">{{ row.hostName }} / {{ row.processId }}</template>
      </el-table-column>
      <el-table-column prop="runtimeMode" label="模式" width="110" />
      <el-table-column label="队列" min-width="220">
        <template #default="{ row }">
          <el-space wrap>
            <el-tag v-for="queue in row.queues" :key="queue" size="small" effect="plain">{{ queue }}</el-tag>
          </el-space>
        </template>
      </el-table-column>
      <el-table-column label="能力" min-width="190">
        <template #default="{ row }">
          <el-space wrap>
            <el-tag :type="row.oneTimeJobWorkerEnabled ? 'success' : 'info'" size="small">一次性任务</el-tag>
            <el-tag :type="row.recurringTaskRunnerEnabled ? 'success' : 'info'" size="small">周期任务</el-tag>
          </el-space>
        </template>
      </el-table-column>
      <el-table-column label="当前任务" min-width="260" show-overflow-tooltip>
        <template #default="{ row }">
          <template v-if="row.currentJobId">
            <el-button link type="primary" :icon="View" @click="router.push(`/ops/jobs/${row.currentJobId}`)">
              {{ row.currentJobId }}
            </el-button>
            <span class="muted-text">{{ row.currentJobType || '-' }}</span>
          </template>
          <span v-else>-</span>
        </template>
      </el-table-column>
      <el-table-column label="启动时间" width="170">
        <template #default="{ row }">{{ formatDateTime(row.startedAtUtc) }}</template>
      </el-table-column>
      <el-table-column label="最后心跳" width="180">
        <template #default="{ row }">
          {{ formatDateTime(row.lastSeenAtUtc) }}
          <span class="muted-text">({{ formatSeconds(row.secondsSinceLastSeen) }})</span>
        </template>
      </el-table-column>
    </DataTable>

    <el-pagination
      v-model:current-page="table.query.pageIndex"
      v-model:page-size="table.query.pageSize"
      :total="table.result.total"
      :page-sizes="[10, 20, 50, 100]"
      layout="total, sizes, prev, pager, next, jumper"
      class="table-pagination"
      @current-change="table.loadData"
      @size-change="table.loadData"
    />
  </PageContainer>
</template>

<style scoped>
.worker-alert {
  margin-bottom: 14px;
}

.muted-text {
  color: #7b8794;
  font-size: 12px;
}

.table-pagination {
  justify-content: flex-end;
  margin-top: 14px;
}
</style>
