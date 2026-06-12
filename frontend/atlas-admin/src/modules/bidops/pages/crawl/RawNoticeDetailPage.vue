<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { ElMessage } from 'element-plus'
import { RefreshRight } from '@element-plus/icons-vue'
import { rawNoticesApi } from '@/api/bidops/rawNotices.api'
import PageContainer from '@/shared/components/PageContainer.vue'
import { formatDateTime } from '@/shared/utils/date'
import PermissionButton from '../../components/PermissionButton.vue'
import BidOpsStatusTag from '../../components/BidOpsStatusTag.vue'
import RawAttachmentTable from '../../components/RawAttachmentTable.vue'
import RawNoticePipelinePanel from '../../components/RawNoticePipelinePanel.vue'
import RawNoticePreview from '../../components/RawNoticePreview.vue'
import { BIDOPS_PERMISSIONS } from '../../constants'
import type { RawAttachmentDto, RawNoticeDto, RawNoticePipelineDto } from '../../types'
import { formatNoticeType } from '../../utils/display'

const route = useRoute()
const loading = ref(false)
const rawNotice = ref<RawNoticeDto | null>(null)
const attachments = ref<RawAttachmentDto[]>([])
const pipeline = ref<RawNoticePipelineDto | null>(null)
const reparseLoading = ref(false)

async function loadData() {
  loading.value = true
  try {
    const id = String(route.params.id || '')
    const [raw, rawAttachments] = await Promise.all([
      rawNoticesApi.get(id),
      rawNoticesApi.attachments(id),
    ])
    rawNotice.value = raw
    attachments.value = rawAttachments
    pipeline.value = await rawNoticesApi.pipeline(id).catch(() => null)
  } catch {
    rawNotice.value = null
    attachments.value = []
    pipeline.value = null
  } finally {
    loading.value = false
  }
}

function isApprovedRawNotice(status: RawNoticeDto['status']) {
  return status === 3 || status === '3' || status === 'Approved'
}

async function reparseRawNotice() {
  if (!rawNotice.value) return

  reparseLoading.value = true
  try {
    const job = await rawNoticesApi.reparse(rawNotice.value.id, { reason: 'Raw notice detail page' })
    ElMessage.success(job.alreadyExists ? `重解析任务已存在：${job.jobId}` : `已提交重解析任务：${job.jobId}`)
    await loadData()
  } finally {
    reparseLoading.value = false
  }
}

onMounted(loadData)
</script>

<template>
  <PageContainer title="原始公告详情" description="查看公告原文、来源地址和采集到的公开附件。">
    <template #actions>
      <PermissionButton
        v-if="rawNotice && !isApprovedRawNotice(rawNotice.status)"
        :icon="RefreshRight"
        :loading="reparseLoading"
        :permission="BIDOPS_PERMISSIONS.REVIEW_APPROVE"
        @click="reparseRawNotice"
      >
        重解析
      </PermissionButton>
    </template>

    <el-skeleton v-if="loading" :rows="8" animated />
    <template v-else-if="rawNotice">
      <div class="content-panel">
        <el-descriptions :column="2" border>
          <el-descriptions-item label="标题">{{ rawNotice.title }}</el-descriptions-item>
          <el-descriptions-item label="来源 ID">{{ rawNotice.sourceId }}</el-descriptions-item>
          <el-descriptions-item label="栏目 ID">{{ rawNotice.channelId || '-' }}</el-descriptions-item>
          <el-descriptions-item label="详情 URL">
            <el-link :href="rawNotice.detailUrl" target="_blank" type="primary">{{ rawNotice.detailUrl }}</el-link>
          </el-descriptions-item>
          <el-descriptions-item label="公告类型">{{ formatNoticeType(rawNotice.noticeType) }}</el-descriptions-item>
          <el-descriptions-item label="发布时间">{{ formatDateTime(rawNotice.publishTime) }}</el-descriptions-item>
          <el-descriptions-item label="抓取时间">{{ formatDateTime(rawNotice.fetchTime) }}</el-descriptions-item>
          <el-descriptions-item label="内容哈希">{{ rawNotice.contentHash }}</el-descriptions-item>
          <el-descriptions-item label="状态"><BidOpsStatusTag :value="rawNotice.status" kind="rawNotice" /></el-descriptions-item>
          <el-descriptions-item label="错误信息">{{ rawNotice.lastError || '-' }}</el-descriptions-item>
        </el-descriptions>
      </div>

      <div class="content-panel detail-section">
        <h2>处理流水线</h2>
        <RawNoticePipelinePanel :pipeline="pipeline" />
      </div>

      <div class="content-panel detail-section">
        <h2>文本预览</h2>
        <RawNoticePreview :text="rawNotice.textContent || rawNotice.textPreview" />
      </div>

      <div class="content-panel detail-section">
        <h2>公开附件</h2>
        <RawAttachmentTable :attachments="attachments" :raw-notice-id="rawNotice.id" />
      </div>
    </template>
  </PageContainer>
</template>

<style scoped>
.detail-section {
  margin-top: 16px;
}

</style>
