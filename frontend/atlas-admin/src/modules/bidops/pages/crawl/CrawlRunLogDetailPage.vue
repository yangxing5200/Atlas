<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Back, Monitor } from '@element-plus/icons-vue'
import { crawlRunLogsApi } from '@/api/bidops/crawlRunLogs.api'
import PageContainer from '@/shared/components/PageContainer.vue'
import { formatDateTime } from '@/shared/utils/date'
import BidOpsStatusTag from '../../components/BidOpsStatusTag.vue'
import type { CrawlRunLogDto } from '../../types'

const route = useRoute()
const router = useRouter()
const loading = ref(false)
const log = ref<CrawlRunLogDto | null>(null)

async function loadData() {
  loading.value = true
  try {
    log.value = await crawlRunLogsApi.get(String(route.params.id || ''))
  } catch {
    log.value = null
  } finally {
    loading.value = false
  }
}

function formatDuration(value?: number | null) {
  if (!value || value <= 0) return '-'
  if (value < 1000) return `${value} ms`
  return `${(value / 1000).toFixed(1)} s`
}

onMounted(loadData)
</script>

<template>
  <PageContainer title="采集运行日志详情" description="查看单条采集运行日志的关联对象、状态和完整消息。">
    <template #actions>
      <el-button :icon="Back" @click="router.push('/bidops/intelligence/run-logs')">返回列表</el-button>
      <el-button
        v-if="log?.backgroundJobId"
        :icon="Monitor"
        type="primary"
        @click="router.push(`/ops/jobs/${log.backgroundJobId}`)"
      >
        后台任务
      </el-button>
    </template>

    <el-skeleton v-if="loading" :rows="8" animated />
    <el-empty v-else-if="!log" description="运行日志不存在或无权访问" />
    <template v-else>
      <div class="content-panel">
        <el-descriptions :column="2" border>
          <el-descriptions-item label="日志 ID">{{ log.id }}</el-descriptions-item>
          <el-descriptions-item label="状态"><BidOpsStatusTag :value="log.status" /></el-descriptions-item>
          <el-descriptions-item label="操作">{{ log.operation || '-' }}</el-descriptions-item>
          <el-descriptions-item label="耗时">{{ formatDuration(log.durationMs) }}</el-descriptions-item>
          <el-descriptions-item label="来源 ID">{{ log.sourceId || '-' }}</el-descriptions-item>
          <el-descriptions-item label="栏目 ID">{{ log.channelId || '-' }}</el-descriptions-item>
          <el-descriptions-item label="后台任务 ID">
            <el-link
              v-if="log.backgroundJobId"
              type="primary"
              :underline="false"
              @click="router.push(`/ops/jobs/${log.backgroundJobId}`)"
            >
              {{ log.backgroundJobId }}
            </el-link>
            <span v-else>-</span>
          </el-descriptions-item>
          <el-descriptions-item label="记录时间">{{ formatDateTime(log.createdAt) }}</el-descriptions-item>
          <el-descriptions-item label="更新时间">{{ formatDateTime(log.updatedAt) }}</el-descriptions-item>
        </el-descriptions>
      </div>

      <div class="content-panel detail-section">
        <h2>运行消息</h2>
        <pre>{{ log.message || '-' }}</pre>
      </div>
    </template>
  </PageContainer>
</template>

<style scoped>
.detail-section {
  margin-top: 16px;
}

pre {
  overflow: auto;
  min-height: 90px;
  max-height: 420px;
  margin: 0;
  padding: 14px;
  border: 1px solid var(--el-border-color-light);
  border-radius: 6px;
  background: var(--el-fill-color-lighter);
  color: var(--el-text-color-primary);
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 13px;
  line-height: 1.65;
  white-space: pre-wrap;
  word-break: break-word;
}
</style>
