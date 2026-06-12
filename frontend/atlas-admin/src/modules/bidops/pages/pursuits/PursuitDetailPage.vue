<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { ArrowLeft, Edit, Plus, Refresh } from '@element-plus/icons-vue'
import { pursuitsApi } from '@/api/bidops/pursuits.api'
import DataTable from '@/shared/components/DataTable.vue'
import FormDrawer from '@/shared/components/FormDrawer.vue'
import PageContainer from '@/shared/components/PageContainer.vue'
import { useRequest } from '@/shared/composables/useRequest'
import { formatDateTime } from '@/shared/utils/date'
import { formatMoney } from '@/shared/utils/money'
import BidOpsStatusTag from '../../components/BidOpsStatusTag.vue'
import PermissionButton from '../../components/PermissionButton.vue'
import RiskLevelTag from '../../components/RiskLevelTag.vue'
import { BIDOPS_PERMISSIONS } from '../../constants'
import type {
  ChangePursuitStatusRequest,
  CreatePursuitFollowRecordRequest,
  PursuitDetailDto,
  PursuitTaskDto,
  UpdatePursuitRequest,
  UpdatePursuitTaskRequest,
} from '../../types'
import {
  formatCategory,
  formatPackageNo,
  formatPursuitFollowType,
  formatPursuitStage,
  formatPursuitTaskStatus,
  formatPursuitTaskType,
  formatSupplierName,
  pursuitFollowTypeOptions,
  pursuitRiskLevelOptions,
  pursuitStageOptions,
  pursuitStatusOptions,
  pursuitTaskStatusOptions,
  pursuitTaskTypeOptions,
} from '../../utils/display'

interface TaskForm {
  title: string
  taskType: string
  status: string
  priority: number
  ownerUserId: string | number | null
  dueAtUtc: string | null
  description: string
  resultNote: string
}

const route = useRoute()
const router = useRouter()
const detail = ref<PursuitDetailDto | null>(null)
const loading = ref(false)
const editDrawerOpen = ref(false)
const taskDrawerOpen = ref(false)
const followDrawerOpen = ref(false)
const editingTaskId = ref<string | null>(null)
const pursuitId = computed(() => String(route.params.id || ''))
const pursuit = computed(() => detail.value?.pursuit || null)
const packageInfo = computed(() => detail.value?.package || null)
const opportunity = computed(() => detail.value?.opportunity || null)
const tasks = computed(() => detail.value?.tasks || [])
const followRecords = computed(() => detail.value?.followRecords || [])
const pageTitle = computed(() => pursuit.value?.title || '作业详情')

const editRequest = useRequest()
const statusRequest = useRequest()
const taskRequest = useRequest()
const followRequest = useRequest()

const editForm = reactive<UpdatePursuitRequest>({
  title: '',
  priority: 3,
  estimatedAmount: null,
  bidDeadlineAtUtc: null,
  ownerUserId: null,
  progressPercent: 0,
  riskLevel: 'None',
  remark: '',
})

const statusForm = reactive<ChangePursuitStatusRequest>({
  stage: 'Preparing',
  status: 'Active',
  reason: '',
})

const taskForm = reactive<TaskForm>({
  title: '',
  taskType: 'Other',
  status: 'Todo',
  priority: 3,
  ownerUserId: null,
  dueAtUtc: null,
  description: '',
  resultNote: '',
})

const followForm = reactive<CreatePursuitFollowRecordRequest>({
  followType: 'Note',
  content: '',
  nextActionAtUtc: null,
})

async function loadData() {
  loading.value = true
  try {
    detail.value = await pursuitsApi.get(pursuitId.value)
    syncForms()
  } catch {
    detail.value = null
  } finally {
    loading.value = false
  }
}

function syncForms() {
  const item = pursuit.value
  if (!item) return
  Object.assign(editForm, {
    title: item.title,
    priority: item.priority,
    estimatedAmount: item.estimatedAmount ?? null,
    bidDeadlineAtUtc: item.bidDeadlineAtUtc ?? null,
    ownerUserId: item.ownerUserId ?? null,
    progressPercent: item.progressPercent,
    riskLevel: item.riskLevel || 'None',
    remark: item.remark,
  })
  Object.assign(statusForm, {
    stage: item.stage || 'Preparing',
    status: item.status || 'Active',
    reason: '',
  })
}

function openEdit() {
  syncForms()
  editDrawerOpen.value = true
}

