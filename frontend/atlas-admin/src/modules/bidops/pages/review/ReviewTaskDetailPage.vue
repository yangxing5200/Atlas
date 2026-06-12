<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useRoute, useRouter } from 'vue-router'
import { reviewTasksApi } from '@/api/bidops/reviewTasks.api'
import PageContainer from '@/shared/components/PageContainer.vue'
import { useRequest } from '@/shared/composables/useRequest'
import { formatDateTime } from '@/shared/utils/date'
import { formatMoney } from '@/shared/utils/money'
import BidOpsStatusTag from '../../components/BidOpsStatusTag.vue'
import RawAttachmentTable from '../../components/RawAttachmentTable.vue'
import RawNoticePreview from '../../components/RawNoticePreview.vue'
import RequirementTable from '../../components/RequirementTable.vue'
import ReviewDecisionPanel from '../../components/ReviewDecisionPanel.vue'
import type { PackageStagingDto, RequirementStagingDto, ReviewTaskDetailDto } from '../../types'
import { formatCategory, formatNoticeType, formatPackageNo } from '../../utils/display'

const route = useRoute()
const router = useRouter()
const detail = ref<ReviewTaskDetailDto | null>(null)
const loading = ref(false)
const decisionRequest = useRequest()
const taskId = computed(() => String(route.params.id || ''))
const task = computed(() => detail.value?.task)
const notice = computed(() => detail.value?.notice)
const rawNotice = computed(() => detail.value?.rawNotice)
const rawText = computed(() => rawNotice.value?.textContent || rawNotice.value?.textPreview || '')
const packages = computed(() => detail.value?.packages || [])
const attachments = computed(() => detail.value?.attachments || [])
const allRequirements = computed(() => packages.value.flatMap((pkg) => pkg.requirements || []))
const rejectRiskCount = computed(() => allRequirements.value.filter((item) => item.isRejectRisk).length)
const mandatoryCount = computed(() => allRequirements.value.filter((item) => item.isMandatory).length)

async function loadData() {
  loading.value = true
  try {
    detail.value = await reviewTasksApi.get(taskId.value)
  } catch {
    detail.value = null
  } finally {
    loading.value = false
  }
}

function confidencePercent(value?: number | null) {
  return `${Math.round(Number(value || 0) * 100)}%`
}

function projectTitle() {
  return notice.value?.projectName || task.value?.projectName || task.value?.taskTitle || '-'
}

function keyDeadline() {
  return notice.value?.bidDeadline || notice.value?.openBidTime || notice.value?.signupDeadline || notice.value?.publishTime
}

function keyDeadlineLabel() {
  if (notice.value?.bidDeadline) return '投标截止'
  if (notice.value?.openBidTime) return '开标时间'
  if (notice.value?.signupDeadline) return '报名截止'
  return '发布时间'
}

function packageTitle(pkg: PackageStagingDto) {
  return pkg.packageName || formatPackageNo(pkg.packageNo) || pkg.lotName || '未命名包件'
}

function packageRequirements(pkg: PackageStagingDto): RequirementStagingDto[] {
  return pkg.requirements || []
}

async function approve(remark: string) {
  try {
    await ElMessageBox.confirm('审核通过后将创建正式公告、包件和要求项。', '审核通过', {
      confirmButtonText: '通过',
      cancelButtonText: '取消',
      type: 'warning',
    })
    const notice = await decisionRequest.run(() => reviewTasksApi.approve(taskId.value, { remark }))
    ElMessage.success(`已生成正式公告：${notice?.id ?? '-'}`)
    await router.push('/bidops/notices')
  } catch {
    return
  }
}

async function ignore(remark: string) {
  try {
    await ElMessageBox.confirm('忽略后该任务不会进入正式业务表。', '忽略审核任务', {
      confirmButtonText: '忽略',
      cancelButtonText: '取消',
      type: 'warning',
    })
    await decisionRequest.run(() => reviewTasksApi.ignore(taskId.value, { remark }))
    ElMessage.success('审核任务已忽略')
    await router.push('/bidops/review/tasks')
  } catch {
    return
  }
}

onMounted(loadData)
</script>

