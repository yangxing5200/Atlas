<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { ArrowLeft, Edit, Plus } from '@element-plus/icons-vue'
import { suppliersApi } from '@/api/bidops/suppliers.api'
import DataTable from '@/shared/components/DataTable.vue'
import FormDrawer from '@/shared/components/FormDrawer.vue'
import PageContainer from '@/shared/components/PageContainer.vue'
import { useRequest } from '@/shared/composables/useRequest'
import { formatDateTime } from '@/shared/utils/date'
import BidOpsStatusTag from '../../components/BidOpsStatusTag.vue'
import PermissionButton from '../../components/PermissionButton.vue'
import { BIDOPS_PERMISSIONS } from '../../constants'
import type {
  CreateSupplierCapabilityRequest,
  CreateSupplierContactRequest,
  CreateSupplierEvidenceDocumentRequest,
  SupplierDetailDto,
  UpdateSupplierRequest,
} from '../../types'
import {
  formatCategory,
  formatSupplierName,
  formatSupplierDocumentType,
  supplierDocumentTypeOptions,
  supplierStatusOptions,
} from '../../utils/display'

const route = useRoute()
const router = useRouter()
const supplierId = computed(() => String(route.params.id || ''))
const detail = ref<SupplierDetailDto | null>(null)
const loading = ref(false)
const supplier = computed(() => detail.value?.supplier || null)
const contacts = computed(() => detail.value?.contacts || [])
const capabilities = computed(() => detail.value?.capabilities || [])
const evidenceDocuments = computed(() => detail.value?.evidenceDocuments || [])
const pageTitle = computed(() => (supplier.value ? formatSupplierName(supplier.value.name) : '厂家详情'))

const editDrawerOpen = ref(false)
const contactDrawerOpen = ref(false)
const capabilityDrawerOpen = ref(false)
const evidenceDrawerOpen = ref(false)

const editRequest = useRequest()
const contactRequest = useRequest()
const capabilityRequest = useRequest()
const evidenceRequest = useRequest()

const editForm = reactive<UpdateSupplierRequest>({
  name: '',
  unifiedSocialCreditCode: '',
  region: '',
  address: '',
  contactName: '',
  contactPhone: '',
  contactEmail: '',
  status: 'Active',
  qualityScore: null,
  remark: '',
})

const contactForm = reactive<CreateSupplierContactRequest>({
  name: '',
  role: '',
  phone: '',
  email: '',
  isPrimary: false,
  remark: '',
})

const capabilityForm = reactive<CreateSupplierCapabilityRequest>({
  category: '',
  productLine: '',
  capabilityTags: '',
  regionScope: '',
  qualificationLevel: '',
  remark: '',
})

const evidenceForm = reactive<CreateSupplierEvidenceDocumentRequest>({
  documentName: '',
  documentType: 'QualificationCertificate',
  evidenceNo: '',
  issuedBy: '',
  validFrom: null,
  validTo: null,
  fileName: '',
  fileUrl: '',
  storageProvider: 'ExternalUrl',
  storageKey: '',
  remark: '',
})

async function loadData() {
  loading.value = true
  try {
    detail.value = await suppliersApi.get(supplierId.value)
    syncEditForm()
  } catch {
    detail.value = null
  } finally {
    loading.value = false
  }
}

function nullableText(value?: string | null) {
  const text = String(value || '').trim()
  return text || null
}

function syncEditForm() {
  const item = supplier.value
  if (!item) return
  Object.assign(editForm, {
    name: item.name,
    unifiedSocialCreditCode: item.unifiedSocialCreditCode,
    region: item.region,
    address: item.address,
    contactName: item.contactName,
    contactPhone: item.contactPhone,
    contactEmail: item.contactEmail,
    status: item.status || 'Active',
    qualityScore: item.qualityScore ?? null,
    remark: item.remark,
  })
}

function openEdit() {
  syncEditForm()
  editDrawerOpen.value = true
}

function openContact() {
  Object.assign(contactForm, {
    name: '',
    role: '',
    phone: '',
    email: '',
    isPrimary: contacts.value.length === 0,
    remark: '',
  })
  contactDrawerOpen.value = true
}

function openCapability() {
  Object.assign(capabilityForm, {
    category: '',
    productLine: '',
    capabilityTags: '',
    regionScope: '',
    qualificationLevel: '',
    remark: '',
  })
  capabilityDrawerOpen.value = true
}

