<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { Plus, Refresh, View } from '@element-plus/icons-vue'
import { pursuitsApi } from '@/api/bidops/pursuits.api'
import DataTable from '@/shared/components/DataTable.vue'
import FormDrawer from '@/shared/components/FormDrawer.vue'
import PageContainer from '@/shared/components/PageContainer.vue'
import SearchForm from '@/shared/components/SearchForm.vue'
import { useRequest } from '@/shared/composables/useRequest'
import { useTableQuery } from '@/shared/composables/useTableQuery'
import { formatDateTime } from '@/shared/utils/date'
import { formatMoney } from '@/shared/utils/money'
import BidOpsStatusTag from '../../components/BidOpsStatusTag.vue'
import PermissionButton from '../../components/PermissionButton.vue'
import RiskLevelTag from '../../components/RiskLevelTag.vue'
import { BIDOPS_PERMISSIONS } from '../../constants'
import type { CreatePursuitRequest, PursuitDto } from '../../types'
import {
  formatPackageNo,
  formatSupplierName,
  pursuitStageOptions,
  pursuitStatusOptions,
} from '../../utils/display'

const props = withDefaults(defineProps<{ mineOnly?: boolean }>(), {
  mineOnly: false,
})

interface PursuitListQuery {
  keyword: string
  stage?: string
  status?: string
  overdueOnly: boolean
  pageIndex: number
  pageSize: number
}

const router = useRouter()
const createDrawerOpen = ref(false)
const createRequest = useRequest()
const pageTitle = computed(() => (props.mineOnly ? '我的任务' : '作业列表'))
const pageDescription = computed(() => (props.mineOnly ? '查看分配给我的投标作业。' : '跟踪包件投标作业、任务和跟进记录。'))

const table = useTableQuery<PursuitDto, PursuitListQuery>(
  (params) =>
    pursuitsApi.search({
      keyword: params.keyword || undefined,
      stage: params.stage || undefined,
      status: params.status || undefined,
      mineOnly: props.mineOnly || undefined,
      overdueOnly: params.overdueOnly || undefined,
      pageIndex: params.pageIndex,
      pageSize: params.pageSize,
    }),
  {
    keyword: '',
    stage: undefined,
    status: undefined,
    overdueOnly: false,
    pageIndex: 1,
    pageSize: 20,
  },
)

const createForm = reactive<CreatePursuitRequest>({
  packageId: '',
  opportunityId: null,
  goNoGoDecisionId: null,
  supplierId: null,
  supplierNameSnapshot: '',
  title: '',
  priority: 3,
  estimatedAmount: null,
  bidDeadlineAtUtc: null,
  ownerUserId: null,
  remark: '',
})

function openCreate() {
  Object.assign(createForm, {
    packageId: '',
    opportunityId: null,
    goNoGoDecisionId: null,
    supplierId: null,
    supplierNameSnapshot: '',
    title: '',
    priority: 3,
    estimatedAmount: null,
    bidDeadlineAtUtc: null,
    ownerUserId: null,
    remark: '',
  })
  createDrawerOpen.value = true
}

async function submitCreate() {
  if (!String(createForm.packageId || '').trim()) {
    ElMessage.warning('请输入包件 ID')
    return
  }

  await createRequest.run(async () => {
    const created = await pursuitsApi.create({
      ...createForm,
      opportunityId: emptyToNull(createForm.opportunityId),
      goNoGoDecisionId: emptyToNull(createForm.goNoGoDecisionId),
      supplierId: emptyToNull(createForm.supplierId),
      supplierNameSnapshot: emptyToNull(createForm.supplierNameSnapshot),
      title: emptyToNull(createForm.title),
      ownerUserId: emptyToNull(createForm.ownerUserId),
      remark: emptyToNull(createForm.remark),
    })
    ElMessage.success('投标作业已创建')
    createDrawerOpen.value = false
    await router.push(`/bidops/pursuits/${created.id}`)
  })
}

function emptyToNull<T>(value: T | null | undefined): T | null {
  if (value === null || value === undefined) return null
  const text = String(value).trim()
  return text ? value : null
}
</script>

