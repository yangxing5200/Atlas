<script setup lang="ts">
import { computed } from 'vue'
import { CircleCheck, CircleClose, Clock, Minus, MoreFilled } from '@element-plus/icons-vue'
import { formatDateTime } from '@/shared/utils/date'
import BidOpsStatusTag from './BidOpsStatusTag.vue'
import type { RawNoticePipelineDto, RawNoticePipelineStepDto } from '../types'

const props = defineProps<{
  pipeline?: RawNoticePipelineDto | null
}>()

const summaryItems = computed(() => {
  const pipeline = props.pipeline
  if (!pipeline) return []

  return [
    {
      label: '附件文本',
      value: `${pipeline.attachmentTextExtractedCount}/${pipeline.attachmentCount}`,
    },
    {
      label: '已下载附件',
      value: `${pipeline.attachmentDownloadedCount}/${pipeline.attachmentCount}`,
    },
    {
      label: '包件',
      value: String(pipeline.packageCount),
    },
    {
      label: '要求项',
      value: String(pipeline.requirementCount),
    },
  ]
})

function iconFor(status: string) {
  if (status === 'Completed') return CircleCheck
  if (status === 'Failed') return CircleClose
  if (status === 'Skipped') return Minus
  if (status === 'Pending') return Clock
  return MoreFilled
}

function markerClass(step: RawNoticePipelineStepDto) {
  return {
    'is-completed': step.status === 'Completed',
    'is-failed': step.status === 'Failed',
    'is-skipped': step.status === 'Skipped',
    'is-pending': step.status === 'Pending',
  }
}
</script>

<template>
  <el-empty v-if="!pipeline" description="暂无流水线数据" />
  <div v-else class="pipeline-panel">
    <div class="pipeline-summary">
      <div v-for="item in summaryItems" :key="item.label" class="summary-item">
        <span>{{ item.label }}</span>
        <strong>{{ item.value }}</strong>
      </div>
    </div>

    <div class="pipeline-steps">
      <div v-for="step in pipeline.steps" :key="step.code" class="pipeline-step">
        <div class="step-marker" :class="markerClass(step)">
          <el-icon><component :is="iconFor(step.status)" /></el-icon>
        </div>
        <div class="step-body">
          <div class="step-title-row">
            <h3>{{ step.title }}</h3>
            <BidOpsStatusTag :value="step.status" />
          </div>
          <p>{{ step.description }}</p>
          <div class="step-meta">
            <span v-if="step.occurredAt">时间：{{ formatDateTime(step.occurredAt) }}</span>
            <span v-if="step.totalCount > 0">总数：{{ step.totalCount }}</span>
            <span v-if="step.succeededCount > 0">成功：{{ step.succeededCount }}</span>
            <span v-if="step.pendingCount > 0">待处理：{{ step.pendingCount }}</span>
            <span v-if="step.failedCount > 0">失败：{{ step.failedCount }}</span>
          </div>
          <el-alert v-if="step.error" :title="step.error" type="error" :closable="false" show-icon />
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.pipeline-panel {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.pipeline-summary {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
  gap: 10px;
}

.summary-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  min-height: 44px;
  padding: 10px 12px;
  border: 1px solid var(--el-border-color-light);
  border-radius: 6px;
  background: var(--el-fill-color-blank);
}

.summary-item span {
  color: var(--el-text-color-secondary);
  font-size: 13px;
}

.summary-item strong {
  color: var(--el-text-color-primary);
  font-size: 18px;
  font-weight: 650;
}

.pipeline-steps {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.pipeline-step {
  display: grid;
  grid-template-columns: 34px minmax(0, 1fr);
  gap: 12px;
}

.step-marker {
  width: 32px;
  height: 32px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  color: var(--el-text-color-secondary);
  background: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color);
}

.step-marker.is-completed {
  color: var(--el-color-success);
  background: var(--el-color-success-light-9);
  border-color: var(--el-color-success-light-5);
}

.step-marker.is-failed {
  color: var(--el-color-danger);
  background: var(--el-color-danger-light-9);
  border-color: var(--el-color-danger-light-5);
}

.step-marker.is-skipped {
  color: var(--el-color-info);
  background: var(--el-fill-color);
}

.step-marker.is-pending {
  color: var(--el-color-warning);
  background: var(--el-color-warning-light-9);
  border-color: var(--el-color-warning-light-5);
}

.step-body {
  min-width: 0;
  padding-bottom: 14px;
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.pipeline-step:last-child .step-body {
  padding-bottom: 0;
  border-bottom: 0;
}

.step-title-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
  margin-bottom: 6px;
}

.step-title-row h3 {
  margin: 0;
  font-size: 15px;
  font-weight: 650;
  color: var(--el-text-color-primary);
}

.step-body p {
  margin: 0;
  color: var(--el-text-color-regular);
  line-height: 1.6;
}

.step-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  margin-top: 8px;
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

.step-meta span {
  white-space: nowrap;
}
</style>
