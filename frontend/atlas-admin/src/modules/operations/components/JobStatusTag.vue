<script setup lang="ts">
import type { BackgroundJobStatus } from '../types'
import { formatJobStatus, jobStatusTagType } from '../utils/display'

defineProps<{
  status?: BackgroundJobStatus | null
  statusName?: string | null
}>()
</script>

<template>
  <el-tag :type="jobStatusTagType(status, statusName)" effect="light">
    {{ formatJobStatus(status, statusName) }}
  </el-tag>
</template>
