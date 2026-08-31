import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { MemoryRouter } from 'react-router-dom'
import MyRequestsPage from './MyRequestsPage.jsx'
import * as requestsApi from '../../api/requests.js'

vi.mock('../../api/requests.js')
vi.mock('../../contexts/AuthContext.jsx', () => ({
  useAuth: () => ({
    user: {
      employeeNumber: 42,
      name: 'Arthur Dent',
      role: 'Engineer',
      rankLevel: 1,
    },
    token: 'mock-token',
    login: vi.fn(),
    logout: vi.fn(),
  }),
}))

const SAMPLE_REQUESTS = [
  {
    requestId: 101,
    requestorEmployeeNumber: 42,
    requestorName: 'Arthur Dent',
    approverEmployeeNumber: 10,
    approverName: 'Ford Prefect',
    status: 'Pending',
    totalEstimatedCost: 25.0,
    requiredByDate: '2026-09-05T00:00:00Z',
    decisionComment: null,
    createdAtUtc: '2026-08-30T10:00:00Z',
    decidedAtUtc: null,
    rowVersion: 'guid-1',
    items: [
      {
        requestItemId: 1,
        itemId: 5,
        itemName: 'Sticky Notes Yellow',
        categoryName: 'Paper',
        supplierId: 1,
        supplierName: 'Stationery Corp',
        quantity: 5,
        unitCostSnapshot: 5.0,
        lineTotal: 25.0,
      },
    ],
    statusHistory: [
      {
        historyId: 1,
        requestId: 101,
        fromStatus: null,
        toStatus: 'Pending',
        actorEmployeeNumber: 42,
        actorName: 'Arthur Dent',
        comment: 'Request created',
        createdAtUtc: '2026-08-30T10:00:00Z',
      },
    ],
  },
  {
    requestId: 102,
    requestorEmployeeNumber: 42,
    requestorName: 'Arthur Dent',
    approverEmployeeNumber: 10,
    approverName: 'Ford Prefect',
    status: 'Pending',
    totalEstimatedCost: 40.0,
    requiredByDate: null,
    decisionComment: null,
    createdAtUtc: '2026-08-29T10:00:00Z',
    decidedAtUtc: null,
    rowVersion: 'guid-2',
    items: [],
    statusHistory: [
      {
        historyId: 2,
        requestId: 102,
        fromStatus: null,
        toStatus: 'Pending',
        actorEmployeeNumber: 42,
        actorName: 'Arthur Dent',
        comment: 'Request created',
        createdAtUtc: '2026-08-29T10:00:00Z',
      },
      {
        historyId: 3,
        requestId: 102,
        fromStatus: 'Pending',
        toStatus: 'Pending',
        actorEmployeeNumber: 42,
        actorName: 'Arthur Dent',
        comment: 'Request submitted for approval',
        createdAtUtc: '2026-08-29T10:05:00Z',
      },
    ],
  },
  {
    requestId: 103,
    requestorEmployeeNumber: 42,
    requestorName: 'Arthur Dent',
    approverEmployeeNumber: 10,
    approverName: 'Ford Prefect',
    status: 'Approved',
    totalEstimatedCost: 50.0,
    requiredByDate: null,
    decisionComment: 'Approved for Q3 work',
    createdAtUtc: '2026-08-25T10:00:00Z',
    decidedAtUtc: '2026-08-26T10:00:00Z',
    rowVersion: 'guid-3',
    items: [],
    statusHistory: [],
  },
]

function renderMyRequestsPage() {
  return render(
    <MemoryRouter>
      <MyRequestsPage />
    </MemoryRouter>,
  )
}

