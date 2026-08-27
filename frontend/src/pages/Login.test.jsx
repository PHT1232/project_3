import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import Login from './Login.jsx'
import { AuthProvider } from '../contexts/AuthContext.jsx'
import * as authApi from '../api/auth.js'

vi.mock('../api/auth.js')

function renderLogin() {
  return render(
    <MemoryRouter initialEntries={['/login']}>
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<Login />} />
          <Route path="/" element={<div>Home page</div>} />
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  )
}

describe('Login page', () => {
  beforeEach(() => {
    localStorage.clear()
    vi.clearAllMocks()
  })

  it('signs in successfully and navigates to the redirect target', async () => {
    authApi.login.mockResolvedValue({
      accessToken: 'token',
      user: { employeeNumber: 101, name: 'Ada', rankLevel: 2 },
    })

    renderLogin()

    await userEvent.type(screen.getByLabelText(/employee number/i), '101')
    await userEvent.type(screen.getByLabelText(/^password/i), 'Password1!')
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }))

    await waitFor(() => expect(screen.getByText('Home page')).toBeInTheDocument())
    expect(authApi.login).toHaveBeenCalledWith(101, 'Password1!')
  })

  it('shows a generic error message for invalid credentials', async () => {
    authApi.login.mockRejectedValue({ response: { status: 401 } })

    renderLogin()

    await userEvent.type(screen.getByLabelText(/employee number/i), '101')
    await userEvent.type(screen.getByLabelText(/^password/i), 'wrong-password')
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }))

    expect(await screen.findByRole('alert')).toHaveTextContent(/incorrect/i)
  })
})
