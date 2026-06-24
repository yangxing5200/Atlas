<script setup lang="ts">
import { ref } from 'vue'
import { ElMessage } from 'element-plus'
import { Document, Download, Tickets, View } from '@element-plus/icons-vue'
import { rawNoticesApi } from '@/api/bidops/rawNotices.api'
import BidOpsStatusTag from './BidOpsStatusTag.vue'
import RawNoticePreview from './RawNoticePreview.vue'
import type { BidOpsId, RawAttachmentDto } from '../types'

const props = defineProps<{
  attachments: RawAttachmentDto[]
  loading?: boolean
  rawNoticeId?: BidOpsId | null
}>()

const textDrawerVisible = ref(false)
const textLoading = ref(false)
const textTitle = ref('')
const textContent = ref('')
const fileLoadingKey = ref('')

function formatFileSize(value?: number | null) {
  if (!value || value <= 0) return '-'
  if (value < 1024) return `${value} B`
  if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} KB`
  return `${(value / 1024 / 1024).toFixed(1)} MB`
}

function fileTypeLabel(value?: string | null) {
  return value?.trim().toUpperCase() || '文件'
}

async function viewExtractedText(row: RawAttachmentDto) {
  if (!props.rawNoticeId) return

  textDrawerVisible.value = true
  textLoading.value = true
  textTitle.value = row.fileName || '附件提取文本'
  textContent.value = ''
  try {
    const result = await rawNoticesApi.attachmentText(props.rawNoticeId, row.id)
    textTitle.value = result.fileName || textTitle.value
    textContent.value = result.textContent || ''
    if (!textContent.value) {
      ElMessage.warning('该附件暂未提取到可查看文本')
    }
  } finally {
    textLoading.value = false
  }
}

function fileActionKey(row: RawAttachmentDto, action: 'preview' | 'download') {
  return `${row.id}:${action}`
}

async function openStoredFile(row: RawAttachmentDto, download: boolean) {
  if (!props.rawNoticeId) return

  const action = download ? 'download' : 'preview'
  const loadingKey = fileActionKey(row, action)
  fileLoadingKey.value = loadingKey

  const previewWindow = download ? null : window.open('', '_blank', 'noopener,noreferrer')
  try {
    const blob = await rawNoticesApi.attachmentFile(props.rawNoticeId, row.id, download)
    const objectUrl = URL.createObjectURL(blob)
    if (download) {
      const link = document.createElement('a')
      link.href = objectUrl
      link.download = row.fileName || `raw-attachment-${row.id}`
      document.body.appendChild(link)
      link.click()
      link.remove()
      window.setTimeout(() => URL.revokeObjectURL(objectUrl), 5000)
      return
    }

    if (previewWindow) {
      previewWindow.location.href = objectUrl
      window.setTimeout(() => URL.revokeObjectURL(objectUrl), 60_000)
    } else {
      window.open(objectUrl, '_blank', 'noopener,noreferrer')
      window.setTimeout(() => URL.revokeObjectURL(objectUrl), 60_000)
    }
  } catch {
    previewWindow?.close()
  } finally {
    fileLoadingKey.value = ''
  }
}
</script>

<template>
  <el-skeleton v-if="loading" :rows="3" animated />
  <el-empty v-else-if="attachments.length === 0" description="未采集到公开附件" />
  <el-table v-else :data="attachments" size="small" border>
    <el-table-column label="附件名称" min-width="260">
      <template #default="{ row }">
        <div class="attachment-name">
          <el-icon><Document /></el-icon>
          <span :title="row.fileName">{{ row.fileName || '未命名附件' }}</span>
        </div>
      </template>
    </el-table-column>
    <el-table-column label="类型" width="90">
      <template #default="{ row }">
        <el-tag effect="light">{{ fileTypeLabel(row.fileType) }}</el-tag>
      </template>
    </el-table-column>
    <el-table-column label="大小" width="110">
      <template #default="{ row }">{{ formatFileSize(row.fileSize) }}</template>
    </el-table-column>
    <el-table-column label="下载状态" width="110">
      <template #default="{ row }">
        <BidOpsStatusTag :value="row.downloadStatus" kind="download" />
      </template>
    </el-table-column>
    <el-table-column label="文本提取" width="110">
      <template #default="{ row }">
        <BidOpsStatusTag :value="row.textExtractStatus" kind="textExtract" />
      </template>
    </el-table-column>
    <el-table-column label="操作" width="260" fixed="right">
      <template #default="{ row }">
        <div class="attachment-actions">
          <el-link
            v-if="row.hasLocalFile && rawNoticeId"
            type="primary"
            :underline="false"
            class="open-link"
            :disabled="fileLoadingKey === fileActionKey(row, 'preview')"
            @click="openStoredFile(row, false)"
          >
            <el-icon><View /></el-icon>
            <span>{{ fileLoadingKey === fileActionKey(row, 'preview') ? '打开中' : '预览' }}</span>
          </el-link>
          <el-link
            v-if="row.hasLocalFile && rawNoticeId"
            type="primary"
            :underline="false"
            class="open-link"
            :disabled="fileLoadingKey === fileActionKey(row, 'download')"
            @click="openStoredFile(row, true)"
          >
            <el-icon><Download /></el-icon>
            <span>{{ fileLoadingKey === fileActionKey(row, 'download') ? '下载中' : '下载' }}</span>
          </el-link>
          <el-link
            v-if="row.fileUrl"
            :href="row.fileUrl"
            target="_blank"
            type="primary"
            :underline="false"
            class="open-link"
          >
            <el-icon><View /></el-icon>
            <span>源文件</span>
          </el-link>
          <el-link
            v-if="row.hasExtractedText && rawNoticeId"
            type="primary"
            :underline="false"
            class="open-link"
            @click="viewExtractedText(row)"
          >
            <el-icon><Tickets /></el-icon>
            <span>查看文本</span>
          </el-link>
          <span v-if="!row.fileUrl && !row.hasLocalFile && !row.hasExtractedText">-</span>
        </div>
      </template>
    </el-table-column>
  </el-table>

  <el-drawer v-model="textDrawerVisible" :title="textTitle || '附件提取文本'" size="52%">
    <el-skeleton v-if="textLoading" :rows="8" animated />
    <RawNoticePreview v-else :text="textContent" variant="extracted" />
  </el-drawer>
</template>

<style scoped>
.attachment-name,
.open-link {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  min-width: 0;
}

.attachment-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
}

.attachment-name span {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
