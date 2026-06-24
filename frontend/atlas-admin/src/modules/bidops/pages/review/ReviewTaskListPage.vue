<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Check, DataAnalysis, MagicStick, RefreshRight, View } from '@element-plus/icons-vue'
import { reviewTasksApi } from '@/api/bidops/reviewTasks.api'
import DataTable from '@/shared/components/DataTable.vue'
import PageContainer from '@/shared/components/PageContainer.vue'
import SearchForm from '@/shared/components/SearchForm.vue'
import { useTableQuery } from '@/shared/composables/useTableQuery'
import { formatDateTime } from '@/shared/utils/date'
import BidOpsStatusTag from '../../components/BidOpsStatusTag.vue'
import RiskLevelTag from '../../components/RiskLevelTag.vue'
import type { ReviewTaskDto, ReviewTaskStatus } from '../../types'
import {
  formatNoticeType,
  formatReviewRecommendation,
  noticeTypeOptions,
  reviewQualityIssueTypeOptions,
  reviewQualityRiskLevelOptions,
  reviewRecommendationOptions,
  reviewTaskStatusOptions,
} from '../../utils/display'

interface ReviewTaskListQuery {
  keyword: string
  projectCode: string
  status?: ReviewTaskStatus | ''
  noticeType?: string
  riskLevel?: string
  reviewRecommendation?: string
  issueType?: string
  hasHighRiskIssue?: boolean | ''
  minQualityScore?: number
  maxQualityScore?: number
  pageIndex: number
  pageSize: number
}

const router = useRouter()
const selectedRows = ref<ReviewTaskDto[]>([])
const batchPromptVisible = ref(false)
const batchPrompt = ref('')
const batchActionLoading = ref(false)
const table = useTableQuery<ReviewTaskDto, ReviewTaskListQuery>(
  (params) =>
    reviewTasksApi.search({
      ...params,
      status: params.status || undefined,
      projectCode: params.projectCode.trim() || undefined,
      noticeType: params.noticeType || undefined,
      riskLevel: params.riskLevel || undefined,
      reviewRecommendation: params.reviewRecommendation || undefined,
      issueType: params.issueType || undefined,
      hasHighRiskIssue: params.hasHighRiskIssue === '' ? undefined : params.hasHighRiskIssue,
      minQualityScore: params.minQualityScore,
      maxQualityScore: params.maxQualityScore,
    }),
  {
    keyword: '',
    projectCode: '',
    status: '',
    noticeType: '',
    riskLevel: '',
    reviewRecommendation: '',
    issueType: '',
    hasHighRiskIssue: '',
    pageIndex: 1,
    pageSize: 20,
  },
  {
    storageKey: 'atlas.bidops.review-tasks.query.v1',
  },
)

const selectedIds = computed(() => selectedRows.value.map((row) => row.id))
const selectedLowRiskCount = computed(
  () => selectedRows.value.filter((row) => row.riskLevel === 'Low' && Number(row.highRiskIssueCount || 0) <= 0).length,
)

function projectTitle(row: ReviewTaskDto) {
  return row.projectName || row.taskTitle || '-'
}

function keyDate(row: ReviewTaskDto) {
  return row.bidDeadline || row.openBidTime || row.signupDeadline || row.publishTime
}

function confidencePercent(value: number) {
  return Math.round(Number(value || 0) * 100)
}

function qualityScore(row: ReviewTaskDto) {
  return Number.isFinite(Number(row.qualityScore)) ? Number(row.qualityScore) : 100
}

function selectableReviewTask(row: ReviewTaskDto) {
  return !['Approved', 'Ignored', 'Merged'].includes(String(row.status))
}

function handleSelectionChange(rows: ReviewTaskDto[]) {
  selectedRows.value = rows
}

async function bulkApproveSelected() {
  if (!selectedIds.value.length) {
    ElMessage.warning('请选择待确认任务')
    return
  }

  try {
    await ElMessageBox.confirm(`确认批量通过 ${selectedLowRiskCount.value} 个低风险任务？非低风险任务会由服务端逐项拒绝。`, '低风险批量确认', {
      type: 'warning',
    })
  } catch {
    return
  }

  batchActionLoading.value = true
  try {
    const result = await reviewTasksApi.bulkApprove({
      reviewTaskIds: selectedIds.value,
      expectedRiskLevel: 'Low',
      maxHighRiskIssueCount: 0,
      remark: '低风险批量确认',
    })
    ElMessage.success(`成功 ${result.succeededCount} 个，失败 ${result.failedCount} 个，跳过 ${result.skippedCount} 个`)
    selectedRows.value = []
    await table.loadData()
  } finally {
    batchActionLoading.value = false
  }
}

