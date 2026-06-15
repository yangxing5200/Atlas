<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { Refresh, View } from '@element-plus/icons-vue'
import { suppliersApi } from '@/api/bidops/suppliers.api'
import DataTable from '@/shared/components/DataTable.vue'
import PageContainer from '@/shared/components/PageContainer.vue'
import { formatDateTime } from '@/shared/utils/date'
import { formatMoney } from '@/shared/utils/money'
import BidOpsStatusTag from '../../components/BidOpsStatusTag.vue'
import type { OutcomeSupplierRecordDto, SupplierAnalysisSummaryDto } from '../../types'
import { formatCategory, formatCommonStatus, formatPackageNo, formatSupplierName } from '../../utils/display'

const router = useRouter()
const loading = ref(false)
const summary = ref<SupplierAnalysisSummaryDto | null>(null)
const recentOutcomeRecords = ref<OutcomeSupplierRecordDto[]>([])

const categoryRows = computed(() => summary.value?.capabilityCategories ?? [])
const evidenceRows = computed(() => summary.value?.evidenceStatuses ?? [])
const supplierRows = computed(() => summary.value?.topSuppliers ?? [])
const outcomeSupplierRows = computed(() => summary.value?.topOutcomeSuppliers ?? [])

const metrics = computed(() => {
  const item = summary.value
  if (!item) return []

  return [
    {
      label: '厂家总数',
      value: item.totalSuppliers,
      detail: `有效 ${item.activeSuppliers} / 停用 ${item.blockedSuppliers}`,
    },
    {
      label: '能力覆盖',
      value: item.suppliersWithCapabilities,
      detail: formatPercent(item.suppliersWithCapabilities, item.totalSuppliers),
    },
    {
      label: '材料覆盖',
      value: item.suppliersWithEvidence,
      detail: `预警 ${item.expiringEvidenceDocuments} / 过期 ${item.expiredEvidenceDocuments}`,
    },
    {
      label: '平均评分',
      value: item.averageQualityScore ?? '-',
      detail: '厂家质量评分',
    },
    {
      label: '匹配厂家',
      value: item.matchedSupplierCount,
      detail: `候选 ${item.candidateSupplierCount}`,
    },
    {
      label: '立项作业',
      value: item.goDecisionCount,
      detail: `作业厂家 ${item.pursuitSupplierCount}`,
    },
    {
      label: '结果线索',
      value: item.outcomeRecordCount,
      detail: `公开厂家 ${item.outcomeSupplierCount}`,
    },
    {
      label: '中标/候选',
      value: item.awardedOutcomeCount,
      detail: `候选 ${item.candidateOutcomeCount} / 已关联 ${item.linkedOutcomeSupplierCount}`,
    },
  ]
})

async function loadData() {
  loading.value = true
  try {
    const [analysis, outcomes] = await Promise.all([
      suppliersApi.analysisSummary(),
      suppliersApi.outcomeRecords({ pageIndex: 1, pageSize: 20, hasAwardAmount: true, sortBy: 'AwardAmountDesc' }),
    ])
    summary.value = analysis
    recentOutcomeRecords.value = outcomes.items
  } finally {
    loading.value = false
  }
}

function formatPercent(part: number, total: number) {
  if (total <= 0) return '-'
  return `${Math.round((part / total) * 100)}%`
}

function formatScore(value?: number | null) {
  return value ?? '-'
}

function formatDate(value?: string | null) {
  return value ? formatDateTime(value) : '-'
}

function formatRank(value?: number | null) {
  return value ? `第 ${value} 名` : '-'
}

onMounted(loadData)
</script>

