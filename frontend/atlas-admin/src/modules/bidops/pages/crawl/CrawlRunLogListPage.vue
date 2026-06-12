<script setup lang="ts">
import { useRouter } from 'vue-router'
import { Monitor, Refresh, View } from '@element-plus/icons-vue'
import { crawlRunLogsApi } from '@/api/bidops/crawlRunLogs.api'
import DataTable from '@/shared/components/DataTable.vue'
import PageContainer from '@/shared/components/PageContainer.vue'
import SearchForm from '@/shared/components/SearchForm.vue'
import { useTableQuery } from '@/shared/composables/useTableQuery'
import { formatDateTime } from '@/shared/utils/date'
import BidOpsStatusTag from '../../components/BidOpsStatusTag.vue'
import type { BidOpsId, CrawlRunLogDto } from '../../types'

interface CrawlRunLogListQuery {
  keyword: string
  sourceId: BidOpsId | ''
  channelId: BidOpsId | ''
  backgroundJobId: BidOpsId | ''
  operation: string
  status: string
  pageIndex: number
  pageSize: number
}

const router = useRouter()

const statusOptions = [
  { label: '成功', value: 'Succeeded' },
  { label: '失败', value: 'Failed' },
  { label: '已跳过', value: 'Skipped' },
  { label: '内容变更', value: 'Changed' },
  { label: '限流跳过', value: 'RateLimited' },
  { label: '公告失败', value: 'NoticeFailed' },
  { label: '附件列表失败', value: 'AttachmentListFailed' },
]

const table = useTableQuery<CrawlRunLogDto, CrawlRunLogListQuery>(
  (params) =>
    crawlRunLogsApi.search({
      ...params,
      sourceId: params.sourceId || undefined,
      channelId: params.channelId || undefined,
      backgroundJobId: params.backgroundJobId || undefined,
      operation: params.operation || undefined,
      status: params.status || undefined,
    }),
  {
    keyword: '',
    sourceId: '',
    channelId: '',
    backgroundJobId: '',
    operation: '',
    status: '',
    pageIndex: 1,
    pageSize: 20,
  },
)

function formatDuration(value?: number | null) {
  if (!value || value <= 0) return '-'
  if (value < 1000) return `${value} ms`
  return `${(value / 1000).toFixed(1)} s`
}
</script>

<template>
  <PageContainer title="采集运行日志" description="查看公开采集、手动导入和 Raw 入库过程的真实运行记录。">
    <template #actions>
      <el-button :icon="Monitor" @click="router.push('/bidops/operations/jobs')">后台任务</el-button>
      <el-button :icon="Refresh" :loading="table.loading" @click="table.loadData">刷新</el-button>
    </template>

    <SearchForm @search="table.search" @reset="table.reset()">
      <el-form-item label="关键词">
        <el-input v-model="table.query.keyword" clearable placeholder="操作 / 状态 / 消息" />
      </el-form-item>
      <el-form-item label="状态">
        <el-select v-model="table.query.status" clearable placeholder="全部" style="width: 180px">
          <el-option v-for="item in statusOptions" :key="item.value" :label="item.label" :value="item.value" />
        </el-select>
      </el-form-item>
      <el-form-item label="操作">
        <el-input v-model="table.query.operation" clearable placeholder="StateGridEcpCrawl" />
      </el-form-item>
      <el-form-item label="SourceId">
        <el-input v-model="table.query.sourceId" clearable placeholder="来源 ID" />
      </el-form-item>
      <el-form-item label="ChannelId">
        <el-input v-model="table.query.channelId" clearable placeholder="栏目 ID" />
      </el-form-item>
      <el-form-item label="JobId">
        <el-input v-model="table.query.backgroundJobId" clearable placeholder="后台任务 ID" />
      </el-form-item>
    </SearchForm>

    <DataTable :data="table.result.items" :loading="table.loading">
      <el-table-column label="状态" width="130">
        <template #default="{ row }"><BidOpsStatusTag :value="row.status" /></template>
      </el-table-column>
      <el-table-column prop="operation" label="操作" min-width="170" show-overflow-tooltip />
      <el-table-column prop="message" label="消息" min-width="320" show-overflow-tooltip />
      <el-table-column prop="sourceId" label="来源 ID" width="120" />
      <el-table-column prop="channelId" label="栏目 ID" width="120" />
      <el-table-column prop="backgroundJobId" label="JobId" width="150" />
      <el-table-column label="耗时" width="100">
        <template #default="{ row }">{{ formatDuration(row.durationMs) }}</template>
      </el-table-column>
      <el-table-column label="记录时间" width="170">
        <template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="120" fixed="right">
        <template #default="{ row }">
          <el-button size="small" :icon="View" @click="router.push(`/bidops/intelligence/run-logs/${row.id}`)">详情</el-button>
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
.table-pagination {
  justify-content: flex-end;
  margin-top: 14px;
}
</style>
