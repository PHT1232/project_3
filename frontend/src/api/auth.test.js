import { describe, it, expect, vi, beforeEach } from 'vitest'
import { login } from './auth.js'
import client from './client.js'

vi.mock('./client.js', () => ({ default: { post: vi.fn() } }))

describe('login()', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    client.post.mockResolvedValue({ data: { accessToken: 'token' } })
  })

  it('sends a digits-only identifier as a numeric employeeNumber', async () => {
    await login('101', 'Password1!')

    expect(client.post).toHaveBeenCalledWith('/auth/login', {
      employeeNumber: 101,
      password: 'Password1!',
    })
  })

  it('sends anything else as an email', async () => {
    await login('ada.manager@hmt.test', 'Password1!')

    expect(client.post).toHaveBeenCalledWith('/auth/login', {
      email: 'ada.manager@hmt.test',
      password: 'Password1!',
    })
  })

  it('trims surrounding whitespace before deciding', async () => {
    await login('  101  ', 'Password1!')

    expect(client.post).toHaveBeenCalledWith('/auth/login', {
      employeeNumber: 101,
      password: 'Password1!',
    })
  })

  it('still accepts a number, as older callers passed', async () => {
    await login(101, 'Password1!')

    expect(client.post).toHaveBeenCalledWith('/auth/login', {
      employeeNumber: 101,
      password: 'Password1!',
    })
  })
})
