import { createContext, useCallback, useContext, useEffect, useState } from 'react'
import { fetchCurrentUser, login as loginRequest } from '../api/auth.js'
import { setUnauthorizedHandler } from '../api/client.js'
import {
  clearStoredAccessToken,
  getStoredAccessToken,
  setStoredAccessToken,
} from '../lib/authStorage.js'

const AuthContext = createContext(undefined)

/**
 * Session state for the whole app. Restores a stored token via `/auth/me` on startup;
 * an invalid/expired token clears the session rather than leaving stale state around.
 */
export function AuthProvider({ children }) {
  const [token, setToken] = useState(() => getStoredAccessToken())
  const [user, setUser] = useState(null)
  const [restoring, setRestoring] = useState(true)

  const clearSession = useCallback(() => {
    clearStoredAccessToken()
    setToken(null)
    setUser(null)
  }, [])

  useEffect(() => {
    if (!token) {
      setRestoring(false)
      return undefined
    }

    let cancelled = false

    fetchCurrentUser()
      .then((currentUser) => {
        if (!cancelled) setUser(currentUser)
      })
      .catch(() => {
        if (!cancelled) clearSession()
      })
      .finally(() => {
        if (!cancelled) setRestoring(false)
      })

    return () => {
      cancelled = true
    }
    // Restore-on-startup runs once against the token read from storage at mount; login()/
    // logout() set state directly and must not re-trigger this redundant /auth/me fetch.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  // Session expiry. The token lives 8 hours; without this, every call in a stale tab returned a
  // raw 401 and the user stayed on a broken page with no way back to login (audit L3).
  // clearSession() flips isAuthenticated to false, and ProtectedRoute redirects from there — so
  // the redirect stays in the router's hands rather than a hard window.location assignment.
  useEffect(() => {
    setUnauthorizedHandler(() => clearSession())
    return () => setUnauthorizedHandler(null)
  }, [clearSession])

  const login = useCallback(async (identifier, password) => {
    const response = await loginRequest(identifier, password)
    setStoredAccessToken(response.accessToken)
    setToken(response.accessToken)
    setUser(response.user)
    return response.user
  }, [])

  const logout = useCallback(() => {
    clearSession()
  }, [clearSession])

  const value = {
    user,
    isAuthenticated: Boolean(user),
    restoring,
    login,
    logout,
  }

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return context
}
