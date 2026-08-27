import { createContext, useCallback, useContext, useEffect, useState } from 'react'
import { fetchCurrentUser, login as loginRequest } from '../api/auth.js'
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
      return
    }

    let cancelled = false
    setRestoring(true)

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
  }, [token, clearSession])

  const login = useCallback(async (employeeNumber, password) => {
    const response = await loginRequest(employeeNumber, password)
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