async function submitEdit() {
  await editRequest.run(async () => {
    await pursuitsApi.update(pursuitId.value, {
      ...editForm,
      title: emptyToNull(editForm.title),
      ownerUserId: emptyToNull(editForm.ownerUserId),
      remark: emptyToNull(editForm.remark),
    })
    ElMessage.success('作业已更新')
    editDrawerOpen.value = false
    await loadData()
  })
}

async function submitStatus() {
  if (!statusForm.stage) {
    ElMessage.warning('请选择阶段')
    return
  }

  await statusRequest.run(async () => {
    await pursuitsApi.changeStatus(pursuitId.value, {
      ...statusForm,
      reason: emptyToNull(statusForm.reason),
    })
    ElMessage.success('阶段已更新')
    await loadData()
  })
}

function openCreateTask() {
  editingTaskId.value = null
  Object.assign(taskForm, {
    title: '',
    taskType: 'Other',
    status: 'Todo',
    priority: 3,
    ownerUserId: pursuit.value?.ownerUserId ?? null,
    dueAtUtc: null,
    description: '',
    resultNote: '',
  })
  taskDrawerOpen.value = true
}

function openEditTask(row: PursuitTaskDto) {
  editingTaskId.value = row.id
  Object.assign(taskForm, {
    title: row.title,
    taskType: row.taskType || 'Other',
    status: row.status || 'Todo',
    priority: row.priority,
    ownerUserId: row.ownerUserId ?? null,
    dueAtUtc: row.dueAtUtc ?? null,
    description: row.description,
    resultNote: row.resultNote,
  })
  taskDrawerOpen.value = true
}

async function submitTask() {
  if (!taskForm.title.trim()) {
    ElMessage.warning('请输入任务标题')
    return
  }

  await taskRequest.run(async () => {
    if (editingTaskId.value) {
      const update: UpdatePursuitTaskRequest = {
        ...taskForm,
        ownerUserId: emptyToNull(taskForm.ownerUserId),
        description: emptyToNull(taskForm.description),
        resultNote: emptyToNull(taskForm.resultNote),
      }
      await pursuitsApi.updateTask(pursuitId.value, editingTaskId.value, update)
      ElMessage.success('任务已更新')
    } else {
      await pursuitsApi.createTask(pursuitId.value, {
        title: taskForm.title,
        taskType: taskForm.taskType,
        priority: taskForm.priority,
        ownerUserId: emptyToNull(taskForm.ownerUserId),
        dueAtUtc: taskForm.dueAtUtc,
        description: emptyToNull(taskForm.description),
      })
      ElMessage.success('任务已创建')
    }
    taskDrawerOpen.value = false
    await loadData()
  })
}

function openFollowDrawer() {
  Object.assign(followForm, {
    followType: 'Note',
    content: '',
    nextActionAtUtc: null,
  })
  followDrawerOpen.value = true
}

async function submitFollow() {
  if (!followForm.content.trim()) {
    ElMessage.warning('请输入跟进内容')
    return
  }

  await followRequest.run(async () => {
    await pursuitsApi.createFollowRecord(pursuitId.value, {
      ...followForm,
      nextActionAtUtc: emptyToNull(followForm.nextActionAtUtc),
    })
    ElMessage.success('跟进已记录')
    followDrawerOpen.value = false
    await loadData()
  })
}

function emptyToNull<T>(value: T | null | undefined): T | null {
  if (value === null || value === undefined) return null
  const text = String(value).trim()
  return text ? value : null
}

onMounted(loadData)
</script>

