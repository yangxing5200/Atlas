<script setup lang="ts">
import { DocumentRemove } from '@element-plus/icons-vue'

withDefaults(
  defineProps<{
    title?: string
    description?: string
  }>(),
  {
    title: '暂无数据',
    description: '',
  },
)
</script>

<template>
  <div class="empty-state">
    <el-icon :size="28"><DocumentRemove /></el-icon>
    <div class="empty-title">{{ title }}</div>
    <div v-if="description" class="empty-description">{{ description }}</div>
  </div>
</template>

<style scoped>
.empty-state {
  display: grid;
  justify-items: center;
  gap: 8px;
  padding: 32px 16px;
  color: #7b8798;
}

.empty-title {
  color: #334155;
  font-weight: 600;
}

.empty-description {
  font-size: 13px;
}
</style>
