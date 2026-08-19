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

// HSEQ.Common.DocumentCategory: Headquarters=0 (no Project - O+AA+DD-SSS-R,
// e.g. "ASYFM-008-A"), Project=1 (PPPP+O+AA+DD+SSS+R, e.g. "P008QHSBD153A03").
export type DocumentCategory = 'Headquarters' | 'Project'

const DOCUMENT_CATEGORY_BY_VALUE: Record<number, DocumentCategory> = {
  0: 'Headquarters',
  1: 'Project',
}

export function documentCategoryLabel(value: number): string {
  return DOCUMENT_CATEGORY_BY_VALUE[value] === 'Project' ? 'پروژه' : 'ستاد'
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
  // Previous link in the revision chain: the revision this document superseded.
  relatedDocumentId: string | null
  relatedDocumentNumber: string | null
  // A newer revision of this document exists. Both a superseded and a deactivated
  // document have isActive === false; this is what tells them apart.
  isSuperseded: boolean
  fileName: string | null
  // Raw HSEQ.Common.DocumentCategory value (0|1) - see documentCategoryLabel().
  category: number
  // Only set when category is Project - Headquarters documents have no Project.
  projectId: string | null
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
  category: DocumentCategory
  formerReviewDate: string | null
  currentReviewDate: string | null
  relatedDocumentId: string | null
  file: File
  // Required only when category is 'Project' - null for 'Headquarters'.
  projectId: string | null
  organizationalManagementId: string
  organizationalActivityId: string
  documentTypeId: string
}

// Metadata only. relatedDocumentId and file are both absent on purpose and the
// server rejects them either way: the chain link is immutable once ReviseDocument
// writes it, and replacing content is what a revision is for - see
// ReviseDocumentInput.
export interface UpdateDocumentInput {
  key: string
  isActive: boolean
  name: string
  formerReviewDate: string | null
  currentReviewDate: string | null
}

// Issues the next revision of `key` as a new document. Number, revision and the
// organizational classification are all derived server-side from the document
// being revised - only the content that actually changes is sent.
export interface ReviseDocumentInput {
  key: string
  name: string
  formerReviewDate: string | null
  currentReviewDate: string | null
  // Required: a revision always carries a new file.
  file: File
}

// جستجوی پیشرفته - همه‌ی فیلترها اختیاری‌اند (نال یعنی نادیده گرفته شود).
// آینه‌ی HSEQ.API.Model.RequestModels.SearchDocumentsRequestModel.
export interface SearchDocumentsInput {
  query: string
  isActive: boolean | null
  category: DocumentCategory | null
  documentTypeId: string | null
  organizationalManagementId: string | null
  organizationalActivityId: string | null
  projectId: string | null
  onlyLatestRevision: boolean
  searchInFileContent: boolean
  pageNumber: number
  pageSize: number
}

// خروجی سبک برای پیشنهاد خودکار هنگام تایپ.
export interface DocumentSuggestion {
  key: string
  number: string
  name: string
}