import { apiGet, apiPostForm } from '../lib/httpClient'
import type { CreateDocumentInput, DocumentDto, PagedResult, UpdateDocumentInput } from '../types/api'

function appendIfPresent(form: FormData, key: string, value: string | null | undefined) {
  if (value !== null && value !== undefined && value !== '') {
    form.append(key, value)
  }
}

export const documentApi = {
  async getPaged(pageNumber: number, pageSize: number, includeInactiveItems = false): Promise<PagedResult<DocumentDto>> {
    return apiGet<PagedResult<DocumentDto>>('/Document/paged', { pageNumber, pageSize, includeInactiveItems })
  },

  async getById(documentId: string): Promise<DocumentDto> {
    return apiGet<DocumentDto>('/Document/Get', { documentId })
  },

  // Document Number, revision and serial number are never sent here - they
  // are generated server-side (HSEQ.Domain.DocumentNumbering) and rejected as
  // input by CreateDocumentRequestModel, which doesn't even expose them.
  async add(input: CreateDocumentInput): Promise<void> {
    const form = new FormData()
    form.append('Name', input.name)
    appendIfPresent(form, 'FormerReviewDate', input.formerReviewDate)
    appendIfPresent(form, 'CurrentReviewDate', input.currentReviewDate)
    appendIfPresent(form, 'RelatedDocumentId', input.relatedDocumentId)
    form.append('File', input.file)
    form.append('ProjectId', input.projectId)
    form.append('OrganizationalManagementId', input.organizationalManagementId)
    form.append('OrganizationalActivityId', input.organizationalActivityId)
    form.append('DocumentTypeId', input.documentTypeId)
    await apiPostForm<void>('/Document/Add', form)
  },

  // Number/Project/organizational classification etc. are immutable and intentionally
  // not sent - UpdateDocumentRequestModel doesn't accept them either. File is
  // only appended when the caller actually picked a new one; the backend now
  // (see DocumentService.UpdateAsync) leaves FileName untouched when no File
  // part is present, instead of blanking it out.
  async update(input: UpdateDocumentInput): Promise<void> {
    const form = new FormData()
    form.append('Key', input.key)
    form.append('IsActive', String(input.isActive))
    form.append('Name', input.name)
    appendIfPresent(form, 'FormerReviewDate', input.formerReviewDate)
    appendIfPresent(form, 'CurrentReviewDate', input.currentReviewDate)
    appendIfPresent(form, 'RelatedDocumentId', input.relatedDocumentId)
    if (input.file) {
      form.append('File', input.file)
    }
    await apiPostForm<void>('/Document/Update', form)
  },

  async remove(documentId: string): Promise<void> {
    const body = new URLSearchParams()
    body.set('documentId', documentId)
    await apiPostForm<void>('/Document/Delete', body)
  },
}