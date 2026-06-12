<script setup lang="ts">
import { reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Edit, Plus, VideoPlay } from '@element-plus/icons-vue'
import { crawlChannelsApi } from '@/api/bidops/crawlChannels.api'
import DataTable from '@/shared/components/DataTable.vue'
import FormDrawer from '@/shared/components/FormDrawer.vue'
import PageContainer from '@/shared/components/PageContainer.vue'
import SearchForm from '@/shared/components/SearchForm.vue'
import { useRequest } from '@/shared/composables/useRequest'
import { useTableQuery } from '@/shared/composables/useTableQuery'
import { formatDateTime } from '@/shared/utils/date'
import { BIDOPS_PERMISSIONS } from '../../constants'
import BidOpsStatusTag from '../../components/BidOpsStatusTag.vue'
import PermissionButton from '../../components/PermissionButton.vue'
import type { BidOpsId, CrawlChannelDto, CreateCrawlChannelRequest } from '../../types'
import { formatNoticeType, noticeTypeOptions } from '../../utils/display'

const defaultChannelForm: CreateCrawlChannelRequest = {
  sourceId: 0,
  code: '',
  name: '',
  noticeType: 'TenderAnnouncement',
  listUrl: '',
  region: '',
  industry: '',
  enabled: true,
}

const table = useTableQuery<CrawlChannelDto, { keyword: string; pageIndex: number; pageSize: number }>(
  crawlChannelsApi.search,
  { keyword: '', pageIndex: 1, pageSize: 20 },
)

const drawerOpen = ref(false)
const editingId = ref<BidOpsId | null>(null)
const form = reactive<CreateCrawlChannelRequest>({ ...defaultChannelForm })
const submitRequest = useRequest()

function openCreate() {
  editingId.value = null
  Object.assign(form, defaultChannelForm)
  drawerOpen.value = true
}

function openEdit(row: CrawlChannelDto) {
  editingId.value = row.id
  Object.assign(form, {
    sourceId: row.sourceId,
    code: row.code,
    name: row.name,
    noticeType: row.noticeType,
    listUrl: row.listUrl,
    region: row.region,
    industry: row.industry,
    enabled: row.enabled,
  })
  drawerOpen.value = true
}

async function submitForm() {
  await submitRequest.run(async () => {
    if (editingId.value) {
      await crawlChannelsApi.update(editingId.value, form)
      ElMessage.success('采集栏目已更新')
    } else {
      await crawlChannelsApi.create(form)
      ElMessage.success('采集栏目已创建')
    }
    drawerOpen.value = false
    await table.loadData()
  })
}

async function scanNow(row: CrawlChannelDto) {
  try {
    await ElMessageBox.confirm('立即扫描会向 Worker 队列提交采集任务。', '立即扫描栏目', {
      confirmButtonText: '提交',
      cancelButtonText: '取消',
      type: 'warning',
    })
    const job = await crawlChannelsApi.scanNow(row.id)
    ElMessage.success(`已入队：JobId=${job.jobId} JobType=${job.jobType} Queue=${job.queue} AlreadyExists=${job.alreadyExists}`)
  } catch {
    return
  }
}
</script>

<template>
  <PageContainer title="采集栏目" description="维护公开公告列表入口，并将扫描任务交给 Worker 执行。">
    <template #actions>
      <PermissionButton type="primary" :icon="Plus" :permission="BIDOPS_PERMISSIONS.CRAWL_MANAGE" @click="openCreate">
        新增栏目
      </PermissionButton>
    </template>

    <el-alert
      type="warning"
      show-icon
      :closable="false"
      title="不得绕过反爬、破解验证码、伪造登录态或高频压测目标网站。"
      class="channel-alert"
    />

    <SearchForm @search="table.search" @reset="table.reset()">
      <el-form-item label="关键词">
        <el-input v-model="table.query.keyword" clearable placeholder="编码 / 名称 / 列表地址" />
      </el-form-item>
    </SearchForm>

    <DataTable :data="table.result.items" :loading="table.loading">
      <el-table-column prop="sourceId" label="来源 ID" width="110" />
      <el-table-column prop="code" label="编码" min-width="130" />
      <el-table-column prop="name" label="名称" min-width="160" />
      <el-table-column label="公告类型" min-width="170">
        <template #default="{ row }">{{ formatNoticeType(row.noticeType) }}</template>
      </el-table-column>
      <el-table-column prop="listUrl" label="列表地址" min-width="280" show-overflow-tooltip />
      <el-table-column prop="region" label="地区" width="120" />
      <el-table-column prop="industry" label="行业" width="120" />
      <el-table-column label="启用" width="110">
        <template #default="{ row }"><BidOpsStatusTag :value="row.enabled" /></template>
      </el-table-column>
      <el-table-column label="最近扫描" width="170">
        <template #default="{ row }">{{ formatDateTime(row.lastScanTime) }}</template>
      </el-table-column>
      <el-table-column label="最近成功" width="170">
        <template #default="{ row }">{{ formatDateTime(row.lastSuccessTime) }}</template>
      </el-table-column>
      <el-table-column prop="lastError" label="错误信息" min-width="220" show-overflow-tooltip />
      <el-table-column label="操作" width="220" fixed="right">
        <template #default="{ row }">
          <div class="table-actions">
            <PermissionButton size="small" :icon="Edit" :permission="BIDOPS_PERMISSIONS.CRAWL_MANAGE" @click="openEdit(row)">
              编辑
            </PermissionButton>
            <PermissionButton
              size="small"
              type="primary"
              plain
              :icon="VideoPlay"
              :permission="BIDOPS_PERMISSIONS.CRAWL_IMPORT"
              @click="scanNow(row)"
            >
              扫描
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
      :title="editingId ? '编辑采集栏目' : '新增采集栏目'"
      :submitting="submitRequest.loading"
      @submit="submitForm"
    >
      <el-form :model="form" label-width="120px" class="form-grid">
        <el-form-item label="来源 ID"><el-input-number v-model="form.sourceId" :min="0" /></el-form-item>
        <el-form-item label="编码"><el-input v-model.trim="form.code" /></el-form-item>
        <el-form-item label="名称"><el-input v-model.trim="form.name" /></el-form-item>
        <el-form-item label="公告类型">
          <el-select v-model="form.noticeType" filterable>
            <el-option v-for="item in noticeTypeOptions" :key="item.value" :label="item.label" :value="item.value" />
          </el-select>
        </el-form-item>
        <el-form-item label="列表地址" class="full-row"><el-input v-model.trim="form.listUrl" /></el-form-item>
        <el-form-item label="地区"><el-input v-model.trim="form.region" /></el-form-item>
        <el-form-item label="行业"><el-input v-model.trim="form.industry" /></el-form-item>
        <el-form-item label="启用"><el-switch v-model="form.enabled" /></el-form-item>
      </el-form>
    </FormDrawer>
  </PageContainer>
</template>

<style scoped>
.channel-alert {
  margin-bottom: 12px;
}

.table-pagination {
  justify-content: flex-end;
  margin-top: 14px;
}
</style>
