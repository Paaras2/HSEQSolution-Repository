import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from './AuthContext'
import type { Capability } from './roles'

interface ProtectedRouteProps {
  requireCapability?: Capability
}

// Unauthenticated users are sent to /login. Authenticated-but-unauthorized
// users (missing the required capability) are sent to the app root instead of
// seeing a page they cannot use - the root now redirects to the documents list.
export function ProtectedRoute({ requireCapability }: ProtectedRouteProps) {
  const { isAuthenticated, isInitializing, hasCapability } = useAuth()
  const location = useLocation()

  if (isInitializing) {
    return <div className="page-loading">در حال بارگذاری…</div>
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location }} />
  }

  if (requireCapability && !hasCapability(requireCapability)) {
    return <Navigate to="/" replace />
  }

  return <Outlet />
}