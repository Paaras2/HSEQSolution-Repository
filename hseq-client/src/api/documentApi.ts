import { apiGet, apiGetBlob, apiPostForm } from '../lib/httpClient'
import type {
  CreateDocumentInput,
  DocumentDto,
  DocumentReservation,
  EditDocumentCodingInput,
  PagedResult,
  ReplaceDocumentFileInput,
  ReserveDocumentInput,
  ReviseDocumentInput,
  UpdateDocumentInput,
} from '../types/api'

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

  // Every revision of the document this key belongs to, oldest first. Works from
  // any revision in the chain, not just the current one.
  async getRevisionHistory(documentId: string): Promise<DocumentDto[]> {
    return apiGet<DocumentDto[]>('/Document/revisions', { documentId })
  },

  // The stored file of one specific document (current or superseded revision).
  // asAttachment=false lets the browser preview it; true forces a save.
  async getFile(documentId: string, asAttachment = false): Promise<Blob> {
    return apiGetBlob('/Document/Download', { documentId, asAttachment })
  },

  // Document Number, revision and serial number are never sent here - they
  // are generated server-side (HSEQ.Domain.DocumentNumbering) and rejected as
  // input by CreateDocumentRequestModel, which doesn't even expose them.
  // کلید سند تازه‌ساخته‌شده را برمی‌گرداند، چون فرم افزودن بلافاصله بعد از ذخیره باید
  // مدارک مرتبطِ انتخاب‌شده را ثبت کند و ارتباط به کلید هر دو سر نیاز دارد.
  async add(input: CreateDocumentInput): Promise<string> {
    const form = new FormData()
    form.append('Name', input.name)
    form.append('Category', input.category)
    form.append('IsEnglishVersion', String(input.isEnglishVersion))
    appendIfPresent(form, 'FormerReviewDate', input.formerReviewDate)
    appendIfPresent(form, 'CurrentReviewDate', input.currentReviewDate)
    appendIfPresent(form, 'RelatedDocumentId', input.relatedDocumentId)
    form.append('File', input.file)
    // Only sent for category 'Project' - omitted (not even an empty string) for
    // 'Headquarters', so CreateDocumentRequestModel.ProjectId binds to null.
    appendIfPresent(form, 'ProjectId', input.projectId)
    form.append('OrganizationalManagementId', input.organizationalManagementId)
    form.append('OrganizationalActivityId', input.organizationalActivityId)
    form.append('DocumentTypeId', input.documentTypeId)
    const created = await apiPostForm<{ key: string }>('/Document/Add', form)
    return created.key
  },

  // «رزرو کد»: شماره را می‌سازد و یک مدرکِ Reserved (بدون فایل) ثبت می‌کند. کد و مهلتِ
  // رزرو برمی‌گردد؛ فایل بعداً با attachFile ضمیمه می‌شود. مثل add، شماره از کلاینت نمی‌رود.
  async reserve(input: ReserveDocumentInput): Promise<DocumentReservation> {
    const form = new FormData()
    form.append('Name', input.name)
    form.append('Category', input.category)
    form.append('IsEnglishVersion', String(input.isEnglishVersion))
    // فقط برای 'Project' فرستاده می‌شود؛ برای 'Headquarters' اصلاً نمی‌رود تا سرور null ببیند.
    appendIfPresent(form, 'ProjectId', input.projectId)
    form.append('OrganizationalManagementId', input.organizationalManagementId)
    form.append('OrganizationalActivityId', input.organizationalActivityId)
    form.append('DocumentTypeId', input.documentTypeId)
    return apiPostForm<DocumentReservation>('/Document/Reserve', form)
  },

  // نهایی‌سازیِ یک رزرو: فایل را به مدرکِ Reserved ضمیمه و آن را Published می‌کند.
  async attachFile(key: string, file: File): Promise<void> {
    const form = new FormData()
    form.append('Key', key)
    form.append('File', file)
    await apiPostForm<void>('/Document/AttachFile', form)
  },

  // Metadata only. Number/Project/organizational classification are immutable and
  // intentionally not sent - UpdateDocumentRequestModel doesn't accept them either.
  // Neither is RelatedDocumentId (owned by the revision flow) nor the file:
  // changing content is a revision, so it goes through revise() below.
  async update(input: UpdateDocumentInput): Promise<void> {
    const form = new FormData()
    form.append('Key', input.key)
    form.append('IsActive', String(input.isActive))
    form.append('Name', input.name)
    appendIfPresent(form, 'FormerReviewDate', input.formerReviewDate)
    appendIfPresent(form, 'CurrentReviewDate', input.currentReviewDate)
    await apiPostForm<void>('/Document/Update', form)
  },

  // Creates the NEXT REVISION of `key` as a new document and retires the one being
  // revised - it does not edit it. The new Document Number is computed server-side
  // (DocumentNumberGeneratorService.GenerateNextRevisionAsync), so nothing about
  // numbering or classification is sent from here.
  async revise(input: ReviseDocumentInput): Promise<void> {
    const form = new FormData()
    form.append('Key', input.key)
    form.append('Name', input.name)
    appendIfPresent(form, 'FormerReviewDate', input.formerReviewDate)
    appendIfPresent(form, 'CurrentReviewDate', input.currentReviewDate)
    form.append('File', input.file)
    await apiPostForm<void>('/Document/Revise', form)
  },

  // جایگزینیِ فایلِ همین سند، بدون بازنگری - سطرِ تازه‌ای ساخته نمی‌شود و شماره/حرفِ
  // بازنگری (مثلاً همان A) دست‌نخورده می‌ماند. برای اصلاحاتی که به بازنگریِ رسمی نیاز
  // ندارند (مثل رفعِ یک خطای تایپی)؛ برای تغییرِ محتوایی سند از revise() استفاده شود.
  async replaceFile(input: ReplaceDocumentFileInput): Promise<void> {
    const form = new FormData()
    form.append('Key', input.key)
    form.append('Name', input.name)
    appendIfPresent(form, 'FormerReviewDate', input.formerReviewDate)
    appendIfPresent(form, 'CurrentReviewDate', input.currentReviewDate)
    form.append('File', input.file)
    await apiPostForm<void>('/Document/ReplaceFile', form)
  },

  // ویرایشِ کدگذاری: عملیاتِ استثنایی برای اصلاحِ یک کدگذاریِ اشتباه - Number را روی
  // یک سندِ موجود (و اگر classification پر باشد، احتمالاً کلِ زنجیره‌اش) عوض می‌کند.
  async editCoding(input: EditDocumentCodingInput): Promise<void> {
    const form = new FormData()
    form.append('Key', input.key)
    form.append('ChangeClassification', String(input.classification !== null))
    if (input.classification) {
      form.append('OrganizationalManagementId', input.classification.organizationalManagementId)
      form.append('OrganizationalActivityId', input.classification.organizationalActivityId)
      form.append('DocumentTypeId', input.classification.documentTypeId)
      appendIfPresent(form, 'SerialNumber', input.classification.serialNumber?.toString())
    }
    form.append('ChangeRevision', String(input.revision !== null))
    if (input.revision) {
      form.append('NewMajorVersion', String(input.revision.lastVersion))
      appendIfPresent(form, 'NewContentRevision', input.revision.contentRevision?.toString())
    }
    await apiPostForm<void>('/Document/EditCoding', form)
  },

  async remove(documentId: string): Promise<void> {
    const body = new URLSearchParams()
    body.set('documentId', documentId)
    await apiPostForm<void>('/Document/Delete', body)
  },
}