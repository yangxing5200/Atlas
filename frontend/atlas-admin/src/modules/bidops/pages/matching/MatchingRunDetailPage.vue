<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { ArrowLeft, Check, Close, Refresh, View } from '@element-plus/icons-vue'
import { matchingApi } from '@/api/bidops/matching.api'
import { packagesApi } from '@/api/bidops/packages.api'
import DataTable from '@/shared/components/DataTable.vue'
import FormDrawer from '@/shared/components/FormDrawer.vue'
import PageContainer from '@/shared/components/PageContainer.vue'
import { useRequest } from '@/shared/composables/useRequest'
import { formatDateTime } from '@/shared/utils/date'
import { formatMoney } from '@/shared/utils/money'
import BidOpsStatusTag from '../../components/BidOpsStatusTag.vue'
import PermissionButton from '../../components/PermissionButton.vue'
import RequirementTable from '../../components/RequirementTable.vue'
import { BIDOPS_PERMISSIONS } from '../../constants'
import type {
  BidOpsId,
  CreateGoNoGoDecisionRequest,
  GoNoGoDecisionDto,
  SupplierMatchResultDto,
  SupplierMatchRunDetailDto,
} from '../../types'
import {
  formatCategory,
  formatEvidenceType,
  formatGoNoGoDecision,
  formatMissingEvidenceStatus,
  formatNoticeType,
  formatPackageNo,
  formatSupplierName,
  formatSupplierMatchLevel,
  goNoGoDecisionOptions,
} from '../../utils/display'

type ProgressStatus = 'success' | 'warning' | 'exception' | undefined

const route = useRoute()
const router = useRouter()
const loading = ref(false)
const detail = ref<SupplierMatchRunDetailDto | null>(null)
const decisions = ref<GoNoGoDecisionDto[]>([])
const decisionDrawerOpen = ref(false)
const runId = computed(() => String(route.params.id || ''))
const run = computed(() => detail.value?.run || null)
const packageInfo = computed(() => detail.value?.package || null)
const requirements = computed(() => detail.value?.requirements || [])
const results = computed(() => detail.value?.results || [])
const pageTitle = computed(() => run.value?.runNo || '匹配详情')
const decisionRequest = useRequest()

const decisionForm = reactive<CreateGoNoGoDecisionRequest>({
  opportunityId: null,
  matchRunId: null,
  supplierMatchResultId: null,
  supplierId: null,
  decision: 'Hold',
  reason: '',
  riskSummary: '',
})

async function loadData() {
  loading.value = true
  try {
    const loaded = await matchingApi.getRun(runId.value)
    detail.value = loaded
    await loadDecisions(loaded.run.packageId)
  } catch {
    detail.value = null
    decisions.value = []
  } finally {
    loading.value = false
  }
}

async function loadDecisions(packageId: BidOpsId) {
  decisions.value = await packagesApi.decisions(packageId)
}

function scorePercentage(score: number) {
  return Math.max(0, Math.min(100, Math.round(score)))
}

function packageDisplayName() {
  const pkg = packageInfo.value
  if (!pkg) return run.value ? `包件 ${run.value.packageId}` : '包件'
  return pkg.packageName || formatPackageNo(pkg.packageNo)
}

function progressStatus(row: SupplierMatchResultDto): ProgressStatus {
  if (row.recommendation === 'Candidate') return 'success'
  if (row.recommendation === 'NotRecommended') return 'exception'
  if (row.recommendation === 'Caution') return 'warning'
  return undefined
}

function openDecision(row?: SupplierMatchResultDto) {
  const selectedRun = run.value
  if (!selectedRun) return

  Object.assign(decisionForm, {
    opportunityId: null,
    matchRunId: selectedRun.id,
    supplierMatchResultId: row?.id ?? null,
    supplierId: row?.supplierId ?? null,
    decision: row?.recommendation === 'Candidate' ? 'Go' : row?.recommendation === 'NotRecommended' ? 'NoGo' : 'Hold',
    reason: row?.explanation || '',
    riskSummary: row?.riskFlags || '',
  })
  decisionDrawerOpen.value = true
}

async function submitDecision() {
  const selectedPackageId = run.value?.packageId
  if (!selectedPackageId) return
  if (!decisionForm.decision) {
    ElMessage.warning('请选择决策')
    return
  }

  await decisionRequest.run(async () => {
    await packagesApi.createDecision(selectedPackageId, {
      ...decisionForm,
      reason: decisionForm.reason || null,
      riskSummary: decisionForm.riskSummary || null,
    })
    ElMessage.success('决策已登记')
    decisionDrawerOpen.value = false
    await loadDecisions(selectedPackageId)
  })
}

onMounted(loadData)
</script>

