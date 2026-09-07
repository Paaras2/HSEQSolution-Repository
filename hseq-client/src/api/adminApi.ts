import { apiGet, apiPostForm } from '../lib/httpClient'
import type {
  AppUser,
  AppRoleValue,
  FileTextIndexResult,
  LegacyDocumentNumberPagedResult,
  MasterDataItem,
  MasterDataKind,
} from '../types/api'

// پنل ادمین: اطلاعات پایه + نقش کاربران.
//
// نوشتن اطلاعات پایه روی MasterDataController است (کنار خواندنِ همان داده‌ها) و
// مدیریت نقش روی AdminController - همان تقسیمی که سمت سرور وجود دارد.
export const adminApi = {
  // ---- اطلاعات پایه ----

  // برخلاف لیست‌های فرم سند، این یکی غیرفعال‌ها را هم می‌دهد تا بشود دوباره فعالشان کرد.
  async listMasterData(kind: MasterDataKind): Promise<MasterDataItem[]> {
    return apiGet<MasterDataItem[]>('/MasterData/admin/list', { kind })
  },

  async createMasterData(input: {
    kind: MasterDataKind
    code: string
    title: string
    isProjectRelated: boolean
    organizationalManagementId: string | null
  }): Promise<void> {
    const body = new URLSearchParams()
    body.set('Kind', String(input.kind))
    body.set('Code', input.code)
    body.set('Title', input.title)
    body.set('IsProjectRelated', String(input.isProjectRelated))
    if (input.organizationalManagementId) {
      body.set('OrganizationalManagementId', input.organizationalManagementId)
    }
    await apiPostForm<void>('/MasterData/admin/create', body)
  },

  // کد عمداً فرستاده نمی‌شود: داخل شماره‌ی مدارک صادرشده حک شده و سرور هم نمی‌پذیردش.
  async updateMasterData(input: {
    kind: MasterDataKind
    key: string
    title: string
    isActive: boolean
    isProjectRelated: boolean
  }): Promise<void> {
    const body = new URLSearchParams()
    body.set('Kind', String(input.kind))
    body.set('Key', input.key)
    body.set('Title', input.title)
    body.set('IsActive', String(input.isActive))
    body.set('IsProjectRelated', String(input.isProjectRelated))
    await apiPostForm<void>('/MasterData/admin/update', body)
  },

  // ---- آرشیو شماره‌های قدیمی ----
  // برخلاف اطلاعات پایه، جستجو و صفحه‌بندی سمت سرور انجام می‌شود: جدول ۷۶۸ ردیف دارد.
  async listLegacyNumbers(
    query: string,
    pageNumber: number,
    pageSize: number,
  ): Promise<LegacyDocumentNumberPagedResult> {
    return apiGet<LegacyDocumentNumberPagedResult>('/MasterData/admin/legacy-numbers', {
      Query: query.trim() || undefined,
      PageNumber: pageNumber,
      PageSize: pageSize,
    })
  },

  // ---- نقش کاربران ----

  async listUsers(): Promise<AppUser[]> {
    return apiGet<AppUser[]>('/Admin/users')
  },

  async setUserRole(pcode: number, role: AppRoleValue): Promise<void> {
    const body = new URLSearchParams()
    body.set('Pcode', String(pcode))
    body.set('Role', String(role))
    await apiPostForm<void>('/Admin/users/set-role', body)
  },

  // برداشتن نقش، کاربر را به «فقط مشاهده» برمی‌گرداند (ردیفش حذف می‌شود).
  async removeUserRole(pcode: number): Promise<void> {
    const body = new URLSearchParams()
    body.set('pcode', String(pcode))
    await apiPostForm<void>('/Admin/users/remove-role', body)
  },

  // ---- نمایه‌ی محتوای فایل ----

  // بازسازی متن قابل‌جستجوی فایل‌ها. onlyMissing=true فقط اسنادِ بدون متن را می‌سازد.
  // درخواستِ کوتاهی نیست (کل آرشیو خوانده می‌شود)، پس فراخوان باید حالت «در حال
  // انجام» را نشان بدهد.
  async reindexFileText(onlyMissing: boolean): Promise<FileTextIndexResult> {
    const body = new URLSearchParams()
    body.set('onlyMissing', String(onlyMissing))
    return apiPostForm<FileTextIndexResult>('/Admin/reindex-file-text', body)
  },
}
