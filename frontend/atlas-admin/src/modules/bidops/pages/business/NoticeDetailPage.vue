<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ArrowLeft, Box, DataAnalysis, Document, Link } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { lifecycleApi } from '@/api/bidops/lifecycle.api'
import { noticesApi } from '@/api/bidops/notices.api'
import { packagesApi } from '@/api/bidops/packages.api'
import DataTable from '@/shared/components/DataTable.vue'
import PageContainer from '@/shared/components/PageContainer.vue'
import { usePermission } from '@/shared/composables/usePermission'
import { formatDateTime } from '@/shared/utils/date'
import { formatMoney } from '@/shared/utils/money'
import BidOpsStatusTag from '../../components/BidOpsStatusTag.vue'
import { BIDOPS_PERMISSIONS } from '../../constants'
import type { NoticeDto, TenderPackageDto } from '../../types'
import { formatCategory, formatNoticeType, formatPackageNo, isResultNoticeType } from '../../utils/display'

const route = useRoute()
const router = useRouter()
const { visible: canAnalyzeLifecycle } = usePermission(BIDOPS_PERMISSIONS.CRAWL_IMPORT)
const loading = ref(false)
const analyzeLoading = ref(false)
const notice = ref<NoticeDto | null>(null)
const packages = ref<TenderPackageDto[]>([])
const noticeId = computed(() => String(route.params.id || ''))
const pageTitle = computed(() => notice.value?.title || '公告详情')
const canAnalyze = computed(() =>
  canAnalyzeLifecycle.value &&
  !!notice.value?.rawNoticeId &&
  isResultNoticeType(notice.value.noticeType, notice.value.title),
)

async function loadData() {
  loading.value = true
  try {
    const currentNotice = await noticesApi.get(noticeId.value)
    notice.value = currentNotice
    const packageResult = await packagesApi.search({
      noticeId: currentNotice.id,
      pageIndex: 1,
      pageSize: 100,
    })
    packages.value = packageResult.items
  } catch {
    notice.value = null
    packages.value = []
  } finally {
    loading.value = false
  }
}

async function analyzeLifecycle() {
  if (!notice.value || !canAnalyze.value) return

  analyzeLoading.value = true
  try {
    const rawNoticeId = notice.value.rawNoticeId
    const job = await lifecycleApi.enqueueReverseClose({
      rawNoticeId,
      persistEvidence: true,
      persistLifecycleLinks: false,
      persistLifecycleLinksOnCompletion: true,
    })
    ElMessage.success(job.alreadyExists ? `闭环分析任务已存在：${job.jobId}` : `闭环分析任务已入队：${job.jobId}`)
    await router.push(`/bidops/outcomes?rawNoticeId=${rawNoticeId}`)
  } finally {
    analyzeLoading.value = false
  }
}

onMounted(loadData)
</script>

