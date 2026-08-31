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
    },
  ],
  statusHistory: [],
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
})