<template>
  <PageContainer :title="pageTitle" description="查看匹配运行、候选厂家、缺失材料和立项决策。">
    <template #actions>
      <el-button :icon="ArrowLeft" @click="router.push('/bidops/matching/runs')">返回</el-button>
      <el-button :icon="Refresh" :loading="loading" @click="loadData">刷新</el-button>
      <PermissionButton
        v-if="run"
        type="primary"
        :icon="Check"
        :permission="BIDOPS_PERMISSIONS.MATCHING_DECIDE"
        @click="openDecision()"
      >
        登记决策
      </PermissionButton>
    </template>

    <el-skeleton v-if="loading" :rows="10" animated />
    <el-empty v-else-if="!detail || !run" description="未找到匹配运行" />
    <template v-else>
      <section class="run-summary">
        <div>
          <h2>{{ run.runNo }}</h2>
          <p>{{ packageDisplayName() }}</p>
        </div>
        <div class="summary-tags">
          <BidOpsStatusTag :value="run.status" />
          <el-tag effect="light">厂家 {{ run.supplierCount }}</el-tag>
          <el-tag type="success" effect="light">候选 {{ run.matchedCount }}</el-tag>
          <el-tag :type="run.missingEvidenceCount > 0 ? 'warning' : 'success'" effect="light">
            缺失材料 {{ run.missingEvidenceCount }}
          </el-tag>
        </div>
      </section>

      <div class="split-grid">
        <section class="content-panel">
          <h2>运行信息</h2>
          <el-descriptions :column="2" border>
            <el-descriptions-item label="运行编号">{{ run.runNo }}</el-descriptions-item>
            <el-descriptions-item label="状态"><BidOpsStatusTag :value="run.status" /></el-descriptions-item>
            <el-descriptions-item label="发起人">{{ run.requestedByUserName || run.requestedByUserId }}</el-descriptions-item>
            <el-descriptions-item label="厂家上限">{{ run.maxSuppliers }}</el-descriptions-item>
            <el-descriptions-item label="后台任务">{{ run.backgroundJobId || '-' }}</el-descriptions-item>
            <el-descriptions-item label="匹配条件">{{ run.criteriaSummary || '-' }}</el-descriptions-item>
            <el-descriptions-item label="开始时间">{{ formatDateTime(run.startedAtUtc) }}</el-descriptions-item>
            <el-descriptions-item label="完成时间">{{ formatDateTime(run.completedAtUtc) }}</el-descriptions-item>
            <el-descriptions-item label="错误信息" :span="2">{{ run.errorMessage || '-' }}</el-descriptions-item>
          </el-descriptions>
        </section>

        <section class="content-panel">
          <h2>包件信息</h2>
          <el-empty v-if="!packageInfo" description="包件信息不可用" />
          <el-descriptions v-else :column="1" border>
            <el-descriptions-item label="包件号">{{ formatPackageNo(packageInfo.packageNo) }}</el-descriptions-item>
            <el-descriptions-item label="包件名称">{{ packageInfo.packageName || '-' }}</el-descriptions-item>
            <el-descriptions-item label="品类">{{ formatCategory(packageInfo.category) }}</el-descriptions-item>
            <el-descriptions-item label="预算金额">{{ formatMoney(packageInfo.budgetAmount) }}</el-descriptions-item>
            <el-descriptions-item label="最高限价">{{ formatMoney(packageInfo.maxPrice) }}</el-descriptions-item>
            <el-descriptions-item label="公告类型">{{ formatNoticeType(packageInfo.noticeType) }}</el-descriptions-item>
            <el-descriptions-item label="投标截止">{{ formatDateTime(packageInfo.bidDeadline) }}</el-descriptions-item>
          </el-descriptions>
          <div v-if="packageInfo" class="panel-actions">
            <el-button :icon="View" @click="router.push(`/bidops/packages/${packageInfo.id}`)">查看包件</el-button>
          </div>
        </section>
      </div>

      <section class="content-panel detail-section">
        <h2>候选厂家</h2>
        <DataTable :data="results" :loading="loading" empty-text="暂无匹配结果">
          <el-table-column type="expand" width="48">
            <template #default="{ row }">
              <div class="expanded-block">
                <p>{{ row.explanation || '-' }}</p>
                <el-empty v-if="row.missingEvidenceChecks.length === 0" description="暂无缺失材料" />
                <el-table v-else :data="row.missingEvidenceChecks" border stripe>
                  <el-table-column label="所需材料" min-width="150">
                    <template #default="{ row: evidence }">{{ formatEvidenceType(evidence.requiredEvidenceType) }}</template>
                  </el-table-column>
                  <el-table-column prop="requirementText" label="对应要求" min-width="320" show-overflow-tooltip />
                  <el-table-column label="状态" width="110">
                    <template #default="{ row: evidence }">{{ formatMissingEvidenceStatus(evidence.status) }}</template>
                  </el-table-column>
                  <el-table-column prop="explanation" label="说明" min-width="240" show-overflow-tooltip />
                </el-table>
              </div>
            </template>
          </el-table-column>
          <el-table-column label="排名" width="80">
            <template #default="{ row }">{{ row.rank }}</template>
          </el-table-column>
          <el-table-column label="厂家" min-width="190" show-overflow-tooltip>
            <template #default="{ row }">
              <el-button link type="primary" @click="router.push(`/bidops/suppliers/${row.supplierId}`)">
                {{ formatSupplierName(row.supplierNameSnapshot) }}
              </el-button>
            </template>
          </el-table-column>
          <el-table-column label="得分" min-width="170">
            <template #default="{ row }">
              <el-progress :percentage="scorePercentage(row.score)" :status="progressStatus(row)" />
            </template>
          </el-table-column>
          <el-table-column label="等级" width="100">
            <template #default="{ row }">{{ formatSupplierMatchLevel(row.matchLevel) }}</template>
          </el-table-column>
          <el-table-column label="建议" width="120">
            <template #default="{ row }">
              <BidOpsStatusTag :value="row.recommendation" />
            </template>
          </el-table-column>
          <el-table-column label="能力" width="90">
            <template #default="{ row }">
              <el-icon :class="row.categoryMatched ? 'ok-icon' : 'bad-icon'">
                <Check v-if="row.categoryMatched" />
                <Close v-else />
              </el-icon>
            </template>
          </el-table-column>
          <el-table-column label="区域" width="90">
            <template #default="{ row }">
              <el-icon :class="row.regionMatched ? 'ok-icon' : 'bad-icon'">
                <Check v-if="row.regionMatched" />
                <Close v-else />
              </el-icon>
            </template>
          </el-table-column>
          <el-table-column label="材料" width="120">
            <template #default="{ row }">{{ row.evidenceMatchedCount }} / {{ row.missingEvidenceCount }}</template>
          </el-table-column>
          <el-table-column prop="riskFlags" label="风险标记" min-width="160" show-overflow-tooltip />
          <el-table-column label="操作" width="120" fixed="right">
            <template #default="{ row }">
              <PermissionButton
                size="small"
                type="primary"
                :permission="BIDOPS_PERMISSIONS.MATCHING_DECIDE"
                @click="openDecision(row)"
              >
                决策
              </PermissionButton>
            </template>
          </el-table-column>
        </DataTable>
      </section>

      <section class="content-panel detail-section">
        <h2>立项决策</h2>
        <DataTable :data="decisions" empty-text="暂无决策记录">
          <el-table-column label="决策" width="100">
            <template #default="{ row }">{{ formatGoNoGoDecision(row.decision) }}</template>
          </el-table-column>
          <el-table-column prop="reason" label="理由" min-width="260" show-overflow-tooltip />
          <el-table-column prop="riskSummary" label="风险摘要" min-width="220" show-overflow-tooltip />
          <el-table-column label="关联厂家" min-width="160">
            <template #default="{ row }">{{ row.supplierId || '-' }}</template>
          </el-table-column>
          <el-table-column label="登记人" min-width="140">
            <template #default="{ row }">{{ row.decidedByUserName || row.decidedByUserId }}</template>
          </el-table-column>
          <el-table-column label="登记时间" width="170">
            <template #default="{ row }">{{ formatDateTime(row.decidedAtUtc) }}</template>
          </el-table-column>
        </DataTable>
      </section>

      <section class="content-panel detail-section">
        <h2>包件要求</h2>
        <RequirementTable :requirements="requirements" :loading="loading" />
      </section>
    </template>

    <FormDrawer
      v-model="decisionDrawerOpen"
      title="登记立项决策"
      width="620px"
      :submitting="decisionRequest.loading"
      @submit="submitDecision"
    >
      <el-form :model="decisionForm" label-width="110px">
        <el-form-item label="决策">
          <el-select v-model="decisionForm.decision" placeholder="请选择">
            <el-option v-for="item in goNoGoDecisionOptions" :key="item.value" :label="item.label" :value="item.value" />
          </el-select>
        </el-form-item>
        <el-form-item label="匹配结果">
          <el-input :model-value="decisionForm.supplierMatchResultId || '-'" disabled />
        </el-form-item>
        <el-form-item label="厂家">
          <el-input :model-value="decisionForm.supplierId || '-'" disabled />
        </el-form-item>
        <el-form-item label="理由">
          <el-input v-model="decisionForm.reason" type="textarea" :rows="4" />
        </el-form-item>
        <el-form-item label="风险摘要">
          <el-input v-model="decisionForm.riskSummary" type="textarea" :rows="3" />
        </el-form-item>
      </el-form>
    </FormDrawer>
  </PageContainer>
</template>

<style scoped>
.run-summary {
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

.run-summary h2,
.content-panel h2 {
  margin: 0;
  color: #17202a;
}

.run-summary h2 {
  font-size: 18px;
  line-height: 1.45;
}

.run-summary p {
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

.panel-actions {
  display: flex;
  justify-content: flex-end;
  margin-top: 12px;
}

.detail-section {
  margin-top: 16px;
}

.expanded-block {
  display: grid;
  gap: 12px;
  padding: 8px 16px 14px;
}

.expanded-block p {
  margin: 0;
  color: #3d4a5c;
  line-height: 1.55;
}

.ok-icon {
  color: #1f9d55;
}

.bad-icon {
  color: #d93025;
}

@media (max-width: 980px) {
  .run-summary {
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
