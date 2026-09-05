import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'

import SupplierOrdersModal from './SupplierOrdersModal.jsx'
import * as api from '../../../api/supplierRequests.js'

vi.mock('../../../api/supplierRequests.js', async (importOriginal) => ({
  ...(await importOriginal()),
  getSupplierRequests: vi.fn(),
  confirmSupplierRequestArrival: vi.fn(),
}))

const ORDER = {
  supplierRequestId: 7,
  supplierId: 2,
  supplierName: 'Global Paper Co.',
  totalCost: 98.0,
  createdAtUtc: '2026-09-04T09:00:00Z',
  createdByEmployeeNumber: 22,
  status: 'PendingArrival',
  receivedAtUtc: null,
  receivedByEmployeeNumber: null,
  receivedByName: null,
  items: [
    { itemId: 2, itemName: 'A3 Copy Paper, 250 Sheets', quantity: 10, unitCostSnapshot: 9.8, lineTotal: 98.0 },
  ],
}

function renderModal(props = {}) {
  return render(
    <SupplierOrdersModal open canConfirm onClose={vi.fn()} onConfirmed={vi.fn()} {...props} />,
  )
}

describe('SupplierOrdersModal', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    api.getSupplierRequests.mockResolvedValue({ items: [ORDER], page: 1, pageSize: 50, totalCount: 1 })
    api.confirmSupplierRequestArrival.mockResolvedValue({ ...ORDER, status: 'Received' })
  })

  it('lists the order with its supplier and line count', async () => {
    renderModal()
    expect(await screen.findByText('Global Paper Co.')).toBeInTheDocument()

    const table = within(screen.getByRole('table'))
    expect(table.getByText('#7')).toBeInTheDocument()
    expect(table.getByText('Pending Arrival')).toBeInTheDocument()
  })

  it('reveals what was actually ordered before you confirm it', async () => {
    const user = userEvent.setup()
    renderModal()
    await screen.findByText('Global Paper Co.')

    // Collapsed by default — you should not be confirming blind, but nor is the detail in the way.
    expect(screen.queryByText('A3 Copy Paper, 250 Sheets')).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /show the items on supplier order #7/i }))

    expect(screen.getByText('A3 Copy Paper, 250 Sheets')).toBeInTheDocument()
    expect(screen.getByText('10')).toBeInTheDocument()
  })

  it('collapses the detail again', async () => {
    const user = userEvent.setup()
    renderModal()
    await screen.findByText('Global Paper Co.')

    await user.click(screen.getByRole('button', { name: /show the items/i }))
    await user.click(screen.getByRole('button', { name: /hide the items/i }))

    expect(screen.queryByText('A3 Copy Paper, 250 Sheets')).not.toBeInTheDocument()
  })

  it('confirms arrival', async () => {
    const user = userEvent.setup()
    renderModal()
    await screen.findByText('Global Paper Co.')

    await user.click(screen.getByRole('button', { name: /confirm arrival of supplier order #7/i }))

    await waitFor(() => expect(api.confirmSupplierRequestArrival).toHaveBeenCalledWith(7))
  })

  it('hides the confirm button from anyone who cannot confirm', async () => {
    renderModal({ canConfirm: false })
    await screen.findByText('Global Paper Co.')

    expect(screen.queryByRole('button', { name: /confirm arrival/i })).not.toBeInTheDocument()
    // The detail is still readable — seeing an order is not the same as certifying it.
    expect(screen.getByRole('button', { name: /show the items/i })).toBeInTheDocument()
  })
})