function openBatchPrompt() {
  if (!selectedIds.value.length) {
    ElMessage.warning('请选择要重解析的任务')
    return
  }
  batchPrompt.value = ''
  batchPromptVisible.value = true
}

async function submitBatchPrompt() {
  if (!batchPrompt.value.trim()) {
    ElMessage.warning('请输入给 AI 的调整提示词')
    return
  }

  batchActionLoading.value = true
  try {
    const result = await reviewTasksApi.batchReparse({
      reviewTaskIds: selectedIds.value,
      prompt: batchPrompt.value.trim(),
      reason: '批量提示词重解析',
    })
    ElMessage.success(`已入队 ${result.succeededCount} 个，失败 ${result.failedCount} 个，跳过 ${result.skippedCount} 个`)
    batchPromptVisible.value = false
    selectedRows.value = []
    await table.loadData()
  } finally {
    batchActionLoading.value = false
  }
}

async function enqueueQualityBackfill() {
  try {
    await ElMessageBox.confirm('将入队回填最近 100 个待审核任务的质量评分，来源已暂停的任务会跳过。', '质量评分回填', {
      type: 'info',
    })
  } catch {
    return
  }

  const job = await reviewTasksApi.qualityBackfill({
    maxItems: 100,
    dryRun: false,
    pauseSourceAware: true,
  })
  ElMessage.success(`质量回填任务已入队：${job.jobTypeName || job.jobType}`)
}
</script>

