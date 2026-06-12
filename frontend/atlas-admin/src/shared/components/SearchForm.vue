<script setup lang="ts">
import { Refresh, Search } from '@element-plus/icons-vue'

defineEmits<{
  search: []
  reset: []
}>()
</script>

<template>
  <el-form class="search-form" inline @submit.prevent>
    <slot />
    <el-form-item>
      <el-button type="primary" :icon="Search" @click="$emit('search')">搜索</el-button>
      <el-button :icon="Refresh" @click="$emit('reset')">重置</el-button>
    </el-form-item>
  </el-form>
</template>

<style scoped>
.search-form {
  display: flex;
  flex-wrap: wrap;
  gap: 0 8px;
  padding: 14px 14px 0;
  margin-bottom: 12px;
  border: 1px solid #dce3ee;
  border-radius: 8px;
  background: #fff;
}
</style>
