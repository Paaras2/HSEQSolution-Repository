import { useCallback, useEffect, useMemo, useState } from 'react'
import type { FormEvent, ReactNode } from 'react'
import { useAuth } from '../auth/AuthContext'
import { adminApi } from '../api/adminApi'
import { ApiError } from '../lib/httpClient'
import {
  APP_ROLES,
  MASTER_DATA_KINDS,
  MASTER_DATA_META,
  appRoleLabel,
} from '../types/api'
import type {
  AppRoleValue,
  AppUser,
  LegacyDocumentNumber,
  LegacyDocumentNumberPagedResult,
  MasterDataItem,
  MasterDataKind,
} from '../types/api'
import { LoadingState, EmptyState, ErrorState } from '../components/StateViews'
import { toLatinDigits, toPersianDigits } from '../lib/digits'
import { sortedByTitle } from '../lib/sorting'
import { IconButton } from '../components/IconButton'
import type { IconTone } from '../components/IconButton'
import {
  ArchiveIcon,
  ArrowDownIcon,
  ArrowUpIcon,
  BriefcaseIcon,
  BuildingIcon,
  CheckIcon,
  CloseIcon,
  DeactivateIcon,
  EditIcon,
  FileTextIcon,
  LayersIcon,
  PlusIcon,
  PowerIcon,
  SearchIcon,
  UserMinusIcon,
  UsersIcon,
} from '../components/Icons'

// تب‌های پنل: چهار نوع اطلاعات پایه + مدیریت کاربران.
const MASTER_DATA_TABS: MasterDataKind[] = [
  MASTER_DATA_KINDS.Project,
  MASTER_DATA_KINDS.DocumentType,
  MASTER_DATA_KINDS.OrganizationalManagement,
  MASTER_DATA_KINDS.OrganizationalActivity,
]

// هر تب آیکون و رنگ اختصاصی خودش را دارد؛ در یک ناوبریِ پنج‌تایی، رنگ سریع‌تر از متن
// خوانده می‌شود و کاربر بدون خواندنِ برچسب هم می‌فهمد کجاست.
const TAB_VISUALS: Record<MasterDataKind, { icon: ReactNode; tone: IconTone }> = {
  [MASTER_DATA_KINDS.Project]: { icon: <BriefcaseIcon size={17} />, tone: 'view' },
  [MASTER_DATA_KINDS.DocumentType]: { icon: <FileTextIcon size={17} />, tone: 'edit' },
  [MASTER_DATA_KINDS.OrganizationalManagement]: { icon: <BuildingIcon size={17} />, tone: 'revise' },
  [MASTER_DATA_KINDS.OrganizationalActivity]: { icon: <LayersIcon size={17} />, tone: 'history' },
}

type Tab =
  | { kind: 'masterData'; value: MasterDataKind }
  | { kind: 'users' }
  // آرشیو شماره‌های قدیمی - فقط خواندنی، پس نه فرم افزودن دارد نه عملیات ردیفی.
  | { kind: 'legacy' }

export function AdminPanelPage() {
  const { user } = useAuth()
  const isAdmin = user?.role === 'Admin'

  const [tab, setTab] = useState<Tab>({ kind: 'masterData', value: MASTER_DATA_KINDS.Project })

  return (
    <div>
      <div className="page-header">
        <div>
          <h1>پنل ادمین</h1>
          <p>تعریف اطلاعات پایه‌ی شماره‌گذاری و مدیریت سطح دسترسی کاربران.</p>
        </div>
      </div>

      {/* ناوبری تب‌ها: آیکون رنگی + برچسب. تب کاربران برای هر دو نقش دیده می‌شود؛
          محدودیت‌های داخلش بر اساس نقشِ خودِ کاربر اعمال می‌شود. */}
      <nav className="section-nav" aria-label="بخش‌های پنل ادمین">
        {MASTER_DATA_TABS.map((kind) => {
          const visual = TAB_VISUALS[kind]
          const isActive = tab.kind === 'masterData' && tab.value === kind
          return (
            <button
              key={kind}
              type="button"
              className={`section-nav__item section-nav__item--${visual.tone}${isActive ? ' is-active' : ''}`}
              onClick={() => setTab({ kind: 'masterData', value: kind })}
              aria-current={isActive ? 'page' : undefined}
            >
              <span className="section-nav__icon">{visual.icon}</span>
              <span className="section-nav__label">{MASTER_DATA_META[kind].label}</span>
            </button>
          )
        })}
        <button
          type="button"
          className={`section-nav__item section-nav__item--success${tab.kind === 'users' ? ' is-active' : ''}`}
          onClick={() => setTab({ kind: 'users' })}
          aria-current={tab.kind === 'users' ? 'page' : undefined}
        >
          <span className="section-nav__icon">
            <UsersIcon size={17} />
          </span>
          <span className="section-nav__label">کاربران و دسترسی</span>
        </button>

        {/* آرشیو شماره‌مدارک قدیمی ستاد - مرجع تاریخیِ واردشده از اکسل ثبت مدارک. */}
        <button
          type="button"
          className={`section-nav__item section-nav__item--warning${tab.kind === 'legacy' ? ' is-active' : ''}`}
          onClick={() => setTab({ kind: 'legacy' })}
          aria-current={tab.kind === 'legacy' ? 'page' : undefined}
        >
          <span className="section-nav__icon">
            <ArchiveIcon size={17} />
          </span>
          <span className="section-nav__label">آرشیو شماره‌های قدیمی</span>
        </button>
      </nav>

      {tab.kind === 'masterData' && (
        // remount با تغییر تب، تا فرم و فهرست از نو ساخته شوند نه اینکه حالت تب قبلی بماند.
        <MasterDataTab key={tab.value} kind={tab.value} />
      )}
      {tab.kind === 'users' && <UsersTab isAdmin={isAdmin} currentPcode={user?.pcode ?? null} />}
      {tab.kind === 'legacy' && <LegacyNumbersTab />}
    </div>
  )
}

