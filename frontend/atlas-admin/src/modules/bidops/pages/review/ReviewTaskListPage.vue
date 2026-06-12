<script setup lang="ts">
import { useRouter } from 'vue-router'
import { View } from '@element-plus/icons-vue'
import { reviewTasksApi } from '@/api/bidops/reviewTasks.api'
import DataTable from '@/shared/components/DataTable.vue'
import PageContainer from '@/shared/components/PageContainer.vue'
import SearchForm from '@/shared/components/SearchForm.vue'
import { useTableQuery } from '@/shared/composables/useTableQuery'
import { formatDateTime } from '@/shared/utils/date'
import BidOpsStatusTag from '../../components/BidOpsStatusTag.vue'
import type { ReviewTaskDto, ReviewTaskStatus } from '../../types'
import { formatNoticeType, reviewTaskStatusOptions } from '../../utils/display'

interface ReviewTaskListQuery {
  keyword: string
  status?: ReviewTaskStatus | ''
  pageIndex: number
  pageSize: number
}

const router = useRouter()
const table = useTableQuery<ReviewTaskDto, ReviewTaskListQuery>(
  (params) => reviewTasksApi.search({ ...params, status: params.status || undefined }),
  { keyword: '', status: '', pageIndex: 1, pageSize: 20 },
)

function projectTitle(row: ReviewTaskDto) {
  return row.projectName || row.taskTitle || '-'
}

function keyDate(row: ReviewTaskDto) {
  return row.bidDeadline || row.openBidTime || row.signupDeadline || row.publishTime
}

function confidencePercent(value: number) {
  return Math.round(Number(value || 0) * 100)
}
</script>

<template>
  <PageContainer title="待审核池" description="人工审核 Raw -> Staging 的解析结果，确认后才写入正式业务表。">
    <SearchForm @search="table.search" @reset="table.reset()">
      <el-form-item label="关键词">
        <el-input v-model="table.query.keyword" clearable placeholder="项目 / 标题 / 备注" />
      </el-form-item>
      <el-form-item label="状态">
        <el-select v-model="table.query.status" clearable placeholder="全部" style="width: 190px">
          <el-option v-for="item in reviewTaskStatusOptions" :key="item.value" :label="item.label" :value="item.value" />
        </el-select>
      </el-form-item>
    </SearchForm>

    <DataTable :data="table.result.items" :loading="table.loading">
      <el-table-column label="待审项目" min-width="360" show-overflow-tooltip>
        <template #default="{ row }">
          <div class="project-cell">
            <strong>{{ projectTitle(row) }}</strong>
            <span>{{ formatNoticeType(row.noticeType) }} · {{ row.region || '未识别地区' }} · 项目编码 {{ row.projectCode || '-' }}</span>
          </div>
        </template>
      </el-table-column>
      <el-table-column label="采购人" min-width="210" show-overflow-tooltip>
        <template #default="{ row }">{{ row.buyerName || '-' }}</template>
      </el-table-column>
      <el-table-column label="关键时间" width="190">
        <template #default="{ row }">
          <div class="date-cell">
            <span>{{ formatDateTime(keyDate(row)) }}</span>
            <small v-if="row.bidDeadline">投标截止</small>
            <small v-else-if="row.openBidTime">开标时间</small>
            <small v-else-if="row.signupDeadline">报名截止</small>
            <small v-else>发布时间</small>
          </div>
        </template>
      </el-table-column>
      <el-table-column label="解析内容" width="180">
        <template #default="{ row }">
          <div class="count-cell">
            <span>{{ row.packageCount }} 包件 / {{ row.requirementCount }} 要求</span>
            <el-tag v-if="row.rejectRiskCount > 0" type="danger" effect="light">{{ row.rejectRiskCount }} 条废标风险</el-tag>
          </div>
        </template>
      </el-table-column>
      <el-table-column label="置信度" width="120">
        <template #default="{ row }">{{ confidencePercent(row.aiConfidence) }}%</template>
      </el-table-column>
      <el-table-column label="状态" width="120"><template #default="{ row }"><BidOpsStatusTag :value="row.status" kind="reviewTask" /></template></el-table-column>
      <el-table-column label="创建时间" width="170"><template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template></el-table-column>
      <el-table-column label="操作" width="120" fixed="right">
        <template #default="{ row }">
          <el-button size="small" type="primary" plain :icon="View" @click="router.push(`/bidops/review/tasks/${row.id}`)">审核</el-button>
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
  </PageContainer>
</template>

<style scoped>
.table-pagination {
  justify-content: flex-end;
  margin-top: 14px;
}

.project-cell,
.date-cell,
.count-cell {
  display: grid;
  gap: 4px;
  min-width: 0;
}

.project-cell strong {
  overflow: hidden;
  color: #17202a;
  font-weight: 650;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.project-cell span,
.date-cell small {
  overflow: hidden;
  color: #687385;
  font-size: 12px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.count-cell {
  align-items: start;
}
</style>
