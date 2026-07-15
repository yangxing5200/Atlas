<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ArrowLeft, Box, DataAnalysis, Document, Edit, Link } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { lifecycleApi } from '@/api/bidops/lifecycle.api'
import { noticesApi } from '@/api/bidops/notices.api'
import { packagesApi } from '@/api/bidops/packages.api'
import DataTable from '@/shared/components/DataTable.vue'
import FormDrawer from '@/shared/components/FormDrawer.vue'
import PageContainer from '@/shared/components/PageContainer.vue'
import { usePermission } from '@/shared/composables/usePermission'
import { useRequest } from '@/shared/composables/useRequest'
import { formatDateTime } from '@/shared/utils/date'
import { formatMoney } from '@/shared/utils/money'
import BidOpsStatusTag from '../../components/BidOpsStatusTag.vue'
import PermissionButton from '../../components/PermissionButton.vue'
import { BIDOPS_PERMISSIONS } from '../../constants'
import type { NoticeDto, TenderPackageDto, UpdateNoticeRequest } from '../../types'
import { formatCategory, formatNoticeType, formatPackageNo, isResultNoticeType, noticeTypeOptions } from '../../utils/display'

const route = useRoute()
const router = useRouter()
const { visible: canAnalyzeLifecycle } = usePermission(BIDOPS_PERMISSIONS.CRAWL_IMPORT)
const loading = ref(false)
const analyzeLoading = ref(false)
const editDrawerOpen = ref(false)
const notice = ref<NoticeDto | null>(null)
const packages = ref<TenderPackageDto[]>([])
const noticeId = computed(() => String(route.params.id || ''))
const pageTitle = computed(() => notice.value?.title || '公告详情')
const canAnalyze = computed(() =>
  canAnalyzeLifecycle.value &&
  !!notice.value?.rawNoticeId &&
  isResultNoticeType(notice.value.noticeType, notice.value.title),
)
const editRequest = useRequest()

const editForm = reactive<UpdateNoticeRequest>({
  title: '',
  noticeType: 'TenderAnnouncement',
  projectName: '',
  projectCode: '',
  buyerName: '',
  agencyName: '',
  region: '',
  budgetAmount: null,
  publishTime: null,
  signupDeadline: null,
  bidDeadline: null,
  openBidTime: null,
})

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
    ElMessage.success(job.alreadyExists ? `闭环分析任务已在队列中：${job.jobId}` : `闭环分析任务已提交：${job.jobId}`)
    await router.push(`/bidops/outcomes?rawNoticeId=${rawNoticeId}`)
  } finally {
    analyzeLoading.value = false
  }
}

function syncEditForm() {
  if (!notice.value) return

  Object.assign(editForm, {
    title: notice.value.title,
    noticeType: notice.value.noticeType,
    projectName: notice.value.projectName,
    projectCode: notice.value.projectCode,
    buyerName: notice.value.buyerName,
    agencyName: notice.value.agencyName,
    region: notice.value.region,
    budgetAmount: notice.value.budgetAmount ?? null,
    publishTime: notice.value.publishTime ?? null,
    signupDeadline: notice.value.signupDeadline ?? null,
    bidDeadline: notice.value.bidDeadline ?? null,
    openBidTime: notice.value.openBidTime ?? null,
  })
}

function openEdit() {
  syncEditForm()
  editDrawerOpen.value = true
}

async function submitEdit() {
  if (!editForm.title.trim() || !editForm.noticeType.trim() || !editForm.projectName.trim()) {
    ElMessage.warning('公告标题、公告类型和项目名称不能为空')
    return
  }

  await editRequest.run(async () => {
    notice.value = await noticesApi.update(noticeId.value, editForm)
    ElMessage.success('公告信息已更新')
    editDrawerOpen.value = false
    syncEditForm()
  })
}

onMounted(loadData)
</script>

