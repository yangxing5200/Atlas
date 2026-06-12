<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { View } from '@element-plus/icons-vue'
import { packagesApi } from '@/api/bidops/packages.api'
import DataTable from '@/shared/components/DataTable.vue'
import PageContainer from '@/shared/components/PageContainer.vue'
import SearchForm from '@/shared/components/SearchForm.vue'
import { useTableQuery } from '@/shared/composables/useTableQuery'
import { formatMoney } from '@/shared/utils/money'
import BidOpsStatusTag from '../../components/BidOpsStatusTag.vue'
import type { BidOpsId, TenderPackageDto } from '../../types'
import { formatCategory, formatPackageNo } from '../../utils/display'

interface PackageListQuery {
  keyword: string
  noticeId?: BidOpsId
  pageIndex: number
  pageSize: number
}

const route = useRoute()
const router = useRouter()
const routeNoticeId = computed(() => {
  const value = String(route.query.noticeId || '').trim()
  return value || undefined
})

const table = useTableQuery<TenderPackageDto, PackageListQuery>(packagesApi.search, {
  keyword: '',
  noticeId: routeNoticeId.value,
  pageIndex: 1,
  pageSize: 20,
})
</script>

<template>
  <PageContainer title="商机包件" description="以包件为最小作业单元，查看预算、交付周期和要求项入口。">
    <SearchForm @search="table.search" @reset="table.reset({ noticeId: undefined })">
      <el-form-item label="关键词">
        <el-input v-model="table.query.keyword" clearable placeholder="包件号 / 包件名称" />
      </el-form-item>
      <el-form-item label="公告 ID">
        <el-input v-model.trim="table.query.noticeId" clearable placeholder="NoticeId" />
      </el-form-item>
    </SearchForm>

    <DataTable :data="table.result.items" :loading="table.loading">
      <el-table-column label="包件号" min-width="130">
        <template #default="{ row }">{{ formatPackageNo(row.packageNo) }}</template>
      </el-table-column>
      <el-table-column prop="packageName" label="包件名称" min-width="260" show-overflow-tooltip />
      <el-table-column label="品类" min-width="140">
        <template #default="{ row }">{{ formatCategory(row.category) }}</template>
      </el-table-column>
      <el-table-column label="预算金额" width="150"><template #default="{ row }">{{ formatMoney(row.budgetAmount) }}</template></el-table-column>
      <el-table-column label="最高限价" width="150"><template #default="{ row }">{{ formatMoney(row.maxPrice) }}</template></el-table-column>
      <el-table-column prop="deliveryPlace" label="交付地点" min-width="180" show-overflow-tooltip />
      <el-table-column prop="deliveryPeriod" label="交付周期" min-width="180" show-overflow-tooltip />
      <el-table-column label="状态" width="130"><template #default="{ row }"><BidOpsStatusTag :value="row.status" /></template></el-table-column>
      <el-table-column label="操作" width="130" fixed="right">
        <template #default="{ row }">
          <el-button size="small" :icon="View" @click="router.push(`/bidops/packages/${row.id}`)">详情</el-button>
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
