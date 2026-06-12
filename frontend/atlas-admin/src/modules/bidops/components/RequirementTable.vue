<script setup lang="ts">
import type { BidOpsId } from '../types'
import { formatEvidenceType, formatExplanation, formatRequirementType } from '../utils/display'
import RiskLevelTag from './RiskLevelTag.vue'

export interface RequirementRow {
  id: BidOpsId
  requirementType: string
  originalText: string
  isMandatory: boolean
  isRejectRisk: boolean
  requiredEvidenceType: string
  riskLevel: string
  aiExplanation?: string
  aiConfidence?: number
}

defineProps<{
  requirements: RequirementRow[]
  loading?: boolean
}>()
</script>

<template>
  <el-table :data="requirements" border stripe v-loading="loading">
    <el-table-column label="要求类型" min-width="130">
      <template #default="{ row }">{{ formatRequirementType(row.requirementType) }}</template>
    </el-table-column>
    <el-table-column prop="originalText" label="原文要求" min-width="320" show-overflow-tooltip />
    <el-table-column label="强制项" width="110">
      <template #default="{ row }">
        <el-tag v-if="row.isMandatory" type="warning" effect="light">强制项</el-tag>
        <span v-else>-</span>
      </template>
    </el-table-column>
    <el-table-column label="废标风险" width="110">
      <template #default="{ row }">
        <el-tag v-if="row.isRejectRisk" type="danger" effect="light">废标风险</el-tag>
        <span v-else>-</span>
      </template>
    </el-table-column>
    <el-table-column label="所需证明" min-width="150">
      <template #default="{ row }">{{ formatEvidenceType(row.requiredEvidenceType) }}</template>
    </el-table-column>
    <el-table-column label="风险等级" width="110">
      <template #default="{ row }">
        <RiskLevelTag :value="row.riskLevel" />
      </template>
    </el-table-column>
    <el-table-column label="解析依据" min-width="220" show-overflow-tooltip>
      <template #default="{ row }">{{ formatExplanation(row.aiExplanation) }}</template>
    </el-table-column>
  </el-table>
</template>