// ---------------------------------------------------------------------------
// نوار بالای هر تب: عنوان + آمار یک‌نگاهی + جستجو + دکمه‌ی افزودن.
// چون هر دو تب همین ساختار را دارند، یک‌بار نوشته شده.
// ---------------------------------------------------------------------------
// یک قرصِ آمار. معنای «فعال/غیرفعال» فقط در اطلاعات پایه صدق می‌کند، پس تفکیک را
// خودِ تب تعیین می‌کند نه این نوار - وگرنه تب کاربران آماری می‌داد که معنا نداشت.
type PanelStat = { label: string; value: number; tone?: 'success' | 'muted' }

function PanelToolbar({
  title,
  stats,
  search,
  onSearch,
  searchPlaceholder,
  addLabel,
  isFormOpen,
  onToggleForm,
}: {
  title: string
  stats: PanelStat[]
  search: string
  onSearch: (value: string) => void
  searchPlaceholder: string
  // سه تای بعدی اختیاری‌اند: تب آرشیو فقط خواندنی است و دکمه‌ی افزودن ندارد.
  addLabel?: string
  isFormOpen?: boolean
  onToggleForm?: () => void
}) {
  return (
    <div className="panel-toolbar">
      <div className="panel-toolbar__lead">
        <h2 className="panel-toolbar__title">{title}</h2>
        {/* آمار به‌جای عددِ خشک: تفکیکِ فهرست در یک نگاه. */}
        <div className="panel-stats">
          {stats.map((stat) => (
            <span key={stat.label} className={`stat-pill${stat.tone ? ` stat-pill--${stat.tone}` : ''}`}>
              <strong>{toPersianDigits(stat.value)}</strong> {stat.label}
            </span>
          ))}
        </div>
      </div>

      <div className="panel-toolbar__actions">
        <div className="panel-search">
          <SearchIcon size={15} />
          <input
            type="search"
            value={search}
            onChange={(event) => onSearch(event.target.value)}
            placeholder={searchPlaceholder}
            aria-label={searchPlaceholder}
          />
        </div>
        {/* همان دکمه هم فرم را باز می‌کند و هم می‌بندد؛ آیکون با چرخش، حالت را نشان می‌دهد. */}
        {onToggleForm && (
          <button
            type="button"
            className={`btn ${isFormOpen ? 'btn-secondary' : 'btn-primary'}`}
            onClick={onToggleForm}
            aria-expanded={isFormOpen}
          >
            <span className={`btn__plus${isFormOpen ? ' is-open' : ''}`}>
              <PlusIcon size={15} />
            </span>
            {isFormOpen ? 'بستن فرم' : addLabel}
          </button>
        )}
      </div>
    </div>
  )
}

