import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { MemoryRouter } from 'react-router-dom'
import Header from './Header.jsx'
import * as authContext from '../../contexts/AuthContext.jsx'
import * as notifApi from '../../api/notifications.js'

vi.mock('../../api/notifications.js', () => ({
  fetchUnreadCount: vi.fn(),
  fetchNotifications: vi.fn(),
  markAsRead: vi.fn(),
  markAllAsRead: vi.fn(),
}))

describe('Header & Notification Integration', () => {
  const mockUser = { id: 1, name: 'John Doe', role: 'Staff' }

  beforeEach(() => {
    vi.clearAllMocks()
    vi.spyOn(authContext, 'useAuth').mockReturnValue({
      user: mockUser,
      isAuthenticated: true,
      logout: vi.fn(),
    })
  })

  it('renders header with notification bell button and polls unread count', async () => {
    notifApi.fetchUnreadCount.mockResolvedValue(3)

    render(
      <MemoryRouter>
        <Header onMenuClick={vi.fn()} />
      </MemoryRouter>
    )

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /Notifications, 3 unread/i })).toBeInTheDocument()
      expect(screen.getByText('3')).toBeInTheDocument()
    })
  })

  it('opens notification dropdown on bell click and loads notifications list', async () => {
    notifApi.fetchUnreadCount.mockResolvedValue(1)
    notifApi.fetchNotifications.mockResolvedValue({
      items: [
        {
          notificationId: 101,
          title: 'Request Approved',
          message: 'Your request #42 has been approved.',
          isRead: false,
          requestId: 42,
          createdAtUtc: new Date().toISOString(),
        },
      ],
      totalCount: 1,
    })

    const user = userEvent.setup()
    render(
      <MemoryRouter>
        <Header onMenuClick={vi.fn()} />
      </MemoryRouter>
    )

    const bellBtn = await screen.findByRole('button', { name: /Notifications/i })
    await user.click(bellBtn)

    expect(screen.getByRole('dialog', { name: /Notifications/i })).toBeInTheDocument()
    expect(await screen.findByText('Request Approved')).toBeInTheDocument()
    expect(screen.getByText('Your request #42 has been approved.')).toBeInTheDocument()
    expect(screen.getByText(/View request #42/i)).toBeInTheDocument()
  })

  it('allows marking all notifications as read', async () => {
    notifApi.fetchUnreadCount.mockResolvedValue(2)
    notifApi.fetchNotifications.mockResolvedValue({
      items: [
        {
          notificationId: 101,
          title: 'Request Approved',
          message: 'Your request #42 has been approved.',
          isRead: false,
          createdAtUtc: new Date().toISOString(),
        },
      ],
      totalCount: 1,
    })
    notifApi.markAllAsRead.mockResolvedValue()

    const user = userEvent.setup()
    render(
      <MemoryRouter>
        <Header onMenuClick={vi.fn()} />
      </MemoryRouter>
    )

    const bellBtn = await screen.findByRole('button', { name: /Notifications/i })
    await user.click(bellBtn)

    const markAllBtn = await screen.findByRole('button', { name: /Mark all read/i })
    await user.click(markAllBtn)

    expect(notifApi.markAllAsRead).toHaveBeenCalledTimes(1)
  })

  it('displays empty state when user has no notifications', async () => {
    notifApi.fetchUnreadCount.mockResolvedValue(0)
    notifApi.fetchNotifications.mockResolvedValue({ items: [], totalCount: 0 })

    const user = userEvent.setup()
    render(
      <MemoryRouter>
        <Header onMenuClick={vi.fn()} />
      </MemoryRouter>
    )

    const bellBtn = screen.getByRole('button', { name: 'Notifications' })
    await user.click(bellBtn)

    expect(await screen.findByText('No notifications yet')).toBeInTheDocument()
  })
})
