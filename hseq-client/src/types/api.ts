// Mirrors HSEQ.API.Model DTOs/RequestModels. Field names match the backend's
// camelCase JSON output (System.Text.Json default naming policy) - do not
// rename these to "look nicer" without checking the actual API response.

export interface LoginResult {
  token: string
}

export interface ApiErrorBody {
  message?: string
  code?: number
}

// HSEQ.Common.DocumentVersion: A=1 .. Z=26. Kept here only to render the
// revision letter for display - the backend is the sole source of this value.
export function documentVersionLabel(value: number): string {
  if (value < 1 || value > 26) return '-'
  return String.fromCharCode(64 + value)
}

export interface DocumentDto {
  key: string
  isActive: boolean
  createdTime: string
  modifiedDate: string | null
  number: string
  name: string
  formerReviewDate: string | null
  currentReviewDate: string | null
  lastVersion: number
  contentRevision: number | null
  serialNumber: number
  relatedDocumentId: string | null
  relatedDocumentNumber: string | null
  fileName: string | null
  projectId: string
  organizationalManagementId: string
  organizationalActivityId: string
  documentTypeId: string
  file: string | null
  createdByPCode: number
}

export interface PagedResult<T> {
  items: T[]
  pageNumber: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface ProjectLookup {
  key: string
  code: string
  title: string
  isProjectRelated: boolean
}

export interface OrganizationalManagementLookup {
  key: string
  code: string
  title: string
}

export interface OrganizationalActivityLookup {
  key: string
  code: string
  title: string
  organizationalManagementId: string
}

export interface DocumentTypeLookup {
  key: string
  code: string
  title: string
}

// Fields the Add Document form actually collects. Document Number, revision
// and serial are never sent - they are computed server-side.
export interface CreateDocumentInput {
  name: string
  formerReviewDate: string | null
  currentReviewDate: string | null
  relatedDocumentId: string | null
  file: File
  projectId: string
  organizationalManagementId: string
  organizationalActivityId: string
  documentTypeId: string
}

export interface UpdateDocumentInput {
  key: string
  isActive: boolean
  name: string
  formerReviewDate: string | null
  currentReviewDate: string | null
  relatedDocumentId: string | null
  file: File | null
}