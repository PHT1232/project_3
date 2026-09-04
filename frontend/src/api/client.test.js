import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'

import client from './client.js'
import { ACCESS_TOKEN_STORAGE_KEY } from '../lib/authStorage.js'

/**
 * The 401 interceptor. A token lasts 8 hours, so expiry mid-session is routine rather than
 * exceptional — without this every widget on the page rendered its own error and the user was
 * left guessing.
 */
describe('client 401 handling', () => {
  let assign

  beforeEach(() => {
    localStorage.setItem(ACCESS_TOKEN_STORAGE_KEY, 'stale-token')
    assign = vi.fn()
    // jsdom's location is not writable; replace it wholesale for the assertion.
    delete window.location
    window.location = { assign, pathname: '/my-requests', href: '' }
  })

  afterEach(() => {
    localStorage.clear()
    vi.restoreAllMocks()
  })

  /** Drives the rejection half of the response interceptor directly. */
  const reject = (error) => {
    const handler = client.interceptors.response.handlers.at(-1).rejected
    return handler(error).catch((e) => e)
  }

  it('clears the dead token and sends the user to sign in', async () => {
    await reject({ response: { status: 401 }, config: { url: '/requests' } })

    expect(localStorage.getItem(ACCESS_TOKEN_STORAGE_KEY)).toBeNull()
    expect(assign).toHaveBeenCalledWith('/login?expired=1')
  })

  it('leaves a failed login alone so the form can show the message', async () => {
    await reject({ response: { status: 401 }, config: { url: '/auth/login' } })

    expect(localStorage.getItem(ACCESS_TOKEN_STORAGE_KEY)).toBe('stale-token')
    expect(assign).not.toHaveBeenCalled()
  })

  it('ignores other failures', async () => {
    await reject({ response: { status: 422 }, config: { url: '/requests/1/submit' } })

    expect(localStorage.getItem(ACCESS_TOKEN_STORAGE_KEY)).toBe('stale-token')
    expect(assign).not.toHaveBeenCalled()
  })

  it('still rejects, so callers keep their own error handling', async () => {
    const original = { response: { status: 401 }, config: { url: '/requests' } }

    await expect(reject(original)).resolves.toBe(original)
  })
})