<template>
  <PageContainer :title="pageTitle" description="查看并维护正式公告基础信息、关联原始公告和包件。">
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
      <PermissionButton
        v-if="notice"
        :permission="BIDOPS_PERMISSIONS.BUSINESS_MANAGE"
        :icon="Edit"
        @click="openEdit"
      >
        编辑公告
      </PermissionButton>
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
          <el-descriptions-item label="项目名称" :span="2">{{ notice.projectName || '-' }}</el-descriptions-item>
          <el-descriptions-item label="采购编号">{{ notice.projectCode || '-' }}</el-descriptions-item>
          <el-descriptions-item label="采购人">{{ notice.buyerName || '-' }}</el-descriptions-item>
          <el-descriptions-item label="代理机构">{{ notice.agencyName || '-' }}</el-descriptions-item>
          <el-descriptions-item label="地区">{{ notice.region || '-' }}</el-descriptions-item>
          <el-descriptions-item label="预算金额">{{ formatMoney(notice.budgetAmount) }}</el-descriptions-item>
          <el-descriptions-item label="发布时间">{{ formatDateTime(notice.publishTime) }}</el-descriptions-item>
          <el-descriptions-item label="报名截止">{{ formatDateTime(notice.signupDeadline) }}</el-descriptions-item>
          <el-descriptions-item label="投标截止">{{ formatDateTime(notice.bidDeadline) }}</el-descriptions-item>
          <el-descriptions-item label="开标时间">{{ formatDateTime(notice.openBidTime) }}</el-descriptions-item>
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

      <FormDrawer
        v-model="editDrawerOpen"
        title="编辑公告信息"
        width="760px"
        :submitting="editRequest.loading"
        @submit="submitEdit"
      >
        <el-alert
          title="本次修改只更新正式公告，不会改写原始公告和审核暂存数据。"
          type="info"
          :closable="false"
          show-icon
          class="edit-tip"
        />
        <el-form :model="editForm" label-width="110px" class="form-grid">
          <el-form-item label="公告标题" required class="full-row">
            <el-input v-model.trim="editForm.title" maxlength="500" show-word-limit />
          </el-form-item>
          <el-form-item label="公告类型" required>
            <el-select v-model="editForm.noticeType" filterable style="width: 100%">
              <el-option v-for="item in noticeTypeOptions" :key="item.value" :label="item.label" :value="item.value" />
            </el-select>
          </el-form-item>
          <el-form-item label="项目名称" required>
            <el-input v-model.trim="editForm.projectName" maxlength="500" />
          </el-form-item>
          <el-form-item label="采购编号">
            <el-input v-model.trim="editForm.projectCode" maxlength="128" />
          </el-form-item>
          <el-form-item label="地区">
            <el-input v-model.trim="editForm.region" maxlength="128" />
          </el-form-item>
          <el-form-item label="采购人">
            <el-input v-model.trim="editForm.buyerName" maxlength="300" />
          </el-form-item>
          <el-form-item label="代理机构">
            <el-input v-model.trim="editForm.agencyName" maxlength="300" />
          </el-form-item>
          <el-form-item label="预算金额">
            <el-input-number v-model="editForm.budgetAmount" :min="0" :precision="2" style="width: 100%" />
          </el-form-item>
          <el-form-item label="发布时间">
            <el-date-picker v-model="editForm.publishTime" type="datetime" value-format="YYYY-MM-DDTHH:mm:ss" clearable style="width: 100%" />
          </el-form-item>
          <el-form-item label="报名截止">
            <el-date-picker v-model="editForm.signupDeadline" type="datetime" value-format="YYYY-MM-DDTHH:mm:ss" clearable style="width: 100%" />
          </el-form-item>
          <el-form-item label="投标截止">
            <el-date-picker v-model="editForm.bidDeadline" type="datetime" value-format="YYYY-MM-DDTHH:mm:ss" clearable style="width: 100%" />
          </el-form-item>
          <el-form-item label="开标时间">
            <el-date-picker v-model="editForm.openBidTime" type="datetime" value-format="YYYY-MM-DDTHH:mm:ss" clearable style="width: 100%" />
          </el-form-item>
        </el-form>
      </FormDrawer>
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

.edit-tip {
  margin-bottom: 18px;
}

.form-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0 16px;
}

.full-row {
  grid-column: 1 / -1;
}

@media (max-width: 720px) {
  .notice-summary {
    flex-direction: column;
  }

  .summary-tags {
    justify-content: flex-start;
  }

  .form-grid {
    grid-template-columns: 1fr;
  }

  .full-row {
    grid-column: auto;
  }
}
</style>
