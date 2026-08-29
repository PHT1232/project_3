import { Routes, Route, Navigate } from 'react-router-dom'

import AppLayout from './components/layout/AppLayout.jsx'
import ProtectedRoute from './routes/ProtectedRoute.jsx'

// Owned by this task
import CataloguePage from './pages/catalogue/CataloguePage.jsx'
import InventoryPage from './pages/inventory/InventoryPage.jsx'
import NotFound from './pages/NotFound.jsx'
import Login from './pages/Login.jsx'
import UserManagementPage from './pages/users/UserManagementPage.jsx'
import ReportsPage from './pages/reports/ReportsPage.jsx'

// Placeholders — owned by other developers (see Plan §6.1 and docs/development/page-map.md)
import Dashboard from './pages/Dashboard.jsx'
import NewRequest from './pages/NewRequest.jsx'
import MyRequests from './pages/MyRequests.jsx'
import Approvals from './pages/Approvals.jsx'
import Suppliers from './pages/Suppliers.jsx'
import Help from './pages/Help.jsx'

/**
 * Application routes.
 *
 * SHARED FILE — add your page's <Route> here; do not introduce a second router.
 *
 * `/login` renders outside AppLayout (no sidebar) and is the only public route.
 * Everything else is behind `ProtectedRoute`; `/user-management` additionally requires
 * `requireManager` (nav hiding is UX only — the server-side 403 is the real control).
 * Self-registration is not in the approved Plan, so `/signup` redirects to `/login`.
 */
export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route path="/signup" element={<Navigate to="/login" replace />} />

      <Route element={<ProtectedRoute />}>
        <Route element={<AppLayout />}>
          <Route path="/" element={<Dashboard />} />
          <Route path="/catalogue" element={<CataloguePage />} />
          <Route path="/new-request" element={<NewRequest />} />
          <Route path="/my-requests" element={<MyRequests />} />
          <Route path="/approvals" element={<Approvals />} />
          <Route path="/inventory" element={<InventoryPage />} />
          <Route path="/suppliers" element={<Suppliers />} />

          {/* Manager+ only (page-map §9 / §12); server-side 403 is the real control. */}
          <Route element={<ProtectedRoute requireManager />}>
            <Route path="/reports" element={<ReportsPage />} />
            <Route path="/user-management" element={<UserManagementPage />} />
          </Route>

          <Route path="/help" element={<Help />} />

          {/* System */}
          <Route path="*" element={<NotFound />} />
        </Route>
      </Route>
    </Routes>
  )
}
