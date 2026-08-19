import { Navigate, Route, BrowserRouter, Routes } from 'react-router-dom'
import { AuthProvider } from './auth/AuthContext'
import { ProtectedRoute } from './auth/ProtectedRoute'
import { AppShell } from './components/AppShell'
import { LoginPage } from './pages/LoginPage'
import { DashboardPage } from './pages/DashboardPage'
import { DocumentsPage } from './pages/DocumentsPage'
import { DocumentCreatePage } from './pages/DocumentCreatePage'
import { AdvancedSearchPage } from './pages/AdvancedSearchPage'

// Route tree is intentionally flat now but grouped so an admin branch can be
// added later (nested under AppShell, gated by requireCapability set to
// "admin:access") without restructuring anything above it.
function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<LoginPage />} />

          <Route element={<ProtectedRoute />}>
            <Route element={<AppShell />}>
              <Route index element={<DashboardPage />} />
              <Route path="documents" element={<DocumentsPage />} />
              <Route path="documents/search" element={<AdvancedSearchPage />} />
              {/* Creating a document is its own destination, so it is linkable and
                  survives a refresh. Editing/revising stay as dialogs on the list. */}
              <Route path="documents/new" element={<DocumentCreatePage />} />
            </Route>
          </Route>

          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  )
}

export default App
