<script setup lang="ts">
import { computed } from 'vue'
import { statusTagType } from '@/shared/utils/enum'
import { formatCommonStatus } from '../utils/display'

const props = defineProps<{
  value: unknown
  kind?: 'rawNotice' | 'review' | 'reviewTask' | 'download' | 'textExtract'
}>()

const statusLabels = {
  rawNotice: ['新建', '待解析', '待审核', '已入库', '已忽略', '失败'],
  review: ['待复核', '复核中', '已通过', '已忽略', '需重解析'],
  reviewTask: ['待审核', '审核中', '已通过', '已忽略', '已合并', '需重解析'],
  download: ['待下载', '下载成功', '下载失败', '已跳过'],
  textExtract: ['待提取', '提取成功', '提取失败', '已跳过'],
}

const statusKeys = {
  rawNotice: ['New', 'ParseQueued', 'ReviewPending', 'Approved', 'Ignored', 'Failed'],
  review: ['Pending', 'InReview', 'Approved', 'Ignored', 'ReparseRequired'],
  reviewTask: ['Pending', 'InReview', 'Approved', 'Ignored', 'Merged', 'ReparseRequired'],
  download: ['Pending', 'Succeeded', 'Failed', 'Skipped'],
  textExtract: ['Pending', 'Succeeded', 'Failed', 'Skipped'],
}

const label = computed(() => {
  if (props.kind && typeof props.value === 'number') {
    return statusLabels[props.kind][props.value] || String(props.value)
  }

  if (props.kind && typeof props.value === 'string' && /^\d+$/.test(props.value)) {
    return statusLabels[props.kind][Number(props.value)] || props.value
  }

  return formatCommonStatus(props.value)
})

const tagValue = computed(() => {
  if (props.kind && typeof props.value === 'number') {
    return statusKeys[props.kind][props.value] || String(props.value)
  }

  if (props.kind && typeof props.value === 'string' && /^\d+$/.test(props.value)) {
    return statusKeys[props.kind][Number(props.value)] || props.value
  }

  return props.value
})
</script>

<template>
  <el-tag :type="statusTagType(tagValue)" effect="light">
    {{ label }}
  </el-tag>
</template>
