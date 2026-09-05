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
 *
 * The response interceptor handles session expiry. Tokens last `Jwt:ExpiryHours` (default 8), so
 * a tab left open overnight wakes up with every call returning 401 — previously surfaced as a raw
 * "Request failed with status code 401" on each page with no route back to login (audit L3).
 * AuthContext registers a handler via `setUnauthorizedHandler`; this file never imports it, which
 * is what keeps the dependency one-way.
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

let onUnauthorized = null

/**
 * Registers the callback fired when the API rejects a call with 401. Called once by AuthContext;
 * pass `null` to unregister. Kept here rather than in AuthContext so `client` stays importable by
 * every api module without pulling React in.
 */
export function setUnauthorizedHandler(handler) {
  onUnauthorized = handler
}

client.interceptors.response.use(
  (response) => response,
  (error) => {
    // A 401 from the login call itself is "wrong password", not an expired session — leave it to
    // the Login page's own error handling, or it would clear a session the user never had.
    const isLoginCall = error.config?.url?.includes('/auth/login')

    if (error.response?.status === 401 && !isLoginCall) {
      clearStoredAccessToken()
      onUnauthorized?.()
    }

    return Promise.reject(error)
  },
)

export default client
