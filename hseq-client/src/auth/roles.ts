export type Role = 'ReadOnly' | 'DocumentManager' | 'Admin'

export type Capability = 'documents:view' | 'documents:manage' | 'admin:access'

const ROLE_CAPABILITIES: Record<Role, Capability[]> = {
  ReadOnly: ['documents:view'],
  DocumentManager: ['documents:view', 'documents:manage'],
  Admin: ['documents:view', 'documents:manage', 'admin:access'],
}

// Mirrors what the API enforces: Document Add/Update/Revise/Delete accept
// "Admin,DocumentManager" (DocumentController.ManageDocumentRoles), and every
// read endpoint is open to any authenticated user. Keep the table above and that
// constant in step - when they disagree, the UI offers actions that fail with a
// 403 only after the user has filled in and submitted a form.
//
// Note that real logins cannot produce a "DocumentManager" claim yet:
// JwtService.GenerateJwtToken issues "Admin" for accounts in the Admins table
// and the placeholder "Addi" for everyone else, which falls through to ReadOnly
// below. For now only the dev-login shortcut mints DocumentManager tokens.
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
