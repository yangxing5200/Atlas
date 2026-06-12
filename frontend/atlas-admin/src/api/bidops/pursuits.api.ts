import { http } from '@/api/http'
import type {
  BidOpsId,
  ChangePursuitStatusRequest,
  CreatePursuitFollowRecordRequest,
  CreatePursuitRequest,
  CreatePursuitTaskRequest,
  PagedResult,
  PursuitDetailDto,
  PursuitDto,
  PursuitFollowRecordDto,
  PursuitSearchQuery,
  PursuitTaskDto,
  UpdatePursuitRequest,
  UpdatePursuitTaskRequest,
} from '@/modules/bidops/types'

const base = '/bidops/pursuits'

export const pursuitsApi = {
  search(params: PursuitSearchQuery) {
    return http.get<PagedResult<PursuitDto>>(base, { params })
  },

  create(request: CreatePursuitRequest) {
    return http.post<PursuitDto>(base, request)
  },

  get(id: BidOpsId) {
    return http.get<PursuitDetailDto>(`${base}/${id}`)
  },

  update(id: BidOpsId, request: UpdatePursuitRequest) {
    return http.put<PursuitDto>(`${base}/${id}`, request)
  },

  changeStatus(id: BidOpsId, request: ChangePursuitStatusRequest) {
    return http.post<PursuitDto>(`${base}/${id}/status`, request)
  },

  tasks(id: BidOpsId) {
    return http.get<PursuitTaskDto[]>(`${base}/${id}/tasks`)
  },

  createTask(id: BidOpsId, request: CreatePursuitTaskRequest) {
    return http.post<PursuitTaskDto>(`${base}/${id}/tasks`, request)
  },

  updateTask(id: BidOpsId, taskId: BidOpsId, request: UpdatePursuitTaskRequest) {
    return http.put<PursuitTaskDto>(`${base}/${id}/tasks/${taskId}`, request)
  },

  followRecords(id: BidOpsId) {
    return http.get<PursuitFollowRecordDto[]>(`${base}/${id}/follow-records`)
  },

  createFollowRecord(id: BidOpsId, request: CreatePursuitFollowRecordRequest) {
    return http.post<PursuitFollowRecordDto>(`${base}/${id}/follow-records`, request)
  },
}
