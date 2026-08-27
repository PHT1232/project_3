/**
 * Single source of truth for the access-token storage key, shared by the axios
 * interceptor (client.js) and AuthContext so both agree on where the token lives.
 */
export const ACCESS_TOKEN_STORAGE_KEY = 'stationeryms.accessToken'

export function getStoredAccessToken() {
  return localStorage.getItem(ACCESS_TOKEN_STORAGE_KEY)
}

export function setStoredAccessToken(token) {
  localStorage.setItem(ACCESS_TOKEN_STORAGE_KEY, token)
}

export function clearStoredAccessToken() {
  localStorage.removeItem(ACCESS_TOKEN_STORAGE_KEY)
}
