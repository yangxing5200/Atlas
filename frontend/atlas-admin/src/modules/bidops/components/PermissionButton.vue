<script setup lang="ts">
import { useAttrs } from 'vue'
import { usePermission } from '@/shared/composables/usePermission'

defineOptions({
  inheritAttrs: false,
})

const props = defineProps<{
  permission?: string
  permissionsAny?: string[]
}>()

const attrs = useAttrs()
const { visible } = usePermission(props.permission, props.permissionsAny)
</script>

<template>
  <el-button v-if="visible" v-bind="attrs">
    <slot />
  </el-button>
</template>