<template>
  <PageContainer title="厂家分析" description="当前统计基于厂家能力库、匹配记录、立项决策和投标作业。">
    <template #actions>
      <el-button :icon="Refresh" :loading="loading" @click="loadData">刷新</el-button>
    </template>

    <el-skeleton v-if="loading && !summary" :rows="8" animated />
    <template v-else>
      <section v-if="summary" class="source-band">
        <div>
          <span>数据来源</span>
          <strong>{{ summary.supplierSourceDescription }}</strong>
        </div>
        <p>{{ summary.outcomeExtractionStatus }}</p>
      </section>

      <section class="metric-grid">
        <article v-for="item in metrics" :key="item.label" class="metric-tile">
          <span>{{ item.label }}</span>
          <strong>{{ item.value }}</strong>
          <small>{{ item.detail }}</small>
        </article>
      </section>

      <section class="analysis-grid">
        <div class="analysis-panel">
          <div class="section-heading">
            <h2>能力分类</h2>
            <span>厂家数 / 标签数</span>
          </div>
          <DataTable :data="categoryRows" :loading="loading" empty-text="暂无能力数据">
            <el-table-column label="分类" min-width="160">
              <template #default="{ row }">{{ formatCategory(row.code) }}</template>
            </el-table-column>
            <el-table-column label="厂家数" width="100">
              <template #default="{ row }">{{ row.supplierCount }}</template>
            </el-table-column>
            <el-table-column label="标签数" width="100">
              <template #default="{ row }">{{ row.count }}</template>
            </el-table-column>
          </DataTable>
        </div>

        <div class="analysis-panel">
          <div class="section-heading">
            <h2>材料状态</h2>
            <span>材料数 / 厂家数</span>
          </div>
          <DataTable :data="evidenceRows" :loading="loading" empty-text="暂无材料数据">
            <el-table-column label="状态" min-width="160">
              <template #default="{ row }">{{ formatCommonStatus(row.code) }}</template>
            </el-table-column>
            <el-table-column label="材料数" width="100">
              <template #default="{ row }">{{ row.count }}</template>
            </el-table-column>
            <el-table-column label="厂家数" width="100">
              <template #default="{ row }">{{ row.supplierCount }}</template>
            </el-table-column>
          </DataTable>
        </div>
      </section>

      <section class="analysis-grid">
        <div class="analysis-panel">
          <div class="section-heading">
            <h2>公开结果厂家</h2>
            <span>按累计金额和次数排序</span>
          </div>
          <DataTable :data="outcomeSupplierRows" :loading="loading" empty-text="暂无公开结果厂家线索">
            <el-table-column label="厂家" min-width="220" show-overflow-tooltip>
              <template #default="{ row }">
                <el-button
                  v-if="row.supplierId"
                  link
                  type="primary"
                  @click="router.push(`/bidops/suppliers/${row.supplierId}`)"
                >
                  {{ formatSupplierName(row.supplierName) }}
                </el-button>
                <span v-else>{{ formatSupplierName(row.supplierName) }}</span>
              </template>
            </el-table-column>
            <el-table-column label="线索" width="90">
              <template #default="{ row }">{{ row.outcomeCount }}</template>
            </el-table-column>
            <el-table-column label="中标" width="90">
              <template #default="{ row }">{{ row.awardedCount }}</template>
            </el-table-column>
            <el-table-column label="候选" width="90">
              <template #default="{ row }">{{ row.candidateCount }}</template>
            </el-table-column>
            <el-table-column label="累计金额" width="130">
              <template #default="{ row }">{{ formatMoney(row.totalAwardAmount) }}</template>
            </el-table-column>
            <el-table-column label="最近公示" width="170">
              <template #default="{ row }">{{ formatDate(row.lastPublishTime) }}</template>
            </el-table-column>
          </DataTable>
        </div>

        <div class="analysis-panel">
          <div class="section-heading">
            <h2>带金额结果线索</h2>
            <span>按金额排序</span>
          </div>
          <DataTable :data="recentOutcomeRecords" :loading="loading" empty-text="暂无带金额结果线索">
            <el-table-column label="厂家" min-width="210" show-overflow-tooltip>
              <template #default="{ row }">{{ formatSupplierName(row.supplierName) }}</template>
            </el-table-column>
            <el-table-column label="结果" width="100">
              <template #default="{ row }">{{ formatCommonStatus(row.outcomeType) }}</template>
            </el-table-column>
            <el-table-column label="名次" width="90">
              <template #default="{ row }">{{ formatRank(row.rank) }}</template>
            </el-table-column>
            <el-table-column label="包件" min-width="170" show-overflow-tooltip>
              <template #default="{ row }">{{ row.packageName || formatPackageNo(row.packageNo) }}</template>
            </el-table-column>
            <el-table-column label="金额" width="120">
              <template #default="{ row }">{{ formatMoney(row.awardAmount) }}</template>
            </el-table-column>
            <el-table-column label="代理服务费" width="130">
              <template #default="{ row }">{{ formatMoney(row.procurementAgencyServiceFeeAmount) }}</template>
            </el-table-column>
            <el-table-column label="发布时间" width="170">
              <template #default="{ row }">{{ formatDate(row.publishTime) }}</template>
            </el-table-column>
            <el-table-column label="原始公告" width="100" fixed="right">
              <template #default="{ row }">
                <el-link v-if="row.sourceUrl" :href="row.sourceUrl" target="_blank" type="primary">查看</el-link>
                <span v-else>-</span>
              </template>
            </el-table-column>
          </DataTable>
        </div>
      </section>

      <section class="analysis-panel">
        <div class="section-heading">
          <h2>厂家表现</h2>
          <span>按作业、立项、候选匹配、资料完整度排序</span>
        </div>
        <DataTable :data="supplierRows" :loading="loading" empty-text="暂无厂家分析数据">
          <el-table-column label="厂家" min-width="260" fixed show-overflow-tooltip>
            <template #default="{ row }">
              <div class="supplier-cell">
                <el-button link type="primary" @click="router.push(`/bidops/suppliers/${row.supplierId}`)">
                  {{ formatSupplierName(row.supplierName) }}
                </el-button>
                <span>{{ row.supplierNo }}</span>
              </div>
            </template>
          </el-table-column>
          <el-table-column label="状态" width="110">
            <template #default="{ row }"><BidOpsStatusTag :value="row.status" /></template>
          </el-table-column>
          <el-table-column prop="region" label="地区" width="120" show-overflow-tooltip />
          <el-table-column label="评分" width="90">
            <template #default="{ row }">{{ formatScore(row.qualityScore) }}</template>
          </el-table-column>
          <el-table-column label="能力/材料" width="130">
            <template #default="{ row }">{{ row.capabilityCount }} / {{ row.evidenceCount }}</template>
          </el-table-column>
          <el-table-column label="预警/过期" width="130">
            <template #default="{ row }">{{ row.expiringEvidenceCount }} / {{ row.expiredEvidenceCount }}</template>
          </el-table-column>
          <el-table-column label="匹配" width="150">
            <template #default="{ row }">
              {{ row.matchResultCount }} 次
              <span class="muted">候选 {{ row.candidateMatchCount }}</span>
            </template>
          </el-table-column>
          <el-table-column label="决策" width="150">
            <template #default="{ row }">
              立项 {{ row.goDecisionCount }}
              <span class="muted">放弃 {{ row.noGoDecisionCount }}</span>
            </template>
          </el-table-column>
          <el-table-column label="作业" width="90">
            <template #default="{ row }">{{ row.pursuitCount }}</template>
          </el-table-column>
          <el-table-column label="最近匹配" width="170">
            <template #default="{ row }">{{ formatDate(row.lastMatchedAtUtc) }}</template>
          </el-table-column>
          <el-table-column label="最近决策" width="170">
            <template #default="{ row }">{{ formatDate(row.lastDecisionAtUtc) }}</template>
          </el-table-column>
          <el-table-column label="风险" min-width="180" show-overflow-tooltip>
            <template #default="{ row }">{{ row.riskFlags || '-' }}</template>
          </el-table-column>
          <el-table-column label="操作" width="100" fixed="right">
            <template #default="{ row }">
              <el-button size="small" :icon="View" @click="router.push(`/bidops/suppliers/${row.supplierId}`)">
                详情
              </el-button>
            </template>
          </el-table-column>
        </DataTable>
      </section>
    </template>
  </PageContainer>