<template>
  <PageContainer :title="pageTitle" description="查看投标作业、任务清单、跟进记录和关联包件。">
    <template #actions>
      <el-button :icon="ArrowLeft" @click="router.push('/bidops/pursuits')">返回</el-button>
      <el-button :icon="Refresh" @click="loadData">刷新</el-button>
      <PermissionButton
        v-if="pursuit"
        type="primary"
        :icon="Edit"
        :permission="BIDOPS_PERMISSIONS.PURSUIT_MANAGE"
        @click="openEdit"
      >
        编辑
      </PermissionButton>
    </template>

    <el-skeleton v-if="loading" :rows="10" animated />
    <el-empty v-else-if="!pursuit" description="未找到投标作业" />
    <template v-else>
      <section class="pursuit-summary">
        <div>
          <h2>{{ pursuit.title }}</h2>
          <p>{{ pursuit.pursuitNo }} · {{ pursuit.projectName || pursuit.noticeTitle || '-' }}</p>
        </div>
        <div class="summary-tags">
          <BidOpsStatusTag :value="pursuit.stage" />
          <BidOpsStatusTag :value="pursuit.status" />
          <RiskLevelTag :value="pursuit.riskLevel" />
          <el-tag v-if="pursuit.overdueTaskCount > 0" type="danger" effect="light">
            {{ pursuit.overdueTaskCount }} 个逾期任务
          </el-tag>
        </div>
      </section>

      <div class="split-grid">
        <section class="content-panel">
          <h2>作业基础信息</h2>
          <el-descriptions :column="2" border>
            <el-descriptions-item label="作业编号">{{ pursuit.pursuitNo }}</el-descriptions-item>
            <el-descriptions-item label="阶段">{{ formatPursuitStage(pursuit.stage) }}</el-descriptions-item>
            <el-descriptions-item label="状态"><BidOpsStatusTag :value="pursuit.status" /></el-descriptions-item>
            <el-descriptions-item label="优先级">{{ pursuit.priority }}</el-descriptions-item>
            <el-descriptions-item label="预估金额">{{ formatMoney(pursuit.estimatedAmount) }}</el-descriptions-item>
            <el-descriptions-item label="投标截止">{{ formatDateTime(pursuit.bidDeadlineAtUtc) }}</el-descriptions-item>
            <el-descriptions-item label="负责人 ID">{{ pursuit.ownerUserId || '-' }}</el-descriptions-item>
            <el-descriptions-item label="进度">
              <el-progress :percentage="pursuit.progressPercent" :stroke-width="8" />
            </el-descriptions-item>
            <el-descriptions-item label="厂家">{{ formatSupplierName(pursuit.supplierNameSnapshot) }}</el-descriptions-item>
            <el-descriptions-item label="风险"><RiskLevelTag :value="pursuit.riskLevel" /></el-descriptions-item>
            <el-descriptions-item label="阶段更新时间">{{ formatDateTime(pursuit.lastStageChangedAtUtc) }}</el-descriptions-item>
            <el-descriptions-item label="创建时间">{{ formatDateTime(pursuit.createdAt) }}</el-descriptions-item>
            <el-descriptions-item label="备注" :span="2">{{ pursuit.remark || '-' }}</el-descriptions-item>
          </el-descriptions>
        </section>

        <section class="content-panel">
          <h2>关联包件</h2>
          <el-descriptions :column="1" border>
            <el-descriptions-item label="包件号">{{ formatPackageNo(packageInfo?.packageNo || pursuit.packageNo) }}</el-descriptions-item>
            <el-descriptions-item label="包件名称">{{ packageInfo?.packageName || pursuit.packageName || '-' }}</el-descriptions-item>
            <el-descriptions-item label="项目编码">{{ pursuit.projectCode || '-' }}</el-descriptions-item>
            <el-descriptions-item label="采购人">{{ pursuit.buyerName || '-' }}</el-descriptions-item>
            <el-descriptions-item label="地区">{{ pursuit.region || '-' }}</el-descriptions-item>
            <el-descriptions-item label="品类">{{ formatCategory(packageInfo?.category) }}</el-descriptions-item>
            <el-descriptions-item label="预算金额">{{ formatMoney(packageInfo?.budgetAmount) }}</el-descriptions-item>
            <el-descriptions-item label="最高限价">{{ formatMoney(packageInfo?.maxPrice) }}</el-descriptions-item>
          </el-descriptions>
        </section>
      </div>

      <div class="split-grid detail-section">
        <section class="content-panel">
          <h2>关联商机</h2>
          <el-empty v-if="!opportunity" description="未关联商机" />
          <el-descriptions v-else :column="1" border>
            <el-descriptions-item label="商机编号">{{ opportunity.opportunityNo }}</el-descriptions-item>
            <el-descriptions-item label="商机标题">{{ opportunity.title }}</el-descriptions-item>
            <el-descriptions-item label="决策"><BidOpsStatusTag :value="opportunity.decision" /></el-descriptions-item>
            <el-descriptions-item label="价值分">{{ opportunity.valueScore ?? '-' }}</el-descriptions-item>
            <el-descriptions-item label="下次跟进">{{ formatDateTime(opportunity.nextActionAtUtc) }}</el-descriptions-item>
          </el-descriptions>
        </section>

        <section class="content-panel">
          <h2>阶段流转</h2>
          <el-form :model="statusForm" label-width="100px" class="form-grid">
            <el-form-item label="目标阶段">
              <el-select v-model="statusForm.stage">
                <el-option v-for="item in pursuitStageOptions" :key="item.value" :label="item.label" :value="item.value" />
              </el-select>
            </el-form-item>
            <el-form-item label="状态">
              <el-select v-model="statusForm.status">
                <el-option v-for="item in pursuitStatusOptions" :key="item.value" :label="item.label" :value="item.value" />
              </el-select>
            </el-form-item>
            <el-form-item label="原因" class="full-row">
              <el-input v-model="statusForm.reason" type="textarea" :rows="3" />
            </el-form-item>
          </el-form>
          <PermissionButton
            type="primary"
            :loading="statusRequest.loading"
            :permission="BIDOPS_PERMISSIONS.PURSUIT_MANAGE"
            @click="submitStatus"
          >
            更新阶段
          </PermissionButton>
        </section>
      </div>

      <section class="content-panel detail-section">
        <div class="panel-head">
          <h2>任务清单</h2>
          <PermissionButton
            type="primary"
            :icon="Plus"
            :permission="BIDOPS_PERMISSIONS.PURSUIT_TASK_MANAGE"
            @click="openCreateTask"
          >
            新建任务
          </PermissionButton>
        </div>
        <DataTable :data="tasks" :loading="loading" empty-text="暂无任务">
          <el-table-column prop="title" label="任务" min-width="220" show-overflow-tooltip />
          <el-table-column label="类型" width="120">
            <template #default="{ row }">{{ formatPursuitTaskType(row.taskType) }}</template>
          </el-table-column>
          <el-table-column label="状态" width="110">
            <template #default="{ row }">{{ formatPursuitTaskStatus(row.status) }}</template>
          </el-table-column>
          <el-table-column prop="priority" label="优先级" width="90" />
          <el-table-column label="负责人" width="120">
            <template #default="{ row }">{{ row.ownerUserId || '-' }}</template>
          </el-table-column>
          <el-table-column label="截止时间" width="170">
            <template #default="{ row }">{{ formatDateTime(row.dueAtUtc) }}</template>
          </el-table-column>
          <el-table-column label="完成时间" width="170">
            <template #default="{ row }">{{ formatDateTime(row.completedAtUtc) }}</template>
          </el-table-column>
          <el-table-column label="操作" width="100" fixed="right">
            <template #default="{ row }">
              <PermissionButton
                size="small"
                :icon="Edit"
                :permission="BIDOPS_PERMISSIONS.PURSUIT_TASK_MANAGE"
                @click="openEditTask(row)"
              >
                编辑
              </PermissionButton>
            </template>
          </el-table-column>
        </DataTable>
      </section>

      <section class="content-panel detail-section">
        <div class="panel-head">
          <h2>跟进记录</h2>
          <PermissionButton
            type="primary"
            :icon="Plus"
            :permission="BIDOPS_PERMISSIONS.PURSUIT_FOLLOW_RECORD_MANAGE"
            @click="openFollowDrawer"
          >
            新增跟进
          </PermissionButton>
        </div>
        <el-empty v-if="followRecords.length === 0" description="暂无跟进记录" />
        <el-timeline v-else>
          <el-timeline-item
            v-for="item in followRecords"
            :key="item.id"
            :timestamp="formatDateTime(item.createdAt)"
            placement="top"
          >
            <div class="timeline-item">
              <strong>{{ formatPursuitFollowType(item.followType) }}</strong>
              <span>{{ item.createdByUserName || item.createdByUserId || '-' }}</span>
              <p>{{ item.content }}</p>
              <small v-if="item.nextActionAtUtc">下次动作：{{ formatDateTime(item.nextActionAtUtc) }}</small>
            </div>
          </el-timeline-item>
        </el-timeline>
      </section>

      <FormDrawer v-model="editDrawerOpen" title="编辑投标作业" width="680px" :submitting="editRequest.loading" @submit="submitEdit">
        <el-form :model="editForm" label-width="120px" class="form-grid">
          <el-form-item label="标题" class="full-row"><el-input v-model.trim="editForm.title" /></el-form-item>
          <el-form-item label="优先级"><el-input-number v-model="editForm.priority" :min="1" :max="5" /></el-form-item>
          <el-form-item label="预估金额">
            <el-input-number v-model="editForm.estimatedAmount" :min="0" :precision="2" />
          </el-form-item>
          <el-form-item label="负责人 ID"><el-input v-model.trim="editForm.ownerUserId" /></el-form-item>
          <el-form-item label="投标截止">
            <el-date-picker v-model="editForm.bidDeadlineAtUtc" type="datetime" value-format="YYYY-MM-DDTHH:mm:ss" />
          </el-form-item>
          <el-form-item label="进度">
            <el-slider v-model="editForm.progressPercent" :min="0" :max="100" />
          </el-form-item>
          <el-form-item label="风险等级">
            <el-select v-model="editForm.riskLevel">
              <el-option v-for="item in pursuitRiskLevelOptions" :key="item.value" :label="item.label" :value="item.value" />
            </el-select>
          </el-form-item>
          <el-form-item label="备注" class="full-row">
            <el-input v-model="editForm.remark" type="textarea" :rows="4" />
          </el-form-item>
        </el-form>
      </FormDrawer>

      <FormDrawer
        v-model="taskDrawerOpen"
        :title="editingTaskId ? '编辑任务' : '新建任务'"
        width="640px"
        :submitting="taskRequest.loading"
        @submit="submitTask"
      >
        <el-form :model="taskForm" label-width="110px" class="form-grid">
          <el-form-item label="任务标题" class="full-row"><el-input v-model.trim="taskForm.title" /></el-form-item>
          <el-form-item label="任务类型">
            <el-select v-model="taskForm.taskType">
              <el-option v-for="item in pursuitTaskTypeOptions" :key="item.value" :label="item.label" :value="item.value" />
            </el-select>
          </el-form-item>
          <el-form-item v-if="editingTaskId" label="状态">
            <el-select v-model="taskForm.status">
              <el-option v-for="item in pursuitTaskStatusOptions" :key="item.value" :label="item.label" :value="item.value" />
            </el-select>
          </el-form-item>
          <el-form-item label="优先级">
            <el-input-number v-model="taskForm.priority" :min="1" :max="5" />
          </el-form-item>
          <el-form-item label="负责人 ID"><el-input v-model.trim="taskForm.ownerUserId" /></el-form-item>
          <el-form-item label="截止时间">
            <el-date-picker v-model="taskForm.dueAtUtc" type="datetime" value-format="YYYY-MM-DDTHH:mm:ss" />
          </el-form-item>
          <el-form-item label="任务说明" class="full-row">
            <el-input v-model="taskForm.description" type="textarea" :rows="3" />
          </el-form-item>
          <el-form-item v-if="editingTaskId" label="处理结果" class="full-row">
            <el-input v-model="taskForm.resultNote" type="textarea" :rows="3" />
          </el-form-item>
        </el-form>
      </FormDrawer>

      <FormDrawer
        v-model="followDrawerOpen"
        title="新增跟进"
        width="560px"
        :submitting="followRequest.loading"
        @submit="submitFollow"
      >
        <el-form :model="followForm" label-width="110px">
          <el-form-item label="跟进类型">
            <el-select v-model="followForm.followType">
              <el-option v-for="item in pursuitFollowTypeOptions" :key="item.value" :label="item.label" :value="item.value" />
            </el-select>
          </el-form-item>
          <el-form-item label="下次动作">
            <el-date-picker v-model="followForm.nextActionAtUtc" type="datetime" value-format="YYYY-MM-DDTHH:mm:ss" />
          </el-form-item>
          <el-form-item label="内容">
            <el-input v-model="followForm.content" type="textarea" :rows="5" />
          </el-form-item>
        </el-form>
      </FormDrawer>
    </template>
  </PageContainer>
