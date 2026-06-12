<script setup lang="ts">
import { ref } from 'vue'
import { Check, Close } from '@element-plus/icons-vue'
import { BIDOPS_PERMISSIONS } from '@/modules/bidops/constants'
import PermissionButton from './PermissionButton.vue'

const emit = defineEmits<{
  approve: [remark: string]
  ignore: [remark: string]
}>()

defineProps<{
  submitting?: boolean
}>()

const remark = ref('')
</script>

<template>
  <div class="review-panel">
    <el-input v-model="remark" type="textarea" :rows="4" placeholder="审核备注" />
    <div class="review-actions">
      <PermissionButton
        type="primary"
        :icon="Check"
        :loading="submitting"
        :permission="BIDOPS_PERMISSIONS.REVIEW_APPROVE"
        @click="emit('approve', remark)"
      >
        审核通过
      </PermissionButton>
      <PermissionButton
        type="danger"
        plain
        :icon="Close"
        :loading="submitting"
        :permission="BIDOPS_PERMISSIONS.REVIEW_APPROVE"
        @click="emit('ignore', remark)"
      >
        忽略
      </PermissionButton>
    </div>
  </div>
</template>

<style scoped>
.review-panel {
  display: grid;
  gap: 12px;
  padding: 16px;
  border: 1px solid #dce3ee;
  border-radius: 8px;
  background: #fff;
}

.review-actions {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 8px;
}
</style>
