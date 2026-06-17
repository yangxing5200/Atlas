<script setup lang="ts">
import type { BackgroundJobStatus } from '../types'
import { formatJobStatus, jobStatusTagType } from '../utils/display'

defineProps<{
  status?: BackgroundJobStatus | null
  statusName?: string | null
  cancellationRequested?: boolean | null
}>()
</script>

<template>
  <el-tag :type="jobStatusTagType(status, statusName, cancellationRequested)" effect="light">
    {{ formatJobStatus(status, statusName, cancellationRequested) }}
  </el-tag>
</template>
