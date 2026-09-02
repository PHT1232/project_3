import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { useNotifications } from './useNotifications.js'
import * as notificationsApi from '../api/notifications.js'

vi.mock('../api/notifications.js')

function Harness({ enabled = true }) {
  const { unreadCount, items, feedLoading, feedError, loadFeed, markRead, markAllRead } =
    useNotifications(enabled)
  return (
    <div>
      <p data-testid="unread-count">{unreadCount}</p>
      <button type="button" onClick={loadFeed}>
        Load feed
      </button>
      <button type="button" onClick={markAllRead}>
        Mark all read
      </button>
      {feedLoading && <p>Loading…</p>}
      {feedError && <p>{feedError}</p>}
      <ul>
        {items.map((n) => (
          <li key={n.id}>
            <button type="button" onClick={() => markRead(n.id)}>
              {n.title} {n.isRead ? '(read)' : '(unread)'}
            </button>
          </li>
        ))}
      </ul>
    </div>
  )
}

const SAMPLE_NOTIFICATION = {
  id: 1,
  eventType: 'RequestSubmitted',
  title: 'Request Submitted',
  message: 'Request #5 was submitted for approval.',
  isRead: false,
  createdAtUtc: '2026-09-02T00:00:00Z',
  requestId: 5,
}

describe('useNotifications', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    notificationsApi.getUnreadCount.mockResolvedValue({ count: 3 })
    notificationsApi.getNotifications.mockResolvedValue({
      items: [SAMPLE_NOTIFICATION],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    })
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('fetches the unread count on mount when enabled', async () => {
    render(<Harness />)

    await waitFor(() => expect(screen.getByTestId('unread-count')).toHaveTextContent('3'))
  })

  it('does not fetch anything when disabled (logged out)', async () => {
    render(<Harness enabled={false} />)

    await new Promise((resolve) => setTimeout(resolve, 0))
    expect(notificationsApi.getUnreadCount).not.toHaveBeenCalled()
    expect(screen.getByTestId('unread-count')).toHaveTextContent('0')
  })

  it('loads the feed on demand, showing loading then items', async () => {
    const user = userEvent.setup()
    render(<Harness />)
    await waitFor(() => expect(screen.getByTestId('unread-count')).toHaveTextContent('3'))

    await user.click(screen.getByText('Load feed'))

    await waitFor(() => expect(screen.getByText(/Request Submitted/)).toBeInTheDocument())
  })

  it('surfaces a feed error instead of crashing', async () => {
    notificationsApi.getNotifications.mockRejectedValue(new Error('network down'))
    const user = userEvent.setup()
    render(<Harness />)
    await waitFor(() => expect(screen.getByTestId('unread-count')).toHaveTextContent('3'))

    await user.click(screen.getByText('Load feed'))

    await waitFor(() => expect(screen.getByText('Could not load notifications.')).toBeInTheDocument())
  })

  it('markRead optimistically decrements the unread count', async () => {
    const user = userEvent.setup()
    render(<Harness />)
    await waitFor(() => expect(screen.getByTestId('unread-count')).toHaveTextContent('3'))
    await user.click(screen.getByText('Load feed'))
    await waitFor(() => expect(screen.getByText(/unread/)).toBeInTheDocument())

    await user.click(screen.getByText(/Request Submitted/))

    await waitFor(() => expect(screen.getByTestId('unread-count')).toHaveTextContent('2'))
    expect(notificationsApi.markNotificationRead).toHaveBeenCalledWith(1)
  })

  it('markAllRead zeroes the unread count immediately', async () => {
    const user = userEvent.setup()
    render(<Harness />)
    await waitFor(() => expect(screen.getByTestId('unread-count')).toHaveTextContent('3'))

    await user.click(screen.getByText('Mark all read'))

    await waitFor(() => expect(screen.getByTestId('unread-count')).toHaveTextContent('0'))
    expect(notificationsApi.markAllNotificationsRead).toHaveBeenCalled()
  })

  it('polls again after 30s while the tab is visible', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    render(<Harness />)

    await vi.waitFor(() => expect(notificationsApi.getUnreadCount).toHaveBeenCalledTimes(1))

    await vi.advanceTimersByTimeAsync(30_000)

    await vi.waitFor(() => expect(notificationsApi.getUnreadCount).toHaveBeenCalledTimes(2))
  })

  it('skips the poll tick while the tab is hidden', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    Object.defineProperty(document, 'hidden', { configurable: true, get: () => true })

    render(<Harness />)
    await vi.waitFor(() => expect(notificationsApi.getUnreadCount).toHaveBeenCalledTimes(1))

    await vi.advanceTimersByTimeAsync(30_000)

    expect(notificationsApi.getUnreadCount).toHaveBeenCalledTimes(1)

    Object.defineProperty(document, 'hidden', { configurable: true, get: () => false })
  })
})
