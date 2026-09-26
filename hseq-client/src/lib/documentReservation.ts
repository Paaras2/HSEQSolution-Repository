// ماشین حالتِ فرمِ «رزرو کد». یک تابعِ خالص و مستقل از React، پس گذارهای غیرقانونی
// (نهایی‌سازی پیش از رزرو، ویرایشِ متادیتا بعد از رزرو، ارسالِ دوباره حینِ busy) اینجا
// ناممکن می‌شوند - نه با شرط‌های پخش‌شده در JSX - و مثل loginFeedback واحد-تست می‌شود.
import type { DocumentCategory } from '../types/api.ts'

export type ReservationStatus = 'draft' | 'reserving' | 'reserved' | 'uploading' | 'done'

export interface ReservationMetadata {
  name: string
  category: DocumentCategory
  isEnglishVersion: boolean
  projectId: string
  managementId: string
  activityId: string
  documentTypeId: string
}

export interface ReservationState {
  status: ReservationStatus
  metadata: ReservationMetadata
  file: File | null
  reservedKey: string | null
  reservedCode: string | null
  reservationExpiresAt: string | null
  error: string | null
}

export type ReservationAction =
  | { type: 'FIELD_CHANGED'; field: keyof ReservationMetadata; value: string | boolean }
  | { type: 'FILE_SELECTED'; file: File | null }
  | { type: 'RESERVE_STARTED' }
  | { type: 'RESERVE_SUCCEEDED'; key: string; code: string; expiresAt: string | null }
  | { type: 'RESERVE_FAILED'; message: string }
  | { type: 'UPLOAD_STARTED' }
  | { type: 'UPLOAD_SUCCEEDED' }
  | { type: 'UPLOAD_FAILED'; message: string }
  | { type: 'RESET' }

export const initialReservationState: ReservationState = {
  status: 'draft',
  metadata: {
    name: '',
    category: 'Headquarters',
    isEnglishVersion: false,
    projectId: '',
    managementId: '',
    activityId: '',
    documentTypeId: '',
  },
  file: null,
  reservedKey: null,
  reservedCode: null,
  reservationExpiresAt: null,
  error: null,
}

export function reservationReducer(state: ReservationState, action: ReservationAction): ReservationState {
  switch (action.type) {
    // متادیتا فقط پیش از رزرو قابل ویرایش است؛ پس از آن قفل می‌شود (کد از روی همین ساخته شده).
    case 'FIELD_CHANGED':
      if (state.status !== 'draft') return state
      return { ...state, metadata: { ...state.metadata, [action.field]: action.value }, error: null }

    case 'FILE_SELECTED':
      return { ...state, file: action.file, error: null }

    case 'RESERVE_STARTED':
      if (state.status !== 'draft') return state // گاردِ دابل‌کلیک
      return { ...state, status: 'reserving', error: null }

    case 'RESERVE_SUCCEEDED':
      return {
        ...state,
        status: 'reserved',
        reservedKey: action.key,
        reservedCode: action.code,
        reservationExpiresAt: action.expiresAt,
        error: null,
      }

    // شکستِ رزرو → برگشت به draft با حفظِ کاملِ متادیتا تا کاربر اصلاح و دوباره تلاش کند.
    case 'RESERVE_FAILED':
      return { ...state, status: 'draft', error: action.message }

    case 'UPLOAD_STARTED':
      if (state.status !== 'reserved' || !state.file) return state // بدون فایل، نهایی‌سازی نه
      return { ...state, status: 'uploading', error: null }

    case 'UPLOAD_SUCCEEDED':
      return { ...state, status: 'done', error: null }

    // نکته‌ی مهمِ UX: شکستِ بارگذاری کدِ رزرو را از دست نمی‌دهد - روی reserved می‌مانیم.
    case 'UPLOAD_FAILED':
      return { ...state, status: 'reserved', error: action.message }

    case 'RESET':
      return initialReservationState

    default:
      return state
  }
}

export function isMetadataComplete(m: ReservationMetadata): boolean {
  return (
    m.name.trim() !== '' &&
    m.managementId !== '' &&
    m.activityId !== '' &&
    m.documentTypeId !== '' &&
    // «پروژه» فقط در دسته‌بندی پروژه الزامی است.
    (m.category !== 'Project' || m.projectId !== '')
  )
}

export interface ReservationFlags {
  canReserve: boolean
  metadataLocked: boolean
  fileRequired: boolean
  canFinalize: boolean
  isBusy: boolean
}

// کلِ منطقِ نمایش از این flagها می‌آید، نه از if های پخش‌شده در JSX.
export function reservationFlags(state: ReservationState): ReservationFlags {
  return {
    canReserve: state.status === 'draft' && isMetadataComplete(state.metadata),
    metadataLocked: state.status !== 'draft',
    fileRequired: state.status === 'reserved',
    canFinalize: state.status === 'reserved' && state.file !== null,
    isBusy: state.status === 'reserving' || state.status === 'uploading',
  }
}
