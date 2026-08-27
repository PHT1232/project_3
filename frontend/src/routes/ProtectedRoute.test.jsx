import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import { describe, it, expect, vi } from 'vitest'
import ProtectedRoute from './ProtectedRoute.jsx'
import { AuthProvider } from '../contexts/AuthContext.jsx'
import * as authApi from '../api/auth.js'
import { ACCESS_TOKEN_STORAGE_KEY } from '../lib/authStorage.js'

vi.mock('../api/auth.js')

function renderWithRoute(initialEntries) {
  return render(
    <MemoryRouter initialEntries={initialEntries}>
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<div>Login page</div>} />
          <Route path="/" element={<div>Home page</div>} />
          <Route element={<ProtectedRoute />}>
            <Route path="/protected" element={<div>Protected content</div>} />
          </Route>
          <Route element={<ProtectedRoute requireManager />}>
            <Route path="/manager-only" element={<div>Manager content</div>} />
          </Route>
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  )
}

describe('ProtectedRoute', () => {
  beforeEach(() => {
    localStorage.clear()
    vi.clearAllMocks()
  })

  it('redirects an anonymous user to /login', async () => {
    renderWithRoute(['/protected'])

    await waitFor(() => expect(screen.getByText('Login page')).toBeInTheDocument())
  })

  it('renders protected content for an authenticated user', async () => {
    localStorage.setItem(ACCESS_TOKEN_STORAGE_KEY, 'token')
    authApi.fetchCurrentUser.mockResolvedValue({ employeeNumber: 101, name: 'Ada', rankLevel: 1 })

    renderWithRoute(['/protected'])

    await waitFor(() => expect(screen.getByText('Protected content')).toBeInTheDocument())
  })

  it('redirects a non-manager away from a requireManager route', async () => {
    localStorage.setItem(ACCESS_TOKEN_STORAGE_KEY, 'token')
    authApi.fetchCurrentUser.mockResolvedValue({ employeeNumber: 202, name: 'Eve', rankLevel: 1 })

    renderWithRoute(['/manager-only'])

    await waitFor(() => expect(screen.getByText('Home page')).toBeInTheDocument())
  })

  it('renders manager-only content for a Manager+ user', async () => {
    localStorage.setItem(ACCESS_TOKEN_STORAGE_KEY, 'token')
    authApi.fetchCurrentUser.mockResolvedValue({ employeeNumber: 101, name: 'Ada', rankLevel: 2 })

    renderWithRoute(['/manager-only'])

    await waitFor(() => expect(screen.getByText('Manager content')).toBeInTheDocument())
  })
})