<template>
  <PageContainer title="审核详情" description="核对公告原文、解析字段、包件和要求项；确认无误后才写入正式业务表。">
    <el-skeleton v-if="loading" :rows="12" animated />
    <el-empty v-else-if="!detail" description="未找到审核任务" />
    <template v-else>
      <section class="review-summary">
        <div class="summary-main">
          <h2>{{ projectTitle() }}</h2>
          <div class="summary-tags">
            <BidOpsStatusTag :value="task?.status" kind="reviewTask" />
            <el-tag effect="light">{{ formatNoticeType(notice?.noticeType || task?.noticeType) }}</el-tag>
            <el-tag v-if="notice?.region || task?.region" type="success" effect="light">{{ notice?.region || task?.region }}</el-tag>
            <el-tag v-if="rejectRiskCount > 0" type="danger" effect="light">{{ rejectRiskCount }} 条废标风险</el-tag>
          </div>
        </div>
        <div class="summary-grid">
          <div>
            <span>采购人</span>
            <strong>{{ notice?.buyerName || task?.buyerName || '-' }}</strong>
          </div>
          <div>
            <span>{{ keyDeadlineLabel() }}</span>
            <strong>{{ formatDateTime(keyDeadline()) }}</strong>
          </div>
          <div>
            <span>包件 / 要求</span>
            <strong>{{ packages.length }} / {{ allRequirements.length }}</strong>
          </div>
          <div>
            <span>强制 / 风险</span>
            <strong>{{ mandatoryCount }} / {{ rejectRiskCount }}</strong>
          </div>
          <div>
            <span>解析置信度</span>
            <strong>{{ confidencePercent(notice?.aiConfidence || task?.aiConfidence) }}</strong>
          </div>
        </div>
      </section>

      <div class="split-grid">
        <section class="content-panel">
          <div class="panel-heading">
            <h2>公告证据</h2>
            <el-link v-if="rawNotice?.detailUrl" :href="rawNotice.detailUrl" target="_blank" type="primary">打开原公告</el-link>
          </div>
          <el-descriptions :column="1" border>
            <el-descriptions-item label="原始标题">{{ rawNotice?.title || '-' }}</el-descriptions-item>
            <el-descriptions-item label="公告类型">{{ formatNoticeType(rawNotice?.noticeType) }}</el-descriptions-item>
            <el-descriptions-item label="发布时间">{{ formatDateTime(rawNotice?.publishTime) }}</el-descriptions-item>
            <el-descriptions-item label="采集时间">{{ formatDateTime(rawNotice?.fetchTime) }}</el-descriptions-item>
            <el-descriptions-item label="采集状态"><BidOpsStatusTag :value="rawNotice?.status" kind="rawNotice" /></el-descriptions-item>
            <el-descriptions-item label="错误信息">{{ rawNotice?.lastError || '-' }}</el-descriptions-item>
          </el-descriptions>

          <h2 class="section-title">公开附件</h2>
          <RawAttachmentTable :attachments="attachments" :raw-notice-id="rawNotice?.id" />

          <h2 class="section-title">公告全文</h2>
          <RawNoticePreview :text="rawText" />
        </section>

        <section class="content-panel">
          <div class="panel-heading">
            <h2>解析结果</h2>
            <span>审核后写入正式公告、包件和要求项</span>
          </div>
          <el-alert
            v-if="!notice"
            type="warning"
            show-icon
            :closable="false"
            title="该任务没有关联到暂存公告，不能直接审核入库。"
          />
          <el-descriptions v-else :column="2" border>
            <el-descriptions-item label="项目名称" :span="2">{{ notice.projectName || '-' }}</el-descriptions-item>
            <el-descriptions-item label="项目编码">{{ notice.projectCode || '-' }}</el-descriptions-item>
            <el-descriptions-item label="地区">{{ notice.region || '-' }}</el-descriptions-item>
            <el-descriptions-item label="采购人">{{ notice.buyerName || '-' }}</el-descriptions-item>
            <el-descriptions-item label="代理机构">{{ notice.agencyName || '-' }}</el-descriptions-item>
            <el-descriptions-item label="预算金额">{{ formatMoney(notice.budgetAmount) }}</el-descriptions-item>
            <el-descriptions-item label="发布时间">{{ formatDateTime(notice.publishTime) }}</el-descriptions-item>
            <el-descriptions-item label="报名截止">{{ formatDateTime(notice.signupDeadline) }}</el-descriptions-item>
            <el-descriptions-item label="投标截止">{{ formatDateTime(notice.bidDeadline) }}</el-descriptions-item>
            <el-descriptions-item label="开标时间">{{ formatDateTime(notice.openBidTime) }}</el-descriptions-item>
            <el-descriptions-item label="置信度">{{ confidencePercent(notice.aiConfidence) }}</el-descriptions-item>
            <el-descriptions-item label="复核状态"><BidOpsStatusTag :value="notice.reviewStatus" kind="review" /></el-descriptions-item>
          </el-descriptions>

          <div class="package-list">
            <el-empty v-if="packages.length === 0" description="没有解析到包件" />
            <section v-for="pkg in packages" :key="pkg.id" class="package-block">
              <div class="package-heading">
                <div>
                  <h3>{{ packageTitle(pkg) }}</h3>
                  <p>{{ pkg.lotName || '未分标段' }} · {{ formatCategory(pkg.category) }}</p>
                </div>
                <el-tag effect="light">置信度 {{ confidencePercent(pkg.aiConfidence) }}</el-tag>
              </div>
              <el-descriptions :column="2" border>
                <el-descriptions-item label="标段号">{{ pkg.lotNo || '-' }}</el-descriptions-item>
                <el-descriptions-item label="包件号">{{ formatPackageNo(pkg.packageNo) }}</el-descriptions-item>
                <el-descriptions-item label="包件名称">{{ pkg.packageName || '-' }}</el-descriptions-item>
                <el-descriptions-item label="预算金额">{{ formatMoney(pkg.budgetAmount) }}</el-descriptions-item>
                <el-descriptions-item label="复核状态"><BidOpsStatusTag :value="pkg.reviewStatus" kind="review" /></el-descriptions-item>
              </el-descriptions>
              <h3 class="requirements-title">资格 / 商务 / 风险要求</h3>
              <RequirementTable :requirements="packageRequirements(pkg)" class="requirements" />
            </section>
          </div>
        </section>
      </div>

      <ReviewDecisionPanel class="decision-panel" :submitting="decisionRequest.loading" @approve="approve" @ignore="ignore" />
    </template>
  </PageContainer>
