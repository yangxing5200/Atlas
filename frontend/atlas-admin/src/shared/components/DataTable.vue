<script setup lang="ts">
import EmptyState from './EmptyState.vue'

defineProps<{
  data: unknown[]
  loading?: boolean
  emptyText?: string
}>()
</script>

<template>
  <el-table :data="data" border stripe v-loading="loading" class="data-table">
    <slot />
    <template #empty>
      <EmptyState :description="emptyText" />
    </template>
  </el-table>
</template>

<style scoped>
.data-table {
  width: 100%;
  border-radius: 8px;
  overflow: hidden;
}
</style>
