import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import NotificationBell from './NotificationBell.jsx'
import { AuthProvider } from '../../contexts/AuthContext.jsx'
import * as authApi from '../../api/auth.js'
import * as notificationsApi from '../../api/notifications.js'
import { setStoredAccessToken } from '../../lib/authStorage.js'

vi.mock('../../api/auth.js')
vi.mock('../../api/notifications.js')

const SAMPLE_USER = {
  employeeNumber: 802,
  name: 'Remy Requestor',
  email: 'remy@hmt.test',
  role: 'Engineer',
  rankLevel: 1,
  superiorEmployeeNumber: 801,
  isApprover: false,
}

function renderBell() {
  return render(
    <AuthProvider>
      <NotificationBell />
    </AuthProvider>,
  )
}

describe('NotificationBell', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    localStorage.clear()
    setStoredAccessToken('test-token')
    authApi.fetchCurrentUser.mockResolvedValue(SAMPLE_USER)
    notificationsApi.getUnreadCount.mockResolvedValue({ count: 0 })
    notificationsApi.getNotifications.mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 })
  })

  it('shows no badge when there are no unread notifications', async () => {
    renderBell()

    await waitFor(() => expect(notificationsApi.getUnreadCount).toHaveBeenCalled())
    expect(screen.queryByText('0')).not.toBeInTheDocument()
  })

  it('shows the unread badge count', async () => {
    notificationsApi.getUnreadCount.mockResolvedValue({ count: 4 })
    renderBell()

    await waitFor(() => expect(screen.getByText('4')).toBeInTheDocument())
  })

  it('caps the badge at 99+', async () => {
    notificationsApi.getUnreadCount.mockResolvedValue({ count: 150 })
    renderBell()

    await waitFor(() => expect(screen.getByText('99+')).toBeInTheDocument())
  })

  it('opens the dropdown and shows the empty state', async () => {
    const user = userEvent.setup()
    renderBell()
    await waitFor(() => expect(notificationsApi.getUnreadCount).toHaveBeenCalled())

    await user.click(screen.getByRole('button', { name: /notifications/i }))

    await waitFor(() => expect(screen.getByText("You're all caught up.")).toBeInTheDocument())
  })

  it('shows notifications in the dropdown, unread ones marked visually', async () => {
    notificationsApi.getUnreadCount.mockResolvedValue({ count: 1 })
    notificationsApi.getNotifications.mockResolvedValue({
      items: [
        {
          id: 1,
          eventType: 'RequestSubmitted',
          title: 'Request Submitted',
          message: 'Request #5 was submitted for approval.',
          isRead: false,
          createdAtUtc: new Date().toISOString(),
          requestId: 5,
        },
      ],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    })
    const user = userEvent.setup()
    renderBell()
    await waitFor(() => expect(screen.getByText('1')).toBeInTheDocument())

    await user.click(screen.getByRole('button', { name: /notifications/i }))

    await waitFor(() => expect(screen.getByText('Request Submitted')).toBeInTheDocument())
    expect(screen.getByText(/Request #5 was submitted for approval\./)).toBeInTheDocument()
  })

  it('shows an error state if the feed fails to load', async () => {
    notificationsApi.getNotifications.mockRejectedValue(new Error('boom'))
    const user = userEvent.setup()
    renderBell()
    await waitFor(() => expect(notificationsApi.getUnreadCount).toHaveBeenCalled())

    await user.click(screen.getByRole('button', { name: /notifications/i }))

    await waitFor(() => expect(screen.getByText('Could not load notifications.')).toBeInTheDocument())
  })

  it('clicking an unread notification marks it read and updates the badge', async () => {
    notificationsApi.getUnreadCount.mockResolvedValue({ count: 1 })
    notificationsApi.getNotifications.mockResolvedValue({
      items: [
        {
          id: 1,
          eventType: 'RequestSubmitted',
          title: 'Request Submitted',
          message: 'Request #5 was submitted for approval.',
          isRead: false,
          createdAtUtc: new Date().toISOString(),
          requestId: 5,
        },
      ],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    })
    notificationsApi.markNotificationRead.mockResolvedValue(undefined)
    const user = userEvent.setup()
    renderBell()
    await waitFor(() => expect(screen.getByText('1')).toBeInTheDocument())
    await user.click(screen.getByRole('button', { name: /notifications/i }))
    await waitFor(() => expect(screen.getByText('Request Submitted')).toBeInTheDocument())

    await user.click(screen.getByText('Request Submitted'))

    expect(notificationsApi.markNotificationRead).toHaveBeenCalledWith(1)
    await waitFor(() => expect(screen.queryByText('1')).not.toBeInTheDocument())
  })

  it('"Mark all read" clears the badge', async () => {
    notificationsApi.getUnreadCount.mockResolvedValue({ count: 2 })
    notificationsApi.markAllNotificationsRead.mockResolvedValue(undefined)
    const user = userEvent.setup()
    renderBell()
    await waitFor(() => expect(screen.getByText('2')).toBeInTheDocument())
    await user.click(screen.getByRole('button', { name: /notifications/i }))
    await waitFor(() => expect(screen.getByText('Mark all read')).toBeInTheDocument())

    await user.click(screen.getByText('Mark all read'))

    expect(notificationsApi.markAllNotificationsRead).toHaveBeenCalled()
    await waitFor(() => expect(screen.queryByText('2')).not.toBeInTheDocument())
  })
})
