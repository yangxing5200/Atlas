<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { ArrowLeft, Edit, Star, StarFilled } from '@element-plus/icons-vue'
import { opportunitiesApi } from '@/api/bidops/opportunities.api'
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
  AssessOpportunityRequest,
  ChangeOpportunityStageRequest,
  OpportunityDetailDto,
  UpdateOpportunityRequest,
} from '../../types'
import {
  formatCategory,
  formatNoticeType,
  formatOpportunityDecision,
  formatOpportunityStage,
  formatOpportunityValueLevel,
  opportunityDecisionOptions,
  opportunityStageOptions,
  opportunityStatusOptions,
  opportunityValueLevelOptions,
} from '../../utils/display'

const route = useRoute()
const router = useRouter()
const detail = ref<OpportunityDetailDto | null>(null)
const loading = ref(false)
const editDrawerOpen = ref(false)
const opportunityId = computed(() => String(route.params.id || ''))
const opportunity = computed(() => detail.value?.opportunity || null)
const packageInfo = computed(() => detail.value?.package || null)
const requirements = computed(() => detail.value?.requirements || [])
const stageHistory = computed(() => detail.value?.stageHistory || [])
const pageTitle = computed(() => opportunity.value?.title || '商机详情')
const rejectRiskCount = computed(() => requirements.value.filter((item) => item.isRejectRisk).length)

const editRequest = useRequest()
const assessRequest = useRequest()
const stageRequest = useRequest()
const watchRequest = useRequest()

const editForm = reactive<UpdateOpportunityRequest>({
  title: '',
  priority: 3,
  estimatedAmount: null,
  ownerUserId: null,
  nextActionAtUtc: null,
  remark: '',
})

const assessForm = reactive<AssessOpportunityRequest>({
  valueScore: null,
  valueLevel: 'Unknown',
  decision: 'Undecided',
  assessmentSummary: '',
})

const stageForm = reactive<ChangeOpportunityStageRequest>({
  stage: 'Assessing',
  status: 'Active',
  reason: '',
})

async function loadData() {
  loading.value = true
  try {
    detail.value = await opportunitiesApi.get(opportunityId.value)
    syncForms()
  } catch {
    detail.value = null
  } finally {
    loading.value = false
  }
}

function syncForms() {
  const item = opportunity.value
  if (!item) return
  Object.assign(editForm, {
    title: item.title,
    priority: item.priority,
    estimatedAmount: item.estimatedAmount ?? null,
    ownerUserId: item.ownerUserId ?? null,
    nextActionAtUtc: item.nextActionAtUtc ?? null,
    remark: item.remark,
  })
  Object.assign(assessForm, {
    valueScore: item.valueScore ?? null,
    valueLevel: item.valueLevel || 'Unknown',
    decision: item.decision || 'Undecided',
    assessmentSummary: item.assessmentSummary,
  })
  Object.assign(stageForm, {
    stage: item.stage || 'Assessing',
    status: item.status || 'Active',
    reason: '',
  })
}

function openEdit() {
  syncForms()
  editDrawerOpen.value = true
}

async function submitEdit() {
  await editRequest.run(async () => {
    await opportunitiesApi.update(opportunityId.value, editForm)
    ElMessage.success('商机已更新')
    editDrawerOpen.value = false
    await loadData()
  })
}

async function submitAssess() {
  await assessRequest.run(async () => {
    await opportunitiesApi.assess(opportunityId.value, {
      ...assessForm,
      assessmentSummary: assessForm.assessmentSummary || null,
    })
    ElMessage.success('评估已保存')
    await loadData()
  })
}

async function submitStage() {
  if (!stageForm.stage) {
    ElMessage.warning('请选择阶段')
    return
  }

  await stageRequest.run(async () => {
    await opportunitiesApi.changeStage(opportunityId.value, {
      ...stageForm,
      reason: stageForm.reason || null,
    })
    ElMessage.success('阶段已更新')
    await loadData()
  })
}

async function toggleWatch() {
  const item = opportunity.value
  if (!item) return

  await watchRequest.run(async () => {
    await opportunitiesApi.watch(item.id, { enabled: !item.watchedByMe })
    ElMessage.success(item.watchedByMe ? '已取消关注' : '已关注')
    await loadData()
  })
}

onMounted(loadData)
</script>

