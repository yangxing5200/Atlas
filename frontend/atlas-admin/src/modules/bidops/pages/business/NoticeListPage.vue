<script setup lang="ts">
import { useRouter } from 'vue-router'
import { Box } from '@element-plus/icons-vue'
import { noticesApi } from '@/api/bidops/notices.api'
import DataTable from '@/shared/components/DataTable.vue'
import PageContainer from '@/shared/components/PageContainer.vue'
import SearchForm from '@/shared/components/SearchForm.vue'
import { useTableQuery } from '@/shared/composables/useTableQuery'
import { formatDateTime } from '@/shared/utils/date'
import { formatMoney } from '@/shared/utils/money'
import BidOpsStatusTag from '../../components/BidOpsStatusTag.vue'
import DeadlineCountdown from '../../components/DeadlineCountdown.vue'
import type { NoticeDto } from '../../types'
import { formatNoticeType, noticeTypeOptions } from '../../utils/display'

const router = useRouter()
const table = useTableQuery<NoticeDto, { keyword: string; noticeType?: string; pageIndex: number; pageSize: number }>(
  (params) => noticesApi.search({ ...params, noticeType: params.noticeType || undefined }),
  {
    keyword: '',
    noticeType: '',
    pageIndex: 1,
    pageSize: 20,
  },
)
</script>

<template>
  <PageContainer title="正式公告库" description="审核通过后生成的正式业务公告；详情接口待后端补充。">
    <SearchForm @search="table.search" @reset="table.reset()">
      <el-form-item label="关键词">
        <el-input v-model="table.query.keyword" clearable placeholder="标题 / 项目 / 采购人" />
      </el-form-item>
      <el-form-item label="公告类型">
        <el-select v-model="table.query.noticeType" clearable filterable placeholder="全部" style="width: 210px">
          <el-option v-for="item in noticeTypeOptions" :key="item.value" :label="item.label" :value="item.value" />
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
      <el-table-column label="状态" width="130"><template #default="{ row }"><BidOpsStatusTag :value="row.status" /></template></el-table-column>
      <el-table-column label="操作" width="140" fixed="right">
        <template #default="{ row }">
          <el-button size="small" :icon="Box" @click="router.push(`/bidops/packages?noticeId=${row.id}`)">查看包件</el-button>
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