</template>

<style scoped>
.review-summary {
  display: grid;
  gap: 14px;
  padding: 16px;
  margin-bottom: 16px;
  border: 1px solid #dce3ee;
  border-radius: 8px;
  background: #fff;
}

.summary-main {
  display: grid;
  gap: 10px;
}

.summary-main h2 {
  margin: 0;
  color: #17202a;
  font-size: 18px;
  line-height: 1.45;
}

.summary-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.summary-grid {
  display: grid;
  grid-template-columns: repeat(5, minmax(120px, 1fr));
  gap: 10px;
}

.summary-grid div {
  display: grid;
  gap: 4px;
  min-width: 0;
  padding: 10px 12px;
  border: 1px solid #e7edf5;
  border-radius: 8px;
  background: #f8fafc;
}

.summary-grid span,
.panel-heading span,
.package-heading p {
  color: #687385;
  font-size: 12px;
}

.summary-grid strong {
  overflow: hidden;
  color: #17202a;
  font-size: 14px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.split-grid {
  display: grid;
  grid-template-columns: minmax(360px, 0.95fr) minmax(460px, 1.05fr);
  gap: 16px;
  align-items: start;
}

.content-panel {
  min-width: 0;
  padding: 16px;
  border: 1px solid #dce3ee;
  border-radius: 8px;
  background: #fff;
}

.panel-heading,
.package-heading {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 12px;
}

.panel-heading h2,
.section-title,
.package-heading h3,
.requirements-title {
  margin: 0;
  color: #17202a;
}

.panel-heading h2,
.section-title {
  font-size: 16px;
}

.section-title {
  margin-top: 18px;
  margin-bottom: 10px;
}

.package-list {
  display: grid;
  gap: 16px;
  margin-top: 16px;
}

.package-block {
  min-width: 0;
  padding-top: 16px;
  border-top: 1px solid #e7edf5;
}

.package-heading h3,
.requirements-title {
  font-size: 15px;
}

.package-heading p {
  margin: 4px 0 0;
}

.requirements-title {
  margin-top: 14px;
  margin-bottom: 8px;
}

.requirements {
  margin-top: 8px;
}

.decision-panel {
  margin-top: 16px;
}

@media (max-width: 1180px) {
  .summary-grid {
    grid-template-columns: repeat(2, minmax(160px, 1fr));
  }

  .split-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 720px) {
  .summary-grid {
    grid-template-columns: 1fr;
  }

  .content-panel {
    padding: 12px;
  }
}
</style>
