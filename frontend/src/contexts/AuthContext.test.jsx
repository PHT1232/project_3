import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { AuthProvider, useAuth } from './AuthContext.jsx'
import * as authApi from '../api/auth.js'
import { ACCESS_TOKEN_STORAGE_KEY } from '../lib/authStorage.js'

vi.mock('../api/auth.js')

function Probe() {
  const { user, isAuthenticated, restoring, login, logout } = useAuth()
  return (
    <div>
      <span data-testid="restoring">{String(restoring)}</span>
      <span data-testid="authenticated">{String(isAuthenticated)}</span>
      <span data-testid="user-name">{user?.name ?? ''}</span>
      <button
        onClick={() => {
          login(101, 'Password1!').catch(() => {})
        }}
      >
        login
      </button>
      <button onClick={logout}>logout</button>
    </div>
  )
}

describe('AuthContext', () => {
  beforeEach(() => {
    localStorage.clear()
    vi.clearAllMocks()
  })

  it('restores a session from a stored token via /auth/me', async () => {
    localStorage.setItem(ACCESS_TOKEN_STORAGE_KEY, 'stored-token')
    authApi.fetchCurrentUser.mockResolvedValue({ employeeNumber: 101, name: 'Ada', rankLevel: 2 })

    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    )

    await waitFor(() => expect(screen.getByTestId('restoring').textContent).toBe('false'))
    expect(screen.getByTestId('authenticated').textContent).toBe('true')
    expect(screen.getByTestId('user-name').textContent).toBe('Ada')
  })

  it('clears an expired/invalid stored token instead of leaving stale state', async () => {
    localStorage.setItem(ACCESS_TOKEN_STORAGE_KEY, 'stale-token')
    authApi.fetchCurrentUser.mockRejectedValue({ response: { status: 401 } })

    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    )

    await waitFor(() => expect(screen.getByTestId('restoring').textContent).toBe('false'))
    expect(screen.getByTestId('authenticated').textContent).toBe('false')
    expect(localStorage.getItem(ACCESS_TOKEN_STORAGE_KEY)).toBeNull()
  })

  it('login stores the token and user', async () => {
    authApi.login.mockResolvedValue({
      accessToken: 'new-token',
      user: { employeeNumber: 101, name: 'Ada', rankLevel: 2 },
    })

    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    )
    await waitFor(() => expect(screen.getByTestId('restoring').textContent).toBe('false'))

    await userEvent.click(screen.getByText('login'))

    await waitFor(() => expect(screen.getByTestId('authenticated').textContent).toBe('true'))
    expect(localStorage.getItem(ACCESS_TOKEN_STORAGE_KEY)).toBe('new-token')
  })

  it('logout removes the token and clears user state', async () => {
    localStorage.setItem(ACCESS_TOKEN_STORAGE_KEY, 'stored-token')
    authApi.fetchCurrentUser.mockResolvedValue({ employeeNumber: 101, name: 'Ada', rankLevel: 2 })

    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    )
    await waitFor(() => expect(screen.getByTestId('authenticated').textContent).toBe('true'))

    await userEvent.click(screen.getByText('logout'))

    expect(screen.getByTestId('authenticated').textContent).toBe('false')
    expect(localStorage.getItem(ACCESS_TOKEN_STORAGE_KEY)).toBeNull()
  })
})
