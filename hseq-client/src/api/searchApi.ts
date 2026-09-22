import { apiGet, apiGetBlob } from '../lib/httpClient'
import type { DocumentDto, DocumentSuggestion, ExportColumn, PagedResult, SearchDocumentsInput } from '../types/api'

// کلیدها با PascalCase چون بایندینگ [FromQuery] سمت سرور روی همون اسم‌های
// SearchDocumentsRequestModel هست (case-insensitive است، اما برای هماهنگی با بقیه‌ی پروژه همینو نگه داشتیم).
function toParams(input: SearchDocumentsInput): Record<string, string | number | boolean | undefined> {
  return {
    Query: input.query.trim() || undefined,
    IsActive: input.isActive ?? undefined,
    Category: input.category ?? undefined,
    DocumentTypeId: input.documentTypeId ?? undefined,
    OrganizationalManagementId: input.organizationalManagementId ?? undefined,
    OrganizationalActivityId: input.organizationalActivityId ?? undefined,
    ProjectId: input.projectId ?? undefined,
    OnlyLatestRevision: input.onlyLatestRevision,
    SearchInFileContent: input.searchInFileContent,
    PageNumber: input.pageNumber,
    PageSize: input.pageSize,
  }
}

export const searchApi = {
  async search(input: SearchDocumentsInput): Promise<PagedResult<DocumentDto>> {
    return apiGet<PagedResult<DocumentDto>>('/Search/documents', toParams(input))
  },

  // پیشنهاد خودکار هنگام تایپ - سرور هم زیر ۲ کاراکتر فهرست خالی می‌ده، این چک اینجا
  // فقط برای جلوگیری از یک درخواست شبکه‌ی بی‌فایده به ازای هر کلید اول است.
  async suggest(query: string): Promise<DocumentSuggestion[]> {
    if (query.trim().length < 2) return []
    return apiGet<DocumentSuggestion[]>('/Search/suggestions', { query: query.trim() })
  },

  // فقط Admin - سرور هم همین را اجبار می‌کند (۴۰۳ برای بقیه)، این چک فقط رابط
  // کاربری را زودتر و بدون یک رفت‌وبرگشتِ ناموفق درست نشان می‌دهد.
  async exportExcel(input: SearchDocumentsInput, columns?: string[]): Promise<Blob> {
    return apiGetBlob('/Search/export', { ...toParams(input), ExportColumns: columns })
  },

  // فهرستِ ستون‌های قابل‌انتخاب برای منوی سفارشی‌سازیِ خروجی - فقط Admin.
  async getExportColumns(): Promise<ExportColumn[]> {
    return apiGet<ExportColumn[]>('/Search/export/columns')
  },
}
