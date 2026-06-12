<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import type { FormInstance, FormRules } from 'element-plus'
import type { ImportPublicUrlRequest } from '@/modules/bidops/types'
import { noticeTypeOptions } from '../utils/display'

const props = defineProps<{
  modelValue: boolean
  submitting?: boolean
}>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  submit: [value: ImportPublicUrlRequest]
}>()

const form = reactive<ImportPublicUrlRequest>({
  sourceId: null,
  channelId: null,
  detailUrl: '',
  title: '',
  noticeType: '',
  textContent: '',
})
const formRef = ref<FormInstance>()

const rules: FormRules<ImportPublicUrlRequest> = {
  detailUrl: [{ required: true, message: '请输入公开公告 URL', trigger: 'blur' }],
}

const visible = computed({
  get: () => props.modelValue,
  set: (value: boolean) => emit('update:modelValue', value),
})

watch(visible, (open) => {
  if (!open) {
    Object.assign(form, {
      sourceId: null,
      channelId: null,
      detailUrl: '',
      title: '',
      noticeType: '',
      textContent: '',
    })
  }
})

async function submit() {
  try {
    await formRef.value?.validate()
  } catch {
    return
  }

  emit('submit', {
    ...form,
    sourceId: form.sourceId || null,
    channelId: form.channelId || null,
    title: form.title || null,
    noticeType: form.noticeType || null,
    textContent: form.textContent || null,
  })
}
</script>

<template>
  <el-dialog v-model="visible" title="手动导入公开 URL" width="680px" destroy-on-close>
    <el-alert
      type="warning"
      show-icon
      :closable="false"
      title="仅允许导入公开可访问 URL，不提供 Cookie、验证码、登录态或反爬绕过配置。"
      class="import-alert"
    />
    <el-form ref="formRef" :model="form" :rules="rules" label-width="104px" @submit.prevent>
      <el-form-item label="来源 ID">
        <el-input-number v-model="form.sourceId" :min="0" :controls="false" />
      </el-form-item>
      <el-form-item label="栏目 ID">
        <el-input-number v-model="form.channelId" :min="0" :controls="false" />
      </el-form-item>
      <el-form-item label="公告 URL" prop="detailUrl">
        <el-input v-model.trim="form.detailUrl" placeholder="https://..." />
      </el-form-item>
      <el-form-item label="标题">
        <el-input v-model.trim="form.title" />
      </el-form-item>
      <el-form-item label="公告类型">
        <el-select v-model="form.noticeType" clearable filterable placeholder="请选择公告类型">
          <el-option v-for="item in noticeTypeOptions" :key="item.value" :label="item.label" :value="item.value" />
        </el-select>
      </el-form-item>
      <el-form-item label="文本内容">
        <el-input v-model="form.textContent" type="textarea" :rows="5" />
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button @click="visible = false">取消</el-button>
      <el-button type="primary" :loading="submitting" @click="submit">提交导入</el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.import-alert {
  margin-bottom: 16px;
}
</style>