// ---------------------------------------------------------------------------
// تب اطلاعات پایه - یک کامپوننت برای هر چهار نوع، چون شکل داده‌شان یکی است.
// ---------------------------------------------------------------------------
function MasterDataTab({ kind }: { kind: MasterDataKind }) {
  const meta = MASTER_DATA_META[kind]

  const [items, setItems] = useState<MasterDataItem[] | null>(null)
  const [managements, setManagements] = useState<MasterDataItem[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)

  const [code, setCode] = useState('')
  const [title, setTitle] = useState('')
  const [isProjectRelated, setIsProjectRelated] = useState(false)
  const [managementId, setManagementId] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)
  // فرم افزودن دیگر همیشه باز نیست: کار غالبِ این صفحه مرور و ویرایش است، نه ثبت
  // مورد جدید. بسته بودنش ارتفاع صفحه را به جدول می‌دهد.
  const [isFormOpen, setFormOpen] = useState(false)

  const [editingKey, setEditingKey] = useState<string | null>(null)
  const [rowError, setRowError] = useState<string | null>(null)
  const [search, setSearch] = useState('')

  const load = useCallback(() => {
    setIsLoading(true)
    setLoadError(null)

    // فعالیت سازمانی برای انتخاب مدیریتِ والد به فهرست مدیریت‌ها هم نیاز دارد.
    const requests: Promise<unknown>[] = [
      adminApi.listMasterData(kind).then(setItems),
    ]
    if (meta.needsManagement) {
      requests.push(
        adminApi.listMasterData(MASTER_DATA_KINDS.OrganizationalManagement).then(setManagements),
      )
    }

    Promise.all(requests)
      .catch((err) =>
        setLoadError(err instanceof ApiError ? err.message : 'امکان بارگذاری اطلاعات پایه وجود ندارد.'),
      )
      .finally(() => setIsLoading(false))
  }, [kind, meta.needsManagement])

  useEffect(() => {
    load()
  }, [load])

  async function handleCreate(event: FormEvent) {
    event.preventDefault()
    if (isSubmitting) return

    if (code.trim().length !== meta.codeLength) {
      setFormError(`طول کد باید دقیقاً ${toPersianDigits(meta.codeLength)} کاراکتر باشد.`)
      return
    }
    if (!title.trim()) {
      setFormError('عنوان را وارد کنید.')
      return
    }
    if (meta.needsManagement && !managementId) {
      setFormError('مدیریت سازمانی را انتخاب کنید.')
      return
    }

    setIsSubmitting(true)
    setFormError(null)
    try {
      await adminApi.createMasterData({
        kind,
        code: code.trim(),
        title: title.trim(),
        // نوع‌هایی که پرچم پروژه‌محور ندارند همیشه false می‌فرستند؛ سرور این فیلد را
        // برای همه‌ی انواع انتظار دارد.
        isProjectRelated: meta.hasProjectFlag ? isProjectRelated : false,
        organizationalManagementId: meta.needsManagement ? managementId || null : null,
      })
      setCode('')
      setTitle('')
      setIsProjectRelated(false)
      setManagementId('')
      setFormOpen(false)
      load()
    } catch (err) {
      setFormError(err instanceof ApiError ? err.message : 'امکان ثبت این مورد وجود ندارد.')
    } finally {
      setIsSubmitting(false)
    }
  }

  async function handleRowSave(
    item: MasterDataItem,
    nextTitle: string,
    nextIsActive: boolean,
    nextIsProjectRelated: boolean,
  ) {
    setEditingKey(item.key)
    setRowError(null)
    try {
      await adminApi.updateMasterData({
        kind,
        key: item.key,
        title: nextTitle.trim(),
        isActive: nextIsActive,
        isProjectRelated: meta.hasProjectFlag ? nextIsProjectRelated : false,
      })
      load()
    } catch (err) {
      setRowError(err instanceof ApiError ? err.message : 'امکان ذخیره تغییرات وجود ندارد.')
    } finally {
      setEditingKey(null)
    }
  }

  // ردیف‌ها الفبایی مرتب می‌شوند. ستون «کد» سر جایش هست، فقط ترتیب با چیزی که کاربر
  // می‌خواند هم‌راستا شده.
  const sortedItems = useMemo(() => (items ? sortedByTitle(items) : null), [items])

  // جستجوی درون‌تبی روی کد و عنوان. فهرست فعالیت سازمانی ده‌ها ردیف دارد و بدون این،
  // پیدا کردن یک مورد یعنی اسکرول کردنِ کل جدول.
  const visibleItems = useMemo(() => {
    if (!sortedItems) return null
    const term = toLatinDigits(search).trim().toLowerCase()
    if (!term) return sortedItems
    return sortedItems.filter(
      (item) => item.code.toLowerCase().includes(term) || item.title.toLowerCase().includes(term),
    )
  }, [sortedItems, search])

  const managementTitleById = useMemo(() => {
    const map = new Map<string, string>()
    // قالب «عنوان (کد)» - همان چیزی که در پنل جستجو و فرم افزودن سند استفاده می‌شود،
    // تا یک داده‌ی واحد همه‌جای برنامه یک شکل دیده شود.
    managements.forEach((m) => map.set(m.key, `${m.title} (${toPersianDigits(m.code)})`))
    return map
  }, [managements])

  // تفکیکِ معنادار برای اطلاعات پایه: کل، فعال و - فقط اگر وجود داشت - غیرفعال.
  const masterDataStats = useMemo<PanelStat[]>(() => {
    const total = sortedItems?.length ?? 0
    const activeCount = sortedItems?.filter((item) => item.isActive).length ?? 0
    const inactiveCount = total - activeCount
    const stats: PanelStat[] = [
      { label: 'مورد', value: total },
      { label: 'فعال', value: activeCount, tone: 'success' },
    ]
    if (inactiveCount > 0) stats.push({ label: 'غیرفعال', value: inactiveCount, tone: 'muted' })
    return stats
  }, [sortedItems])

  return (
    <div className="card admin-panel">
      <PanelToolbar
        title={meta.label}
        stats={masterDataStats}
        search={search}
        onSearch={setSearch}
        searchPlaceholder="جستجو در کد یا عنوان..."
        addLabel={`افزودن ${meta.label}`}
        isFormOpen={isFormOpen}
        onToggleForm={() => setFormOpen((open) => !open)}
      />

      {/* فرم افزودن، کشویی زیر نوار ابزار. */}
      {isFormOpen && (
        <form className="create-panel" onSubmit={handleCreate}>
          <p className="create-panel__hint">
            کد پس از ثبت قابل تغییر نیست، چون داخل شماره‌ی مدارک صادرشده حک می‌شود. طول کد برای{' '}
            {meta.label} دقیقاً {toPersianDigits(meta.codeLength)} کاراکتر است.
          </p>

          <div className="form-grid">
            <div className="field">
              <label htmlFor="md-code">
                کد <span className="required-mark" aria-hidden="true">*</span>
              </label>
              <input
                id="md-code"
                className="text-input mono"
                value={code}
                // کد به سرور می‌رود، پس ارقام فارسی/عربیِ تایپ‌شده همین‌جا لاتین می‌شوند.
                onChange={(e) => setCode(toLatinDigits(e.target.value).toUpperCase())}
                maxLength={meta.codeLength}
                placeholder={'X'.repeat(meta.codeLength)}
                disabled={isSubmitting}
                autoComplete="off"
              />
            </div>

            <div className="field">
              <label htmlFor="md-title">
                عنوان <span className="required-mark" aria-hidden="true">*</span>
              </label>
              <input
                id="md-title"
                className="text-input"
                value={title}
                onChange={(e) => setTitle(e.target.value)}
                disabled={isSubmitting}
              />
            </div>

            {meta.needsManagement && (
              <div className="field">
                <label htmlFor="md-management">
                  مدیریت سازمانی <span className="required-mark" aria-hidden="true">*</span>
                </label>
                <select
                  id="md-management"
                  className="select-input"
                  value={managementId}
                  onChange={(e) => setManagementId(e.target.value)}
                  disabled={isSubmitting}
                >
                  <option value="">انتخاب کنید…</option>
                  {sortedByTitle(managements.filter((m) => m.isActive)).map((m) => (
                    <option key={m.key} value={m.key}>
                      {m.title} ({toPersianDigits(m.code)})
                    </option>
                  ))}
                </select>
              </div>
            )}

            {meta.hasProjectFlag && (
              <div className="field">
                <label className="check-row">
                  <input
                    type="checkbox"
                    checked={isProjectRelated}
                    onChange={(e) => setIsProjectRelated(e.target.checked)}
                    disabled={isSubmitting}
                  />
                  <span className="check-row__text">
                    <strong>پروژه‌محور</strong>
                    <span>بازنگری مدارک این پروژه به‌صورت A۰۱، A۰۲ شماره‌گذاری می‌شود.</span>
                  </span>
                </label>
              </div>
            )}
          </div>

          {formError && (
            <div className="form-error" role="alert">
              {formError}
            </div>
          )}

          <div className="create-panel__actions">
            <button type="button" className="btn btn-ghost btn-sm" onClick={() => setFormOpen(false)}>
              انصراف
            </button>
            <button type="submit" className="btn btn-primary btn-sm" disabled={isSubmitting}>
              {isSubmitting ? 'در حال ثبت…' : `افزودن ${meta.label}`}
            </button>
          </div>
        </form>
      )}

      {rowError && (
        <div className="form-error admin-panel__row-error" role="alert">
          {rowError}
        </div>
      )}

      {isLoading && <LoadingState title="در حال بارگذاری…" />}

      {!isLoading && loadError && (
        <ErrorState
          title={loadError}
          action={
            <button type="button" className="btn btn-secondary btn-sm" onClick={load}>
              تلاش مجدد
            </button>
          }
        />
      )}

      {!isLoading && !loadError && visibleItems && visibleItems.length === 0 && (
        <EmptyState
          title={search ? 'موردی با این عبارت پیدا نشد.' : `هنوز ${meta.label}ی ثبت نشده است.`}
          description={search ? 'عبارت دیگری را امتحان کنید.' : undefined}
        />
      )}

      {!isLoading && !loadError && visibleItems && visibleItems.length > 0 && (
        <div className="table-scroll">
          <table className="data-table">
            <thead>
              <tr>
                <th>کد</th>
                <th>عنوان</th>
                {meta.needsManagement && <th>مدیریت سازمانی</th>}
                {meta.hasProjectFlag && <th>پروژه‌محور</th>}
                <th>مدارک</th>
                <th>وضعیت</th>
                <th>عملیات</th>
              </tr>
            </thead>
            <tbody>
              {visibleItems.map((item) => (
                <MasterDataRow
                  key={item.key}
                  item={item}
                  meta={meta}
                  managementTitle={
                    item.organizationalManagementId
                      ? managementTitleById.get(item.organizationalManagementId) ?? '—'
                      : '—'
                  }
                  isBusy={editingKey === item.key}
                  onSave={handleRowSave}
                />
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

// یک ردیف قابل ویرایش. حالت ویرایش داخل خود ردیف نگه داشته می‌شود تا تغییر یک ردیف،
// بقیه‌ی جدول را دوباره رندر نکند.
function MasterDataRow({
  item,
  meta,
  managementTitle,
  isBusy,
  onSave,
}: {
  item: MasterDataItem
  meta: (typeof MASTER_DATA_META)[MasterDataKind]
  managementTitle: string
  isBusy: boolean
  onSave: (item: MasterDataItem, title: string, isActive: boolean, isProjectRelated: boolean) => void
}) {
  const [isEditing, setIsEditing] = useState(false)
  const [title, setTitle] = useState(item.title)
  const [isProjectRelated, setIsProjectRelated] = useState(item.isProjectRelated ?? false)

  // غیرفعال‌سازی ردیفی که مدرکی به آن وابسته است سمت سرور رد می‌شود. قبلاً دکمه‌اش
  // اصلاً رندر نمی‌شد و کاربر دلیلش را نمی‌فهمید؛ حالا غیرفعال ولی دیده می‌شود و
  // راهنمای شناورش علت را می‌گوید.
  const lockedByUsage = item.isActive && item.usageCount > 0

  return (
    <tr className={item.isActive ? undefined : 'is-inactive-row'}>
      <td className="mono">{toPersianDigits(item.code)}</td>
      <td>
        {isEditing ? (
          <input
            className="text-input"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            disabled={isBusy}
          />
        ) : (
          // عنوان، متنِ آزادی است که خودِ کاربر نوشته - دست‌نخورده نمایش داده می‌شود.
          // فارسی‌سازی ارقام فقط شامل کدها و اعداد رابط کاربری است.
          item.title
        )}
      </td>

      {meta.needsManagement && <td>{managementTitle}</td>}

      {meta.hasProjectFlag && (
        <td>
          {isEditing ? (
            <input
              type="checkbox"
              checked={isProjectRelated}
              onChange={(e) => setIsProjectRelated(e.target.checked)}
              disabled={isBusy}
            />
          ) : (
            <span className={`badge ${item.isProjectRelated ? 'badge-success' : 'badge-muted'}`}>
              {item.isProjectRelated ? 'بله' : 'خیر'}
            </span>
          )}
        </td>
      )}

      <td>{toPersianDigits(item.usageCount)}</td>

      <td>
        <span className={`badge ${item.isActive ? 'badge-success' : 'badge-danger'}`}>
          {item.isActive ? 'فعال' : 'غیرفعال'}
        </span>
      </td>

      {/* عملیات ردیف با آیکون‌های رنگی؛ نام هر عملیات روی راهنمای شناور و aria-label. */}
      <td className="actions-cell">
        {isEditing ? (
          <>
            <IconButton
              label="ذخیره"
              icon={<CheckIcon />}
              tone="success"
              pending={isBusy}
              onClick={() => onSave(item, title, item.isActive, isProjectRelated)}
            />
            <IconButton
              label="انصراف"
              icon={<CloseIcon />}
              tone="muted"
              disabled={isBusy}
              onClick={() => {
                setTitle(item.title)
                setIsProjectRelated(item.isProjectRelated ?? false)
                setIsEditing(false)
              }}
            />
          </>
        ) : (
          <>
            <IconButton label="ویرایش" icon={<EditIcon />} tone="edit" onClick={() => setIsEditing(true)} />
            {item.isActive ? (
              <IconButton
                label={
                  lockedByUsage
                    ? `غیرفعال کردن ممکن نیست: ${toPersianDigits(item.usageCount)} مدرک به این مورد وابسته است`
                    : 'غیرفعال کردن'
                }
                icon={<DeactivateIcon />}
                tone="danger"
                disabled={lockedByUsage}
                pending={isBusy}
                onClick={() => onSave(item, item.title, false, item.isProjectRelated ?? false)}
              />
            ) : (
              <IconButton
                label="فعال کردن"
                icon={<PowerIcon />}
                tone="success"
                pending={isBusy}
                onClick={() => onSave(item, item.title, true, item.isProjectRelated ?? false)}
              />
            )}
          </>
        )}
      </td>
    </tr>
  )
}

// ---------------------------------------------------------------------------
// تب آرشیو شماره‌های قدیمی - فقط خواندنی
// این جدول یک‌بار از اکسل ثبت مدارک شرکت وارد شده و هیچ‌وقت به‌روزرسانی یا حذف
// نمی‌شود؛ کارکردش این است که شمارنده‌ی سریالِ هر کد ۵حرفی از آخرین شماره‌ی همان کد
// در آرشیو ادامه پیدا کند. پس اینجا نه فرم افزودن هست، نه عملیات ردیفی.
// ---------------------------------------------------------------------------
const LEGACY_PAGE_SIZE = 20

function LegacyNumbersTab() {
  const [result, setResult] = useState<LegacyDocumentNumberPagedResult | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)

  // برخلاف تب‌های اطلاعات پایه، جستجو و صفحه‌بندی سمت سرور است: جدول ۷۶۸ ردیف دارد و
  // کشیدن کل آن به مرورگر بی‌مورد است.
  useEffect(() => {
    let cancelled = false
    setIsLoading(true)
    setLoadError(null)

    // تایمر کوتاه تا هر حرفِ تایپ‌شده یک درخواست جدا نسازد.
    const timer = window.setTimeout(() => {
      adminApi
        .listLegacyNumbers(toLatinDigits(search), page, LEGACY_PAGE_SIZE)
        .then((data) => {
          if (!cancelled) setResult(data)
        })
        .catch((err) => {
          if (!cancelled) {
            setLoadError(err instanceof ApiError ? err.message : 'امکان بارگذاری آرشیو وجود ندارد.')
          }
        })
        .finally(() => {
          if (!cancelled) setIsLoading(false)
        })
    }, 300)

    return () => {
      cancelled = true
      window.clearTimeout(timer)
    }
  }, [search, page])

  // با عوض شدن عبارت جستجو باید به صفحه‌ی اول برگشت، وگرنه ممکن است روی صفحه‌ای
  // بایستیم که در نتیجه‌ی جدید اصلاً وجود ندارد.
  function handleSearch(value: string) {
    setSearch(value)
    setPage(1)
  }

  const totalPages = Math.max(1, result?.totalPages ?? 1)

  return (
    <div className="card admin-panel">
      <PanelToolbar
        title="آرشیو شماره‌های قدیمی"
        stats={[
          { label: 'ردیف', value: result?.totalCount ?? 0 },
          { label: 'در این صفحه', value: result?.items.length ?? 0, tone: 'success' },
        ]}
        search={search}
        onSearch={handleSearch}
        searchPlaceholder="جستجو در شماره، کد، نام یا واحد..."
      />

      {/* توضیح ماهیت این تب - بدون آن، کاربر انتظار دارد بتواند ردیف اضافه یا ویرایش کند. */}
      <p className="panel-note">
        آرشیو شماره‌مدارک ستاد، واردشده از اکسل ثبت مدارک شرکت. این فهرست فقط مرجع تاریخی است و
        قابل ویرایش نیست؛ شماره‌گذاری اسناد جدید ستاد از آخرین سریالِ هر کد در همین آرشیو ادامه
        پیدا می‌کند.
      </p>

      {isLoading && <LoadingState title="در حال بارگذاری آرشیو…" />}

      {!isLoading && loadError && (
        <ErrorState
          title={loadError}
          action={
            <button type="button" className="btn btn-secondary btn-sm" onClick={() => setPage(page)}>
              تلاش مجدد
            </button>
          }
        />
      )}

      {!isLoading && !loadError && result && result.items.length === 0 && (
        <EmptyState
          title={search ? 'ردیفی با این عبارت پیدا نشد.' : 'آرشیو خالی است.'}
          description={search ? 'عبارت دیگری را امتحان کنید.' : undefined}
        />
      )}

      {!isLoading && !loadError && result && result.items.length > 0 && (
        <>
          <div className="table-scroll">
            <table className="data-table">
              <thead>
                <tr>
                  <th>شماره مدرک</th>
                  <th>نام مدرک</th>
                  <th>کد ۵حرفی</th>
                  <th>سریال</th>
                  <th>بازنگری</th>
                  <th>واحد سازمانی</th>
                  <th>تاریخ بازبینی قبلی</th>
                  <th>تاریخ بازبینی جاری</th>
                </tr>
              </thead>
              <tbody>
                {result.items.map((row) => (
                  <LegacyNumberRow key={row.key} row={row} />
                ))}
              </tbody>
            </table>
          </div>

          <div className="pagination">
            <span>{toPersianDigits(result.totalCount)} ردیف</span>
            <div className="pagination-controls">
              <button
                type="button"
                className="btn btn-secondary btn-sm"
                disabled={page <= 1}
                onClick={() => setPage((p) => p - 1)}
              >
                قبلی
              </button>
              <span>
                صفحه {toPersianDigits(page)} از {toPersianDigits(totalPages)}
              </span>
              <button
                type="button"
                className="btn btn-secondary btn-sm"
                disabled={page >= totalPages}
                onClick={() => setPage((p) => p + 1)}
              >
                بعدی
              </button>
            </div>
          </div>
        </>
      )}
    </div>
  )
}

// یک ردیف آرشیو. چهار ردیفِ استثنایی اکسل کد ۵حرفی ندارند (شماره‌شان با الگوی استاندارد
// نمی‌خواند) - آن‌ها با یک نشانِ خاکستری مشخص می‌شوند تا معلوم باشد نبودِ کد یک نقص
// نمایش نیست، بلکه واقعیتِ همان ردیف است.
function LegacyNumberRow({ row }: { row: LegacyDocumentNumber }) {
  return (
    <tr>
      <td className="mono">{toPersianDigits(row.rawNumber)}</td>
      <td>{row.name ?? '—'}</td>
      <td>
        {row.code5 ? (
          <span className="legacy-code">
            <span className="mono">{toPersianDigits(row.code5)}</span>
            {/* تجزیه‌ی کد به سه جزئش، همان‌طور که در شماره‌گذاری معنا دارد. */}
            <span className="legacy-code__parts">
              {row.managementCode}
              <span aria-hidden="true">+</span>
              {row.activityCode}
              <span aria-hidden="true">+</span>
              {row.documentTypeCode}
            </span>
          </span>
        ) : (
          <span className="badge badge-muted">بدون کد استاندارد</span>
        )}
      </td>
      <td className="mono">{row.serialNumber === null ? '—' : toPersianDigits(row.serialNumber)}</td>
      <td>
        {/* دو مفهوم جدا: پسوند بازنگریِ داخل خودِ شماره، و آخرین حرف بازنگری در اکسل. */}
        {row.currentVersion ? <span className="badge badge-muted">{row.currentVersion}</span> : '—'}
        {row.revisionSuffix && <span className="row-subtext">پسوند شماره: {row.revisionSuffix}</span>}
      </td>
      <td>{row.unitLabel ?? '—'}</td>
      {/* تاریخ‌ها متن خام شمسی‌اند و از تبدیل رد نمی‌شوند؛ فقط ارقامشان فارسی می‌شود. */}
      <td className="mono">{row.lastEditShamsiDate ? toPersianDigits(row.lastEditShamsiDate) : '—'}</td>
      <td className="mono">{row.currentEditShamsiDate ? toPersianDigits(row.currentEditShamsiDate) : '—'}</td>
    </tr>
  )
}

// ---------------------------------------------------------------------------
// تب کاربران - ارتقا/تنزل سطح دسترسی
// ---------------------------------------------------------------------------
function UsersTab({ isAdmin, currentPcode }: { isAdmin: boolean; currentPcode: string | null }) {
  const [users, setUsers] = useState<AppUser[] | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)

  const [pcode, setPcode] = useState('')
  const [role, setRole] = useState<AppRoleValue>(APP_ROLES.DocumentManager)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)
  const [pendingPcode, setPendingPcode] = useState<number | null>(null)
  const [isFormOpen, setFormOpen] = useState(false)
  const [search, setSearch] = useState('')

  const load = useCallback(() => {
    setIsLoading(true)
    setLoadError(null)
    adminApi
      .listUsers()
      .then(setUsers)
      .catch((err) =>
        setLoadError(err instanceof ApiError ? err.message : 'امکان بارگذاری فهرست کاربران وجود ندارد.'),
      )
      .finally(() => setIsLoading(false))
  }, [])

  useEffect(() => {
    load()
  }, [load])

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    if (isSubmitting) return

    const parsed = Number(pcode.trim())
    if (!Number.isInteger(parsed) || parsed <= 0) {
      setFormError('کد پرسنلی باید یک عدد معتبر باشد.')
      return
    }

    setIsSubmitting(true)
    setFormError(null)
    try {
      await adminApi.setUserRole(parsed, role)
      setPcode('')
      setFormOpen(false)
      load()
    } catch (err) {
      setFormError(err instanceof ApiError ? err.message : 'امکان ثبت دسترسی وجود ندارد.')
    } finally {
      setIsSubmitting(false)
    }
  }

  async function handleChangeRole(target: AppUser, nextRole: AppRoleValue) {
    setPendingPcode(target.pcode)
    setFormError(null)
    try {
      await adminApi.setUserRole(target.pcode, nextRole)
      load()
    } catch (err) {
      setFormError(err instanceof ApiError ? err.message : 'امکان تغییر نقش وجود ندارد.')
    } finally {
      setPendingPcode(null)
    }
  }

  async function handleRemove(target: AppUser) {
    const confirmed = window.confirm(
      `دسترسی کاربر ${toPersianDigits(target.pcode)} برداشته شود؟ او به «فقط مشاهده» برمی‌گردد.`,
    )
    if (!confirmed) return

    setPendingPcode(target.pcode)
    setFormError(null)
    try {
      await adminApi.removeUserRole(target.pcode)
      load()
    } catch (err) {
      setFormError(err instanceof ApiError ? err.message : 'امکان برداشتن دسترسی وجود ندارد.')
    } finally {
      setPendingPcode(null)
    }
  }

  // جستجو روی کد پرسنلی؛ ارقام فارسیِ تایپ‌شده هم پیدا می‌کنند چون عبارت لاتین می‌شود.
  const visibleUsers = useMemo(() => {
    if (!users) return null
    const term = toLatinDigits(search).trim()
    if (!term) return users
    return users.filter((u) => String(u.pcode).includes(term))
  }, [users, search])

  // کاربران «فعال/غیرفعال» ندارند؛ تفکیک معنادارشان نقش است.
  const userStats = useMemo<PanelStat[]>(() => {
    const total = users?.length ?? 0
    const adminCount = users?.filter((u) => u.role === APP_ROLES.Admin).length ?? 0
    return [
      { label: 'کاربر', value: total },
      { label: 'مدیر سیستم', value: adminCount, tone: 'success' },
      { label: 'مدیر اسناد', value: total - adminCount },
    ]
  }, [users])

  return (
    <div className="card admin-panel">
      <PanelToolbar
        title="کاربران و دسترسی"
        stats={userStats}
        search={search}
        onSearch={setSearch}
        searchPlaceholder="جستجو در کد پرسنلی..."
        addLabel="دادن دسترسی"
        isFormOpen={isFormOpen}
        onToggleForm={() => setFormOpen((open) => !open)}
      />

      {isFormOpen && (
        <form className="create-panel" onSubmit={handleSubmit}>
          <p className="create-panel__hint">
            کاربر با کد پرسنلی مشخص می‌شود. کاربری که اینجا ثبت نشده باشد، «فقط مشاهده» است.
            {!isAdmin && ' شما به‌عنوان مدیر اسناد فقط می‌توانید نقش «مدیر اسناد» بدهید.'}
          </p>

          <div className="form-grid">
            <div className="field">
              <label htmlFor="user-pcode">
                کد پرسنلی <span className="required-mark" aria-hidden="true">*</span>
              </label>
              <input
                id="user-pcode"
                className="text-input mono"
                value={pcode}
                // ارقام فارسی/عربی هم پذیرفته می‌شوند و بلافاصله لاتین می‌شوند، چون مقدار
                // نهایی با Number خوانده و به سرور فرستاده می‌شود.
                onChange={(e) => setPcode(toLatinDigits(e.target.value).replace(/\D/g, ''))}
                placeholder="مثلاً ۳۲۵۶"
                disabled={isSubmitting}
                autoComplete="off"
              />
            </div>

            <div className="field">
              <label htmlFor="user-role">سطح دسترسی</label>
              <select
                id="user-role"
                className="select-input"
                value={role}
                onChange={(e) => setRole(Number(e.target.value) as AppRoleValue)}
                disabled={isSubmitting}
              >
                <option value={APP_ROLES.DocumentManager}>مدیر اسناد</option>
                {/* فقط مدیر سیستم می‌تواند مدیر سیستم بسازد؛ سرور هم همین را اعمال می‌کند. */}
                {isAdmin && <option value={APP_ROLES.Admin}>مدیر سیستم</option>}
              </select>
            </div>
          </div>

          {formError && (
            <div className="form-error" role="alert">
              {formError}
            </div>
          )}

          <div className="create-panel__actions">
            <button type="button" className="btn btn-ghost btn-sm" onClick={() => setFormOpen(false)}>
              انصراف
            </button>
            <button type="submit" className="btn btn-primary btn-sm" disabled={isSubmitting}>
              {isSubmitting ? 'در حال ثبت…' : 'ثبت دسترسی'}
            </button>
          </div>
        </form>
      )}

      {/* خطای عملیاتِ ردیفی، وقتی فرم بسته است هم باید دیده شود. */}
      {formError && !isFormOpen && (
        <div className="form-error admin-panel__row-error" role="alert">
          {formError}
        </div>
      )}

      {isLoading && <LoadingState title="در حال بارگذاری کاربران…" />}

      {!isLoading && loadError && (
        <ErrorState
          title={loadError}
          action={
            <button type="button" className="btn btn-secondary btn-sm" onClick={load}>
              تلاش مجدد
            </button>
          }
        />
      )}

      {!isLoading && !loadError && visibleUsers && visibleUsers.length === 0 && (
        <EmptyState title={search ? 'کاربری با این کد پیدا نشد.' : 'هیچ کاربری دسترسی ویژه ندارد.'} />
      )}

      {!isLoading && !loadError && visibleUsers && visibleUsers.length > 0 && (
        <div className="table-scroll">
          <table className="data-table">
            <thead>
              <tr>
                <th>کد پرسنلی</th>
                <th>سطح دسترسی</th>
                <th>عملیات</th>
              </tr>
            </thead>
            <tbody>
              {visibleUsers.map((u) => {
                // نقش خودِ کاربر جاری قابل تغییر نیست (سرور هم ردش می‌کند)، و
                // دست‌زدن به یک «مدیر سیستم» فقط از عهده‌ی مدیر سیستم برمی‌آید.
                const isSelf = currentPcode !== null && Number(currentPcode) === u.pcode
                const locked = isSelf || (u.role === APP_ROLES.Admin && !isAdmin)
                const busy = pendingPcode === u.pcode

                return (
                  <tr key={u.key}>
                    <td className="mono">{toPersianDigits(u.pcode)}</td>
                    <td>
                      {/* نقش مدیر سیستم رنگ متمایز می‌گیرد؛ در فهرستی از نقش‌های خاکستری،
                          بالاترین سطح دسترسی باید فوراً دیده شود. */}
                      <span className={`badge ${u.role === APP_ROLES.Admin ? 'badge-warning' : 'badge-muted'}`}>
                        {appRoleLabel(u.role)}
                      </span>
                      {isSelf && <span className="row-subtext">شما</span>}
                    </td>
                    <td className="actions-cell">
                      {locked ? (
                        <span className="lock-note">
                          {isSelf ? 'نقش خودتان قابل تغییر نیست' : 'نیازمند دسترسی مدیر سیستم'}
                        </span>
                      ) : (
                        <>
                          {u.role === APP_ROLES.DocumentManager && isAdmin && (
                            <IconButton
                              label="ارتقا به مدیر سیستم"
                              icon={<ArrowUpIcon />}
                              tone="view"
                              pending={busy}
                              onClick={() => handleChangeRole(u, APP_ROLES.Admin)}
                            />
                          )}
                          {u.role === APP_ROLES.Admin && (
                            <IconButton
                              label="تنزل به مدیر اسناد"
                              icon={<ArrowDownIcon />}
                              tone="warning"
                              pending={busy}
                              onClick={() => handleChangeRole(u, APP_ROLES.DocumentManager)}
                            />
                          )}
                          <IconButton
                            label="برداشتن دسترسی"
                            icon={<UserMinusIcon />}
                            tone="danger"
                            pending={busy}
                            onClick={() => handleRemove(u)}
                          />
                        </>
                      )}
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