<template>
  <PageContainer title="待审核池" description="人工审核 Raw -> Staging 的解析结果，确认后才写入正式业务表。">
    <SearchForm @search="table.search" @reset="table.reset()">
      <el-form-item label="关键词">
        <el-input v-model="table.query.keyword" clearable placeholder="项目 / 标题 / 采购人" />
      </el-form-item>
      <el-form-item label="采购编号">
        <el-input v-model="table.query.projectCode" clearable placeholder="采购编号 / 项目编号" style="width: 210px" />
      </el-form-item>
      <el-form-item label="状态">
        <el-select v-model="table.query.status" clearable placeholder="全部" style="width: 190px">
          <el-option v-for="item in reviewTaskStatusOptions" :key="item.value" :label="item.label" :value="item.value" />
        </el-select>
      </el-form-item>
      <el-form-item label="公告类型">
        <el-select v-model="table.query.noticeType" clearable filterable placeholder="全部" style="width: 210px">
          <el-option v-for="item in noticeTypeOptions" :key="item.value" :label="item.label" :value="item.value" />
        </el-select>
      </el-form-item>
      <el-form-item label="风险等级">
        <el-select v-model="table.query.riskLevel" clearable placeholder="全部" style="width: 150px">
          <el-option v-for="item in reviewQualityRiskLevelOptions" :key="item.value" :label="item.label" :value="item.value" />
        </el-select>
      </el-form-item>
      <el-form-item label="推荐动作">
        <el-select v-model="table.query.reviewRecommendation" clearable placeholder="全部" style="width: 190px">
          <el-option v-for="item in reviewRecommendationOptions" :key="item.value" :label="item.label" :value="item.value" />
        </el-select>
      </el-form-item>
      <el-form-item label="异常类型">
        <el-select v-model="table.query.issueType" clearable filterable placeholder="全部" style="width: 190px">
          <el-option v-for="item in reviewQualityIssueTypeOptions" :key="item.value" :label="item.label" :value="item.value" />
        </el-select>
      </el-form-item>
      <el-form-item label="高风险">
        <el-select v-model="table.query.hasHighRiskIssue" clearable placeholder="全部" style="width: 130px">
          <el-option label="有" :value="true" />
          <el-option label="无" :value="false" />
        </el-select>
      </el-form-item>
    </SearchForm>

    <div class="review-actions">
      <div class="selection-summary">已选 {{ selectedRows.length }} 个，低风险 {{ selectedLowRiskCount }} 个</div>
      <div class="action-buttons">
        <el-button :icon="Check" type="success" plain :loading="batchActionLoading" @click="bulkApproveSelected">低风险批量确认</el-button>
        <el-button :icon="MagicStick" type="primary" plain :loading="batchActionLoading" @click="openBatchPrompt">批量提示词重解析</el-button>
        <el-button :icon="RefreshRight" plain @click="enqueueQualityBackfill">回填质量</el-button>
        <el-button :icon="DataAnalysis" plain @click="router.push('/bidops/review/quality-analysis')">质量分析</el-button>
      </div>
    </div>

    <DataTable :data="table.result.items" :loading="table.loading" @selection-change="handleSelectionChange">
      <el-table-column type="selection" width="48" :selectable="selectableReviewTask" />
      <el-table-column label="待审项目" min-width="320" show-overflow-tooltip>
        <template #default="{ row }">
          <div class="project-cell">
            <strong>{{ projectTitle(row) }}</strong>
            <span>{{ formatNoticeType(row.noticeType) }} · {{ row.region || '未识别地区' }}</span>
          </div>
        </template>
      </el-table-column>
      <el-table-column label="采购编号" min-width="170" show-overflow-tooltip>
        <template #default="{ row }">{{ row.projectCode || '-' }}</template>
      </el-table-column>
      <el-table-column label="采购人" min-width="210" show-overflow-tooltip>
        <template #default="{ row }">{{ row.buyerName || '-' }}</template>
      </el-table-column>
      <el-table-column label="关键时间" width="190">
        <template #default="{ row }">
          <div class="date-cell">
            <span>{{ formatDateTime(keyDate(row)) }}</span>
            <small v-if="row.bidDeadline">投标截止</small>
            <small v-else-if="row.openBidTime">开标时间</small>
            <small v-else-if="row.signupDeadline">报名截止</small>
            <small v-else>发布时间</small>
          </div>
        </template>
      </el-table-column>
      <el-table-column label="解析内容" width="180">
        <template #default="{ row }">
          <div class="count-cell">
            <span>{{ row.packageCount }} 包件 / {{ row.requirementCount }} 要求</span>
            <el-tag v-if="row.rejectRiskCount > 0" type="danger" effect="light">{{ row.rejectRiskCount }} 条废标风险</el-tag>
          </div>
        </template>
      </el-table-column>
      <el-table-column label="质量" width="170">
        <template #default="{ row }">
          <div class="quality-cell">
            <div class="quality-main">
              <strong>{{ qualityScore(row) }}</strong>
              <RiskLevelTag :value="row.riskLevel" />
            </div>
            <small>{{ row.qualityIssueCount || 0 }} 异常 / {{ row.highRiskIssueCount || 0 }} 高风险</small>
          </div>
        </template>
      </el-table-column>
      <el-table-column label="建议" width="150" show-overflow-tooltip>
        <template #default="{ row }">{{ formatReviewRecommendation(row.reviewRecommendation) }}</template>
      </el-table-column>
      <el-table-column label="置信度" width="120">
        <template #default="{ row }">{{ confidencePercent(row.aiConfidence) }}%</template>
      </el-table-column>
      <el-table-column label="状态" width="120"><template #default="{ row }"><BidOpsStatusTag :value="row.status" kind="reviewTask" /></template></el-table-column>
      <el-table-column label="创建时间" width="170"><template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template></el-table-column>
      <el-table-column label="最后更新时间" width="170"><template #default="{ row }">{{ formatDateTime(row.updatedAt || row.createdAt) }}</template></el-table-column>
      <el-table-column label="操作" width="120" fixed="right">
        <template #default="{ row }">
          <el-button size="small" type="primary" plain :icon="View" @click="router.push(`/bidops/review/tasks/${row.id}`)">审核</el-button>
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

    <el-dialog v-model="batchPromptVisible" title="批量 AI 提示词重解析" width="640px">
      <el-input
        v-model="batchPrompt"
        type="textarea"
        :rows="7"
        maxlength="4000"
        show-word-limit
        placeholder="例如：请重点识别采购一览表中的“最高限价（万元）”，金额字段统一换算为元；中标候选人表请保留候选排名。"
      />
      <template #footer>
        <el-button @click="batchPromptVisible = false">取消</el-button>
        <el-button type="primary" :loading="batchActionLoading" @click="submitBatchPrompt">入队重解析</el-button>
      </template>
    </el-dialog>
  </PageContainer>
</template>

<style scoped>
.table-pagination {
  justify-content: flex-end;
  margin-top: 14px;
}

.review-actions {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin: 0 0 12px;
}

.selection-summary {
  color: #687385;
  font-size: 13px;
  white-space: nowrap;
}

.action-buttons {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 8px;
}

.project-cell,
.date-cell,
.count-cell,
.quality-cell {
  display: grid;
  gap: 4px;
  min-width: 0;
}

.project-cell strong {
  overflow: hidden;
  color: #17202a;
  font-weight: 650;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.project-cell span,
.date-cell small {
  overflow: hidden;
  color: #687385;
  font-size: 12px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.count-cell {
  align-items: start;
}

.quality-main {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}

.quality-main strong {
  color: #17202a;
}

.quality-cell small {
  overflow: hidden;
  color: #687385;
  font-size: 12px;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
