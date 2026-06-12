<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { Plus, View } from '@element-plus/icons-vue'
import { suppliersApi } from '@/api/bidops/suppliers.api'
import DataTable from '@/shared/components/DataTable.vue'
import FormDrawer from '@/shared/components/FormDrawer.vue'
import PageContainer from '@/shared/components/PageContainer.vue'
import SearchForm from '@/shared/components/SearchForm.vue'
import { useRequest } from '@/shared/composables/useRequest'
import { useTableQuery } from '@/shared/composables/useTableQuery'
import { formatDateTime } from '@/shared/utils/date'
import BidOpsStatusTag from '../../components/BidOpsStatusTag.vue'
import PermissionButton from '../../components/PermissionButton.vue'
import { BIDOPS_PERMISSIONS } from '../../constants'
import type { CreateSupplierRequest, SupplierDto } from '../../types'
import { formatSupplierName, supplierStatusOptions } from '../../utils/display'

interface SupplierListQuery {
  keyword: string
  status?: string
  region: string
  category: string
  evidenceExpiringOnly?: boolean
  pageIndex: number
  pageSize: number
}

const router = useRouter()
const createDrawerOpen = ref(false)
const createRequest = useRequest()

const table = useTableQuery<SupplierDto, SupplierListQuery>(
  (params) =>
    suppliersApi.search({
      ...params,
      status: params.status || undefined,
      region: params.region || undefined,
      category: params.category || undefined,
      evidenceExpiringOnly: params.evidenceExpiringOnly || undefined,
    }),
  {
    keyword: '',
    status: undefined,
    region: '',
    category: '',
    evidenceExpiringOnly: undefined,
    pageIndex: 1,
    pageSize: 20,
  },
)

const createForm = reactive<CreateSupplierRequest>({
  name: '',
  unifiedSocialCreditCode: '',
  region: '',
  address: '',
  contactName: '',
  contactPhone: '',
  contactEmail: '',
  qualityScore: null,
  remark: '',
})

function resetCreateForm() {
  Object.assign(createForm, {
    name: '',
    unifiedSocialCreditCode: '',
    region: '',
    address: '',
    contactName: '',
    contactPhone: '',
    contactEmail: '',
    qualityScore: null,
    remark: '',
  })
}

function openCreate() {
  resetCreateForm()
  createDrawerOpen.value = true
}

function nullableText(value?: string | null) {
  const text = String(value || '').trim()
  return text || null
}

async function submitCreate() {
  if (!createForm.name.trim()) {
    ElMessage.warning('请输入厂家名称')
    return
  }

  await createRequest.run(async () => {
    const created = await suppliersApi.create({
      name: createForm.name.trim(),
      unifiedSocialCreditCode: nullableText(createForm.unifiedSocialCreditCode),
      region: nullableText(createForm.region),
      address: nullableText(createForm.address),
      contactName: nullableText(createForm.contactName),
      contactPhone: nullableText(createForm.contactPhone),
      contactEmail: nullableText(createForm.contactEmail),
      qualityScore: createForm.qualityScore ?? null,
      remark: nullableText(createForm.remark),
    })
    ElMessage.success('厂家已创建')
    createDrawerOpen.value = false
    await router.push(`/bidops/suppliers/${created.id}`)
  })
}
</script>