<template>
  <PageContainer :title="pageTitle" description="查看商机、包件、要求项、评估和阶段记录。">
    <template #actions>
      <el-button :icon="ArrowLeft" @click="router.push('/bidops/opportunities')">返回</el-button>
      <PermissionButton
        v-if="opportunity"
        :icon="opportunity.watchedByMe ? StarFilled : Star"
        :type="opportunity.watchedByMe ? 'warning' : 'default'"
        :permission="BIDOPS_PERMISSIONS.OPPORTUNITY_WATCH"
        @click="toggleWatch"
      >
        {{ opportunity.watchedByMe ? '取消关注' : '关注' }}
      </PermissionButton>
      <PermissionButton
        v-if="opportunity"
        type="primary"
        :icon="Edit"
        :permission="BIDOPS_PERMISSIONS.OPPORTUNITY_MANAGE"
        @click="openEdit"
      >
        编辑
      </PermissionButton>
    </template>

    <el-skeleton v-if="loading" :rows="10" animated />
    <el-empty v-else-if="!opportunity" description="未找到商机" />
    <template v-else>
      <section class="opportunity-summary">
        <div>
          <h2>{{ opportunity.title }}</h2>
          <p>{{ opportunity.opportunityNo }} · {{ opportunity.projectName || opportunity.noticeTitle || '-' }}</p>
        </div>
        <div class="summary-tags">
          <BidOpsStatusTag :value="opportunity.stage" />
          <BidOpsStatusTag :value="opportunity.status" />
          <el-tag effect="light">{{ formatOpportunityDecision(opportunity.decision) }}</el-tag>
          <el-tag effect="light">{{ formatOpportunityValueLevel(opportunity.valueLevel) }}</el-tag>
          <el-tag v-if="rejectRiskCount > 0" type="danger" effect="light">{{ rejectRiskCount }} 条废标风险</el-tag>
        </div>
      </section>

      <div class="split-grid">
        <section class="content-panel">
          <h2>商机基础信息</h2>
          <el-descriptions :column="2" border>
            <el-descriptions-item label="商机编号">{{ opportunity.opportunityNo }}</el-descriptions-item>
            <el-descriptions-item label="阶段">{{ formatOpportunityStage(opportunity.stage) }}</el-descriptions-item>
            <el-descriptions-item label="状态"><BidOpsStatusTag :value="opportunity.status" /></el-descriptions-item>
            <el-descriptions-item label="优先级">{{ opportunity.priority }}</el-descriptions-item>
            <el-descriptions-item label="预估金额">{{ formatMoney(opportunity.estimatedAmount) }}</el-descriptions-item>
            <el-descriptions-item label="价值分">{{ opportunity.valueScore ?? '-' }}</el-descriptions-item>
            <el-descriptions-item label="价值等级">{{ formatOpportunityValueLevel(opportunity.valueLevel) }}</el-descriptions-item>
            <el-descriptions-item label="决策">{{ formatOpportunityDecision(opportunity.decision) }}</el-descriptions-item>
            <el-descriptions-item label="关注人数">{{ opportunity.watchCount }}</el-descriptions-item>
            <el-descriptions-item label="下次跟进">{{ formatDateTime(opportunity.nextActionAtUtc) }}</el-descriptions-item>
            <el-descriptions-item label="阶段更新时间">{{ formatDateTime(opportunity.lastStageChangedAtUtc) }}</el-descriptions-item>
            <el-descriptions-item label="创建时间">{{ formatDateTime(opportunity.createdAt) }}</el-descriptions-item>
            <el-descriptions-item label="备注" :span="2">{{ opportunity.remark || '-' }}</el-descriptions-item>
            <el-descriptions-item label="评估摘要" :span="2">{{ opportunity.assessmentSummary || '-' }}</el-descriptions-item>
          </el-descriptions>
        </section>

        <section class="content-panel">
          <h2>关联包件</h2>
          <el-descriptions :column="1" border>
            <el-descriptions-item label="包件号">{{ packageInfo?.packageNo || opportunity.packageNo || '-' }}</el-descriptions-item>
            <el-descriptions-item label="包件名称">{{ packageInfo?.packageName || opportunity.packageName || '-' }}</el-descriptions-item>
            <el-descriptions-item label="品类">{{ formatCategory(packageInfo?.category) }}</el-descriptions-item>
            <el-descriptions-item label="预算金额">{{ formatMoney(packageInfo?.budgetAmount) }}</el-descriptions-item>
            <el-descriptions-item label="最高限价">{{ formatMoney(packageInfo?.maxPrice) }}</el-descriptions-item>
            <el-descriptions-item label="交付地点">{{ packageInfo?.deliveryPlace || '-' }}</el-descriptions-item>
          </el-descriptions>
        </section>
      </div>

      <div class="split-grid detail-section">
        <section class="content-panel">
          <h2>关联公告</h2>
          <el-descriptions :column="1" border>
            <el-descriptions-item label="公告标题">{{ opportunity.noticeTitle || '-' }}</el-descriptions-item>
            <el-descriptions-item label="项目名称">{{ opportunity.projectName || '-' }}</el-descriptions-item>
            <el-descriptions-item label="项目编码">{{ opportunity.projectCode || '-' }}</el-descriptions-item>
            <el-descriptions-item label="采购人">{{ opportunity.buyerName || '-' }}</el-descriptions-item>
            <el-descriptions-item label="地区">{{ opportunity.region || '-' }}</el-descriptions-item>
            <el-descriptions-item label="公告类型">{{ formatNoticeType(packageInfo?.noticeType) }}</el-descriptions-item>
            <el-descriptions-item label="发布时间">{{ formatDateTime(opportunity.publishTime) }}</el-descriptions-item>
            <el-descriptions-item label="投标截止">{{ formatDateTime(opportunity.bidDeadline) }}</el-descriptions-item>
          </el-descriptions>
        </section>

        <section class="content-panel">
          <h2>阶段记录</h2>
          <el-empty v-if="stageHistory.length === 0" description="暂无阶段记录" />
          <el-timeline v-else>
            <el-timeline-item
              v-for="item in stageHistory"
              :key="item.id"
              :timestamp="formatDateTime(item.occurredAtUtc)"
              placement="top"
            >
              <div class="timeline-item">
                <strong>{{ formatOpportunityStage(item.fromStage) }} -> {{ formatOpportunityStage(item.toStage) }}</strong>
                <p>{{ item.reason || '-' }}</p>
              </div>
            </el-timeline-item>
          </el-timeline>
        </section>
      </div>

      <div class="split-grid detail-section">
        <section class="content-panel">
          <h2>商机评估</h2>
          <el-form :model="assessForm" label-width="110px" class="form-grid">
            <el-form-item label="价值分">
              <el-input-number v-model="assessForm.valueScore" :min="0" :max="100" :precision="2" />
            </el-form-item>
            <el-form-item label="价值等级">
              <el-select v-model="assessForm.valueLevel">
                <el-option v-for="item in opportunityValueLevelOptions" :key="item.value" :label="item.label" :value="item.value" />
              </el-select>
            </el-form-item>
            <el-form-item label="决策">
              <el-select v-model="assessForm.decision">
                <el-option v-for="item in opportunityDecisionOptions" :key="item.value" :label="item.label" :value="item.value" />
              </el-select>
            </el-form-item>
            <el-form-item label="摘要" class="full-row">
              <el-input v-model="assessForm.assessmentSummary" type="textarea" :rows="4" />
            </el-form-item>
          </el-form>
          <PermissionButton
            type="primary"
            :loading="assessRequest.loading"
            :permission="BIDOPS_PERMISSIONS.OPPORTUNITY_ASSESS"
            @click="submitAssess"
          >
            保存评估
          </PermissionButton>
        </section>

        <section class="content-panel">
          <h2>阶段流转</h2>
          <el-form :model="stageForm" label-width="110px" class="form-grid">
            <el-form-item label="目标阶段">
              <el-select v-model="stageForm.stage">
                <el-option v-for="item in opportunityStageOptions" :key="item.value" :label="item.label" :value="item.value" />
              </el-select>
            </el-form-item>
            <el-form-item label="状态">
              <el-select v-model="stageForm.status">
                <el-option v-for="item in opportunityStatusOptions" :key="item.value" :label="item.label" :value="item.value" />
              </el-select>
            </el-form-item>
            <el-form-item label="原因" class="full-row">
              <el-input v-model="stageForm.reason" type="textarea" :rows="4" />
            </el-form-item>
          </el-form>
          <PermissionButton
            type="primary"
            :loading="stageRequest.loading"
            :permission="BIDOPS_PERMISSIONS.OPPORTUNITY_MANAGE"
            @click="submitStage"
          >
            更新阶段
          </PermissionButton>
        </section>
      </div>

      <section class="content-panel detail-section">
        <h2>包件要求项</h2>
        <RequirementTable :requirements="requirements" :loading="loading" />
      </section>

      <FormDrawer v-model="editDrawerOpen" title="编辑商机" :submitting="editRequest.loading" @submit="submitEdit">
        <el-form :model="editForm" label-width="120px" class="form-grid">
          <el-form-item label="标题" class="full-row"><el-input v-model.trim="editForm.title" /></el-form-item>
          <el-form-item label="优先级"><el-input-number v-model="editForm.priority" :min="1" :max="5" /></el-form-item>
          <el-form-item label="预估金额">
            <el-input-number v-model="editForm.estimatedAmount" :min="0" :precision="2" />
          </el-form-item>
          <el-form-item label="负责人 ID"><el-input v-model.trim="editForm.ownerUserId" /></el-form-item>
          <el-form-item label="下次跟进">
            <el-date-picker v-model="editForm.nextActionAtUtc" type="datetime" value-format="YYYY-MM-DDTHH:mm:ss" />
          </el-form-item>
          <el-form-item label="备注" class="full-row">
            <el-input v-model="editForm.remark" type="textarea" :rows="4" />
          </el-form-item>
        </el-form>
      </FormDrawer>
    </template>
  </PageContainer>
</template>

<style scoped>
.opportunity-summary {
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

.opportunity-summary h2,
.content-panel h2 {
  margin: 0;
  color: #17202a;
}

.opportunity-summary h2 {
  font-size: 18px;
  line-height: 1.45;
}

.opportunity-summary p {
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

.timeline-item p {
  margin: 0;
  color: #3d4a5c;
  line-height: 1.55;
}

@media (max-width: 980px) {
  .opportunity-summary {
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
