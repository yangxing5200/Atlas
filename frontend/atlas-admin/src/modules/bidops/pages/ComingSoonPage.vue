<script setup lang="ts">
import { useRoute, useRouter } from 'vue-router'
import { Back, Clock, FolderOpened } from '@element-plus/icons-vue'
import PageContainer from '@/shared/components/PageContainer.vue'

const props = defineProps<{
  title: string
  moduleName: string
}>()

const route = useRoute()
const router = useRouter()
</script>

<template>
  <PageContainer :title="props.title" :description="`${props.moduleName} · 路由占位`">
    <section class="coming-soon">
      <div class="status-icon">
        <el-icon :size="30"><Clock /></el-icon>
      </div>
      <div class="content">
        <el-tag type="info" effect="plain">ComingSoon</el-tag>
        <h2>{{ props.moduleName }}</h2>
        <p>该模块当前只开放前端菜单和路由占位，后端实体、API、权限和后台任务尚未接入。</p>
        <dl>
          <div>
            <dt>当前路由</dt>
            <dd>{{ route.path }}</dd>
          </div>
          <div>
            <dt>页面状态</dt>
            <dd>规划中</dd>
          </div>
        </dl>
        <div class="actions">
          <el-button :icon="Back" @click="router.back()">返回</el-button>
          <el-button type="primary" :icon="FolderOpened" @click="router.push('/bidops')">指挥中心</el-button>
        </div>
      </div>
    </section>
  </PageContainer>
</template>

<style scoped>
.coming-soon {
  display: grid;
  grid-template-columns: 64px minmax(0, 1fr);
  gap: 18px;
  max-width: 760px;
  padding: 22px;
  border: 1px solid #dce3ee;
  border-radius: 8px;
  background: #fff;
}

.status-icon {
  display: grid;
  width: 56px;
  height: 56px;
  place-items: center;
  border-radius: 8px;
  background: #eef4ff;
  color: #2563eb;
}

.content {
  min-width: 0;
}

.content h2 {
  margin: 12px 0 8px;
  color: #17202a;
  font-size: 18px;
  line-height: 1.35;
}

.content p {
  margin: 0;
  color: #526071;
  line-height: 1.7;
}

dl {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
  margin: 18px 0 0;
}

dl > div {
  min-width: 0;
  padding: 12px;
  border: 1px solid #e5eaf3;
  border-radius: 8px;
  background: #f8fafc;
}

dt {
  color: #7b8798;
  font-size: 12px;
}

dd {
  overflow-wrap: anywhere;
  margin: 6px 0 0;
  color: #17202a;
  font-size: 14px;
}

.actions {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  margin-top: 20px;
}

@media (max-width: 720px) {
  .coming-soon {
    grid-template-columns: 1fr;
  }

  dl {
    grid-template-columns: 1fr;
  }
}
</style>
