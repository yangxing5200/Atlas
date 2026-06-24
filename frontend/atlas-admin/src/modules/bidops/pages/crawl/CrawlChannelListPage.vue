<script setup lang="ts">
import { reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Edit, Plus, SwitchButton, Timer, VideoPlay } from '@element-plus/icons-vue'
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
  scheduleMode: 'Interval',
  scanIntervalMinutes: null,
  dailyScanTime: '',
}

const table = useTableQuery<CrawlChannelDto, { keyword: string; pageIndex: number; pageSize: number }>(
  crawlChannelsApi.search,
  { keyword: '', pageIndex: 1, pageSize: 20 },
)

const drawerOpen = ref(false)
const backfillOpen = ref(false)
const editingId = ref<BidOpsId | null>(null)
const backfillChannel = ref<CrawlChannelDto | null>(null)
const form = reactive<CreateCrawlChannelRequest>({ ...defaultChannelForm })
const backfillForm = reactive({
  startPublishTime: '',
  endPublishTime: '',
  startPage: 1,
  pageSize: 20,
  maxPagesPerRun: 3,
  resetCursor: true,
})
const submitRequest = useRequest()
const actionRequest = useRequest()

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
    scheduleMode: row.scheduleMode || 'Interval',
    scanIntervalMinutes: row.scanIntervalMinutes || null,
    dailyScanTime: row.dailyScanTime || '',
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
    ElMessage.success(`已入队：${job.jobTypeName || job.jobType}，JobId=${job.jobId}`)
  } catch {
    return
  }
}

async function toggleEnabled(row: CrawlChannelDto) {
  const enabled = !row.enabled
  try {
    await ElMessageBox.confirm(enabled ? '打开后定时扫描会继续处理该栏目。' : '关闭后定时扫描会跳过该栏目。', enabled ? '打开扫描' : '关闭扫描', {
      confirmButtonText: enabled ? '打开' : '关闭',
      cancelButtonText: '取消',
      type: enabled ? 'success' : 'warning',
    })
    await actionRequest.run(() => crawlChannelsApi.setEnabled(row.id, { enabled }))
    ElMessage.success(enabled ? '栏目扫描已打开' : '栏目扫描已关闭')
    await table.loadData()
  } catch {
    return
  }
}

function openBackfill(row: CrawlChannelDto) {
  backfillChannel.value = row
  Object.assign(backfillForm, {
    startPublishTime: '',
    endPublishTime: '',
    startPage: 1,
    pageSize: 20,
    maxPagesPerRun: 3,
    resetCursor: true,
  })
  backfillOpen.value = true
}

async function submitBackfill() {
  if (!backfillChannel.value) return
  await actionRequest.run(async () => {
    const job = await crawlChannelsApi.startBackfill(backfillChannel.value!.id, {
      startPublishTime: backfillForm.startPublishTime || null,
      endPublishTime: backfillForm.endPublishTime || null,
      startPage: backfillForm.startPage,
      pageSize: backfillForm.pageSize,
      maxPagesPerRun: backfillForm.maxPagesPerRun,
      resetCursor: backfillForm.resetCursor,
    })
    ElMessage.success(`补采已入队：JobId=${job.jobId}`)
    backfillOpen.value = false
    await table.loadData()
  })
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
      <el-table-column label="定时规则" width="150">
        <template #default="{ row }">
          {{ row.scheduleMode === 'Daily' ? `每天 ${row.dailyScanTime || '-'}` : `${row.scanIntervalMinutes || '来源'} 分钟` }}
        </template>
      </el-table-column>
      <el-table-column label="最近扫描" width="170">
        <template #default="{ row }">{{ formatDateTime(row.lastScanTime) }}</template>
      </el-table-column>
      <el-table-column label="最近成功" width="170">
        <template #default="{ row }">{{ formatDateTime(row.lastSuccessTime) }}</template>
      </el-table-column>
      <el-table-column prop="lastError" label="错误信息" min-width="220" show-overflow-tooltip />
      <el-table-column label="操作" width="360" fixed="right">
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
            <PermissionButton
              size="small"
              plain
              :icon="Timer"
              :permission="BIDOPS_PERMISSIONS.CRAWL_IMPORT"
              @click="openBackfill(row)"
            >
              补采
            </PermissionButton>
            <PermissionButton
              size="small"
              :type="row.enabled ? 'warning' : 'success'"
              plain
              :icon="SwitchButton"
              :permission="BIDOPS_PERMISSIONS.CRAWL_MANAGE"
              @click="toggleEnabled(row)"
            >
              {{ row.enabled ? '关闭' : '打开' }}
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
        <el-form-item label="定时规则">
          <el-segmented
            v-model="form.scheduleMode"
            :options="[
              { label: '按间隔', value: 'Interval' },
              { label: '每日固定', value: 'Daily' },
            ]"
          />
        </el-form-item>
        <el-form-item v-if="form.scheduleMode === 'Interval'" label="扫描间隔">
          <el-input-number v-model="form.scanIntervalMinutes" :min="1" :max="1440" placeholder="留空用来源设置" />
        </el-form-item>
        <el-form-item v-if="form.scheduleMode === 'Daily'" label="每日时间">
          <el-input v-model.trim="form.dailyScanTime" placeholder="HH:mm，例如 09:30" />
        </el-form-item>
      </el-form>
    </FormDrawer>

    <el-dialog v-model="backfillOpen" title="启动历史补采" width="520px">
      <el-form :model="backfillForm" label-width="130px">
        <el-form-item label="栏目">
          <span>{{ backfillChannel?.name || '-' }}</span>
        </el-form-item>
        <el-form-item label="开始发布日期">
          <el-date-picker v-model="backfillForm.startPublishTime" type="datetime" value-format="YYYY-MM-DDTHH:mm:ss" clearable />
        </el-form-item>
        <el-form-item label="结束发布日期">
          <el-date-picker v-model="backfillForm.endPublishTime" type="datetime" value-format="YYYY-MM-DDTHH:mm:ss" clearable />
        </el-form-item>
        <el-form-item label="起始页">
          <el-input-number v-model="backfillForm.startPage" :min="1" />
        </el-form-item>
        <el-form-item label="每页条数">
          <el-input-number v-model="backfillForm.pageSize" :min="1" :max="50" />
        </el-form-item>
        <el-form-item label="每段页数">
          <el-input-number v-model="backfillForm.maxPagesPerRun" :min="1" :max="20" />
        </el-form-item>
        <el-form-item label="重置游标">
          <el-switch v-model="backfillForm.resetCursor" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="backfillOpen = false">取消</el-button>
        <el-button type="primary" :loading="actionRequest.loading" @click="submitBackfill">提交</el-button>
      </template>
    </el-dialog>
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
