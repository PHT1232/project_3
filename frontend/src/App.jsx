import { Routes, Route, Navigate } from 'react-router-dom'

import AppLayout from './components/layout/AppLayout.jsx'
import ProtectedRoute from './routes/ProtectedRoute.jsx'

// Owned by this task
import CataloguePage from './pages/catalogue/CataloguePage.jsx'
import InventoryPage from './pages/inventory/InventoryPage.jsx'
import ItemManagement from './pages/manager/ItemManagement.jsx'
import SupplierManagement from './pages/manager/SupplierManagement.jsx'
import NotFound from './pages/NotFound.jsx'
import Login from './pages/Login.jsx'
import UserManagementPage from './pages/users/UserManagementPage.jsx'
import ReportsPage from './pages/reports/ReportsPage.jsx'
import ApprovalsPage from './pages/requests/ApprovalsPage.jsx'

import DashboardPage from './pages/dashboard/DashboardPage.jsx'
import NewRequestPage from './pages/requests/NewRequestPage.jsx'
import MyRequestsPage from './pages/requests/MyRequestsPage.jsx'
import Help from './pages/Help.jsx'
import SupportInboxPage from './pages/support/SupportInboxPage.jsx'

/**
 * Application routes.
 *
 * SHARED FILE — add your page's <Route> here; do not introduce a second router.
 *
 * `/login` renders outside AppLayout (no sidebar) and is the only public route.
 * Everything else is behind `ProtectedRoute`; the Manager+ group additionally requires
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
          <Route path="/" element={<DashboardPage />} />
          <Route path="/catalogue" element={<CataloguePage />} />
          <Route path="/new-request" element={<NewRequestPage />} />
          <Route path="/my-requests" element={<MyRequestsPage />} />
          <Route path="/approvals" element={<ApprovalsPage />} />

          {/* Any authenticated user — the report data is row-scoped to their role
              server-side (ReportQueries), so a requestor only ever sees their own spend. */}
          <Route path="/reports" element={<ReportsPage />} />

          {/* Manager+ only; server-side policy is the real control.
              Support Inbox sits here, not in the Business Manager group below, because
              SupportController's read endpoints are [Authorize(Policy = "RequireManager")] —
              a Manager may read and triage; only the Managing Director may resolve, which
              SupportInboxPage gates separately and the server enforces. */}
          <Route element={<ProtectedRoute requireManager />}>
            <Route path="/inventory" element={<InventoryPage />} />
            <Route path="/suppliers" element={<SupplierManagement />} />
            <Route path="/support-inbox" element={<SupportInboxPage />} />
          </Route>

          {/* Business Manager+ only; Engineer and Manager cannot manage items or users. */}
          <Route element={<ProtectedRoute minimumRankLevel={3} />}>
            <Route path="/catalogue/manage" element={<ItemManagement />} />
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
