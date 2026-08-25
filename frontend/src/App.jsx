import { Routes, Route } from 'react-router-dom'

import AppLayout from './components/layout/AppLayout.jsx'

// Owned by this task
import CataloguePage from './pages/catalogue/CataloguePage.jsx'
import InventoryPage from './pages/inventory/InventoryPage.jsx'
import NotFound from './pages/NotFound.jsx'

// Placeholders — owned by other developers (see Plan §6.1 and docs/development/page-map.md)
import Login from './pages/Login.jsx'
import SignUp from './pages/SignUp.jsx'
import Dashboard from './pages/Dashboard.jsx'
import NewRequest from './pages/NewRequest.jsx'
import MyRequests from './pages/MyRequests.jsx'
import Approvals from './pages/Approvals.jsx'
import Reports from './pages/Reports.jsx'
import Suppliers from './pages/Suppliers.jsx'
import UserManagement from './pages/UserManagement.jsx'
import Help from './pages/Help.jsx'

/**
 * Application routes.
 *
 * SHARED FILE — add your page's <Route> here; do not introduce a second router.
 *
 * Auth routes render outside AppLayout (no sidebar). Everything else renders inside it.
 * Route protection is not wired: `ProtectedRoute` / `AuthContext` are M1's work (Plan T1.8).
 */
export default function App() {
  return (
    <Routes>
      {/* Public / authentication */}
      <Route path="/login" element={<Login />} />
      <Route path="/signup" element={<SignUp />} />

      {/* Main application */}
      <Route element={<AppLayout />}>
        <Route path="/" element={<Dashboard />} />
        <Route path="/catalogue" element={<CataloguePage />} />
        <Route path="/new-request" element={<NewRequest />} />
        <Route path="/my-requests" element={<MyRequests />} />
        <Route path="/approvals" element={<Approvals />} />
        <Route path="/reports" element={<Reports />} />
        <Route path="/inventory" element={<InventoryPage />} />
        <Route path="/suppliers" element={<Suppliers />} />
        <Route path="/user-management" element={<UserManagement />} />
        <Route path="/help" element={<Help />} />

        {/* System */}
        <Route path="*" element={<NotFound />} />
      </Route>
    </Routes>
  )
}
