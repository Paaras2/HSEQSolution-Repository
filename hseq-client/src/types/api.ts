// Mirrors HSEQ.API.Model DTOs/RequestModels. Field names match the backend's
// camelCase JSON output (System.Text.Json default naming policy) - do not
// rename these to "look nicer" without checking the actual API response.

// پاسخِ ورود. «user» بخشی از پاسخِ checkCredential سامانه‌ی مدیریت کاربران است که
// صفحه لازم دارد - کد ملی و موبایل عمداً به مرورگر نمی‌رسند.
export interface LoginUser {
  pCode: number
  firstName?: string | null
  lastName?: string | null
  isFirstLogin: boolean
}

export interface LoginResult {
  token: string
  // سرورِ پیش از این تغییر فقط token برمی‌گرداند.
  user?: LoginUser
}

export interface ApiErrorBody {
  message?: string
  code?: number
  reason?: string
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
  // نسخه‌ی انگلیسی مدرک؛ شماره‌اش با « (EN)» تمام می‌شود.
  isEnglishVersion: boolean
  // شماره‌ی مدرک با ساختار کدِ فعلی نمی‌خواند: مدرکی از سامانه‌ی قدیم که با شماره‌ی
  // تاریخیِ خودش وارد شده تا از دست نرود. فقط یک نشانِ دیداری است و هیچ رفتاری را
  // عوض نمی‌کند؛ تصمیم درباره‌ی کدینگ این مدارک بعداً گرفته می‌شود.
  isOutsideCodingStructure: boolean
  // Previous link in the revision chain: the revision this document superseded.
  relatedDocumentId: string | null
  relatedDocumentNumber: string | null
  // شماره‌ی مدارک مرتبط (جدول DocumentRelations) - با relatedDocumentNumber بالا فرق دارد
  // که نسخه‌ی قبلی در زنجیره‌ی بازنگری است. فقط فهرست صفحه‌بندی‌شده آن را پر می‌کند؛
  // بقیه‌ی مسیرها آرایه‌ی خالی می‌دهند.
  relatedDocumentNumbers: string[]
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
  // تکه‌ی متنِ فایل که عبارت جستجو در آن پیدا شده. فقط وقتی مقدار دارد که «جستجو در
  // محتوای فایل» روشن باشد و تطابق در خودِ فایل رخ داده باشد.
  contentSnippet: string | null
}

// نتیجه‌ی بازسازی متن قابل‌جستجوی فایل اسناد (پنل ادمین).
export interface FileTextIndexResult {
  total: number
  indexed: number
  // فایل باز شد ولی متنی نداشت - یعنی PDF اسکن‌شده یا تصویر.
  withoutText: number
  fileMissing: number
  failed: number
  problems: string[]
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
  // نسخه‌ی انگلیسی: سرور پسوند « (EN)» را آخر شماره می‌گذارد. سریال مستقل گرفته می‌شود.
  isEnglishVersion: boolean
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

// آینه‌ی HSEQ.API.Model.Dtos.DashboardSummaryDto - همه‌ی محاسبات سمت سرور انجام شده،
// این فقط شکل داده‌ی آماده‌ی رندر است.
export interface NamedCount {
  code: string
  label: string
  count: number
}

export interface MonthCount {
  monthLabel: string
  count: number
}

export interface DocumentAlert {
  key: string
  number: string
  name: string
  // هر دو فقط برای فهرست «بدون تاریخ بازبینی» نال‌اند.
  reviewDate: string | null
  daysUntilDue: number | null
}

export interface ReviewStatusBreakdown {
  onTrack: number
  dueSoon: number
  overdue: number
  noDateSet: number
}

export interface DashboardSummary {
  totalDocuments: number
  activeDocuments: number
  dueForReview: number
  overdueReviews: number

  expiredReviews: DocumentAlert[]
  dueWithin7Days: DocumentAlert[]
  dueWithin30Days: DocumentAlert[]
  missingReviewDate: DocumentAlert[]

