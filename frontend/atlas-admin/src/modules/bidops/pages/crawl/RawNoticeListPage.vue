<script setup lang="ts">
import { ElMessage } from 'element-plus'
import { Upload, View } from '@element-plus/icons-vue'
import { useRouter } from 'vue-router'
import { rawNoticesApi } from '@/api/bidops/rawNotices.api'
import DataTable from '@/shared/components/DataTable.vue'
import PageContainer from '@/shared/components/PageContainer.vue'
import SearchForm from '@/shared/components/SearchForm.vue'
import { useRequest } from '@/shared/composables/useRequest'
import { useTableQuery } from '@/shared/composables/useTableQuery'
import { formatDateTime } from '@/shared/utils/date'
import { BIDOPS_PERMISSIONS } from '../../constants'
import BidOpsStatusTag from '../../components/BidOpsStatusTag.vue'
import ManualUrlImportDialog from '../../components/ManualUrlImportDialog.vue'
import PermissionButton from '../../components/PermissionButton.vue'
import type { ImportPublicUrlRequest, RawNoticeDto, RawNoticeStatus } from '../../types'
import { formatNoticeType, rawNoticeStatusOptions } from '../../utils/display'
import { ref } from 'vue'

interface RawNoticeListQuery {
  keyword: string
  status?: RawNoticeStatus | ''
  pageIndex: number
  pageSize: number
}

const router = useRouter()
const importOpen = ref(false)
const importRequest = useRequest()

const table = useTableQuery<RawNoticeDto, RawNoticeListQuery>(
  (params) => rawNoticesApi.search({ ...params, status: params.status || undefined }),
  { keyword: '', status: '', pageIndex: 1, pageSize: 20 },
)

async function submitImport(data: ImportPublicUrlRequest) {
  if (!data.detailUrl.trim()) {
    ElMessage.warning('请输入公开公告 URL')
    return
  }

  await importRequest.run(async () => {
    const job = await rawNoticesApi.importUrl(data)
    ElMessage.success(`已入队：JobId=${job.jobId} JobType=${job.jobType} Queue=${job.queue} AlreadyExists=${job.alreadyExists}`)
    importOpen.value = false
    await table.loadData()
  })
}
</script>

<template>
  <PageContainer title="原始公告" description="查看 Raw 层公告和手动导入公开 URL 的处理状态。">
    <template #actions>
      <PermissionButton type="primary" :icon="Upload" :permission="BIDOPS_PERMISSIONS.CRAWL_IMPORT" @click="importOpen = true">
        手动导入
      </PermissionButton>
    </template>

    <SearchForm @search="table.search" @reset="table.reset()">
      <el-form-item label="关键词">
        <el-input v-model="table.query.keyword" clearable placeholder="标题 / URL" />
      </el-form-item>
      <el-form-item label="状态">
        <el-select v-model="table.query.status" clearable placeholder="全部" style="width: 180px">
          <el-option v-for="item in rawNoticeStatusOptions" :key="item.value" :label="item.label" :value="item.value" />
        </el-select>
      </el-form-item>
    </SearchForm>

    <DataTable :data="table.result.items" :loading="table.loading">
      <el-table-column prop="title" label="标题" min-width="260" show-overflow-tooltip />
      <el-table-column label="公告类型" min-width="160">
        <template #default="{ row }">{{ formatNoticeType(row.noticeType) }}</template>
      </el-table-column>
      <el-table-column prop="sourceId" label="来源 ID" width="110" />
      <el-table-column prop="channelId" label="栏目 ID" width="110" />
      <el-table-column label="发布时间" width="170">
        <template #default="{ row }">{{ formatDateTime(row.publishTime) }}</template>
      </el-table-column>
      <el-table-column label="采集时间" width="170">
        <template #default="{ row }">{{ formatDateTime(row.fetchTime) }}</template>
      </el-table-column>
      <el-table-column label="状态" width="130">
        <template #default="{ row }"><BidOpsStatusTag :value="row.status" kind="rawNotice" /></template>
      </el-table-column>
      <el-table-column prop="lastError" label="错误信息" min-width="220" show-overflow-tooltip />
      <el-table-column label="操作" width="130" fixed="right">
        <template #default="{ row }">
          <el-button size="small" :icon="View" @click="router.push(`/bidops/crawl/raw-notices/${row.id}`)">详情</el-button>
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

    <ManualUrlImportDialog v-model="importOpen" :submitting="importRequest.loading" @submit="submitImport" />
  </PageContainer>
</template>

<style scoped>
.table-pagination {
  justify-content: flex-end;
  margin-top: 14px;
}
</style>
