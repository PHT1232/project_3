import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext.jsx'
import { LoadingState } from '../components/ui/StateBlock.jsx'

/**
 * Gate for every application route. `requireManager` additionally hides routes from
 * non-Manager+ users — this is UX only, never the real control (server-side 403 is).
 */
export default function ProtectedRoute({ requireManager = false }) {
  const { isAuthenticated, restoring, user } = useAuth()
  const location = useLocation()

  if (restoring) {
    return <LoadingState label="Restoring your session…" />
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location }} />
  }

  if (requireManager && (user?.rankLevel ?? 0) < 2) {
    return <Navigate to="/" replace />
  }

  return <Outlet />
}
