export type Role = 'ReadOnly' | 'DocumentManager' | 'Admin'

export type Capability = 'documents:view' | 'documents:manage' | 'admin:access'

const ROLE_CAPABILITIES: Record<Role, Capability[]> = {
  ReadOnly: ['documents:view'],
  // «مدیر اسناد» هم به پنل ادمین دسترسی دارد: اطلاعات پایه را تعریف می‌کند و
  // می‌تواند مدیر اسناد دیگری بسازد. اما نقش «مدیر سیستم» را نمی‌تواند بدهد یا
  // بگیرد - آن قاعده سمت سرور در AdminService اعمال می‌شود.
  DocumentManager: ['documents:view', 'documents:manage', 'admin:access'],
  Admin: ['documents:view', 'documents:manage', 'admin:access'],
}

// Mirrors what the API enforces: Document Add/Update/Revise/Delete accept
// "Admin,DocumentManager" (DocumentController.ManageDocumentRoles), and every
// read endpoint is open to any authenticated user. Keep the table above and that
// constant in step - when they disagree, the UI offers actions that fail with a
// 403 only after the user has filled in and submitted a form.
//
// نقش‌ها حالا در دیتابیس ماندگارند: جدول Admins یک ستون Role دارد و
// JwtService.GenerateJwtToken همان را در توکن می‌گذارد ("Admin" یا
// "DocumentManager"). کاربر بدون ردیف، مقدار "Addi" می‌گیرد که پایین به
// ReadOnly تبدیل می‌شود. dev-login هم مثل قبل هر سه نقش را مستقیم می‌سازد.
export function resolveRole(backendRoleClaim: string | null): Role {
  switch (backendRoleClaim) {
    case 'Admin':
      return 'Admin'
    case 'DocumentManager':
      return 'DocumentManager'
    default:
      return 'ReadOnly'
  }
}

export function capabilitiesFor(role: Role): Set<Capability> {
  return new Set(ROLE_CAPABILITIES[role])
}
