<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { Plus, Refresh, View } from '@element-plus/icons-vue'
import { matchingApi } from '@/api/bidops/matching.api'
import { packagesApi } from '@/api/bidops/packages.api'
import DataTable from '@/shared/components/DataTable.vue'
import FormDrawer from '@/shared/components/FormDrawer.vue'
import PageContainer from '@/shared/components/PageContainer.vue'
import SearchForm from '@/shared/components/SearchForm.vue'
import { useRequest } from '@/shared/composables/useRequest'
import { useTableQuery } from '@/shared/composables/useTableQuery'
import { formatDateTime } from '@/shared/utils/date'
import BidOpsStatusTag from '../../components/BidOpsStatusTag.vue'
import PermissionButton from '../../components/PermissionButton.vue'
import { BIDOPS_PERMISSIONS } from '../../constants'
import type { SupplierMatchRunDto } from '../../types'
import { supplierMatchRunStatusOptions } from '../../utils/display'

const props = withDefaults(defineProps<{ packageMode?: boolean; decisionMode?: boolean }>(), {
  packageMode: false,
  decisionMode: false,
})

interface MatchingRunListQuery {
  keyword: string
  packageId: string
  status?: string
  pageIndex: number
  pageSize: number
}

const router = useRouter()
const startDrawerOpen = ref(false)
const startRequest = useRequest()
const pageTitle = computed(() => {
  if (props.packageMode) return '包件匹配'
  if (props.decisionMode) return '立项决策'
  return '匹配记录'
})

const table = useTableQuery<SupplierMatchRunDto, MatchingRunListQuery>(
  (params) =>
    matchingApi.searchRuns({
      keyword: params.keyword || undefined,
      packageId: params.packageId || undefined,
      status: params.status || undefined,
      pageIndex: params.pageIndex,
      pageSize: params.pageSize,
    }),
  {
    keyword: '',
    packageId: '',
    status: undefined,
    pageIndex: 1,
    pageSize: 20,
  },
)

const startForm = reactive({
  packageId: '',
  maxSuppliers: 100,
  criteriaSummary: '',
})

function openStartDrawer() {
  Object.assign(startForm, {
    packageId: '',
    maxSuppliers: 100,
    criteriaSummary: '',
  })
  startDrawerOpen.value = true
}

async function submitStartRun() {
  const packageId = startForm.packageId.trim()
  if (!packageId) {
    ElMessage.warning('请输入包件 ID')
    return
  }

  await startRequest.run(async () => {
    const response = await packagesApi.matchSuppliers(packageId, {
      maxSuppliers: startForm.maxSuppliers,
      criteriaSummary: startForm.criteriaSummary || null,
    })
    ElMessage.success('匹配任务已入队')
    startDrawerOpen.value = false
    await router.push(`/bidops/matching/runs/${response.run.id}`)
  })
}
</script>

<template>
  <PageContainer :title="pageTitle" description="查看厂家匹配运行、后台状态和匹配结果。">
    <template #actions>
      <el-button :icon="Refresh" @click="table.loadData">刷新</el-button>
      <PermissionButton
        type="primary"
        :icon="Plus"
        :permission="BIDOPS_PERMISSIONS.MATCHING_RUN"
        @click="openStartDrawer"
      >
        启动匹配
      </PermissionButton>
    </template>

    <SearchForm @search="table.search" @reset="table.reset()">
      <el-form-item label="关键词">
        <el-input v-model="table.query.keyword" clearable placeholder="运行编号 / 条件" />
      </el-form-item>
      <el-form-item label="包件 ID">
        <el-input v-model="table.query.packageId" clearable placeholder="TenderPackageId" style="width: 180px" />
      </el-form-item>
      <el-form-item label="状态">
        <el-select v-model="table.query.status" clearable placeholder="全部" style="width: 140px">
          <el-option v-for="item in supplierMatchRunStatusOptions" :key="item.value" :label="item.label" :value="item.value" />
        </el-select>
      </el-form-item>
    </SearchForm>

    <DataTable :data="table.result.items" :loading="table.loading" empty-text="暂无匹配运行">
      <el-table-column label="运行编号" min-width="190" show-overflow-tooltip>
        <template #default="{ row }">
          <div class="main-cell">
            <strong>{{ row.runNo }}</strong>
            <span>包件 {{ row.packageId }}</span>
          </div>
        </template>
      </el-table-column>
      <el-table-column label="状态" width="120">
        <template #default="{ row }">
          <BidOpsStatusTag :value="row.status" />
        </template>
      </el-table-column>
      <el-table-column prop="criteriaSummary" label="匹配条件" min-width="260" show-overflow-tooltip />
      <el-table-column label="厂家数" width="90">
        <template #default="{ row }">{{ row.supplierCount }}</template>
      </el-table-column>
      <el-table-column label="候选" width="90">
        <template #default="{ row }">{{ row.matchedCount }}</template>
      </el-table-column>
      <el-table-column label="缺失材料" width="110">
        <template #default="{ row }">
          <el-tag :type="row.missingEvidenceCount > 0 ? 'warning' : 'success'" effect="light">
            {{ row.missingEvidenceCount }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="创建时间" width="170">
        <template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="完成时间" width="170">
        <template #default="{ row }">{{ formatDateTime(row.completedAtUtc) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="110" fixed="right">
        <template #default="{ row }">
          <el-button size="small" :icon="View" @click="router.push(`/bidops/matching/runs/${row.id}`)">详情</el-button>
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

    <FormDrawer
      v-model="startDrawerOpen"
      title="启动厂家匹配"
      width="560px"
      :submitting="startRequest.loading"
      @submit="submitStartRun"
    >
      <el-form :model="startForm" label-width="110px">
        <el-form-item label="包件 ID">
          <el-input v-model.trim="startForm.packageId" placeholder="TenderPackageId" />
        </el-form-item>
        <el-form-item label="厂家上限">
          <el-input-number v-model="startForm.maxSuppliers" :min="1" :max="500" />
        </el-form-item>
        <el-form-item label="人工条件">
          <el-input v-model="startForm.criteriaSummary" type="textarea" :rows="3" />
        </el-form-item>
      </el-form>
    </FormDrawer>
  </PageContainer>
</template>

<style scoped>
.main-cell {
  display: grid;
  gap: 4px;
  min-width: 0;
}

.main-cell strong {
  overflow: hidden;
  color: #17202a;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.main-cell span {
  color: #687385;
  font-size: 12px;
}

.table-pagination {
  justify-content: flex-end;
  margin-top: 14px;
}
</style>