<template>
  <PageContainer title="厂家列表" description="维护厂家基础档案、能力标签和材料有效期。">
    <template #actions>
      <PermissionButton type="primary" :icon="Plus" :permission="BIDOPS_PERMISSIONS.SUPPLIER_MANAGE" @click="openCreate">
        新建厂家
      </PermissionButton>
    </template>

    <SearchForm @search="table.search" @reset="table.reset()">
      <el-form-item label="关键词">
        <el-input v-model="table.query.keyword" clearable placeholder="厂家 / 编号 / 信用代码 / 联系人" />
      </el-form-item>
      <el-form-item label="状态">
        <el-select v-model="table.query.status" clearable placeholder="全部" style="width: 140px">
          <el-option v-for="item in supplierStatusOptions" :key="item.value" :label="item.label" :value="item.value" />
        </el-select>
      </el-form-item>
      <el-form-item label="地区">
        <el-input v-model="table.query.region" clearable placeholder="省市" style="width: 140px" />
      </el-form-item>
      <el-form-item label="能力分类">
        <el-input v-model="table.query.category" clearable placeholder="物资 / 施工" style="width: 150px" />
      </el-form-item>
      <el-form-item label="材料预警">
        <el-switch v-model="table.query.evidenceExpiringOnly" />
      </el-form-item>
    </SearchForm>

    <DataTable :data="table.result.items" :loading="table.loading" empty-text="暂无厂家">
      <el-table-column label="厂家" min-width="260" show-overflow-tooltip>
        <template #default="{ row }">
          <div class="main-cell">
            <strong>{{ formatSupplierName(row.name) }}</strong>
            <span>{{ row.supplierNo }}</span>
          </div>
        </template>
      </el-table-column>
      <el-table-column prop="unifiedSocialCreditCode" label="统一社会信用代码" min-width="180" show-overflow-tooltip />
      <el-table-column prop="region" label="地区" width="120" show-overflow-tooltip />
      <el-table-column label="状态" width="110">
        <template #default="{ row }"><BidOpsStatusTag :value="row.status" /></template>
      </el-table-column>
      <el-table-column label="质量评分" width="110">
        <template #default="{ row }">{{ row.qualityScore ?? '-' }}</template>
      </el-table-column>
      <el-table-column label="联系人" min-width="170" show-overflow-tooltip>
        <template #default="{ row }">
          {{ row.contactName || '-' }}
          <span v-if="row.contactPhone"> / {{ row.contactPhone }}</span>
        </template>
      </el-table-column>
      <el-table-column label="能力" width="90">
        <template #default="{ row }">{{ row.capabilityCount }}</template>
      </el-table-column>
      <el-table-column label="材料" width="120">
        <template #default="{ row }">
          <span>{{ row.evidenceCount }}</span>
          <el-tag v-if="row.expiringEvidenceCount > 0" type="warning" effect="light" class="count-tag">
            {{ row.expiringEvidenceCount }} 项预警
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="创建时间" width="170">
        <template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="110" fixed="right">
        <template #default="{ row }">
          <el-button size="small" :icon="View" @click="router.push(`/bidops/suppliers/${row.id}`)">详情</el-button>
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
      v-model="createDrawerOpen"
      title="新建厂家"
      width="680px"
      :submitting="createRequest.loading"
      @submit="submitCreate"
    >
      <el-form :model="createForm" label-width="130px" class="form-grid">
        <el-form-item label="厂家名称" class="full-row">
          <el-input v-model.trim="createForm.name" placeholder="请输入厂家名称" />
        </el-form-item>
        <el-form-item label="统一信用代码" class="full-row">
          <el-input v-model.trim="createForm.unifiedSocialCreditCode" />
        </el-form-item>
        <el-form-item label="地区">
          <el-input v-model.trim="createForm.region" />
        </el-form-item>
        <el-form-item label="质量评分">
          <el-input-number v-model="createForm.qualityScore" :min="0" :max="100" :precision="2" />
        </el-form-item>
        <el-form-item label="联系人">
          <el-input v-model.trim="createForm.contactName" />
        </el-form-item>
        <el-form-item label="联系电话">
          <el-input v-model.trim="createForm.contactPhone" />
        </el-form-item>
        <el-form-item label="联系邮箱" class="full-row">
          <el-input v-model.trim="createForm.contactEmail" />
        </el-form-item>
        <el-form-item label="地址" class="full-row">
          <el-input v-model.trim="createForm.address" />
        </el-form-item>
        <el-form-item label="备注" class="full-row">
          <el-input v-model="createForm.remark" type="textarea" :rows="3" />
        </el-form-item>
      </el-form>
    </FormDrawer>
  </PageContainer>
</template>

<style scoped>
.main-cell {
  display: grid;
  gap: 4px;
  min-width: 0;
}

.main-cell strong {
  overflow: hidden;
  color: #17202a;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.main-cell span {
  color: #687385;
  font-size: 12px;
}

.count-tag {
  margin-left: 8px;
}

.table-pagination {
  justify-content: flex-end;
  margin-top: 14px;
}

.form-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  column-gap: 12px;
}

.full-row {
  grid-column: 1 / -1;
}

@media (max-width: 720px) {
  .form-grid {
    display: block;
  }
}
</style>