</template>

<style scoped>
.source-band {
  display: grid;
  gap: 8px;
  margin-bottom: 14px;
  padding: 14px 16px;
  border: 1px solid #dbe3ef;
  border-left: 4px solid #2f6fed;
  border-radius: 8px;
  background: #f8fbff;
}

.source-band span {
  display: block;
  margin-bottom: 4px;
  color: #687385;
  font-size: 12px;
}

.source-band strong {
  color: #17202a;
  font-size: 14px;
  font-weight: 600;
  line-height: 1.5;
}

.source-band p {
  margin: 0;
  color: #687385;
  font-size: 13px;
  line-height: 1.5;
}

.metric-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 12px;
  margin-bottom: 14px;
}

.metric-tile {
  min-width: 0;
  padding: 14px;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  background: #ffffff;
}

.metric-tile:nth-child(2n) {
  border-top: 3px solid #0f9f6e;
}

.metric-tile:nth-child(2n + 1) {
  border-top: 3px solid #2f6fed;
}

.metric-tile span,
.metric-tile small {
  display: block;
  overflow: hidden;
  color: #687385;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.metric-tile strong {
  display: block;
  margin: 6px 0;
  color: #17202a;
  font-size: 24px;
  line-height: 1.2;
}

.analysis-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 14px;
  margin-bottom: 14px;
}

.analysis-panel {
  min-width: 0;
  margin-bottom: 14px;
}

.section-heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 8px;
}

.section-heading h2 {
  margin: 0;
  color: #17202a;
  font-size: 16px;
  font-weight: 650;
  line-height: 1.4;
}

.section-heading span {
  color: #687385;
  font-size: 12px;
}

.supplier-cell {
  display: grid;
  gap: 2px;
  min-width: 0;
}

.supplier-cell :deep(.el-button) {
  justify-content: flex-start;
  min-width: 0;
  padding: 0;
  overflow: hidden;
}

.supplier-cell span {
  overflow: hidden;
  color: #687385;
  font-size: 12px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.muted {
  display: block;
  color: #687385;
  font-size: 12px;
}

@media (max-width: 1200px) {
  .metric-grid {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }
}

@media (max-width: 860px) {
  .analysis-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 720px) {
  .metric-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
</style>
