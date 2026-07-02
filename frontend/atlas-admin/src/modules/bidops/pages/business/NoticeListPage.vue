<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { Box, DataAnalysis, View } from '@element-plus/icons-vue'
import { lifecycleApi } from '@/api/bidops/lifecycle.api'
import { noticesApi } from '@/api/bidops/notices.api'
import DataTable from '@/shared/components/DataTable.vue'
import PageContainer from '@/shared/components/PageContainer.vue'
import SearchForm from '@/shared/components/SearchForm.vue'
import { usePermission } from '@/shared/composables/usePermission'
import { useTableQuery } from '@/shared/composables/useTableQuery'
import { formatDateTime } from '@/shared/utils/date'
import { formatMoney } from '@/shared/utils/money'
import BidOpsStatusTag from '../../components/BidOpsStatusTag.vue'
import DeadlineCountdown from '../../components/DeadlineCountdown.vue'
import { BIDOPS_PERMISSIONS } from '../../constants'
import type { NoticeDto } from '../../types'
import { formatNoticeType, isResultNoticeType, lifecycleReviewStatusOptions, noticeTypeOptions } from '../../utils/display'

const router = useRouter()
const analyzingRawNoticeId = ref('')
const { visible: canAnalyzeLifecycle } = usePermission(BIDOPS_PERMISSIONS.CRAWL_IMPORT)

// 正式公告库需要在刷新后恢复最近一次搜索条件，复用通用表格查询的本地缓存。
const noticeListQueryStorageKey = 'atlas.bidops.notices.query.v1'

const table = useTableQuery<NoticeDto, { keyword: string; noticeType: string; lifecycleReviewStatus: string; pageIndex: number; pageSize: number }>(
  (params) => noticesApi.search({
    ...params,
    noticeType: params.noticeType || undefined,
    lifecycleReviewStatus: params.lifecycleReviewStatus || undefined,
  }),
  {
    keyword: '',
    noticeType: 'AwardAnnouncement',
    lifecycleReviewStatus: '',
    pageIndex: 1,
    pageSize: 20,
  },
  {
    storageKey: noticeListQueryStorageKey,
  },
)

function canAnalyze(row: NoticeDto) {
  return canAnalyzeLifecycle.value && isResultNoticeType(row.noticeType, row.title) && !!row.rawNoticeId
}

async function analyzeLifecycle(row: NoticeDto) {
  if (!canAnalyze(row)) return

  analyzingRawNoticeId.value = row.rawNoticeId
  try {
    const job = await lifecycleApi.enqueueReverseClose({
      rawNoticeId: row.rawNoticeId,
      persistEvidence: true,
      persistLifecycleLinks: false,
      persistLifecycleLinksOnCompletion: true,
    })
    ElMessage.success(job.alreadyExists ? `闭环分析任务已在队列中：${job.jobId}` : `闭环分析任务已提交：${job.jobId}`)
    await router.push(`/bidops/outcomes?rawNoticeId=${row.rawNoticeId}`)
  } finally {
    analyzingRawNoticeId.value = ''
  }
}
</script>

<template>
  <PageContainer title="正式公告库" description="审核通过后生成的正式业务公告；结果类公告可直接发起生命周期闭环分析。">
    <SearchForm @search="table.search" @reset="table.reset()">
      <el-form-item label="关键词">
        <el-input v-model="table.query.keyword" clearable placeholder="标题 / 项目 / 采购人" />
      </el-form-item>
      <el-form-item label="公告类型">
        <el-select v-model="table.query.noticeType" clearable filterable :value-on-clear="''" placeholder="全部" style="width: 210px">
          <el-option v-for="item in noticeTypeOptions" :key="item.value" :label="item.label" :value="item.value" />
        </el-select>
      </el-form-item>
      <el-form-item label="闭环审核">
        <el-select v-model="table.query.lifecycleReviewStatus" clearable :value-on-clear="''" placeholder="全部" style="width: 180px">
          <el-option v-for="item in lifecycleReviewStatusOptions" :key="item.value" :label="item.label" :value="item.value" />
        </el-select>
      </el-form-item>
    </SearchForm>

    <DataTable :data="table.result.items" :loading="table.loading">
      <el-table-column prop="title" label="标题" min-width="240" show-overflow-tooltip />
      <el-table-column label="公告类型" min-width="160">
        <template #default="{ row }">{{ formatNoticeType(row.noticeType) }}</template>
      </el-table-column>
      <el-table-column prop="projectName" label="项目名称" min-width="220" show-overflow-tooltip />
      <el-table-column prop="projectCode" label="项目编码" min-width="150" />
      <el-table-column prop="buyerName" label="采购人" min-width="180" show-overflow-tooltip />
      <el-table-column prop="region" label="地区" width="120" />
      <el-table-column label="预算金额" width="150"><template #default="{ row }">{{ formatMoney(row.budgetAmount) }}</template></el-table-column>
      <el-table-column label="发布时间" width="170"><template #default="{ row }">{{ formatDateTime(row.publishTime) }}</template></el-table-column>
      <el-table-column label="投标截止" width="170"><template #default="{ row }">{{ formatDateTime(row.bidDeadline) }}</template></el-table-column>
      <el-table-column label="最后更新时间" width="170"><template #default="{ row }">{{ formatDateTime(row.updatedAt || row.createdAt) }}</template></el-table-column>
      <el-table-column label="倒计时" width="110"><template #default="{ row }"><DeadlineCountdown :value="row.bidDeadline" /></template></el-table-column>
      <el-table-column label="闭环审核" width="130"><template #default="{ row }"><BidOpsStatusTag :value="row.lifecycleReviewStatus" /></template></el-table-column>
      <el-table-column label="状态" width="130"><template #default="{ row }"><BidOpsStatusTag :value="row.status" /></template></el-table-column>
      <el-table-column label="操作" width="280" fixed="right">
        <template #default="{ row }">
          <el-button size="small" :icon="View" @click="router.push(`/bidops/notices/${row.id}`)">详情</el-button>
          <el-button size="small" :icon="Box" @click="router.push(`/bidops/packages?noticeId=${row.id}`)">包件</el-button>
          <el-button
            v-if="canAnalyze(row)"
            size="small"
            type="primary"
            plain
            :icon="DataAnalysis"
            :loading="analyzingRawNoticeId === row.rawNoticeId"
            @click="analyzeLifecycle(row)"
          >
            分析闭环
          </el-button>
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
