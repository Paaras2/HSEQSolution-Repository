import { useEffect, useState } from 'react'
import type { DragEvent, FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { documentApi } from '../api/documentApi'
import { documentRelationApi } from '../api/documentRelationApi'
import { masterDataApi } from '../api/masterDataApi'
import { ApiError } from '../lib/httpClient'
import { documentRelationTypeLabel } from '../types/api'
import type {
  DocumentCategory,
  DocumentTypeLookup,
  OrganizationalActivityLookup,
  OrganizationalManagementLookup,
  ProjectLookup,
} from '../types/api'
import { DocumentRelationPicker } from '../components/DocumentRelationPicker'
import { PersianDatePicker } from '../components/PersianDatePicker'
import type { PickedRelation } from '../components/DocumentRelationPicker'
import { LoadingState, ErrorState } from '../components/StateViews'
import { ChevronDownIcon, LinkIcon, UploadIcon } from '../components/Icons'
import { toPersianDigits } from '../lib/digits'
import { sortedByTitle } from '../lib/sorting'

export interface DocumentLookups {
  projects: ProjectLookup[]
  managements: OrganizationalManagementLookup[]
  activities: OrganizationalActivityLookup[]
  documentTypes: DocumentTypeLookup[]
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${toPersianDigits(bytes)} بایت`
  if (bytes < 1024 * 1024) return `${toPersianDigits((bytes / 1024).toFixed(1))} کیلوبایت`
  return `${toPersianDigits((bytes / (1024 * 1024)).toFixed(1))} مگابایت`
}

// Creating a document is a distinct destination rather than an overlay: it
// collects four master-data choices that permanently determine the Document
// Number, so it benefits from its own URL, working browser navigation, and room
// to show what that number will be built from. Editing and revising stay as
// dialogs - they are short, and they act on one specific row.
export function DocumentCreatePage() {
  const navigate = useNavigate()

  const [lookups, setLookups] = useState<DocumentLookups | null>(null)
  const [lookupError, setLookupError] = useState<string | null>(null)
  const [isLoadingLookups, setIsLoadingLookups] = useState(true)

  const [name, setName] = useState('')
  // پیش‌فرض «ستاد» طبق درخواست - کاربر فقط با انتخاب صریح «پروژه» فیلد پروژه را می‌بیند.
  const [category, setCategory] = useState<DocumentCategory>('Headquarters')
  // نسخه‌ی انگلیسی مدرک. بعضی مدارک هر دو نسخه را دارند و در قرارداد شرکت، نسخه‌ی
  // انگلیسی با پسوند « (EN)» آخر شماره مشخص می‌شود.
  const [isEnglishVersion, setEnglishVersion] = useState(false)
  const [projectId, setProjectId] = useState('')
  const [managementId, setManagementId] = useState('')
  const [activityId, setActivityId] = useState('')
  const [documentTypeId, setDocumentTypeId] = useState('')
  const [formerReviewDate, setFormerReviewDate] = useState('')
  const [currentReviewDate, setCurrentReviewDate] = useState('')
  const [file, setFile] = useState<File | null>(null)
  const [isDraggingFile, setIsDraggingFile] = useState(false)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // ارتباط‌ها اینجا فقط در صف می‌مانند و بعد از ذخیره ثبت می‌شوند: ثبت ارتباط به کلید هر
  // دو سر نیاز دارد و سند تا لحظه‌ی ذخیره کلیدی ندارد.
  const [pendingRelations, setPendingRelations] = useState<PickedRelation[]>([])
  // «مدارک مرتبط» اختیاری است و در بیشتر ثبت‌ها خالی می‌ماند، پس بسته باز می‌شود تا طول فرم
  // را بی‌دلیل زیاد نکند؛ فقط با درخواست صریح کاربر باز می‌شود.
  const [isRelationsOpen, setIsRelationsOpen] = useState(false)
  // وقتی سند ذخیره شد ولی بخشی از ارتباط‌ها ثبت نشدند. در این حالت ماندن روی فرم امن‌تر از
  // بازگشت خاموش به فهرست است، اما ارسال دوباره‌ی فرم هم نباید ممکن باشد - سند ساخته شده.
  const [savedWithRelationErrors, setSavedWithRelationErrors] = useState<string[] | null>(null)

  function loadLookups() {
    setIsLoadingLookups(true)
    setLookupError(null)
    Promise.all([
      masterDataApi.getProjects(),
      masterDataApi.getManagements(),
      masterDataApi.getActivities(),
      masterDataApi.getDocumentTypes(),
    ])
      .then(([projects, managements, activities, documentTypes]) =>
        setLookups({ projects, managements, activities, documentTypes }),
      )
      .catch((err) =>
        setLookupError(
          err instanceof ApiError ? err.message : 'امکان بارگذاری داده‌های پایه وجود ندارد.',
        ),
      )
      .finally(() => setIsLoadingLookups(false))
  }

  useEffect(loadLookups, [])

  const selectedProject = lookups?.projects.find((p) => p.key === projectId)
  const selectedManagement = lookups?.managements.find((m) => m.key === managementId)
  const selectedActivity = lookups?.activities.find((a) => a.key === activityId)
  const selectedType = lookups?.documentTypes.find((t) => t.key === documentTypeId)
  // همه‌ی فهرست‌های داده‌ی پایه الفبایی مرتب می‌شوند؛ ترتیبِ سرور بر اساس کد است و
  // چون برچسب با عنوان شروع می‌شود، آن ترتیب روی صفحه بی‌قاعده دیده می‌شد.
  const activitiesForManagement = sortedByTitle(
    lookups?.activities.filter((a) => a.organizationalManagementId === managementId) ?? [],
  )

  // موارد الزامیِ همین لحظه. «پروژه» فقط در دسته‌بندی پروژه الزامی است، پس اصلاً وارد
  // شمارش نمی‌شود مگر آن دسته‌بندی انتخاب شده باشد - وگرنه در حالت «ستاد» یک موردِ
  // همیشه‌تکمیل به شمارش اضافه می‌کرد و پیشرفت را از صفر شروع نمی‌کرد.
  const requiredChecks = [
    name.trim().length > 0,
    ...(category === 'Project' ? [Boolean(projectId)] : []),
    Boolean(documentTypeId),
    Boolean(managementId),
    Boolean(activityId),
    Boolean(file),
  ]
  const requiredCount = requiredChecks.length
  const completedCount = requiredChecks.filter(Boolean).length
  const isComplete = completedCount === requiredCount

  function handleFileDrop(event: DragEvent<HTMLLabelElement>) {
    event.preventDefault()
    setIsDraggingFile(false)
    if (isSubmitting) return
    const dropped = event.dataTransfer.files?.[0]
    if (dropped) setFile(dropped)
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    if (isSubmitting) return

    if (!name.trim()) {
      setError('نام الزامی است.')
      return
    }
    if (category === 'Project' && !projectId) {
      setError('برای دسته‌بندی «پروژه»، انتخاب پروژه الزامی است.')
      return
    }
    if (!managementId || !activityId || !documentTypeId) {
      setError('مدیریت، فعالیت و نوع سند همگی الزامی هستند.')
      return
    }
    if (!file) {
      setError('هنگام افزودن سند، فایل الزامی است.')
      return
    }

    setIsSubmitting(true)
    setError(null)

    let createdKey: string
    try {
      createdKey = await documentApi.add({
        name: name.trim(),
        category,
        isEnglishVersion,
        formerReviewDate: formerReviewDate || null,
        currentReviewDate: currentReviewDate || null,
        relatedDocumentId: null,
        file,
        projectId: category === 'Project' ? projectId : null,
        organizationalManagementId: managementId,
        organizationalActivityId: activityId,
        documentTypeId,
      })
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'امکان ذخیره سند وجود ندارد. لطفاً دوباره تلاش کنید.')
      setIsSubmitting(false)
      return
    }

    // از اینجا به بعد سند ساخته شده است. شکست یک ارتباط نباید به شکست کل عملیات ترجمه شود
    // (سند برمی‌نگردد) و نباید هم بی‌صدا رد شود - پس هر کدام جدا تلاش و خطایش جمع می‌شود.
    const failures: string[] = []
    for (const relation of pendingRelations) {
      try {
        await documentRelationApi.add({
          sourceDocumentId: createdKey,
          targetDocumentId: relation.target.key,
          relationType: relation.relationType,
          note: relation.note,
        })
      } catch (err) {
        const reason = err instanceof ApiError ? err.message : 'خطای نامشخص'
        failures.push(`${toPersianDigits(relation.target.number)}: ${reason}`)
      }
    }

    if (failures.length > 0) {
      setSavedWithRelationErrors(failures)
      return
    }

    navigate('/documents')
  }

  function handleQueueRelation(picked: PickedRelation): Promise<boolean> {
    setPendingRelations((current) => [...current, picked])
    return Promise.resolve(true)
  }

  function handleRemoveQueuedRelation(targetKey: string) {
    setPendingRelations((current) => current.filter((r) => r.target.key !== targetKey))
  }

  return (
    // کلاس ریشه فقط برای محدود کردن بازنویسی‌های ظاهریِ این صفحه است (مثل کادر فایل)،
    // تا کلاس‌های مشترک در داشبورد و کشوی ویرایش سند دست‌نخورده بمانند.
    <div className="create-page">
      <div className="page-header">
        <div>
          <Link to="/documents" className="page-back">
            <span aria-hidden="true">›</span> بازگشت به فهرست اسناد
          </Link>
          <h1>افزودن سند</h1>
          <p>پس از ذخیره، شماره سند تولید می‌شود و دیگر قابل تغییر نخواهد بود.</p>
        </div>
      </div>

      {isLoadingLookups && (
        <div className="card">
          <LoadingState title="در حال بارگذاری داده‌های پایه..." />
        </div>
      )}

      {!isLoadingLookups && lookupError && (
        <div className="card">
          <ErrorState
            title={lookupError}
            action={
              <button type="button" className="btn btn-secondary btn-sm" onClick={loadLookups}>
                تلاش مجدد
              </button>
            }
          />
        </div>
      )}

      {!isLoadingLookups && !lookupError && lookups && (
        <form onSubmit={handleSubmit}>
          <div className="create-layout">
            <div className="create-main">
              {error && (
                <div className="create-alert" role="alert">
                  {error}
                </div>
              )}

              {/* گام ۱ - نام و دسته‌بندی: دو چیزی که شکل کلی سند را تعیین می‌کنند، در یک کارت. */}
              <section className="card create-card">
                <div className="create-card__head">
                  <span className="create-card__step" aria-hidden="true">
                    {toPersianDigits(1)}
                  </span>
                  <h2 className="create-card__title">اطلاعات پایه</h2>
                </div>

                <div className="form-grid">
                  <div className="field">
                    <label htmlFor="doc-name">
                      نام سند <span className="required-mark" aria-hidden="true">*</span>
                    </label>
                    <input
                      id="doc-name"
                      className="text-input"
                      value={name}
                      onChange={(e) => setName(e.target.value)}
                      disabled={isSubmitting}
                      placeholder="مثلاً: دستورالعمل کنترل مدارک"
                    />
                  </div>

                  {/* دسته‌بندی کنارِ «نام سند» در همان ردیف می‌نشیند - دو تصمیمِ کوتاهِ اول فرم
                      با هم دیده می‌شوند به‌جای اینکه دو ردیفِ جدا ارتفاع بگیرند. دو گزینه هم
                      در یک کنترل قرصیِ تک‌خطی‌اند؛ ورودی رادیو سر جایش هست و فقط دیده نمی‌شود.
                      قالبِ کدگذاری از روی گزینه‌ها برداشته شد چون همان ساختار - زنده و
                      تفکیک‌شده - در «پیش‌نمایش شماره» کنار همین فرم دیده می‌شود. */}
                  <div className="field">
                    <label htmlFor="doc-category-hq">دسته‌بندی سند</label>
                    <div className="category-row">
                    <div className="segmented" role="group" aria-label="دسته‌بندی سند">
                      <label
                        className={`segmented__option ${category === 'Headquarters' ? 'is-active' : ''}`}
                      >
                        <input
                          id="doc-category-hq"
                          type="radio"
                          name="doc-category"
                          checked={category === 'Headquarters'}
                          onChange={() => setCategory('Headquarters')}
                          disabled={isSubmitting}
                        />
                        <span className="segmented__label">ستاد</span>
                      </label>

                      <label
                        className={`segmented__option ${category === 'Project' ? 'is-active' : ''}`}
                      >
                        <input
                          type="radio"
                          name="doc-category"
                          checked={category === 'Project'}
                          onChange={() => setCategory('Project')}
                          disabled={isSubmitting}
                        />
                        <span className="segmented__label">پروژه</span>
                      </label>
                    </div>

                      {/* نسخه‌ی انگلیسی، هم‌قد و هم‌خانواده‌ی کنترل دسته‌بندی و در همان ردیف:
                          هر دو با هم شکل شماره را می‌سازند، پس باید کنار هم دیده شوند نه
                          به‌صورت یک چک‌باکس جدا زیر آن. */}
                      <label
                        className={`en-chip${isEnglishVersion ? ' is-active' : ''}`}
                        title="نسخه‌ی انگلیسی مدرک - پسوند (EN) به انتهای شماره اضافه می‌شود"
                      >
                        <input
                          type="checkbox"
                          checked={isEnglishVersion}
                          onChange={(e) => setEnglishVersion(e.target.checked)}
                          disabled={isSubmitting}
                        />
                        <span className="en-chip__label">EN</span>
                      </label>
                    </div>

                    {/* یک خط راهنما به‌جای دو تا: همان مثال، با اثرِ زنده‌ی تیک EN رویش -
                        کاربر نتیجه را می‌بیند به‌جای اینکه توضیحش را بخواند. */}
                    <p className="field-hint">
                      {category === 'Project'
                        ? 'سند متعلق به یک پروژه - مثل P۰۰۸QHSBD۱۵۳A۰۳'
                        : 'سند بدون پروژه - مثل ASYFM-۰۰۸-A'}
                      {isEnglishVersion && <span className="field-hint__en"> (EN)</span>}.
                    </p>
                  </div>

                  {category === 'Project' && (
                    <div className="field span-2">
                      <label htmlFor="doc-project">
                        پروژه <span className="required-mark" aria-hidden="true">*</span>
                      </label>
                      <select
                        id="doc-project"
                        className="select-input"
                        value={projectId}
                        onChange={(e) => setProjectId(e.target.value)}
                        disabled={isSubmitting}
                      >
                        <option value="">یک پروژه انتخاب کنید…</option>
                        {sortedByTitle(lookups.projects).map((p) => (
                          <option key={p.key} value={p.key}>
                            {p.title} ({toPersianDigits(p.code)})
                          </option>
                        ))}
                      </select>
                    </div>
                  )}
                </div>
              </section>

              {/* گام ۲ - سه انتخابِ سازنده‌ی شماره در یک ردیف، و تاریخ‌های اختیاری زیر یک
                  جداکننده‌ی نازک در همان کارت (قبلاً دو کارت جدا بودند). */}
              <section className="card create-card">
                <div className="create-card__head">
                  <span className="create-card__step" aria-hidden="true">
                    {toPersianDigits(2)}
                  </span>
                  <h2 className="create-card__title">طبقه‌بندی سازمانی</h2>
                  <span className="create-card__badge">سازنده‌ی شماره سند</span>
                </div>

                <div className="form-grid form-grid--3">
                  <div className="field">
                    <label htmlFor="doc-type">
                      نوع سند <span className="required-mark" aria-hidden="true">*</span>
                    </label>
                    <select
                      id="doc-type"
                      className="select-input"
                      value={documentTypeId}
                      onChange={(e) => setDocumentTypeId(e.target.value)}
                      disabled={isSubmitting}
                    >
                      <option value="">یک نوع انتخاب کنید…</option>
                      {sortedByTitle(lookups.documentTypes).map((t) => (
                        <option key={t.key} value={t.key}>
                          {t.title} ({toPersianDigits(t.code)})
                        </option>
                      ))}
                    </select>
                  </div>

                  <div className="field">
                    <label htmlFor="doc-management">
                      مدیریت سازمانی <span className="required-mark" aria-hidden="true">*</span>
                    </label>
                    <select
                      id="doc-management"
                      className="select-input"
                      value={managementId}
                      onChange={(e) => {
                        setManagementId(e.target.value)
                        setActivityId('')
                      }}
                      disabled={isSubmitting}
                    >
                      <option value="">یک مدیریت انتخاب کنید…</option>
                      {sortedByTitle(lookups.managements).map((m) => (
                        <option key={m.key} value={m.key}>
                          {m.title} ({toPersianDigits(m.code)})
                        </option>
                      ))}
                    </select>
                  </div>

                  <div className="field">
                    <label htmlFor="doc-activity">
                      فعالیت سازمانی <span className="required-mark" aria-hidden="true">*</span>
                    </label>
                    <select
                      id="doc-activity"
                      className="select-input"
                      value={activityId}
                      onChange={(e) => setActivityId(e.target.value)}
                      disabled={isSubmitting || !managementId}
                    >
                      <option value="">
                        {managementId ? 'یک فعالیت انتخاب کنید…' : 'ابتدا مدیریت را انتخاب کنید'}
                      </option>
                      {activitiesForManagement.map((a) => (
                        <option key={a.key} value={a.key}>
                          {a.title} ({toPersianDigits(a.code)})
                        </option>
                      ))}
                    </select>
                  </div>
                </div>

                <p className="create-subtitle">
                  <span>تاریخ‌های بازبینی</span>
                  <span className="create-subtitle__tag">اختیاری</span>
                </p>

                <div className="form-grid form-grid--3">
                  <div className="field">
                    <label htmlFor="doc-former-date">تاریخ بازبینی قبلی</label>
                    {/* نمایش شمسی، مقدار میلادی - همان «yyyy-MM-dd» که به سرور می‌رود. */}
                    <PersianDatePicker
                      id="doc-former-date"
                      value={formerReviewDate}
                      onChange={setFormerReviewDate}
                      disabled={isSubmitting}
                    />
                  </div>

                  <div className="field">
                    <label htmlFor="doc-current-date">تاریخ بازبینی فعلی</label>
                    <PersianDatePicker
                      id="doc-current-date"
                      value={currentReviewDate}
                      onChange={setCurrentReviewDate}
                      disabled={isSubmitting}
                    />
                  </div>
                </div>
              </section>

              {/* گام ۳ - فایل: کادر رها کردن حالا یک نوار افقی کوتاه است، نه مربعِ بلندِ قبلی. */}
              <section className="card create-card">
                <div className="create-card__head">
                  <span className="create-card__step" aria-hidden="true">
                    {toPersianDigits(3)}
                  </span>
                  <h2 className="create-card__title">
                    فایل سند <span className="required-mark" aria-hidden="true">*</span>
                  </h2>
                </div>

                <label
                  htmlFor="doc-file"
                  className={`file-drop ${isDraggingFile ? 'is-dragging' : ''} ${file ? 'has-file' : ''}`}
                  onDragOver={(e) => {
                    e.preventDefault()
                    if (!isSubmitting) setIsDraggingFile(true)
                  }}
                  onDragLeave={() => setIsDraggingFile(false)}
                  onDrop={handleFileDrop}
                >
                  <input
                    id="doc-file"
                    type="file"
                    className="file-drop__input"
                    onChange={(e) => setFile(e.target.files?.[0] ?? null)}
                    disabled={isSubmitting}
                  />

                  <span className="file-drop__icon" aria-hidden="true">
                    <UploadIcon size={20} />
                  </span>

                  {file ? (
                    <div className="file-drop__selected">
                      <span className="file-drop__name mono">{file.name}</span>
                      <span className="file-drop__size">{formatFileSize(file.size)}</span>
                    </div>
                  ) : (
                    <div className="file-drop__prompt">
                      <strong>فایل را اینجا رها کنید</strong>
                      <span>یا برای انتخاب کلیک کنید</span>
                    </div>
                  )}

                  {/* داخل خودِ کادر می‌ماند تا نوار یک‌خطی بشکند؛ چون روی لایه‌ی نامرئیِ ورودی
                      فایل می‌نشیند، کلیکش باید صریحاً جلوی باز شدن پنجره‌ی انتخاب را بگیرد. */}
                  {file && (
                    <button
                      type="button"
                      className="btn btn-ghost btn-sm file-drop__clear"
                      onClick={(e) => {
                        e.preventDefault()
                        setFile(null)
                      }}
                      disabled={isSubmitting}
                    >
                      حذف فایل
                    </button>
                  )}
                </label>
              </section>

              {/* اختیاری، و برخلاف بقیه‌ی فرم بلافاصله ثبت نمی‌شود: ارتباط به کلید هر دو سر
                  نیاز دارد و سند تا لحظه‌ی «ذخیره سند» کلیدی ندارد. پس انتخاب‌ها اینجا صف
                  می‌شوند و درست بعد از ساخته‌شدن سند ثبت می‌گردند. */}
              <section className={`card create-card create-collapse ${isRelationsOpen ? 'is-open' : ''}`}>
                <h2 className="create-collapse__heading">
                  <button
                    type="button"
                    className="create-collapse__toggle"
                    onClick={() => setIsRelationsOpen((open) => !open)}
                    aria-expanded={isRelationsOpen}
                    aria-controls="doc-relations-body"
                  >
                    <span className="create-card__step create-card__step--icon" aria-hidden="true">
                      <LinkIcon size={14} />
                    </span>
                    <span className="create-collapse__title">مدارک مرتبط</span>
                    {pendingRelations.length > 0 ? (
                      <span className="badge badge-success">
                        {toPersianDigits(pendingRelations.length)} مورد
                      </span>
                    ) : (
                      <span className="create-card__badge">اختیاری</span>
                    )}
                    <ChevronDownIcon className="create-collapse__chevron" size={18} />
                  </button>
                </h2>

                {isRelationsOpen && (
                  <div className="create-collapse__body" id="doc-relations-body">
                    <p className="create-card__hint">
                      مدارکی که این سند به آن‌ها ارجاع دارد - مثلاً فرمی که با این دستورالعمل پر
                      می‌شود. پس از ذخیره‌ی سند ثبت می‌شوند و بعداً هم از فهرست اسناد قابل تغییرند.
                    </p>

                    {pendingRelations.length > 0 && (
                      <ul className="relation-list relation-list--compact">
                        {pendingRelations.map((relation) => (
                          <li key={relation.target.key} className="relation-item">
                            <div className="relation-item__head">
                              <span className="mono">{toPersianDigits(relation.target.number)}</span>
                              <span className="badge badge-muted">
                                {documentRelationTypeLabel(relation.relationType, true)}
                              </span>
                              <button
                                type="button"
                                className="btn btn-ghost btn-sm relation-item__remove"
                                onClick={() => handleRemoveQueuedRelation(relation.target.key)}
                                disabled={isSubmitting}
                              >
                                حذف
                              </button>
                            </div>

                            <div className="relation-item__title">{relation.target.name}</div>

                            {relation.note && <p className="relation-item__note">{relation.note}</p>}
                          </li>
                        ))}
                      </ul>
                    )}

                    <DocumentRelationPicker
                      excludedDocumentIds={pendingRelations.map((r) => r.target.key)}
                      disabled={isSubmitting}
                      // صف‌کردن هیچ درخواستی به سرور نمی‌زند، پس حالت «در حال ثبت» ندارد.
                      isSubmitting={false}
                      error={null}
                      submitLabel="افزودن به فهرست"
                      submittingLabel="افزودن به فهرست"
                      onPick={handleQueueRelation}
                    />
                  </div>
                )}
              </section>

              {savedWithRelationErrors && (
                <div className="card form-note" role="alert">
                  <strong>سند ذخیره شد</strong>، اما این ارتباط‌ها ثبت نشدند:
                  <ul>
                    {savedWithRelationErrors.map((message) => (
                      <li key={message}>{message}</li>
                    ))}
                  </ul>
                  می‌توانید آن‌ها را از فهرست اسناد، با دکمه‌ی «مدارک مرتبط» همین سند، دوباره ثبت کنید.
                </div>
              )}
            </div>

            <aside className="create-aside">
              <div className="card create-card create-summary">
                <div className="create-card__head">
                  <h2 className="create-card__title">شماره سند</h2>
                </div>

                {/* پیش‌نمایش زنده‌ی خودِ شماره: هر بخش با انتخاب‌شدن از جای‌خالیِ کم‌رنگ به مقدار
                    واقعی تبدیل می‌شود، پس کاربر شکل نهایی را پیش از ذخیره می‌بیند. */}
                <p className="code-strip mono" dir="ltr">
                  {category === 'Project' && (
                    <span className={`code-strip__part ${selectedProject ? 'is-set' : ''}`}>
                      {toPersianDigits(selectedProject?.code) || 'PPPP'}
                    </span>
                  )}
                  <span className={`code-strip__part ${selectedManagement ? 'is-set' : ''}`}>
                    {toPersianDigits(selectedManagement?.code) || 'O'}
                  </span>
                  <span className={`code-strip__part ${selectedActivity ? 'is-set' : ''}`}>
                    {toPersianDigits(selectedActivity?.code) || 'AA'}
                  </span>
                  <span className={`code-strip__part ${selectedType ? 'is-set' : ''}`}>
                    {toPersianDigits(selectedType?.code) || 'DD'}
                  </span>
                  {category === 'Headquarters' && <span className="code-strip__sep">-</span>}
                  <span className="code-strip__part is-auto">SSS</span>
                  {category === 'Headquarters' && <span className="code-strip__sep">-</span>}
                  <span className="code-strip__part is-auto">R</span>
                </p>

                <ol className="code-preview">
                  {category === 'Project' && (
                    <li className={selectedProject ? 'is-set' : ''}>
                      <span className="code-preview__value mono">{toPersianDigits(selectedProject?.code) || 'PPPP'}</span>
                      <span className="code-preview__label">پروژه</span>
                    </li>
                  )}
                  <li className={selectedManagement ? 'is-set' : ''}>
                    <span className="code-preview__value mono">{toPersianDigits(selectedManagement?.code) || 'O'}</span>
                    <span className="code-preview__label">مدیریت</span>
                  </li>
                  <li className={selectedActivity ? 'is-set' : ''}>
                    <span className="code-preview__value mono">{toPersianDigits(selectedActivity?.code) || 'AA'}</span>
                    <span className="code-preview__label">فعالیت</span>
                  </li>
                  <li className={selectedType ? 'is-set' : ''}>
                    <span className="code-preview__value mono">{toPersianDigits(selectedType?.code) || 'DD'}</span>
                    <span className="code-preview__label">نوع سند</span>
                  </li>
                  {/* سریال و بازنگری اینجا نمی‌آیند: سرور آن‌ها را هنگام ذخیره تولید می‌کند،
                      پس نمایش جای خالی‌شان در پیش‌نمایش چیزی به کاربر اضافه نمی‌کرد. */}
                  {isEnglishVersion && (
                    <li className="is-set">
                      <span className="code-preview__value mono">(EN)</span>
                      <span className="code-preview__label">نسخه</span>
                    </li>
                  )}
                </ol>

                {/* پیشرفتِ موارد الزامی - جوابِ همان سؤالی که کاربر وسط فرم می‌پرسد: «چقدر مانده؟» */}
                <div className="create-progress">
                  <div
                    className="create-progress__track"
                    role="progressbar"
                    aria-valuemin={0}
                    aria-valuemax={requiredCount}
                    aria-valuenow={completedCount}
                    aria-label="پیشرفت تکمیل موارد الزامی"
                  >
                    <span
                      className={`create-progress__fill ${isComplete ? 'is-complete' : ''}`}
                      style={{ inlineSize: `${(completedCount / requiredCount) * 100}%` }}
                    />
                  </div>
                  <p className="create-progress__text">
                    {isComplete
                      ? 'همه‌ی موارد الزامی تکمیل شد.'
                      : `${toPersianDigits(completedCount)} از ${toPersianDigits(requiredCount)} مورد الزامی تکمیل شده.`}
                  </p>
                </div>
              </div>
            </aside>
          </div>

          {/* نوار عملیات به پایین پنجره می‌چسبد تا «ذخیره سند» بدون اسکرول تا ته فرم در
              دسترس بماند - مهم‌ترین دکمه‌ی صفحه نباید ته صفحه پنهان شود. */}
          <div className="create-actions">
            {savedWithRelationErrors ? (
              // سند دیگر ساخته شده؛ ارسال دوباره‌ی فرم یک سند تکراری می‌سازد، پس تنها راه
              // پیشِ رو رفتن به فهرست است.
              <>
                <p className="create-actions__hint">سند ساخته شد.</p>
                <button type="button" className="btn btn-primary" onClick={() => navigate('/documents')}>
                  رفتن به فهرست اسناد
                </button>
              </>
            ) : (
              <>
                <p className="create-actions__hint">
                  {isComplete
                    ? 'آماده‌ی ذخیره است.'
                    : `${toPersianDigits(requiredCount - completedCount)} مورد الزامی باقی مانده.`}
                </p>
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={() => navigate('/documents')}
                  disabled={isSubmitting}
                >
                  انصراف
                </button>
                <button type="submit" className="btn btn-primary" disabled={isSubmitting}>
                  {isSubmitting ? 'در حال ذخیره…' : 'ذخیره سند'}
                </button>
              </>
            )}
          </div>
        </form>
      )}
    </div>
  )
}
