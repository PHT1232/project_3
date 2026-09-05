import axios from 'axios'
import { clearStoredAccessToken, getStoredAccessToken } from '../lib/authStorage.js'

/**
 * Shared axios instance. SHARED FILE.
 *
 * Base path is `/api/v1` per Plan §4.1 (versioned from day one; camelCase JSON, kebab-case URLs).
 * In dev, Vite proxies `/api` to the ASP.NET host (see vite.config.js); in production the SPA is
 * served from the API's own `wwwroot`, so the path is same-origin either way.
 *
 * The request interceptor attaches the bearer token from `localStorage` (Plan §9.2). AuthContext
 * owns writing/clearing that token; this file only reads it.
 */
const client = axios.create({
  baseURL: '/api/v1',
  headers: { 'Content-Type': 'application/json' },
})

client.interceptors.request.use((config) => {
  const token = getStoredAccessToken()
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

/**
 * A 401 means the token is gone, expired (they last 8 hours) or belongs to a deactivated
 * account — the server already re-checks IsActive on every request. Whatever the cause the
 * session is unusable, so drop the stale token and send them to sign in rather than letting
 * every widget on the page render its own "something went wrong".
 *
 * A hard redirect, not a router navigate: this module has no access to the router, and a full
 * reload also clears any in-memory state built from the dead session.
 *
 * The login call itself is excluded — a wrong password there is a normal 401 the form must
 * show, not a reason to bounce the page.
 */
client.interceptors.response.use(
  (response) => response,
  (error) => {
    const status = error.response?.status
    const url = error.config?.url ?? ''
    const isLoginAttempt = url.includes('/auth/login')

    if (status === 401 && !isLoginAttempt && !window.location.pathname.startsWith('/login')) {
      clearStoredAccessToken()
      window.location.assign('/login?expired=1')
    }

    return Promise.reject(error)
  },
)

export default client