function openEvidence() {
  Object.assign(evidenceForm, {
    documentName: '',
    documentType: 'QualificationCertificate',
    evidenceNo: '',
    issuedBy: '',
    validFrom: null,
    validTo: null,
    fileName: '',
    fileUrl: '',
    storageProvider: 'ExternalUrl',
    storageKey: '',
    remark: '',
  })
  evidenceDrawerOpen.value = true
}

async function submitEdit() {
  if (!editForm.name.trim()) {
    ElMessage.warning('请输入厂家名称')
    return
  }

  await editRequest.run(async () => {
    await suppliersApi.update(supplierId.value, {
      name: editForm.name.trim(),
      unifiedSocialCreditCode: nullableText(editForm.unifiedSocialCreditCode),
      region: nullableText(editForm.region),
      address: nullableText(editForm.address),
      contactName: nullableText(editForm.contactName),
      contactPhone: nullableText(editForm.contactPhone),
      contactEmail: nullableText(editForm.contactEmail),
      status: editForm.status || 'Active',
      qualityScore: editForm.qualityScore ?? null,
      remark: nullableText(editForm.remark),
    })
    ElMessage.success('厂家档案已更新')
    editDrawerOpen.value = false
    await loadData()
  })
}

async function submitContact() {
  if (!contactForm.name.trim()) {
    ElMessage.warning('请输入联系人')
    return
  }

  await contactRequest.run(async () => {
    await suppliersApi.addContact(supplierId.value, {
      name: contactForm.name.trim(),
      role: nullableText(contactForm.role),
      phone: nullableText(contactForm.phone),
      email: nullableText(contactForm.email),
      isPrimary: contactForm.isPrimary,
      remark: nullableText(contactForm.remark),
    })
    ElMessage.success('联系人已添加')
    contactDrawerOpen.value = false
    await loadData()
  })
}

async function submitCapability() {
  if (!capabilityForm.category.trim()) {
    ElMessage.warning('请输入能力分类')
    return
  }

  await capabilityRequest.run(async () => {
    await suppliersApi.addCapability(supplierId.value, {
      category: capabilityForm.category.trim(),
      productLine: nullableText(capabilityForm.productLine),
      capabilityTags: nullableText(capabilityForm.capabilityTags),
      regionScope: nullableText(capabilityForm.regionScope),
      qualificationLevel: nullableText(capabilityForm.qualificationLevel),
      remark: nullableText(capabilityForm.remark),
    })
    ElMessage.success('能力标签已添加')
    capabilityDrawerOpen.value = false
    await loadData()
  })
}

async function submitEvidence() {
  if (!evidenceForm.documentName.trim()) {
    ElMessage.warning('请输入材料名称')
    return
  }
  if (!evidenceForm.documentType.trim()) {
    ElMessage.warning('请选择材料类型')
    return
  }

  await evidenceRequest.run(async () => {
    await suppliersApi.addEvidenceDocument(supplierId.value, {
      documentName: evidenceForm.documentName.trim(),
      documentType: evidenceForm.documentType.trim(),
      evidenceNo: nullableText(evidenceForm.evidenceNo),
      issuedBy: nullableText(evidenceForm.issuedBy),
      validFrom: evidenceForm.validFrom || null,
      validTo: evidenceForm.validTo || null,
      fileName: nullableText(evidenceForm.fileName),
      fileUrl: nullableText(evidenceForm.fileUrl),
      storageProvider: nullableText(evidenceForm.storageProvider),
      storageKey: nullableText(evidenceForm.storageKey),
      remark: nullableText(evidenceForm.remark),
    })
    ElMessage.success('资质材料已添加')
    evidenceDrawerOpen.value = false
    await loadData()
  })
}

function splitTags(value?: string | null) {
  return String(value || '')
    .split(/[,\n，、;]/)
    .map((item) => item.trim())
    .filter(Boolean)
}

onMounted(loadData)
</script>

