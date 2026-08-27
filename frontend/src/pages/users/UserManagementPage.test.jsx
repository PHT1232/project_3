import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import UserManagementPage from './UserManagementPage.jsx'
import * as usersApi from '../../api/users.js'

vi.mock('../../api/users.js')

const SAMPLE_USER = {
  employeeNumber: 101,
  name: 'Ada Manager',
  email: 'ada@hmt.test',
  role: 'Manager',
  rankLevel: 2,
  superiorEmployeeNumber: null,
  grade: null,
  location: null,
  isActive: true,
}

describe('UserManagementPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('shows a loading state while the request is pending', () => {
    usersApi.getUsers.mockReturnValue(new Promise(() => {}))

    render(<UserManagementPage />)

    expect(screen.getByText(/loading users/i)).toBeInTheDocument()
  })

  it('shows an error state when the request fails', async () => {
    usersApi.getUsers.mockRejectedValue(new Error('network down'))

    render(<UserManagementPage />)

    expect(await screen.findByText(/something went wrong/i)).toBeInTheDocument()
  })

  it('shows an empty state when there are no users', async () => {
    usersApi.getUsers.mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 })

    render(<UserManagementPage />)

    expect(await screen.findByText(/no users found/i)).toBeInTheDocument()
  })

  it('renders a populated table of users', async () => {
    usersApi.getUsers.mockResolvedValue({ items: [SAMPLE_USER], page: 1, pageSize: 20, totalCount: 1 })

    render(<UserManagementPage />)

    expect(await screen.findByText('Ada Manager')).toBeInTheDocument()
    expect(screen.getByText('ada@hmt.test')).toBeInTheDocument()
  })

  it('confirms and applies a status change', async () => {
    usersApi.getUsers.mockResolvedValue({ items: [SAMPLE_USER], page: 1, pageSize: 20, totalCount: 1 })
    usersApi.setUserStatus.mockResolvedValue({ ...SAMPLE_USER, isActive: false })

    render(<UserManagementPage />)
    await screen.findByText('Ada Manager')

    await userEvent.click(screen.getByRole('button', { name: /deactivate ada manager/i }))
    expect(await screen.findByRole('dialog', { name: /deactivate ada manager\?/i })).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: /^deactivate$/i }))

    await waitFor(() => expect(usersApi.setUserStatus).toHaveBeenCalledWith(101, false))
  })
})
