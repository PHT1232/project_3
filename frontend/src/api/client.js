import axios from 'axios'
import { getStoredAccessToken } from '../lib/authStorage.js'

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

export default client
