<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { DataAnalysis, Plus } from '@element-plus/icons-vue'
import { opportunitiesApi } from '@/api/bidops/opportunities.api'
import { packagesApi } from '@/api/bidops/packages.api'
import { pursuitsApi } from '@/api/bidops/pursuits.api'
import PageContainer from '@/shared/components/PageContainer.vue'
import { useRequest } from '@/shared/composables/useRequest'
import { formatDateTime } from '@/shared/utils/date'
import { formatMoney } from '@/shared/utils/money'
import BidOpsStatusTag from '../../components/BidOpsStatusTag.vue'
import PermissionButton from '../../components/PermissionButton.vue'
import RequirementTable from '../../components/RequirementTable.vue'
import { BIDOPS_PERMISSIONS } from '../../constants'
import type {
  PackageHistoricalSupplierLeadDto,
  PackageTimelineItemDto,
  RequirementItemDto,
  TenderPackageDto,
} from '../../types'
import { formatCategory, formatCommonStatus, formatNoticeType, formatPackageNo } from '../../utils/display'

const route = useRoute()
const router = useRouter()
const loading = ref(false)
const createOpportunityRequest = useRequest()
const createPursuitRequest = useRequest()
const matchRequest = useRequest()
const packageInfo = ref<TenderPackageDto | null>(null)
const timeline = ref<PackageTimelineItemDto[]>([])
const requirements = ref<RequirementItemDto[]>([])
const historicalSuppliers = ref<PackageHistoricalSupplierLeadDto[]>([])
const packageId = computed(() => String(route.params.id || ''))
const pageTitle = computed(() => (packageInfo.value ? packageInfo.value.packageName || formatPackageNo(packageInfo.value.packageNo) : '包件详情'))
const rejectRiskCount = computed(() => requirements.value.filter((item) => item.isRejectRisk).length)
const mandatoryCount = computed(() => requirements.value.filter((item) => item.isMandatory).length)

async function loadData() {
  loading.value = true
  try {
    const id = packageId.value
    const [pkg, packageTimeline, packageRequirements, supplierLeads] = await Promise.all([
      packagesApi.get(id),
      packagesApi.timeline(id),
      packagesApi.requirements(id),
      packagesApi.historicalSuppliers(id),
    ])
    packageInfo.value = pkg
    timeline.value = packageTimeline
    requirements.value = packageRequirements
    historicalSuppliers.value = supplierLeads
  } catch {
    packageInfo.value = null
    timeline.value = []
    requirements.value = []
    historicalSuppliers.value = []
  } finally {
    loading.value = false
  }
}

onMounted(loadData)

async function createOpportunity() {
  if (!packageInfo.value) return

  await createOpportunityRequest.run(async () => {
    const created = await opportunitiesApi.create({
      packageId: packageInfo.value!.id,
      title: packageInfo.value!.packageName || packageInfo.value!.packageNo,
      priority: 3,
      estimatedAmount: packageInfo.value!.budgetAmount ?? packageInfo.value!.maxPrice ?? null,
      remark: '',
    })
    ElMessage.success('商机已创建')
    await router.push(`/bidops/opportunities/${created.id}`)
  })
}

async function createPursuit() {
  if (!packageInfo.value) return

  await createPursuitRequest.run(async () => {
    const created = await pursuitsApi.create({
      packageId: packageInfo.value!.id,
      title: packageInfo.value!.packageName || packageInfo.value!.packageNo,
      priority: 3,
      estimatedAmount: packageInfo.value!.budgetAmount ?? packageInfo.value!.maxPrice ?? null,
      remark: '',
    })
    ElMessage.success('投标作业已创建')
    await router.push(`/bidops/pursuits/${created.id}`)
  })
}

async function startSupplierMatch() {
  if (!packageInfo.value) return

  await matchRequest.run(async () => {
    const response = await packagesApi.matchSuppliers(packageInfo.value!.id, {
      maxSuppliers: 100,
      criteriaSummary: '按包件品类、地区、资质材料与风险要求自动匹配',
    })
    ElMessage.success('厂家匹配任务已入队')
    await router.push(`/bidops/matching/runs/${response.run.id}`)
  })
}

function formatRank(value?: number | null) {
  return value ? `第 ${value} 名` : '-'
}

function formatScore(value?: number | null) {
  if (value === null || value === undefined) return '-'
  return `${Math.round(value * 100)}%`
}
</script>