<template>
  <PageContainer :title="pageTitle" description="查看正式公告基础信息、关联原始公告和包件。">
    <template #actions>
      <el-button :icon="ArrowLeft" @click="router.push('/bidops/notices')">返回</el-button>
      <el-button
        v-if="notice?.rawNoticeId"
        :icon="Document"
        @click="router.push(`/bidops/crawl/raw-notices/${notice.rawNoticeId}`)"
      >
        原始公告
      </el-button>
      <el-button
        v-if="notice"
        :icon="Box"
        @click="router.push(`/bidops/packages?noticeId=${notice.id}`)"
      >
        包件
      </el-button>
      <el-button
        v-if="canAnalyze"
        type="primary"
        :icon="DataAnalysis"
        :loading="analyzeLoading"
        @click="analyzeLifecycle"
      >
        分析闭环
      </el-button>
    </template>

    <el-skeleton v-if="loading" :rows="10" animated />
    <el-empty v-else-if="!notice" description="未找到公告" />
    <template v-else>
      <section class="notice-summary">
        <div>
          <h2>{{ notice.projectName || notice.title }}</h2>
          <p>{{ notice.projectCode || '未识别采购编号' }} · {{ notice.buyerName || '未识别采购人' }}</p>
        </div>
        <div class="summary-tags">
          <BidOpsStatusTag :value="notice.status" />
          <el-tag effect="light">{{ formatNoticeType(notice.noticeType) }}</el-tag>
          <el-tag v-if="isResultNoticeType(notice.noticeType, notice.title)" type="success" effect="light">结果类公告</el-tag>
        </div>
      </section>

      <section class="content-panel">
        <h2>公告信息</h2>
        <el-descriptions :column="2" border>
          <el-descriptions-item label="公告标题" :span="2">{{ notice.title || '-' }}</el-descriptions-item>
          <el-descriptions-item label="公告类型">{{ formatNoticeType(notice.noticeType) }}</el-descriptions-item>
          <el-descriptions-item label="状态"><BidOpsStatusTag :value="notice.status" /></el-descriptions-item>
          <el-descriptions-item label="采购编号">{{ notice.projectCode || '-' }}</el-descriptions-item>
          <el-descriptions-item label="采购人">{{ notice.buyerName || '-' }}</el-descriptions-item>
          <el-descriptions-item label="地区">{{ notice.region || '-' }}</el-descriptions-item>
          <el-descriptions-item label="预算金额">{{ formatMoney(notice.budgetAmount) }}</el-descriptions-item>
          <el-descriptions-item label="发布时间">{{ formatDateTime(notice.publishTime) }}</el-descriptions-item>
          <el-descriptions-item label="投标截止">{{ formatDateTime(notice.bidDeadline) }}</el-descriptions-item>
          <el-descriptions-item label="RawNoticeId">
            <el-button link type="primary" :icon="Link" @click="router.push(`/bidops/crawl/raw-notices/${notice.rawNoticeId}`)">
              {{ notice.rawNoticeId }}
            </el-button>
          </el-descriptions-item>
          <el-descriptions-item label="更新时间">{{ formatDateTime(notice.updatedAt || notice.createdAt) }}</el-descriptions-item>
        </el-descriptions>
      </section>

      <section class="content-panel">
        <div class="section-heading">
          <h2>关联包件</h2>
          <span>{{ packages.length }} 个</span>
        </div>
        <DataTable :data="packages" :loading="loading" empty-text="暂无关联包件">
          <el-table-column label="分标" min-width="180" show-overflow-tooltip>
            <template #default="{ row }">{{ row.lotNo || '-' }} / {{ row.lotName || '-' }}</template>
          </el-table-column>
          <el-table-column label="包件" min-width="240" show-overflow-tooltip>
            <template #default="{ row }">{{ formatPackageNo(row.packageNo) }} / {{ row.packageName || '-' }}</template>
          </el-table-column>
          <el-table-column label="品类" width="130">
            <template #default="{ row }">{{ formatCategory(row.category) }}</template>
          </el-table-column>
          <el-table-column label="预算" width="140" align="right">
            <template #default="{ row }">{{ formatMoney(row.budgetAmount) }}</template>
          </el-table-column>
          <el-table-column label="最高限价" width="140" align="right">
            <template #default="{ row }">{{ formatMoney(row.maxPrice) }}</template>
          </el-table-column>
          <el-table-column label="状态" width="120">
            <template #default="{ row }"><BidOpsStatusTag :value="row.status" /></template>
          </el-table-column>
          <el-table-column label="操作" width="110" fixed="right">
            <template #default="{ row }">
              <el-button size="small" @click="router.push(`/bidops/packages/${row.id}`)">详情</el-button>
            </template>
          </el-table-column>
        </DataTable>
      </section>
    </template>
  </PageContainer>
</template>

<style scoped>
.notice-summary,
.content-panel {
  margin-bottom: 16px;
  padding: 16px;
  border: 1px solid #e4e7ed;
  border-radius: 6px;
  background: #fff;
}

.notice-summary {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
}

.notice-summary h2,
.content-panel h2 {
  margin: 0;
  color: #1f2d3d;
  font-size: 16px;
  font-weight: 600;
}

.notice-summary p {
  margin: 6px 0 0;
  color: #687385;
  font-size: 13px;
}

.summary-tags,
.section-heading {
  display: flex;
  align-items: center;
  gap: 8px;
}

.summary-tags {
  flex-wrap: wrap;
  justify-content: flex-end;
}

.section-heading {
  justify-content: space-between;
  margin-bottom: 12px;
}

.section-heading span {
  color: #687385;
  font-size: 13px;
}

@media (max-width: 720px) {
  .notice-summary {
    flex-direction: column;
  }

  .summary-tags {
    justify-content: flex-start;
  }
}
</style>
