import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import client, { setUnauthorizedHandler } from './client.js'
import {
  ACCESS_TOKEN_STORAGE_KEY,
  getStoredAccessToken,
  setStoredAccessToken,
} from '../lib/authStorage.js'

/**
 * The 401 response interceptor (audit L3). Tokens last 8 hours, so a tab left open overnight used
 * to hit a raw "Request failed with status code 401" on every call, with the stale token still in
 * localStorage and no route back to login.
 *
 * The transport is stubbed by swapping axios's adapter rather than adding a mocking library to a
 * shared package.json. `validateStatus` is applied by axios *inside* each built-in adapter, not
 * by the core, so this stub rejects non-2xx itself — producing the same AxiosError (`.response`
 * populated) the interceptor sees in production. The interceptor chain under test is the real one.
 */
const realAdapter = client.defaults.adapter

function respondWith(status) {
  client.defaults.adapter = (config) => {
    const response = {
      data: status === 200 ? [{ categoryId: 1 }] : { detail: 'stubbed' },
      status,
      statusText: String(status),
      headers: {},
      config,
      request: {},
    }

    if (status >= 200 && status < 300) return Promise.resolve(response)

    const error = new Error(`Request failed with status code ${status}`)
    error.config = config
    error.response = response
    error.isAxiosError = true
    return Promise.reject(error)
  }
}

describe('client 401 handling', () => {
  beforeEach(() => {
    localStorage.clear()
    setUnauthorizedHandler(null)
  })

  afterEach(() => {
    client.defaults.adapter = realAdapter
    setUnauthorizedHandler(null)
  })

  it('clears the stored token and notifies the handler on a 401', async () => {
    setStoredAccessToken('expired-token')
    const onUnauthorized = vi.fn()
    setUnauthorizedHandler(onUnauthorized)
    respondWith(401)

    await expect(client.get('/requests')).rejects.toMatchObject({
      response: { status: 401 },
    })

    expect(getStoredAccessToken()).toBeNull()
    expect(onUnauthorized).toHaveBeenCalledTimes(1)
  })

  it('leaves a failed login alone — that is a wrong password, not an expired session', async () => {
    // Guards against signing a user out of a session they never had, and against swallowing the
    // Login page's own "invalid credentials" message.
    setStoredAccessToken('someone-elses-token')
    const onUnauthorized = vi.fn()
    setUnauthorizedHandler(onUnauthorized)
    respondWith(401)

    await expect(client.post('/auth/login', {})).rejects.toMatchObject({
      response: { status: 401 },
    })

    expect(localStorage.getItem(ACCESS_TOKEN_STORAGE_KEY)).toBe('someone-elses-token')
    expect(onUnauthorized).not.toHaveBeenCalled()
  })

  it('does not fire on other error statuses', async () => {
    setStoredAccessToken('good-token')
    const onUnauthorized = vi.fn()
    setUnauthorizedHandler(onUnauthorized)
    respondWith(403)

    await expect(client.get('/reports/cost-by-item')).rejects.toMatchObject({
      response: { status: 403 },
    })

    expect(getStoredAccessToken()).toBe('good-token')
    expect(onUnauthorized).not.toHaveBeenCalled()
  })

  it('passes successful responses through untouched', async () => {
    respondWith(200)

    const response = await client.get('/categories')

    expect(response.data).toEqual([{ categoryId: 1 }])
  })
})
