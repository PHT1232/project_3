import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext.jsx'
import { LoadingState } from '../components/ui/StateBlock.jsx'

/**
 * Gate for every application route. Rank floors are UX only; server-side policies enforce access.
 */
export default function ProtectedRoute({ minimumRankLevel = 1, requireManager = false }) {
  const { isAuthenticated, restoring, user } = useAuth()
  const location = useLocation()

  if (restoring) {
    return <LoadingState label="Restoring your session…" />
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location }} />
  }

  const requiredRankLevel = requireManager ? 2 : minimumRankLevel
  if ((user?.rankLevel ?? 0) < requiredRankLevel) {
    return <Navigate to="/" replace />
  }

  return <Outlet />
}
