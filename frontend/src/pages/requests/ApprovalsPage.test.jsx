import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import ApprovalsPage from './ApprovalsPage.jsx'
import * as requestsApi from '../../api/requests.js'

vi.mock('../../api/requests.js')

const SAMPLE_REQUEST = {
  requestId: 1,
  requestorEmployeeNumber: 202,
  requestorName: 'Eve Engineer',
  approverEmployeeNumber: 101,
  approverName: 'Ada Manager',
  status: 'Pending',
  totalEstimatedCost: 12.5,
  requiredByDate: null,
  decisionComment: null,
  createdAtUtc: '2026-08-28T00:00:00Z',
  decidedAtUtc: null,
  rowVersion: 'v1',
  items: [
    {
      requestItemId: 1,
      itemId: 10,
      itemName: 'Stapler',
      categoryName: 'Office',
      supplierId: null,
      supplierName: null,
      quantity: 2,
      unitCostSnapshot: 6.25,
      lineTotal: 12.5,
      decision: null,
      approvedQuantity: null,
    },
  ],
  statusHistory: [],
}

const CANCELLATION_REQUEST = {
  ...SAMPLE_REQUEST,
  requestId: 2,
  status: 'CancellationPending',
  rowVersion: 'v7',
  items: [{ ...SAMPLE_REQUEST.items[0], requestItemId: 2, decision: 'approved', approvedQuantity: 2 }],
  statusHistory: [
    {
      historyId: 9,
      requestId: 2,
      fromStatus: 'Approved',
      toStatus: 'CancellationPending',
      actorEmployeeNumber: 202,
      actorName: 'Eve Engineer',
      comment: 'Ordered the wrong colour',
      createdAtUtc: '2026-08-29T00:00:00Z',
    },
  ],
}

describe('ApprovalsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('shows a loading state while fetching', () => {
    requestsApi.getPendingApprovals.mockReturnValue(new Promise(() => {}))

    render(<ApprovalsPage />)

    expect(screen.getByText(/loading pending approvals/i)).toBeInTheDocument()
  })

  it('shows an error state when the request fails', async () => {
    requestsApi.getPendingApprovals.mockRejectedValue(new Error('network down'))

    render(<ApprovalsPage />)

    expect(await screen.findByText(/something went wrong/i)).toBeInTheDocument()
  })

  it('shows an empty state when nothing is pending', async () => {
    requestsApi.getPendingApprovals.mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 })

    render(<ApprovalsPage />)

    expect(await screen.findByText(/nothing pending/i)).toBeInTheDocument()
  })

  it('renders a populated table and submits a decision', async () => {
    requestsApi.getPendingApprovals.mockResolvedValue({
      items: [SAMPLE_REQUEST],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    })
    requestsApi.approveRequest.mockResolvedValue({ ...SAMPLE_REQUEST, status: 'Approved' })

    render(<ApprovalsPage />)
    await screen.findByText('Eve Engineer')

    await userEvent.click(screen.getByRole('button', { name: /review/i }))
    expect(await screen.findByText(/review request #1/i)).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: /submit decision/i }))

    await waitFor(() =>
      expect(requestsApi.approveRequest).toHaveBeenCalledWith(1, {
        rowVersion: 'v1',
        lineDecisions: [{ requestItemId: 1, decision: 'approved', modifiedQuantity: null }],
        comment: null,
      }),
    )
  })

  it('lets the approver decide a cancellation request', async () => {
    requestsApi.getPendingApprovals.mockResolvedValue({
      items: [SAMPLE_REQUEST, CANCELLATION_REQUEST],
      page: 1,
      pageSize: 20,
      totalCount: 2,
    })
    requestsApi.approveCancellation.mockResolvedValue({ ...CANCELLATION_REQUEST, status: 'Cancelled' })

    render(<ApprovalsPage />)
    expect(await screen.findAllByText('Eve Engineer')).toHaveLength(2)

    // A CancellationPending row gets "Decide", not "Review"
    expect(screen.getByRole('button', { name: /review request #1/i })).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: /decide cancellation of request #2/i }))

    expect(await screen.findByText(/cancellation request for #2/i)).toBeInTheDocument()
    expect(screen.getByText('Ordered the wrong colour')).toBeInTheDocument()

    await userEvent.type(screen.getByRole('textbox', { name: /your comment/i }), 'Fine by me')
    await userEvent.click(screen.getByRole('button', { name: /approve cancellation of request #2/i }))

    await waitFor(() =>
      expect(requestsApi.approveCancellation).toHaveBeenCalledWith(2, {
        rowVersion: 'v7',
        approved: true,
        reason: 'Fine by me',
      }),
    )
  })

  // Plan §3.6 guards Pending -> Rejected with "Comment required" (revision-3 finding M5). The
  // server refuses a commentless rejection with 400; these pin the UI half, so the approver is
  // told on the field rather than after pressing Submit.
  describe('rejection requires a comment', () => {
    async function openReviewModal() {
      requestsApi.getPendingApprovals.mockResolvedValue({
        items: [SAMPLE_REQUEST],
        page: 1,
        pageSize: 20,
        totalCount: 1,
      })
      requestsApi.approveRequest.mockResolvedValue({ ...SAMPLE_REQUEST, status: 'Rejected' })

      render(<ApprovalsPage />)
      await screen.findByText('Eve Engineer')
      await userEvent.click(screen.getByRole('button', { name: /review/i }))
      await screen.findByText(/review request #1/i)
    }

    it('blocks submitting a rejection with no comment', async () => {
      await openReviewModal()

      await userEvent.selectOptions(screen.getByRole('combobox'), 'rejected')

      expect(screen.getByLabelText(/comment \(required\)/i)).toBeInTheDocument()
      expect(screen.getByRole('button', { name: /submit decision/i })).toBeDisabled()

      expect(requestsApi.approveRequest).not.toHaveBeenCalled()
    })

    it('submits the rejection once a comment is typed', async () => {
      await openReviewModal()

      await userEvent.selectOptions(screen.getByRole('combobox'), 'rejected')
      await userEvent.type(screen.getByLabelText(/comment \(required\)/i), 'Out of budget')
      await userEvent.click(screen.getByRole('button', { name: /submit decision/i }))

      await waitFor(() =>
        expect(requestsApi.approveRequest).toHaveBeenCalledWith(1, {
          rowVersion: 'v1',
          lineDecisions: [{ requestItemId: 1, decision: 'rejected', modifiedQuantity: null }],
          comment: 'Out of budget',
        }),
      )
    })

    it('leaves the comment optional for an approval', async () => {
      await openReviewModal()

      expect(screen.getByLabelText(/comment \(optional\)/i)).toBeInTheDocument()
      expect(screen.getByRole('button', { name: /submit decision/i })).toBeEnabled()
    })
  })
})