</template>

<style scoped>
.pursuit-summary {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  padding: 16px;
  margin-bottom: 16px;
  border: 1px solid #dce3ee;
  border-radius: 8px;
  background: #fff;
}

.pursuit-summary h2,
.content-panel h2 {
  margin: 0;
  color: #17202a;
}

.pursuit-summary h2 {
  font-size: 18px;
  line-height: 1.45;
}

.pursuit-summary p {
  margin: 6px 0 0;
  color: #687385;
  line-height: 1.5;
}

.summary-tags,
.panel-head {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
}

.summary-tags {
  justify-content: flex-end;
  gap: 8px;
}

.split-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 16px;
  align-items: start;
}

.detail-section {
  margin-top: 16px;
}

.form-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0 12px;
}

.full-row {
  grid-column: 1 / -1;
}

.timeline-item {
  display: grid;
  gap: 4px;
}

.timeline-item strong {
  color: #17202a;
}

.timeline-item span,
.timeline-item small {
  color: #687385;
  font-size: 12px;
}

.timeline-item p {
  margin: 0;
  color: #3d4a5c;
  line-height: 1.55;
}

@media (max-width: 980px) {
  .pursuit-summary {
    display: grid;
  }

  .summary-tags {
    justify-content: flex-start;
  }

  .split-grid,
  .form-grid {
    grid-template-columns: 1fr;
  }
}
</style>
