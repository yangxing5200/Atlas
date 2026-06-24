<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { Refresh } from '@element-plus/icons-vue'
import { reviewTasksApi } from '@/api/bidops/reviewTasks.api'
import PageContainer from '@/shared/components/PageContainer.vue'
import SearchForm from '@/shared/components/SearchForm.vue'
import { formatDateTime } from '@/shared/utils/date'
import type { ReviewCorrectionAnalysisDto, ReviewCorrectionSampleDto, ReviewEfficiencyMetricsDto } from '../../types'
import {
  formatNoticeType,
  formatReviewCorrectionSourceKind,
  noticeTypeOptions,
  reviewCorrectionSourceKindOptions,
} from '../../utils/display'

const loading = ref(false)
const metrics = ref<ReviewEfficiencyMetricsDto>()
const analysis = ref<ReviewCorrectionAnalysisDto>()
const filters = reactive({
  sourceKind: '',
  noticeType: '',
  keyword: '',
})

const metricItems: Array<{ key: keyof ReviewEfficiencyMetricsDto; label: string; suffix?: string }> = [
  { key: 'pendingReviewTasks', label: '待审总数' },
  { key: 'todayNewReviewTasks', label: '今日新增' },
  { key: 'lowRiskRatio', label: '低风险占比', suffix: '%' },
  { key: 'bulkApprovedToday', label: '今日批量确认' },
  { key: 'averageHandlingMinutes', label: '平均处理分钟' },
  { key: 'reparsePromptSamplesToday', label: '今日重解析提示词' },
]

async function loadData() {
  loading.value = true
  try {
    const [metricResult, analysisResult] = await Promise.all([
      reviewTasksApi.efficiencyMetrics(),
      reviewTasksApi.correctionAnalysis({
        sourceKind: filters.sourceKind || undefined,
        noticeType: filters.noticeType || undefined,
        keyword: filters.keyword || undefined,
      }),
    ])
    metrics.value = metricResult
    analysis.value = analysisResult
  } finally {
    loading.value = false
  }
}

function resetFilters() {
  filters.sourceKind = ''
  filters.noticeType = ''
  filters.keyword = ''
  loadData()
}

function metricValue(key: keyof ReviewEfficiencyMetricsDto) {
  return metrics.value?.[key] ?? 0
}

function shortText(value?: string | null, max = 160) {
  const text = String(value || '').trim()
  if (!text) return '-'
  return text.length <= max ? text : `${text.slice(0, max)}...`
}

function sampleSummary(row: ReviewCorrectionSampleDto) {
  return row.reviewerPrompt || row.correctedValue || row.reason || row.originalValue
}

onMounted(loadData)
</script>

<template>
  <PageContainer title="审核质量分析" description="查看审核效率、纠错样本和规则改进线索。">
    <div v-loading="loading" class="metrics-grid">
      <div v-for="item in metricItems" :key="item.key" class="metric-tile">
        <span>{{ item.label }}</span>
        <strong>{{ metricValue(item.key) }}{{ item.suffix || '' }}</strong>
      </div>
      <div class="metric-tile risk-tile">
        <span>风险分布</span>
        <strong>{{ metrics?.lowRiskCount || 0 }} / {{ metrics?.mediumRiskCount || 0 }} / {{ metrics?.highRiskCount || 0 }}</strong>
      </div>
    </div>

    <SearchForm @search="loadData" @reset="resetFilters">
      <el-form-item label="样本类型">
        <el-select v-model="filters.sourceKind" clearable placeholder="全部" style="width: 190px">
          <el-option v-for="item in reviewCorrectionSourceKindOptions" :key="item.value" :label="item.label" :value="item.value" />
        </el-select>
      </el-form-item>
      <el-form-item label="公告类型">
        <el-select v-model="filters.noticeType" clearable filterable placeholder="全部" style="width: 210px">
          <el-option v-for="item in noticeTypeOptions" :key="item.value" :label="item.label" :value="item.value" />
        </el-select>
      </el-form-item>
      <el-form-item label="关键词">
        <el-input v-model="filters.keyword" clearable placeholder="字段 / 表头 / 提示词" />
      </el-form-item>
      <el-button :icon="Refresh" plain @click="loadData">刷新</el-button>
    </SearchForm>

    <div class="analysis-grid">
      <section class="analysis-panel">
        <h3>高频错误字段</h3>
        <el-table :data="analysis?.topFields || []" size="small" border>
          <el-table-column prop="key" label="字段" min-width="160" />
          <el-table-column prop="count" label="次数" width="90" />
        </el-table>
      </section>
      <section class="analysis-panel">
        <h3>高频原始表头</h3>
        <el-table :data="analysis?.topOriginalHeaders || []" size="small" border>
          <el-table-column prop="key" label="表头" min-width="180" show-overflow-tooltip />
          <el-table-column prop="count" label="次数" width="90" />
        </el-table>
      </section>
      <section class="analysis-panel">
        <h3>金额单位线索</h3>
        <el-table :data="analysis?.amountUnitIssues || []" size="small" border>
          <el-table-column prop="key" label="字段/表头" min-width="180" show-overflow-tooltip />
          <el-table-column prop="count" label="次数" width="90" />
        </el-table>
      </section>
      <section class="analysis-panel">
        <h3>资质要求线索</h3>
        <el-table :data="analysis?.requirementIssues || []" size="small" border>
          <el-table-column prop="key" label="字段/表头" min-width="180" show-overflow-tooltip />
          <el-table-column prop="count" label="次数" width="90" />
        </el-table>
      </section>
    </div>

    <section class="analysis-panel recent-panel">
      <h3>最近纠错样本</h3>
      <el-table :data="analysis?.recentSamples || []" border>
        <el-table-column label="时间" width="170">
          <template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template>
        </el-table-column>
        <el-table-column label="来源" width="130">
          <template #default="{ row }">{{ formatReviewCorrectionSourceKind(row.sourceKind) }}</template>
        </el-table-column>
        <el-table-column label="公告类型" width="150">
          <template #default="{ row }">{{ formatNoticeType(row.noticeType) }}</template>
        </el-table-column>
        <el-table-column prop="fieldName" label="字段" width="180" show-overflow-tooltip />
        <el-table-column label="样本内容" min-width="360" show-overflow-tooltip>
          <template #default="{ row }">{{ shortText(sampleSummary(row)) }}</template>
        </el-table-column>
        <el-table-column prop="originalHeader" label="原表头" width="180" show-overflow-tooltip />
      </el-table>
    </section>
  </PageContainer>
</template>

<style scoped>
.metrics-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
  gap: 10px;
  margin-bottom: 14px;
}

.metric-tile {
  display: grid;
  gap: 6px;
  min-height: 78px;
  padding: 14px;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  background: #fff;
}

.metric-tile span {
  color: #687385;
  font-size: 13px;
}

.metric-tile strong {
  color: #17202a;
  font-size: 22px;
  font-weight: 700;
}

.risk-tile strong {
  font-size: 18px;
}

.analysis-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
  margin-top: 14px;
}

.analysis-panel {
  min-width: 0;
}

.analysis-panel h3 {
  margin: 0 0 8px;
  color: #17202a;
  font-size: 15px;
  font-weight: 650;
}

.recent-panel {
  margin-top: 16px;
}

@media (max-width: 900px) {
  .analysis-grid {
    grid-template-columns: 1fr;
  }
}
</style>