  documentsByManagement: NamedCount[]
  documentsByActivity: NamedCount[]
  reviewStatus: ReviewStatusBreakdown
  reviewTrend: MonthCount[]
  documentsByType: NamedCount[]
}

// ---------------------------------------------------------------------------
// مدارک مرتبط - شبکه‌ی ارجاع میان مدارک مستقل.
//
// این را با relatedDocumentId بالا اشتباه نگیرید: آن فیلد زنجیره‌ی بازنگریِ خودِ
// همان مدرک است (نسخه‌ی قبلی)، در حالی که اینجا صحبت از دو مدرک با شماره‌های
// متفاوت است - مثلاً یک دستورالعمل و فرمی که با آن پر می‌شود.
// ---------------------------------------------------------------------------

// مقدار خام HSEQ.Common.DocumentRelationType.
export const DOCUMENT_RELATION_TYPES = [0, 1, 2, 3] as const
export type DocumentRelationTypeValue = (typeof DOCUMENT_RELATION_TYPES)[number]

// آینه‌ی HSEQ.API.Model.Dtos.DocumentRelationDto. همیشه مشخصات مدرکِ «سمت مقابل»
// را دارد، نه مدرکی که فهرست برایش باز شده.
export interface DocumentRelation {
  // کلید ردیفِ ارتباط (برای حذف) - نه کلید هیچ‌کدام از دو مدرک.
  key: string
  // مدرک سمت مقابل.
  documentId: string
  number: string
  name: string
  isActive: boolean
  isSuperseded: boolean
  relationType: number
  // مدرک جاری مبدأ این ارتباط است. برچسب نوع، برای نوع‌های جهت‌دار، بر همین اساس
  // معکوس می‌شود - documentRelationTypeLabel() را ببینید.
  isOutgoing: boolean
  note: string | null
  createdTime: string
  createdByPCode: number
}

export interface CreateDocumentRelationInput {
  sourceDocumentId: string
  targetDocumentId: string
  relationType: DocumentRelationTypeValue
  note: string | null
}

// یک ردیف ارتباط از دو سمت دو معنی دارد، چون ردیف جهت‌دار ذخیره می‌شود. مثلاً وقتی
// روی مدرک A ثبت می‌شود که «B مرجعِ A است»، همان ردیف در فهرست مدرک B باید بگوید
// «A وابسته است»، نه «A مرجع است». این جدول همان وارونگی است.
const RELATION_TYPE_LABELS: Record<number, { outgoing: string; incoming: string }> = {
  0: { outgoing: 'مرتبط', incoming: 'مرتبط' },
  1: { outgoing: 'مرجع', incoming: 'وابسته' },
  2: { outgoing: 'پیوست', incoming: 'مدرک اصلی' },
  3: { outgoing: 'جایگزین‌شده', incoming: 'جایگزین' },
}

// توضیح کامل هر ردیف، برای عنوان کمکی (title) کنار برچسب کوتاه.
const RELATION_TYPE_DESCRIPTIONS: Record<number, { outgoing: string; incoming: string }> = {
  0: { outgoing: 'این دو مدرک به هم مرتبط‌اند.', incoming: 'این دو مدرک به هم مرتبط‌اند.' },
  1: { outgoing: 'این مدرک به مدرک مقابل استناد می‌کند.', incoming: 'مدرک مقابل به این مدرک استناد می‌کند.' },
  2: { outgoing: 'مدرک مقابل، پیوست یا فرمِ این مدرک است.', incoming: 'این مدرک، پیوست یا فرمِ مدرک مقابل است.' },
  3: { outgoing: 'این مدرک جایگزین مدرک مقابل شده است.', incoming: 'مدرک مقابل جایگزین این مدرک شده است.' },
}

export function documentRelationTypeLabel(relationType: number, isOutgoing: boolean): string {
  const entry = RELATION_TYPE_LABELS[relationType] ?? RELATION_TYPE_LABELS[0]
  return isOutgoing ? entry.outgoing : entry.incoming
}

export function documentRelationTypeDescription(relationType: number, isOutgoing: boolean): string {
  const entry = RELATION_TYPE_DESCRIPTIONS[relationType] ?? RELATION_TYPE_DESCRIPTIONS[0]
  return isOutgoing ? entry.outgoing : entry.incoming
}

// گزینه‌های فرم افزودن. همیشه از دید مدرکی نوشته شده‌اند که فهرست برایش باز است -
// آن مدرک همیشه مبدأ ارتباط جدید است، پس فقط حالت outgoing لازم می‌شود.
export const DOCUMENT_RELATION_TYPE_OPTIONS: { value: DocumentRelationTypeValue; label: string }[] = [
  { value: 0, label: 'مرتبط با این مدرک' },
  { value: 1, label: 'مرجعِ این مدرک است' },
  { value: 2, label: 'پیوست/فرمِ این مدرک است' },
  { value: 3, label: 'این مدرک جایگزین آن شده است' },
]


// ---------------------------------------------------------------------------
// پنل ادمین
// ---------------------------------------------------------------------------

// آینه‌ی HSEQ.API.Model.RequestModels.MasterDataKind - یک اندپوینت مشترک برای
// هر چهار نوع اطلاعات پایه، با همین عدد تفکیک می‌شود.
export const MASTER_DATA_KINDS = {
  Project: 0,
  DocumentType: 1,
  OrganizationalManagement: 2,
  OrganizationalActivity: 3,
} as const

export type MasterDataKind = (typeof MASTER_DATA_KINDS)[keyof typeof MASTER_DATA_KINDS]

// آینه‌ی HSEQ.Common.AppRole. نبودِ ردیف یعنی «فقط مشاهده»، پس ۰ اینجا وجود ندارد.
export const APP_ROLES = {
  DocumentManager: 1,
  Admin: 2,
} as const

export type AppRoleValue = (typeof APP_ROLES)[keyof typeof APP_ROLES]

export function appRoleLabel(role: number): string {
  return role === APP_ROLES.Admin ? 'مدیر سیستم' : 'مدیر اسناد'
}

// آینه‌ی HSEQ.API.Model.Dtos.MasterDataItemDto. فیلدهای اختیاری فقط برای نوعی که
// به آن‌ها نیاز دارد پر می‌شوند - یک جدول مشترک برای هر چهار نوع.
export interface MasterDataItem {
  key: string
  code: string
  title: string
  isActive: boolean
  // فقط پروژه
  isProjectRelated: boolean | null
  // فقط فعالیت سازمانی
  organizationalManagementId: string | null
  // تعداد مدارک وابسته؛ مبنای اینکه غیرفعال‌سازی مجاز است یا نه.
  usageCount: number
}

// یک ردیف از آرشیو شماره‌مدارک قدیمی ستاد. آینه‌ی LegacyDocumentNumberDto سمت سرور.
// فقط خواندنی: این جدول یک‌بار از اکسل ثبت مدارک وارد شده و به‌روزرسانی نمی‌شود.
export interface LegacyDocumentNumber {
  key: string
  rawNumber: string
  code5: string | null
  serialNumber: number | null
  revisionSuffix: string | null
  managementCode: string | null
  activityCode: string | null
  documentTypeCode: string | null
  name: string | null
  unitLabel: string | null
  // تاریخ‌ها متن خام شمسی‌اند، نه ISO میلادی - همان‌طور که در دیتابیس ذخیره شده‌اند،
  // پس برخلاف بقیه‌ی تاریخ‌های برنامه از isoToJalaliText رد نمی‌شوند.
  lastEditShamsiDate: string | null
  currentEditShamsiDate: string | null
  currentVersion: string | null
}

export interface LegacyDocumentNumberPagedResult {
  items: LegacyDocumentNumber[]
  pageNumber: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface AppUser {
  key: string
  pcode: number
  role: number
  createdTime: string
  modifiedDate: string | null
}

// عنوان و طول کدِ هر نوع. طول‌ها با پیکربندی EF و ساختار شماره‌ی مدرک یکی‌اند.
export const MASTER_DATA_META: Record<
  MasterDataKind,
  { label: string; codeLength: number; needsManagement: boolean; hasProjectFlag: boolean }
> = {
  [MASTER_DATA_KINDS.Project]: { label: 'پروژه', codeLength: 4, needsManagement: false, hasProjectFlag: true },
  [MASTER_DATA_KINDS.DocumentType]: { label: 'نوع سند', codeLength: 2, needsManagement: false, hasProjectFlag: false },
  [MASTER_DATA_KINDS.OrganizationalManagement]: { label: 'مدیریت سازمانی', codeLength: 1, needsManagement: false, hasProjectFlag: false },
  [MASTER_DATA_KINDS.OrganizationalActivity]: { label: 'فعالیت سازمانی', codeLength: 2, needsManagement: true, hasProjectFlag: false },
}