<template>
  <PageContainer :title="pageTitle" description="查看厂家基础档案、联系人、能力标签和资质材料。">
    <template #actions>
      <el-button :icon="ArrowLeft" @click="router.push('/bidops/suppliers')">返回</el-button>
      <PermissionButton
        v-if="supplier"
        type="primary"
        :icon="Edit"
        :permission="BIDOPS_PERMISSIONS.SUPPLIER_MANAGE"
        @click="openEdit"
      >
        编辑档案
      </PermissionButton>
    </template>

    <el-skeleton v-if="loading" :rows="10" animated />
    <el-empty v-else-if="!supplier" description="未找到厂家" />
    <template v-else>
      <section class="supplier-summary">
        <div>
          <h2>{{ formatSupplierName(supplier.name) }}</h2>
          <p>{{ supplier.supplierNo }} · {{ supplier.unifiedSocialCreditCode || '未填写统一社会信用代码' }}</p>
        </div>
        <div class="summary-tags">
          <BidOpsStatusTag :value="supplier.status" />
          <el-tag effect="light">联系人 {{ supplier.contactCount }}</el-tag>
          <el-tag effect="light">能力 {{ supplier.capabilityCount }}</el-tag>
          <el-tag :type="supplier.expiringEvidenceCount > 0 ? 'warning' : 'success'" effect="light">
            材料 {{ supplier.evidenceCount }} / 预警 {{ supplier.expiringEvidenceCount }}
          </el-tag>
        </div>
      </section>

      <div class="split-grid">
        <section class="content-panel">
          <h2>基础信息</h2>
          <el-descriptions :column="2" border>
            <el-descriptions-item label="厂家编号">{{ supplier.supplierNo }}</el-descriptions-item>
            <el-descriptions-item label="状态"><BidOpsStatusTag :value="supplier.status" /></el-descriptions-item>
            <el-descriptions-item label="地区">{{ supplier.region || '-' }}</el-descriptions-item>
            <el-descriptions-item label="质量评分">{{ supplier.qualityScore ?? '-' }}</el-descriptions-item>
            <el-descriptions-item label="统一信用代码" :span="2">{{ supplier.unifiedSocialCreditCode || '-' }}</el-descriptions-item>
            <el-descriptions-item label="地址" :span="2">{{ supplier.address || '-' }}</el-descriptions-item>
            <el-descriptions-item label="主联系人">{{ supplier.contactName || '-' }}</el-descriptions-item>
            <el-descriptions-item label="联系电话">{{ supplier.contactPhone || '-' }}</el-descriptions-item>
            <el-descriptions-item label="联系邮箱" :span="2">{{ supplier.contactEmail || '-' }}</el-descriptions-item>
            <el-descriptions-item label="备注" :span="2">{{ supplier.remark || '-' }}</el-descriptions-item>
            <el-descriptions-item label="创建时间">{{ formatDateTime(supplier.createdAt) }}</el-descriptions-item>
            <el-descriptions-item label="更新时间">{{ formatDateTime(supplier.updatedAt) }}</el-descriptions-item>
          </el-descriptions>
        </section>

        <section class="content-panel">
          <div class="panel-head">
            <h2>联系人</h2>
            <PermissionButton
              size="small"
              :icon="Plus"
              :permission="BIDOPS_PERMISSIONS.SUPPLIER_MANAGE"
              @click="openContact"
            >
              新增
            </PermissionButton>
          </div>
          <DataTable :data="contacts" empty-text="暂无联系人">
            <el-table-column prop="name" label="姓名" min-width="100" show-overflow-tooltip />
            <el-table-column prop="role" label="角色" min-width="100" show-overflow-tooltip />
            <el-table-column prop="phone" label="电话" min-width="120" show-overflow-tooltip />
            <el-table-column prop="email" label="邮箱" min-width="150" show-overflow-tooltip />
            <el-table-column label="主联系人" width="100">
              <template #default="{ row }">{{ row.isPrimary ? '是' : '否' }}</template>
            </el-table-column>
          </DataTable>
        </section>
      </div>

      <section class="content-panel detail-section">
        <div class="panel-head">
          <h2>能力标签</h2>
          <PermissionButton
            size="small"
            :icon="Plus"
            :permission="BIDOPS_PERMISSIONS.SUPPLIER_MANAGE"
            @click="openCapability"
          >
            新增
          </PermissionButton>
        </div>
        <DataTable :data="capabilities" empty-text="暂无能力标签">
          <el-table-column label="分类" min-width="130">
            <template #default="{ row }">{{ formatCategory(row.category) }}</template>
          </el-table-column>
          <el-table-column prop="productLine" label="产品线" min-width="160" show-overflow-tooltip />
          <el-table-column label="标签" min-width="240">
            <template #default="{ row }">
              <div class="tag-list">
                <el-tag v-for="tag in splitTags(row.capabilityTags)" :key="tag" effect="light">{{ tag }}</el-tag>
                <span v-if="splitTags(row.capabilityTags).length === 0">-</span>
              </div>
            </template>
          </el-table-column>
          <el-table-column prop="regionScope" label="覆盖地区" min-width="150" show-overflow-tooltip />
          <el-table-column prop="qualificationLevel" label="资质等级" min-width="140" show-overflow-tooltip />
          <el-table-column prop="remark" label="备注" min-width="180" show-overflow-tooltip />
        </DataTable>
      </section>

      <section class="content-panel detail-section">
        <div class="panel-head">
          <h2>资质材料</h2>
          <PermissionButton
            size="small"
            :icon="Plus"
            :permission="BIDOPS_PERMISSIONS.SUPPLIER_EVIDENCE_MANAGE"
            @click="openEvidence"
          >
            新增
          </PermissionButton>
        </div>
        <DataTable :data="evidenceDocuments" empty-text="暂无资质材料">
          <el-table-column prop="documentName" label="材料名称" min-width="200" show-overflow-tooltip />
          <el-table-column label="类型" width="140">
            <template #default="{ row }">{{ formatSupplierDocumentType(row.documentType) }}</template>
          </el-table-column>
          <el-table-column prop="evidenceNo" label="证书编号" min-width="140" show-overflow-tooltip />
          <el-table-column prop="issuedBy" label="签发机构" min-width="160" show-overflow-tooltip />
          <el-table-column label="有效期" min-width="220">
            <template #default="{ row }">
              {{ formatDateTime(row.validFrom) }} 至 {{ formatDateTime(row.validTo) }}
            </template>
          </el-table-column>
          <el-table-column label="状态" width="110">
            <template #default="{ row }"><BidOpsStatusTag :value="row.status" /></template>
          </el-table-column>
          <el-table-column label="附件" min-width="160" show-overflow-tooltip>
            <template #default="{ row }">
              <el-link v-if="row.fileUrl" :href="row.fileUrl" target="_blank" type="primary">
                {{ row.fileName || '查看附件' }}
              </el-link>
              <span v-else>{{ row.fileName || '-' }}</span>
            </template>
          </el-table-column>
          <el-table-column prop="remark" label="备注" min-width="180" show-overflow-tooltip />
        </DataTable>
      </section>

      <FormDrawer
        v-model="editDrawerOpen"
        title="编辑厂家档案"
        width="680px"
        :submitting="editRequest.loading"
        @submit="submitEdit"
      >
        <el-form :model="editForm" label-width="130px" class="form-grid">
          <el-form-item label="厂家名称" class="full-row">
            <el-input v-model.trim="editForm.name" />
          </el-form-item>
          <el-form-item label="状态">
            <el-select v-model="editForm.status" style="width: 100%">
              <el-option v-for="item in supplierStatusOptions" :key="item.value" :label="item.label" :value="item.value" />
            </el-select>
          </el-form-item>
          <el-form-item label="质量评分">
            <el-input-number v-model="editForm.qualityScore" :min="0" :max="100" :precision="2" />
          </el-form-item>
          <el-form-item label="统一信用代码" class="full-row">
            <el-input v-model.trim="editForm.unifiedSocialCreditCode" />
          </el-form-item>
          <el-form-item label="地区">
            <el-input v-model.trim="editForm.region" />
          </el-form-item>
          <el-form-item label="联系人">
            <el-input v-model.trim="editForm.contactName" />
          </el-form-item>
          <el-form-item label="联系电话">
            <el-input v-model.trim="editForm.contactPhone" />
          </el-form-item>
          <el-form-item label="联系邮箱">
            <el-input v-model.trim="editForm.contactEmail" />
          </el-form-item>
          <el-form-item label="地址" class="full-row">
            <el-input v-model.trim="editForm.address" />
          </el-form-item>
          <el-form-item label="备注" class="full-row">
            <el-input v-model="editForm.remark" type="textarea" :rows="3" />
          </el-form-item>
        </el-form>
      </FormDrawer>

      <FormDrawer
        v-model="contactDrawerOpen"
        title="新增联系人"
        :submitting="contactRequest.loading"
        @submit="submitContact"
      >
        <el-form :model="contactForm" label-width="110px">
          <el-form-item label="姓名">
            <el-input v-model.trim="contactForm.name" />
          </el-form-item>
          <el-form-item label="角色">
            <el-input v-model.trim="contactForm.role" />
          </el-form-item>
          <el-form-item label="电话">
            <el-input v-model.trim="contactForm.phone" />
          </el-form-item>
          <el-form-item label="邮箱">
            <el-input v-model.trim="contactForm.email" />
          </el-form-item>
          <el-form-item label="主联系人">
            <el-switch v-model="contactForm.isPrimary" />
          </el-form-item>
          <el-form-item label="备注">
            <el-input v-model="contactForm.remark" type="textarea" :rows="3" />
          </el-form-item>
        </el-form>
      </FormDrawer>

      <FormDrawer
        v-model="capabilityDrawerOpen"
        title="新增能力标签"
        :submitting="capabilityRequest.loading"
        @submit="submitCapability"
      >
        <el-form :model="capabilityForm" label-width="110px">
          <el-form-item label="能力分类">
            <el-input v-model.trim="capabilityForm.category" placeholder="Material / Service / Construction" />
          </el-form-item>
          <el-form-item label="产品线">
            <el-input v-model.trim="capabilityForm.productLine" />
          </el-form-item>
          <el-form-item label="能力标签">
            <el-input v-model="capabilityForm.capabilityTags" type="textarea" :rows="3" placeholder="多个标签用逗号或换行分隔" />
          </el-form-item>
          <el-form-item label="覆盖地区">
            <el-input v-model.trim="capabilityForm.regionScope" />
          </el-form-item>
          <el-form-item label="资质等级">
            <el-input v-model.trim="capabilityForm.qualificationLevel" />
          </el-form-item>
          <el-form-item label="备注">
            <el-input v-model="capabilityForm.remark" type="textarea" :rows="3" />
          </el-form-item>
        </el-form>
      </FormDrawer>

      <FormDrawer
        v-model="evidenceDrawerOpen"
        title="新增资质材料"
        width="680px"
        :submitting="evidenceRequest.loading"
        @submit="submitEvidence"
      >
        <el-form :model="evidenceForm" label-width="120px" class="form-grid">
          <el-form-item label="材料名称" class="full-row">
            <el-input v-model.trim="evidenceForm.documentName" />
          </el-form-item>
          <el-form-item label="材料类型">
            <el-select v-model="evidenceForm.documentType" style="width: 100%">
              <el-option v-for="item in supplierDocumentTypeOptions" :key="item.value" :label="item.label" :value="item.value" />
            </el-select>
          </el-form-item>
          <el-form-item label="证书编号">
            <el-input v-model.trim="evidenceForm.evidenceNo" />
          </el-form-item>
          <el-form-item label="签发机构" class="full-row">
            <el-input v-model.trim="evidenceForm.issuedBy" />
          </el-form-item>
          <el-form-item label="有效期开始">
            <el-date-picker v-model="evidenceForm.validFrom" type="datetime" value-format="YYYY-MM-DDTHH:mm:ss" />
          </el-form-item>
          <el-form-item label="有效期结束">
            <el-date-picker v-model="evidenceForm.validTo" type="datetime" value-format="YYYY-MM-DDTHH:mm:ss" />
          </el-form-item>
          <el-form-item label="文件名">
            <el-input v-model.trim="evidenceForm.fileName" />
          </el-form-item>
          <el-form-item label="附件地址">
            <el-input v-model.trim="evidenceForm.fileUrl" />
          </el-form-item>
          <el-form-item label="存储提供方">
            <el-input v-model.trim="evidenceForm.storageProvider" />
          </el-form-item>
          <el-form-item label="存储键">
            <el-input v-model.trim="evidenceForm.storageKey" />
          </el-form-item>
          <el-form-item label="备注" class="full-row">
            <el-input v-model="evidenceForm.remark" type="textarea" :rows="3" />
          </el-form-item>
        </el-form>
      </FormDrawer>
    </template>
  </PageContainer>
