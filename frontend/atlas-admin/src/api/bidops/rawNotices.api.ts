import { http } from '@/api/http'
import type {
  EnqueueJobDto,
  ImportPublicUrlRequest,
  PagedResult,
  RawAttachmentDto,
  RawAttachmentTextDto,
  RawNoticeDto,
  RawNoticePipelineDto,
  RawNoticeSearchQuery,
  ReparseRawNoticeRequest,
  BidOpsId,
} from '@/modules/bidops/types'

const base = '/bidops/raw-notices'

export const rawNoticesApi = {
  search(params: RawNoticeSearchQuery) {
    return http.get<PagedResult<RawNoticeDto>>(base, { params })
  },

  get(id: BidOpsId) {
    return http.get<RawNoticeDto>(`${base}/${id}`)
  },

  pipeline(id: BidOpsId) {
    return http.get<RawNoticePipelineDto>(`${base}/${id}/pipeline`)
  },

  attachments(id: BidOpsId) {
    return http.get<RawAttachmentDto[]>(`${base}/${id}/attachments`)
  },

  attachmentText(id: BidOpsId, attachmentId: BidOpsId) {
    return http.get<RawAttachmentTextDto>(`${base}/${id}/attachments/${attachmentId}/text`)
  },

  attachmentFile(id: BidOpsId, attachmentId: BidOpsId, download = false) {
    return http.get<Blob>(`${base}/${id}/attachments/${attachmentId}/file`, {
      params: { download },
      responseType: 'blob',
    })
  },

  importUrl(data: ImportPublicUrlRequest) {
    return http.post<EnqueueJobDto>(`${base}/import-url`, data)
  },

  reparse(id: BidOpsId, data?: ReparseRawNoticeRequest) {
    return http.post<EnqueueJobDto>(`${base}/${id}/reparse`, data ?? {})
  },
}
