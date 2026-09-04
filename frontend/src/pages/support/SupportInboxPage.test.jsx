import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'

import SupportInboxPage from './SupportInboxPage.jsx'
import * as supportApi from '../../api/support.js'

vi.mock('../../api/support.js')
vi.mock('../../contexts/AuthContext.jsx', () => ({
  useAuth: () => ({
    user: { employeeNumber: 801, name: 'Meg Manager', role: 'Manager', rankLevel: 2 },
    token: 'mock-token',
    login: vi.fn(),
    logout: vi.fn(),
  }),
}))

const OPEN_MSG = {
  id: 1,
  senderEmployeeNumber: 802,
  senderName: 'Ed Engineer',
  area: 'Approvals',
  subject: 'Approve button does nothing',
  body: 'It just spins.',
  diagnostics: 'App version: abc123',
  status: 'New',
  createdAtUtc: '2026-09-03T10:00:00Z',
  resolvedAtUtc: null,
  resolvedByName: null,
}

describe('SupportInboxPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    supportApi.getSupportMessages.mockResolvedValue({
      items: [OPEN_MSG],
      page: 1,
      pageSize: 100,
      totalCount: 1,
    })
    supportApi.setSupportMessageResolved.mockResolvedValue({ ...OPEN_MSG, status: 'Resolved' })
  })

  it('shows a skeleton while the list loads', async () => {
    let resolve
    supportApi.getSupportMessages.mockReturnValue(new Promise((r) => { resolve = r }))
    render(<SupportInboxPage />)

    expect(screen.getByRole('status')).toHaveTextContent(/loading messages/i)

    resolve({ items: [], page: 1, pageSize: 100, totalCount: 0 })
    await waitFor(() => expect(screen.queryByRole('status')).not.toBeInTheDocument())
  })

  it('lists messages and defaults to the open filter', async () => {
    render(<SupportInboxPage />)

    expect(await screen.findByText('Approve button does nothing')).toBeInTheDocument()
    expect(supportApi.getSupportMessages).toHaveBeenCalledWith(
      expect.objectContaining({ status: 'New' }),
    )
  })

  it('resolves a message', async () => {
    const user = userEvent.setup()
    render(<SupportInboxPage />)

    await screen.findByText('Approve button does nothing')
    await user.click(screen.getByRole('button', { name: /mark resolved/i }))

    await waitFor(() =>
      expect(supportApi.setSupportMessageResolved).toHaveBeenCalledWith(1, true),
    )
  })

  it('switches the status filter', async () => {
    const user = userEvent.setup()
    render(<SupportInboxPage />)

    await screen.findByText('Approve button does nothing')
    await user.click(screen.getByRole('button', { name: /^resolved$/i }))

    await waitFor(() =>
      expect(supportApi.getSupportMessages).toHaveBeenLastCalledWith(
        expect.objectContaining({ status: 'Resolved' }),
      ),
    )
  })

  it('does not let the viewer resolve a message they sent', async () => {
    supportApi.getSupportMessages.mockResolvedValue({
      items: [{ ...OPEN_MSG, id: 2, senderEmployeeNumber: 801, senderName: 'Meg Manager' }],
      page: 1,
      pageSize: 100,
      totalCount: 1,
    })
    render(<SupportInboxPage />)

    await screen.findByText('Approve button does nothing')
    expect(screen.queryByRole('button', { name: /mark resolved/i })).not.toBeInTheDocument()
    expect(screen.getByText(/you sent this/i)).toBeInTheDocument()
  })

  it('reveals session diagnostics on demand', async () => {
    const user = userEvent.setup()
    render(<SupportInboxPage />)

    await screen.findByText('Approve button does nothing')
    expect(screen.queryByText('App version: abc123')).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /show session details/i }))
    expect(screen.getByText('App version: abc123')).toBeInTheDocument()
  })
})