</template>

<style scoped>
.supplier-summary {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  margin-bottom: 16px;
  padding: 18px;
  border: 1px solid #dce3ee;
  border-radius: 8px;
  background: #fff;
}

.supplier-summary h2 {
  margin: 0;
  color: #17202a;
  font-size: 20px;
  line-height: 1.35;
}

.supplier-summary p {
  margin: 6px 0 0;
  color: #687385;
}

.summary-tags,
.tag-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.split-grid {
  display: grid;
  grid-template-columns: minmax(0, 1.1fr) minmax(360px, 0.9fr);
  gap: 16px;
}

.content-panel {
  min-width: 0;
  padding: 16px;
  border: 1px solid #dce3ee;
  border-radius: 8px;
  background: #fff;
}

.content-panel h2 {
  margin: 0 0 12px;
  color: #17202a;
  font-size: 16px;
  line-height: 1.35;
}

.panel-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 12px;
}

.panel-head h2 {
  margin: 0;
}

.detail-section {
  margin-top: 16px;
}

.form-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  column-gap: 12px;
}

.full-row {
  grid-column: 1 / -1;
}

@media (max-width: 980px) {
  .supplier-summary,
  .split-grid {
    display: block;
  }

  .summary-tags,
  .split-grid > .content-panel + .content-panel {
    margin-top: 12px;
  }
}

@media (max-width: 720px) {
  .form-grid {
    display: block;
  }
}
</style>
