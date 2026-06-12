<script setup lang="ts">
import { reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { CircleCheck, Edit, Plus, SwitchButton } from '@element-plus/icons-vue'
import { crawlSourcesApi } from '@/api/bidops/crawlSources.api'
import DataTable from '@/shared/components/DataTable.vue'
import FormDrawer from '@/shared/components/FormDrawer.vue'
import PageContainer from '@/shared/components/PageContainer.vue'
import SearchForm from '@/shared/components/SearchForm.vue'
import { useRequest } from '@/shared/composables/useRequest'
import { useTableQuery } from '@/shared/composables/useTableQuery'
import { BIDOPS_PERMISSIONS } from '../../constants'
import BidOpsStatusTag from '../../components/BidOpsStatusTag.vue'
import PermissionButton from '../../components/PermissionButton.vue'
import type { BidOpsId, CrawlSourceDto, CreateCrawlSourceRequest } from '../../types'
import { formatSourceType } from '../../utils/display'

const defaultSourceForm: CreateCrawlSourceRequest = {
  code: '',
  name: '',
  sourceType: 'Mock',
  baseUrl: '',
  enabled: true,
  rateLimitPerMinute: 10,
  crawlIntervalMinutes: 60,
  maxRetryCount: 3,
  needJsRender: false,
  needLogin: false,
  respectRobots: true,
  robotsPolicyNote: '',
  remark: '',
}

const table = useTableQuery<CrawlSourceDto, { keyword: string; pageIndex: number; pageSize: number }>(
  crawlSourcesApi.search,
  { keyword: '', pageIndex: 1, pageSize: 20 },
)

const drawerOpen = ref(false)
const editingId = ref<BidOpsId | null>(null)
const form = reactive<CreateCrawlSourceRequest>({ ...defaultSourceForm })
const submitRequest = useRequest()

function openCreate() {
  editingId.value = null
  Object.assign(form, defaultSourceForm)
  drawerOpen.value = true
}

function openEdit(row: CrawlSourceDto) {
  editingId.value = row.id
  Object.assign(form, {
    code: row.code,
    name: row.name,
    sourceType: row.sourceType,
    baseUrl: row.baseUrl,
    enabled: row.enabled,
    rateLimitPerMinute: row.rateLimitPerMinute,
    crawlIntervalMinutes: row.crawlIntervalMinutes,
    maxRetryCount: row.maxRetryCount,
    needJsRender: false,
    needLogin: row.needLogin,
    respectRobots: row.respectRobots,
    robotsPolicyNote: row.robotsPolicyNote,
    remark: row.pauseReason || '',
  })
  drawerOpen.value = true
}

async function submitForm() {
  await submitRequest.run(async () => {
    if (editingId.value) {
      await crawlSourcesApi.update(editingId.value, form)
      ElMessage.success('采集来源已更新')
    } else {
      await crawlSourcesApi.create(form)
      ElMessage.success('采集来源已创建')
    }
    drawerOpen.value = false
    await table.loadData()
  })
}

async function enableSource(row: CrawlSourceDto) {
  await crawlSourcesApi.enable(row.id)
  ElMessage.success('采集来源已启用')
  await table.loadData()
}

async function disableSource(row: CrawlSourceDto) {
  try {
    const result = await ElMessageBox.prompt('请输入停用原因', '停用采集来源', {
      confirmButtonText: '停用',
      cancelButtonText: '取消',
      inputValue: row.pauseReason || '',
    })
    await crawlSourcesApi.disable(row.id, { remark: result.value })
    ElMessage.success('采集来源已停用')
    await table.loadData()
  } catch {
    return
  }
}
</script>

<template>
  <PageContainer title="采集来源" description="仅维护公开站点采集来源、限速和暂停策略。">
    <template #actions>
      <PermissionButton type="primary" :icon="Plus" :permission="BIDOPS_PERMISSIONS.CRAWL_MANAGE" @click="openCreate">
        新增来源
      </PermissionButton>
    </template>

    <el-alert
      type="warning"
      show-icon
      :closable="false"
      title="仅允许处理公开网页和公开附件，不得配置需要登录、验证码、短信、人脸、企业证书或客户端证书才能访问的数据源。"
      class="source-alert"
    />

    <SearchForm @search="table.search" @reset="table.reset()">
      <el-form-item label="关键词">
        <el-input v-model="table.query.keyword" clearable placeholder="编码 / 名称 / 基础地址" />
      </el-form-item>
    </SearchForm>

    <DataTable :data="table.result.items" :loading="table.loading">
      <el-table-column prop="code" label="编码" min-width="130" />
      <el-table-column prop="name" label="名称" min-width="160" />
      <el-table-column label="来源类型" width="150">
        <template #default="{ row }">{{ formatSourceType(row.sourceType) }}</template>
      </el-table-column>
      <el-table-column prop="baseUrl" label="基础地址" min-width="260" show-overflow-tooltip />
      <el-table-column label="启用" width="110">
        <template #default="{ row }">
          <BidOpsStatusTag :value="row.enabled" />
        </template>
      </el-table-column>
      <el-table-column prop="rateLimitPerMinute" label="每分钟限速" width="150" />
      <el-table-column prop="crawlIntervalMinutes" label="采集间隔" width="140" />
      <el-table-column prop="maxRetryCount" label="最大重试" width="120" />
      <el-table-column label="需要登录" width="120">
        <template #default="{ row }">
          <el-tag :type="row.needLogin ? 'danger' : 'info'" effect="light">{{ row.needLogin ? '是' : '否' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="遵守 robots" width="140">
        <template #default="{ row }">
          <el-tag :type="row.respectRobots ? 'success' : 'danger'" effect="light">
            {{ row.respectRobots ? '是' : '否' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="pauseReason" label="暂停原因" min-width="180" show-overflow-tooltip />
      <el-table-column label="操作" width="230" fixed="right">
        <template #default="{ row }">
          <div class="table-actions">
            <PermissionButton
              size="small"
              :icon="Edit"
              :permission="BIDOPS_PERMISSIONS.CRAWL_MANAGE"
              @click="openEdit(row)"
            >
              编辑
            </PermissionButton>
            <PermissionButton
              v-if="!row.enabled"
              size="small"
              type="success"
              :icon="CircleCheck"
              :permission="BIDOPS_PERMISSIONS.CRAWL_MANAGE"
              @click="enableSource(row)"
            >
              启用
            </PermissionButton>
            <PermissionButton
              v-else
              size="small"
              type="danger"
              plain
              :icon="SwitchButton"
              :permission="BIDOPS_PERMISSIONS.CRAWL_MANAGE"
              @click="disableSource(row)"
            >
              停用
            </PermissionButton>
          </div>
        </template>
      </el-table-column>
    </DataTable>

    <el-pagination
      v-model:current-page="table.query.pageIndex"
      v-model:page-size="table.query.pageSize"
      :total="table.result.total"
      :page-sizes="[10, 20, 50, 100]"
      layout="total, sizes, prev, pager, next, jumper"
      class="table-pagination"
      @current-change="table.loadData"
      @size-change="table.loadData"
    />

    <FormDrawer
      v-model="drawerOpen"
      :title="editingId ? '编辑采集来源' : '新增采集来源'"
      :submitting="submitRequest.loading"
      @submit="submitForm"
    >
      <el-alert v-if="form.needLogin" type="error" show-icon :closable="false" title="需要登录 = 是 存在合规风险。" />
      <el-alert
        v-if="!form.respectRobots"
        type="error"
        show-icon
        :closable="false"
        title="遵守 robots = 否 存在合规风险。"
        class="drawer-alert"
      />
      <el-form :model="form" label-width="160px" class="form-grid">
        <el-form-item label="编码"><el-input v-model.trim="form.code" /></el-form-item>
        <el-form-item label="名称"><el-input v-model.trim="form.name" /></el-form-item>
        <el-form-item label="来源类型"><el-input v-model.trim="form.sourceType" /></el-form-item>
        <el-form-item label="基础地址" class="full-row"><el-input v-model.trim="form.baseUrl" /></el-form-item>
        <el-form-item label="启用"><el-switch v-model="form.enabled" /></el-form-item>
        <el-form-item label="每分钟限速">
          <el-input-number v-model="form.rateLimitPerMinute" :min="1" />
        </el-form-item>
        <el-form-item label="采集间隔">
          <el-input-number v-model="form.crawlIntervalMinutes" :min="1" />
        </el-form-item>
        <el-form-item label="最大重试"><el-input-number v-model="form.maxRetryCount" :min="0" /></el-form-item>
        <el-form-item label="需要 JS 渲染"><el-switch v-model="form.needJsRender" /></el-form-item>
        <el-form-item label="需要登录"><el-switch v-model="form.needLogin" /></el-form-item>
        <el-form-item label="遵守 robots"><el-switch v-model="form.respectRobots" /></el-form-item>
        <el-form-item label="robots 备注" class="full-row">
          <el-input v-model="form.robotsPolicyNote" type="textarea" :rows="3" />
        </el-form-item>
        <el-form-item label="备注" class="full-row">
          <el-input v-model="form.remark" type="textarea" :rows="3" />
        </el-form-item>
      </el-form>
    </FormDrawer>
  </PageContainer>
</template>

<style scoped>
.source-alert {
  margin-bottom: 12px;
}

.drawer-alert {
  margin-top: 8px;
}

.table-pagination {
  justify-content: flex-end;
  margin-top: 14px;
}
</style>