<template>
  <PageContainer :title="pageTitle" description="查看包件基础信息、关联公告、时间线和要求项。">
    <template #actions>
      <PermissionButton
        :icon="DataAnalysis"
        :loading="matchRequest.loading"
        :permission="BIDOPS_PERMISSIONS.MATCHING_RUN"
        @click="startSupplierMatch"
      >
        厂家匹配
      </PermissionButton>
      <PermissionButton
        :icon="Plus"
        :loading="createPursuitRequest.loading"
        :permission="BIDOPS_PERMISSIONS.PURSUIT_MANAGE"
        @click="createPursuit"
      >
        创建作业
      </PermissionButton>
      <PermissionButton
        type="primary"
        :icon="Plus"
        :loading="createOpportunityRequest.loading"
        :permission="BIDOPS_PERMISSIONS.OPPORTUNITY_MANAGE"
        @click="createOpportunity"
      >
        创建商机
      </PermissionButton>
    </template>
    <el-skeleton v-if="loading" :rows="10" animated />
    <el-empty v-else-if="!packageInfo" description="未找到包件" />
    <template v-else>
      <section class="package-summary">
        <div>
          <h2>{{ packageInfo.packageName || formatPackageNo(packageInfo.packageNo) }}</h2>
          <p>{{ packageInfo.projectName || packageInfo.noticeTitle || '-' }}</p>
        </div>
        <div class="summary-tags">
          <BidOpsStatusTag :value="packageInfo.status" />
          <el-tag effect="light">{{ formatCategory(packageInfo.category) }}</el-tag>
          <el-tag v-if="packageInfo.noticeType" effect="light">{{ formatNoticeType(packageInfo.noticeType) }}</el-tag>
          <el-tag v-if="rejectRiskCount > 0" type="danger" effect="light">{{ rejectRiskCount }} 条废标风险</el-tag>
        </div>
      </section>

      <div class="split-grid">
        <section class="content-panel">
          <h2>包件基础信息</h2>
          <el-descriptions :column="2" border>
            <el-descriptions-item label="包件号">{{ formatPackageNo(packageInfo.packageNo) }}</el-descriptions-item>
            <el-descriptions-item label="包件名称">{{ packageInfo.packageName || '-' }}</el-descriptions-item>
            <el-descriptions-item label="标段号">{{ packageInfo.lotNo || '-' }}</el-descriptions-item>
            <el-descriptions-item label="标段名称">{{ packageInfo.lotName || '-' }}</el-descriptions-item>
            <el-descriptions-item label="品类">{{ formatCategory(packageInfo.category) }}</el-descriptions-item>
            <el-descriptions-item label="状态"><BidOpsStatusTag :value="packageInfo.status" /></el-descriptions-item>
            <el-descriptions-item label="数量">{{ packageInfo.quantity ?? '-' }}</el-descriptions-item>
            <el-descriptions-item label="单位">{{ packageInfo.unit || '-' }}</el-descriptions-item>
            <el-descriptions-item label="预算金额">{{ formatMoney(packageInfo.budgetAmount) }}</el-descriptions-item>
            <el-descriptions-item label="最高限价">{{ formatMoney(packageInfo.maxPrice) }}</el-descriptions-item>
            <el-descriptions-item label="交付地点">{{ packageInfo.deliveryPlace || '-' }}</el-descriptions-item>
            <el-descriptions-item label="交付周期">{{ packageInfo.deliveryPeriod || '-' }}</el-descriptions-item>
            <el-descriptions-item label="要求项">{{ packageInfo.requirementCount ?? requirements.length }}</el-descriptions-item>
            <el-descriptions-item label="强制 / 风险">{{ mandatoryCount }} / {{ rejectRiskCount }}</el-descriptions-item>
            <el-descriptions-item label="创建时间">{{ formatDateTime(packageInfo.createdAt) }}</el-descriptions-item>
            <el-descriptions-item label="更新时间">{{ formatDateTime(packageInfo.updatedAt) }}</el-descriptions-item>
          </el-descriptions>
        </section>

        <section class="content-panel">
          <h2>关联公告</h2>
          <el-descriptions :column="1" border>
            <el-descriptions-item label="公告标题">{{ packageInfo.noticeTitle || '-' }}</el-descriptions-item>
            <el-descriptions-item label="项目名称">{{ packageInfo.projectName || '-' }}</el-descriptions-item>
            <el-descriptions-item label="项目编码">{{ packageInfo.projectCode || '-' }}</el-descriptions-item>
            <el-descriptions-item label="采购人">{{ packageInfo.buyerName || '-' }}</el-descriptions-item>
            <el-descriptions-item label="地区">{{ packageInfo.region || '-' }}</el-descriptions-item>
            <el-descriptions-item label="公告类型">{{ formatNoticeType(packageInfo.noticeType) }}</el-descriptions-item>
            <el-descriptions-item label="发布时间">{{ formatDateTime(packageInfo.publishTime) }}</el-descriptions-item>
            <el-descriptions-item label="投标截止">{{ formatDateTime(packageInfo.bidDeadline) }}</el-descriptions-item>
          </el-descriptions>
        </section>
      </div>

      <div class="content-panel detail-section">
        <h2>包件时间线</h2>
        <el-empty v-if="timeline.length === 0" description="暂无时间线" />
        <el-timeline v-else>
          <el-timeline-item
            v-for="item in timeline"
            :key="`${item.eventType}-${item.occurredAt}`"
            :timestamp="formatDateTime(item.occurredAt)"
            placement="top"
          >
            <div class="timeline-item">
              <strong>{{ item.title }}</strong>
              <span>{{ formatCommonStatus(item.status) }}</span>
              <p>{{ item.description || '-' }}</p>
            </div>
          </el-timeline-item>
        </el-timeline>
      </div>

      <div class="content-panel detail-section">
        <h2>历史中标厂家线索</h2>
        <el-table :data="historicalSuppliers" empty-text="暂无历史厂家线索" border>
          <el-table-column label="厂家" min-width="220" fixed show-overflow-tooltip>
            <template #default="{ row }">
              <el-button
                v-if="row.supplierId"
                link
                type="primary"
                @click="router.push(`/bidops/suppliers/${row.supplierId}`)"
              >
                {{ row.supplierName }}
              </el-button>
              <span v-else>{{ row.supplierName }}</span>
            </template>
          </el-table-column>
          <el-table-column label="结果" width="100">
            <template #default="{ row }">{{ formatCommonStatus(row.outcomeType) }}</template>
          </el-table-column>
          <el-table-column label="名次" width="90">
            <template #default="{ row }">{{ formatRank(row.rank) }}</template>
          </el-table-column>
          <el-table-column label="历史包件" min-width="220" show-overflow-tooltip>
            <template #default="{ row }">{{ row.packageName || formatPackageNo(row.packageNo) }}</template>
          </el-table-column>
          <el-table-column label="品类" width="120" show-overflow-tooltip>
            <template #default="{ row }">{{ formatCategory(row.category) }}</template>
          </el-table-column>
          <el-table-column label="匹配依据" min-width="180" show-overflow-tooltip>
            <template #default="{ row }">{{ row.matchReason }}</template>
          </el-table-column>
          <el-table-column label="匹配度" width="90">
            <template #default="{ row }">{{ formatScore(row.matchScore) }}</template>
          </el-table-column>
          <el-table-column label="金额" width="130">
            <template #default="{ row }">{{ formatMoney(row.awardAmount) }}</template>
          </el-table-column>
          <el-table-column label="代理服务费" width="130">
            <template #default="{ row }">{{ formatMoney(row.procurementAgencyServiceFeeAmount) }}</template>
          </el-table-column>
          <el-table-column label="发布时间" width="170">
            <template #default="{ row }">{{ formatDateTime(row.publishTime) }}</template>
          </el-table-column>
          <el-table-column label="原始公告" width="100" fixed="right">
            <template #default="{ row }">
              <el-link v-if="row.sourceUrl" :href="row.sourceUrl" target="_blank" type="primary">查看</el-link>
              <span v-else>-</span>
            </template>
          </el-table-column>
        </el-table>
      </div>

      <div class="content-panel detail-section">
        <h2>要求项</h2>
        <RequirementTable :requirements="requirements" :loading="loading" />
      </div>
    </template>
  </PageContainer>
</template>

<style scoped>
.package-summary {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  padding: 16px;
  margin-bottom: 16px;
  border: 1px solid #dce3ee;
  border-radius: 8px;
  background: #fff;
}

.package-summary h2,
.content-panel h2 {
  margin: 0;
  color: #17202a;
}

.package-summary h2 {
  font-size: 18px;
  line-height: 1.45;
}

.package-summary p {
  margin: 6px 0 0;
  color: #687385;
  line-height: 1.5;
}

.summary-tags {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 8px;
}

.split-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 16px;
  align-items: start;
}

.requirements-panel {
  margin-top: 16px;
}

.detail-section {
  margin-top: 16px;
}

.timeline-item {
  display: grid;
  gap: 4px;
}

.timeline-item strong {
  color: #17202a;
}

.timeline-item span {
  color: #687385;
  font-size: 12px;
}

.timeline-item p {
  margin: 0;
  color: #3d4a5c;
  line-height: 1.55;
}

@media (max-width: 980px) {
  .package-summary {
    display: grid;
  }

  .summary-tags {
    justify-content: flex-start;
  }

  .split-grid {
    grid-template-columns: 1fr;
  }
}
</style>
