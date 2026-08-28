import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import SupplierManagement from './SupplierManagement.jsx'
import * as suppliersApi from '../../api/suppliers.js'

vi.mock('../../api/suppliers.js')

const SAMPLE_SUPPLIER = { supplierId: 1, name: 'Acme Corp', leadTimeDays: 5, isActive: true, rowVersion: 'v1' }

describe('SupplierManagement', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('shows a loading state while fetching', () => {
    suppliersApi.getSuppliers.mockReturnValue(new Promise(() => {}))

    render(<SupplierManagement />)

    expect(screen.getByText(/loading suppliers/i)).toBeInTheDocument()
  })

  it('shows an error state when the request fails', async () => {
    suppliersApi.getSuppliers.mockRejectedValue(new Error('network down'))

    render(<SupplierManagement />)

    expect(await screen.findByText(/something went wrong/i)).toBeInTheDocument()
  })

  it('shows an empty state when there are no suppliers', async () => {
    suppliersApi.getSuppliers.mockResolvedValue({ items: [], page: 1, pageSize: 50, totalCount: 0 })

    render(<SupplierManagement />)

    expect(await screen.findByText(/no suppliers yet/i)).toBeInTheDocument()
  })

  it('renders a populated table and surfaces a 409 on deactivate', async () => {
    suppliersApi.getSuppliers.mockResolvedValue({
      items: [SAMPLE_SUPPLIER],
      page: 1,
      pageSize: 50,
      totalCount: 1,
    })
    suppliersApi.deactivateSupplier.mockRejectedValue({
      response: { data: { detail: 'Supplier 1 still supplies active items and cannot be deactivated.' } },
    })

    render(<SupplierManagement />)
    await screen.findByText('Acme Corp')

    await userEvent.click(screen.getByRole('button', { name: /deactivate acme corp/i }))
    await userEvent.click(screen.getByRole('button', { name: /^deactivate$/i }))

    expect(
      await screen.findByText(/still supplies active items/i),
    ).toBeInTheDocument()
  })
})
