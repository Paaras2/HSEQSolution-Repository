export type Role = 'ReadOnly' | 'DocumentManager' | 'Admin'

export type Capability = 'documents:view' | 'documents:manage' | 'admin:access'

const ROLE_CAPABILITIES: Record<Role, Capability[]> = {
  ReadOnly: ['documents:view'],
  DocumentManager: ['documents:view', 'documents:manage'],
  Admin: ['documents:view', 'documents:manage', 'admin:access'],
}

// The backend (HSEQ.Service.Services.Services.JwtService) currently issues
// only two distinct role claims: "Admin" for accounts present in the Admins
// table, and a non-admin fallback for everyone else - there is no backend
// concept of "Document Manager" yet. The three-tier model below is the
// frontend's authorization contract, matched 1:1 to what the API actually
// enforces today (Document Add-Update-Delete are [Authorize(Roles =
// "Admin")] only). When the backend adds a real "DocumentManager" claim,
// only the switch below needs a new case - no route or component changes.
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
