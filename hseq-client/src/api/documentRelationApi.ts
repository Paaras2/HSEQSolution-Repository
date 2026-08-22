import { apiGet, apiPostForm } from '../lib/httpClient'
import type { CreateDocumentRelationInput, DocumentRelation } from '../types/api'

// مدارک مرتبط. مسیرها زیر /Document هستند چون ارتباط همیشه در دامنه‌ی یک مدرک معنا
// پیدا می‌کند، اما جدا از /Document/revisions نگه داشته شده‌اند: آن یکی نسخه‌های
// مختلفِ همین مدرک را برمی‌گرداند، این یکی مدارک دیگر را.
export const documentRelationApi = {
  // ارتباط‌های هر دو سمت را می‌دهد؛ لازم نیست کلاینت بداند مدرک مبدأ بوده یا مقصد.
  async getForDocument(documentId: string): Promise<DocumentRelation[]> {
    return apiGet<DocumentRelation[]>('/Document/relations', { documentId })
  },

  async add(input: CreateDocumentRelationInput): Promise<void> {
    const body = new URLSearchParams()
    body.set('SourceDocumentId', input.sourceDocumentId)
    body.set('TargetDocumentId', input.targetDocumentId)
    body.set('RelationType', String(input.relationType))
    if (input.note) body.set('Note', input.note)
    await apiPostForm<void>('/Document/relations/add', body)
  },

  // relationId کلید ردیفِ ارتباط است (DocumentRelation.key)، نه کلید مدرک.
  async remove(relationId: string): Promise<void> {
    const body = new URLSearchParams()
    body.set('relationId', relationId)
    await apiPostForm<void>('/Document/relations/delete', body)
  },
}