<template>
  <PageContainer :title="pageTitle" :description="pageDescription">
    <template #actions>
      <el-button :icon="Refresh" @click="table.loadData">刷新</el-button>
      <PermissionButton
        type="primary"
        :icon="Plus"
        :permission="BIDOPS_PERMISSIONS.PURSUIT_MANAGE"
        @click="openCreate"
      >
        新建作业
      </PermissionButton>
    </template>

    <SearchForm @search="table.search" @reset="table.reset()">
      <el-form-item label="关键词">
        <el-input v-model="table.query.keyword" clearable placeholder="作业 / 包件 / 厂家" />
      </el-form-item>
      <el-form-item label="阶段">
        <el-select v-model="table.query.stage" clearable placeholder="全部" style="width: 150px">
          <el-option v-for="item in pursuitStageOptions" :key="item.value" :label="item.label" :value="item.value" />
        </el-select>
      </el-form-item>
      <el-form-item label="状态">
        <el-select v-model="table.query.status" clearable placeholder="全部" style="width: 140px">
          <el-option v-for="item in pursuitStatusOptions" :key="item.value" :label="item.label" :value="item.value" />
        </el-select>
      </el-form-item>
      <el-form-item label="只看逾期">
        <el-switch v-model="table.query.overdueOnly" />
      </el-form-item>
    </SearchForm>

    <DataTable :data="table.result.items" :loading="table.loading" empty-text="暂无投标作业">
      <el-table-column label="作业" min-width="280" show-overflow-tooltip>
        <template #default="{ row }">
          <div class="main-cell">
            <strong>{{ row.title || row.pursuitNo }}</strong>
            <span>{{ row.pursuitNo }}</span>
          </div>
        </template>
      </el-table-column>
      <el-table-column label="包件" min-width="230" show-overflow-tooltip>
        <template #default="{ row }">
          {{ formatPackageNo(row.packageNo) }} {{ row.packageName || '' }}
        </template>
      </el-table-column>
      <el-table-column prop="projectCode" label="项目编码" width="150" show-overflow-tooltip />
      <el-table-column prop="buyerName" label="采购人" min-width="170" show-overflow-tooltip />
      <el-table-column label="厂家" min-width="150" show-overflow-tooltip>
        <template #default="{ row }">{{ formatSupplierName(row.supplierNameSnapshot) }}</template>
      </el-table-column>
      <el-table-column label="阶段" width="120">
        <template #default="{ row }"><BidOpsStatusTag :value="row.stage" /></template>
      </el-table-column>
      <el-table-column label="状态" width="105">
        <template #default="{ row }"><BidOpsStatusTag :value="row.status" /></template>
      </el-table-column>
      <el-table-column label="进度" width="130">
        <template #default="{ row }">
          <el-progress :percentage="row.progressPercent" :stroke-width="8" />
        </template>
      </el-table-column>
      <el-table-column label="任务" width="120">
        <template #default="{ row }">
          <el-tag :type="row.overdueTaskCount > 0 ? 'danger' : row.openTaskCount > 0 ? 'warning' : 'success'" effect="light">
            {{ row.openTaskCount }}/{{ row.taskCount }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="风险" width="90">
        <template #default="{ row }"><RiskLevelTag :value="row.riskLevel" /></template>
      </el-table-column>
      <el-table-column label="投标截止" width="170">
        <template #default="{ row }">{{ formatDateTime(row.bidDeadlineAtUtc) }}</template>
      </el-table-column>
      <el-table-column label="金额" width="140">
        <template #default="{ row }">{{ formatMoney(row.estimatedAmount) }}</template>
      </el-table-column>
      <el-table-column label="更新" width="170">
        <template #default="{ row }">{{ formatDateTime(row.updatedAt || row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="110" fixed="right">
        <template #default="{ row }">
          <el-button size="small" :icon="View" @click="router.push(`/bidops/pursuits/${row.id}`)">详情</el-button>
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
      v-model="createDrawerOpen"
      title="新建投标作业"
      width="680px"
      :submitting="createRequest.loading"
      @submit="submitCreate"
    >
      <el-form :model="createForm" label-width="130px" class="form-grid">
        <el-form-item label="包件 ID" class="full-row">
          <el-input v-model.trim="createForm.packageId" placeholder="TenderPackageId" />
        </el-form-item>
        <el-form-item label="商机 ID">
          <el-input v-model.trim="createForm.opportunityId" />
        </el-form-item>
        <el-form-item label="立项决策 ID">
          <el-input v-model.trim="createForm.goNoGoDecisionId" />
        </el-form-item>
        <el-form-item label="厂家 ID">
          <el-input v-model.trim="createForm.supplierId" />
        </el-form-item>
        <el-form-item label="厂家名称">
          <el-input v-model.trim="createForm.supplierNameSnapshot" />
        </el-form-item>
        <el-form-item label="作业标题" class="full-row">
          <el-input v-model.trim="createForm.title" placeholder="留空则使用商机或包件名称" />
        </el-form-item>
        <el-form-item label="优先级">
          <el-input-number v-model="createForm.priority" :min="1" :max="5" />
        </el-form-item>
        <el-form-item label="预估金额">
          <el-input-number v-model="createForm.estimatedAmount" :min="0" :precision="2" />
        </el-form-item>
        <el-form-item label="负责人 ID">
          <el-input v-model.trim="createForm.ownerUserId" />
        </el-form-item>
        <el-form-item label="投标截止">
          <el-date-picker v-model="createForm.bidDeadlineAtUtc" type="datetime" value-format="YYYY-MM-DDTHH:mm:ss" />
        </el-form-item>
        <el-form-item label="备注" class="full-row">
          <el-input v-model="createForm.remark" type="textarea" :rows="3" />
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

.form-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0 12px;
}

.full-row {
  grid-column: 1 / -1;
}

@media (max-width: 860px) {
  .form-grid {
    grid-template-columns: 1fr;
  }
}
</style>
