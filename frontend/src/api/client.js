import axios from 'axios'

/**
 * Shared axios instance. SHARED FILE.
 *
 * Base path is `/api/v1` per Plan §4.1 (versioned from day one; camelCase JSON, kebab-case URLs).
 * In dev, Vite proxies `/api` to the ASP.NET host (see vite.config.js); in production the SPA is
 * served from the API's own `wwwroot`, so the path is same-origin either way.
 *
 * The JWT request interceptor is intentionally NOT added here — token storage and the auth
 * context are M1's work (Plan T1.8/§9.2, JWT in `localStorage` read by this interceptor).
 * M1 should attach it to this instance rather than creating a second client.
 */
const client = axios.create({
  baseURL: '/api/v1',
  headers: { 'Content-Type': 'application/json' },
})

export default client
