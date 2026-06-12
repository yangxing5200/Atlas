<script setup lang="ts">
import { computed } from 'vue'
import dayjs from 'dayjs'

const props = defineProps<{
  value?: string | null
}>()

const label = computed(() => {
  if (!props.value) return '-'
  const deadline = dayjs(props.value)
  const hours = deadline.diff(dayjs(), 'hour')
  if (hours < 0) return '已截止'
  if (hours < 24) return `${hours} 小时`
  return `${Math.ceil(hours / 24)} 天`
})

const type = computed(() => {
  if (!props.value) return 'info'
  const hours = dayjs(props.value).diff(dayjs(), 'hour')
  if (hours < 0) return 'danger'
  if (hours <= 72) return 'warning'
  return 'success'
})
</script>

<template>
  <el-tag :type="type" effect="light">{{ label }}</el-tag>
</template>