describe('MyRequestsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('shows loading state while fetching requests', () => {
    requestsApi.getMyRequests.mockReturnValue(new Promise(() => {}))
    renderMyRequestsPage()
    expect(screen.getByText(/loading your stationery requests/i)).toBeInTheDocument()
  })

  it('shows empty state when no requests exist', async () => {
    requestsApi.getMyRequests.mockResolvedValue({ items: [], page: 1, pageSize: 15, totalCount: 0 })
    renderMyRequestsPage()
    expect(await screen.findByText(/no stationery requests yet/i)).toBeInTheDocument()
  })

  it('renders requests table with statuses and actions', async () => {
    requestsApi.getMyRequests.mockResolvedValue({
      items: SAMPLE_REQUESTS,
      page: 1,
      pageSize: 15,
      totalCount: 3,
    })

    renderMyRequestsPage()

    expect(await screen.findByText('#101')).toBeInTheDocument()
    expect(screen.getByText('#102')).toBeInTheDocument()
    expect(screen.getByText('#103')).toBeInTheDocument()

    // 101 is unsubmitted Pending -> has Submit button and Delete button
    expect(screen.getByRole('button', { name: /submit request #101/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /delete draft request #101/i })).toBeInTheDocument()

    // 102 is submitted Pending -> has Withdraw button
    expect(screen.getByRole('button', { name: /withdraw request #102/i })).toBeInTheDocument()

    // 103 is Approved -> has Cancel button
    expect(screen.getByRole('button', { name: /request cancellation for #103/i })).toBeInTheDocument()
  })

  it('opens request detail modal when clicking View', async () => {
    requestsApi.getMyRequests.mockResolvedValue({
      items: SAMPLE_REQUESTS,
      page: 1,
      pageSize: 15,
      totalCount: 3,
    })

    renderMyRequestsPage()

    const viewBtn = await screen.findByRole('button', { name: /view details for request #101/i })
    await userEvent.click(viewBtn)

    expect(await screen.findByText('Sticky Notes Yellow')).toBeInTheDocument()
    expect(screen.getByText('Stationery Corp')).toBeInTheDocument()
  })

  it('submits an unsubmitted request from the table action', async () => {
    requestsApi.getMyRequests.mockResolvedValue({
      items: SAMPLE_REQUESTS,
      page: 1,
      pageSize: 15,
      totalCount: 3,
    })
    requestsApi.submitRequest.mockResolvedValue({ ...SAMPLE_REQUESTS[0], status: 'Pending' })

    renderMyRequestsPage()

    const submitBtn = await screen.findByRole('button', { name: /submit request #101/i })
    await userEvent.click(submitBtn)

    await waitFor(() => {
      expect(requestsApi.submitRequest).toHaveBeenCalledWith(101, 'guid-1')
    })
  })

  it('withdraws a submitted pending request with confirmation', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    requestsApi.getMyRequests.mockResolvedValue({
      items: SAMPLE_REQUESTS,
      page: 1,
      pageSize: 15,
      totalCount: 3,
    })
    requestsApi.withdrawRequest.mockResolvedValue({ ...SAMPLE_REQUESTS[1], status: 'Withdrawn' })

    renderMyRequestsPage()

    const withdrawBtn = await screen.findByRole('button', { name: /withdraw request #102/i })
    await userEvent.click(withdrawBtn)

    await waitFor(() => {
      expect(requestsApi.withdrawRequest).toHaveBeenCalledWith(102, 'guid-2')
    })
  })

  it('requests cancellation for approved request through modal', async () => {
    requestsApi.getMyRequests.mockResolvedValue({
      items: SAMPLE_REQUESTS,
      page: 1,
      pageSize: 15,
      totalCount: 3,
    })
    requestsApi.requestCancellation.mockResolvedValue({ ...SAMPLE_REQUESTS[2], status: 'CancellationPending' })

    renderMyRequestsPage()

    const cancelBtn = await screen.findByRole('button', { name: /request cancellation for #103/i })
    await userEvent.click(cancelBtn)

    expect(await screen.findByText(/request cancellation for #103/i)).toBeInTheDocument()

    const reasonInput = screen.getByRole('textbox', { name: /reason for cancellation/i })
    await userEvent.type(reasonInput, 'Project scope changed')

    const confirmBtn = screen.getByRole('button', { name: /submit cancellation request/i })
    await userEvent.click(confirmBtn)

    await waitFor(() => {
      expect(requestsApi.requestCancellation).toHaveBeenCalledWith(103, 'guid-3', 'Project scope changed')
    })
  })
})
