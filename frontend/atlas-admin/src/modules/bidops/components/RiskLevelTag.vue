<script setup lang="ts">
import type { ElementTagType } from '@/shared/utils/enum'
import { formatRiskLevel } from '../utils/display'

const props = defineProps<{
  value?: string | null
}>()

function tagType(value?: string | null): ElementTagType {
  const normalized = String(value || '').toLowerCase()
  if (['high', '高', 'danger'].includes(normalized)) return 'danger'
  if (['medium', '中', 'warning'].includes(normalized)) return 'warning'
  if (['low', '低', 'success'].includes(normalized)) return 'success'
  return 'info'
}
</script>

<template>
  <el-tag :type="tagType(props.value)" effect="light">
    {{ formatRiskLevel(props.value) }}
  </el-tag>
</template>
