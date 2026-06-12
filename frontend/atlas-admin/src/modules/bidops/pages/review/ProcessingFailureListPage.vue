<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { Refresh, RefreshRight, View } from '@element-plus/icons-vue'
import { processingApi } from '@/api/bidops/processing.api'
import { rawNoticesApi } from '@/api/bidops/rawNotices.api'
import DataTable from '@/shared/components/DataTable.vue'
import PageContainer from '@/shared/components/PageContainer.vue'
import SearchForm from '@/shared/components/SearchForm.vue'
import { useTableQuery } from '@/shared/composables/useTableQuery'
import { formatDateTime } from '@/shared/utils/date'
import BidOpsStatusTag from '../../components/BidOpsStatusTag.vue'
import PermissionButton from '../../components/PermissionButton.vue'
import { BIDOPS_PERMISSIONS } from '../../constants'
import type { ProcessingFailureDto } from '../../types'
import { formatNoticeType } from '../../utils/display'

interface ProcessingFailureListQuery {
  keyword: string
  pageIndex: number
  pageSize: number
}

const router = useRouter()
const reparseLoadingId = ref<string | null>(null)

const table = useTableQuery<ProcessingFailureDto, ProcessingFailureListQuery>(
  (params) => processingApi.failures(params),
  { keyword: '', pageIndex: 1, pageSize: 20 },
)

async function reparseRawNotice(row: ProcessingFailureDto) {
  const rawNoticeId = String(row.rawNoticeId)
  reparseLoadingId.value = rawNoticeId
  try {
    const job = await rawNoticesApi.reparse(row.rawNoticeId, { reason: 'Processing failure queue' })
    ElMessage.success(job.alreadyExists ? `重解析任务已存在：${job.jobId}` : `已提交重解析任务：${job.jobId}`)
    await table.loadData()
  } finally {
    reparseLoadingId.value = null
  }
}
</script>

<template>
  <PageContainer title="解析失败" description="查看 RawNotice 解析或处理失败记录，进入详情可查看流水线和原始公告。">
    <template #actions>
      <el-button :icon="Refresh" :loading="table.loading" @click="table.loadData">刷新</el-button>
    </template>

    <SearchForm @search="table.search" @reset="table.reset()">
      <el-form-item label="关键词">
        <el-input v-model="table.query.keyword" clearable placeholder="标题 / URL / 错误信息" />
      </el-form-item>
    </SearchForm>

    <DataTable :data="table.result.items" :loading="table.loading" empty-text="当前没有解析失败记录">
      <el-table-column label="状态" width="110">
        <template #default="{ row }"><BidOpsStatusTag :value="row.rawStatus" kind="rawNotice" /></template>
      </el-table-column>
      <el-table-column prop="title" label="标题" min-width="280" show-overflow-tooltip />
      <el-table-column label="公告类型" width="160">
        <template #default="{ row }">{{ formatNoticeType(row.noticeType) }}</template>
      </el-table-column>
      <el-table-column label="发布时间" width="170">
        <template #default="{ row }">{{ formatDateTime(row.publishTime) }}</template>
      </el-table-column>
      <el-table-column label="抓取时间" width="170">
        <template #default="{ row }">{{ formatDateTime(row.fetchTime) }}</template>
      </el-table-column>
      <el-table-column prop="lastError" label="失败原因" min-width="320" show-overflow-tooltip />
      <el-table-column label="操作" width="210" fixed="right">
        <template #default="{ row }">
          <el-button size="small" :icon="View" @click="router.push(`/bidops/crawl/raw-notices/${row.rawNoticeId}`)">详情</el-button>
          <PermissionButton
            size="small"
            type="primary"
            :icon="RefreshRight"
            :loading="reparseLoadingId === String(row.rawNoticeId)"
            :permission="BIDOPS_PERMISSIONS.REVIEW_APPROVE"
            @click="reparseRawNotice(row)"
          >
            重解析
          </PermissionButton>
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
