<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { Plus, Star, StarFilled, View } from '@element-plus/icons-vue'
import { opportunitiesApi } from '@/api/bidops/opportunities.api'
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
import { BIDOPS_PERMISSIONS } from '../../constants'
import type { CreateOpportunityRequest, OpportunityDto } from '../../types'
import {
  formatOpportunityDecision,
  formatOpportunityValueLevel,
  opportunityStageOptions,
  opportunityStatusOptions,
} from '../../utils/display'

const props = withDefaults(defineProps<{ watchedOnly?: boolean }>(), {
  watchedOnly: false,
})

interface OpportunityListQuery {
  keyword: string
  stage?: string
  status?: string
  watchedByMe?: boolean
  pageIndex: number
  pageSize: number
}

const route = useRoute()
const router = useRouter()
const routeWatchedOnly = computed(() => props.watchedOnly || String(route.query.watchedByMe || '') === 'true')
const pageTitle = computed(() => (routeWatchedOnly.value ? '关注商机' : '商机列表'))
const pageDescription = computed(() => (routeWatchedOnly.value ? '查看已关注的包件商机。' : '跟进包件级商机、阶段和价值评估。'))

const table = useTableQuery<OpportunityDto, OpportunityListQuery>(
  (params) =>
    opportunitiesApi.search({
      ...params,
      stage: params.stage || undefined,
      status: params.status || undefined,
      watchedByMe: params.watchedByMe || undefined,
    }),
  {
    keyword: '',
    stage: undefined,
    status: undefined,
    watchedByMe: routeWatchedOnly.value ? true : undefined,
    pageIndex: 1,
    pageSize: 20,
  },
)

const createDrawerOpen = ref(false)
const createRequest = useRequest()
const watchRequest = useRequest()
const createForm = reactive<CreateOpportunityRequest>({
  packageId: '',
  title: '',
  priority: 3,
  estimatedAmount: null,
  ownerUserId: null,
  nextActionAtUtc: null,
  remark: '',
})

function openCreate() {
  Object.assign(createForm, {
    packageId: '',
    title: '',
    priority: 3,
    estimatedAmount: null,
    ownerUserId: null,
    nextActionAtUtc: null,
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
    const created = await opportunitiesApi.create({
      ...createForm,
      title: createForm.title || null,
      remark: createForm.remark || null,
    })
    ElMessage.success('商机已创建')
    createDrawerOpen.value = false
    await router.push(`/bidops/opportunities/${created.id}`)
  })
}

async function toggleWatch(row: OpportunityDto) {
  await watchRequest.run(async () => {
    await opportunitiesApi.watch(row.id, { enabled: !row.watchedByMe })
    ElMessage.success(row.watchedByMe ? '已取消关注' : '已关注')
    await table.loadData()
  })
}
</script>

<template>
  <PageContainer :title="pageTitle" :description="pageDescription">
    <template #actions>
      <PermissionButton type="primary" :icon="Plus" :permission="BIDOPS_PERMISSIONS.OPPORTUNITY_MANAGE" @click="openCreate">
        新建商机
      </PermissionButton>
    </template>

    <SearchForm @search="table.search" @reset="table.reset({ watchedByMe: routeWatchedOnly ? true : undefined })">
      <el-form-item label="关键词">
        <el-input v-model="table.query.keyword" clearable placeholder="商机 / 包件 / 项目" />
      </el-form-item>
      <el-form-item label="阶段">
        <el-select v-model="table.query.stage" clearable placeholder="全部" style="width: 150px">
          <el-option v-for="item in opportunityStageOptions" :key="item.value" :label="item.label" :value="item.value" />
        </el-select>
      </el-form-item>
      <el-form-item label="状态">
        <el-select v-model="table.query.status" clearable placeholder="全部" style="width: 150px">
          <el-option v-for="item in opportunityStatusOptions" :key="item.value" :label="item.label" :value="item.value" />
        </el-select>
      </el-form-item>
      <el-form-item label="只看关注">
        <el-switch v-model="table.query.watchedByMe" />
      </el-form-item>
    </SearchForm>

    <DataTable :data="table.result.items" :loading="table.loading" empty-text="暂无商机">
      <el-table-column label="商机" min-width="280" show-overflow-tooltip>
        <template #default="{ row }">
          <div class="main-cell">
            <strong>{{ row.title || row.opportunityNo }}</strong>
            <span>{{ row.opportunityNo }}</span>
          </div>
        </template>
      </el-table-column>
      <el-table-column label="包件" min-width="220" show-overflow-tooltip>
        <template #default="{ row }">
          {{ row.packageNo || '-' }} {{ row.packageName || '' }}
        </template>
      </el-table-column>
      <el-table-column prop="projectCode" label="项目编码" width="150" show-overflow-tooltip />
      <el-table-column prop="buyerName" label="采购人" min-width="180" show-overflow-tooltip />
      <el-table-column prop="region" label="地区" width="110" />
      <el-table-column label="投标截止" width="170">
        <template #default="{ row }">{{ formatDateTime(row.bidDeadline) }}</template>
      </el-table-column>
      <el-table-column label="阶段" width="120">
        <template #default="{ row }"><BidOpsStatusTag :value="row.stage" /></template>
      </el-table-column>
      <el-table-column label="决策" width="110">
        <template #default="{ row }">{{ formatOpportunityDecision(row.decision) }}</template>
      </el-table-column>
      <el-table-column label="价值" width="160">
        <template #default="{ row }">
          <div class="value-cell">
            <span>{{ row.valueScore ?? '-' }}</span>
            <el-tag effect="light">{{ formatOpportunityValueLevel(row.valueLevel) }}</el-tag>
          </div>
        </template>
      </el-table-column>
      <el-table-column label="预估金额" width="150">
        <template #default="{ row }">{{ formatMoney(row.estimatedAmount) }}</template>
      </el-table-column>
      <el-table-column label="关注" width="100">
        <template #default="{ row }">
          <el-button
            link
            :type="row.watchedByMe ? 'warning' : 'default'"
            :icon="row.watchedByMe ? StarFilled : Star"
            @click="toggleWatch(row)"
          >
            {{ row.watchCount }}
          </el-button>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="120" fixed="right">
        <template #default="{ row }">
          <el-button size="small" :icon="View" @click="router.push(`/bidops/opportunities/${row.id}`)">详情</el-button>
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
      title="新建商机"
      :submitting="createRequest.loading"
      @submit="submitCreate"
    >
      <el-form :model="createForm" label-width="120px" class="form-grid">
        <el-form-item label="包件 ID" class="full-row">
          <el-input v-model.trim="createForm.packageId" placeholder="TenderPackageId" />
        </el-form-item>
        <el-form-item label="商机标题" class="full-row">
          <el-input v-model.trim="createForm.title" placeholder="留空则使用包件名称" />
        </el-form-item>
        <el-form-item label="优先级">
          <el-input-number v-model="createForm.priority" :min="1" :max="5" />
        </el-form-item>
        <el-form-item label="预估金额">
          <el-input-number v-model="createForm.estimatedAmount" :min="0" :precision="2" />
        </el-form-item>
        <el-form-item label="下次跟进" class="full-row">
          <el-date-picker v-model="createForm.nextActionAtUtc" type="datetime" value-format="YYYY-MM-DDTHH:mm:ss" />
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

.value-cell {
  display: flex;
  align-items: center;
  gap: 8px;
}

.table-pagination {
  justify-content: flex-end;
  margin-top: 14px;
}
</style>
