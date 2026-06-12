<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { Lock, User } from '@element-plus/icons-vue'
import { authApi } from '@/api/auth.api'
import { useAuthStore } from '@/stores/auth.store'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const loading = ref(false)

const form = reactive({
  domain: 'bidops',
  userName: 'bidops_admin',
  password: 'Pass1234!',
  rememberMe: true,
})

function getSafeRedirect() {
  return typeof route.query.redirect === 'string' && route.query.redirect.startsWith('/')
    ? route.query.redirect
    : '/bidops'
}

async function submit() {
  loading.value = true
  auth.logout()
  try {
    const response = await authApi.login(form)
    if (!response.success || !response.token) {
      ElMessage.error(response.message || '登录失败')
      return
    }

    auth.setSession(response)

    const context = await authApi.context()
    auth.setContext({
      tenantId: String(context.tenantId),
      storeId: context.storeId ? String(context.storeId) : '',
      permissions: context.permissions,
    })

    ElMessage.success('登录成功')
    await router.push(getSafeRedirect())
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <main class="login-page">
    <section class="login-panel">
      <div class="brand">
        <div class="brand-mark">A</div>
        <div>
          <h1>Atlas Admin</h1>
          <p>BidOps 招投标作业后台</p>
        </div>
      </div>

      <el-form :model="form" label-position="top" class="login-form" @submit.prevent="submit">
        <el-form-item label="租户域">
          <el-input v-model.trim="form.domain" autocomplete="organization" />
        </el-form-item>
        <el-form-item label="账号">
          <el-input v-model.trim="form.userName" :prefix-icon="User" autocomplete="username" />
        </el-form-item>
        <el-form-item label="密码">
          <el-input
            v-model="form.password"
            :prefix-icon="Lock"
            type="password"
            show-password
            autocomplete="current-password"
          />
        </el-form-item>
        <el-form-item>
          <el-checkbox v-model="form.rememberMe">记住登录</el-checkbox>
        </el-form-item>
        <el-button type="primary" native-type="submit" :loading="loading" class="login-button">登录</el-button>
      </el-form>

      <div class="dev-account">
        本地账号：租户域 <strong>bidops</strong>，账号 <strong>bidops_admin</strong>，密码 <strong>Pass1234!</strong>
      </div>
    </section>
  </main>
</template>

<style scoped>
.login-page {
  display: grid;
  min-height: 100vh;
  place-items: center;
  padding: 24px;
  background:
    linear-gradient(135deg, rgba(15, 118, 110, 0.12), rgba(37, 99, 235, 0.08)),
    #f5f7fb;
}

.login-panel {
  width: min(440px, 100%);
  padding: 28px;
  border: 1px solid #dce3ee;
  border-radius: 8px;
  background: #fff;
  box-shadow: 0 18px 48px rgba(15, 23, 42, 0.12);
}

.brand {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 24px;
}

.brand-mark {
  display: grid;
  width: 44px;
  height: 44px;
  place-items: center;
  border-radius: 8px;
  background: #0f766e;
  color: #fff;
  font-size: 20px;
  font-weight: 700;
}

.brand h1 {
  margin: 0;
  font-size: 23px;
  line-height: 1.2;
}

.brand p {
  margin: 4px 0 0;
  color: #687385;
  font-size: 14px;
}

.login-form {
  margin-top: 8px;
}

.login-button {
  width: 100%;
}

.dev-account {
  margin-top: 16px;
  padding: 10px 12px;
  border: 1px solid #dce3ee;
  border-radius: 8px;
  background: #f8fafc;
  color: #526071;
  font-size: 13px;
  line-height: 1.6;
}
</style>
