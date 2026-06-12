<script setup lang="ts">
import { Lock } from '@element-plus/icons-vue'
</script>

<template>
  <div class="forbidden-page">
    <el-icon :size="36"><Lock /></el-icon>
    <h1>403</h1>
    <p>当前账号没有访问该页面的权限。</p>
    <el-button type="primary" @click="$router.push('/bidops')">返回作业台</el-button>
  </div>
</template>

<style scoped>
.forbidden-page {
  display: grid;
  min-height: 420px;
  place-items: center;
  align-content: center;
  gap: 10px;
  color: #526071;
  text-align: center;
}

.forbidden-page h1 {
  margin: 0;
  color: #17202a;
  font-size: 32px;
}

.forbidden-page p {
  margin: 0 0 8px;
}
</style>
