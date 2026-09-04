import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'

import InventoryPage from './InventoryPage.jsx'
import * as inventoryApi from '../../api/inventory.js'
import * as supplierRequestsApi from '../../api/supplierRequests.js'
import * as suppliersApi from '../../api/suppliers.js'

vi.mock('../../api/inventory.js', async () => {
  const actual = await vi.importActual('../../api/inventory.js')
  return { ...actual, getInventory: vi.fn() }
})
vi.mock('../../api/supplierRequests.js')
vi.mock('../../api/suppliers.js')

// InventoryPage reads the caller's rank to decide whether to offer "Goods Arrived" on a supplier
// order. A Manager (rank 2) may raise orders but not confirm arrivals — the cart behaviour these
// tests cover is the same either way.
vi.mock('../../contexts/AuthContext.jsx', () => ({
  useAuth: () => ({
    user: { employeeNumber: 401, name: 'Mia Manager', role: 'Manager', rankLevel: 2 },
    isAuthenticated: true,
    restoring: false,
    login: vi.fn(),
    logout: vi.fn(),
  }),
}))

const ROWS = [
  {
    itemId: 1,
    itemName: 'Ballpoint Pens',
    quantityAvailable: 10,
    reorderLevel: 20,
    unitCost: 5,
    status: 'REORDER_NOW',
    rowVersion: 'v1',
    supplierId: 7,
    supplierName: 'Supplier X',
  },
  {
    itemId: 2,
    itemName: 'A5 Notebook',
    quantityAvailable: 80,
    reorderLevel: 20,
    unitCost: 3,
    status: 'OK',
    rowVersion: 'v2',
    supplierId: 8,
    supplierName: 'Supplier Y',
  },
  {
    itemId: 3,
    itemName: 'Orphan Item',
    quantityAvailable: 40,
    reorderLevel: 10,
    unitCost: 2,
    status: 'OK',
    rowVersion: 'v3',
    supplierId: null,
    supplierName: null,
  },
]

function mockInventory(rows = ROWS) {
  inventoryApi.getInventory.mockResolvedValue({
    items: rows,
    summary: { totalItems: rows.length, lowStockAlerts: 1, totalValue: 100 },
  })
}

async function selectItem(user, name) {
  await user.click(screen.getByRole('checkbox', { name: new RegExp(`Select ${name}`, 'i') }))
}

describe('Inventory supplier-request cart', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockInventory()
    suppliersApi.getSuppliers.mockResolvedValue({
      items: [{ supplierId: 9, name: 'Fallback Supplier' }],
    })
  })

  it('disables the request button until items are selected', async () => {
    render(<InventoryPage />)
    await screen.findByText('Ballpoint Pens')

    expect(screen.getByRole('button', { name: /request from suppliers/i })).toBeDisabled()

    await selectItem(userEvent.setup(), 'Ballpoint Pens')

    expect(screen.getByRole('button', { name: /request from suppliers/i })).toBeEnabled()
  })

  it('shows every selected item with its supplier in the review modal', async () => {
    const user = userEvent.setup()
    render(<InventoryPage />)
    await screen.findByText('Ballpoint Pens')

    await selectItem(user, 'Ballpoint Pens')
    await selectItem(user, 'A5 Notebook')
    await user.click(screen.getByRole('button', { name: /request from suppliers/i }))

    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText('Ballpoint Pens')).toBeInTheDocument()
    expect(within(dialog).getByText('Supplier X')).toBeInTheDocument()
    expect(within(dialog).getByText('A5 Notebook')).toBeInTheDocument()
    expect(within(dialog).getByText('Supplier Y')).toBeInTheDocument()
  })

  it('submits each item with its independently edited quantity', async () => {
    const user = userEvent.setup()
    supplierRequestsApi.createSupplierRequests.mockResolvedValue([
      {
        supplierRequestId: 1,
        supplierId: 7,
        supplierName: 'Supplier X',
        totalCost: 50,
        items: [{ itemId: 1, itemName: 'Ballpoint Pens', quantity: 10, lineTotal: 50 }],
      },
    ])

    render(<InventoryPage />)
    await screen.findByText('Ballpoint Pens')

    await selectItem(user, 'Ballpoint Pens')
    await user.click(screen.getByRole('button', { name: /request from suppliers/i }))

    const dialog = await screen.findByRole('dialog')
    const qty = within(dialog).getByLabelText(/quantity for Ballpoint Pens/i)
    await user.clear(qty)
    await user.type(qty, '10')

    await user.click(within(dialog).getByRole('button', { name: /submit request/i }))

    await waitFor(() =>
      expect(supplierRequestsApi.createSupplierRequests).toHaveBeenCalledWith([
        { itemId: 1, quantity: 10, supplierId: 7 },
      ]),
    )
  })

  it('shows grouped success feedback and clears the selection', async () => {
    const user = userEvent.setup()
    supplierRequestsApi.createSupplierRequests.mockResolvedValue([
      {
        supplierRequestId: 1,
        supplierId: 7,
        supplierName: 'Supplier X',
        totalCost: 5,
        items: [{ itemId: 1, itemName: 'Ballpoint Pens', quantity: 1, lineTotal: 5 }],
      },
      {
        supplierRequestId: 2,
        supplierId: 8,
        supplierName: 'Supplier Y',
        totalCost: 3,
        items: [{ itemId: 2, itemName: 'A5 Notebook', quantity: 1, lineTotal: 3 }],
      },
    ])

    render(<InventoryPage />)
    await screen.findByText('Ballpoint Pens')

    await selectItem(user, 'Ballpoint Pens')
    await selectItem(user, 'A5 Notebook')
    await user.click(screen.getByRole('button', { name: /request from suppliers/i }))

    const dialog = await screen.findByRole('dialog')
    await user.click(within(dialog).getByRole('button', { name: /submit request/i }))

    expect(await screen.findByText(/2 items requested from 2 suppliers/i)).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /done/i }))

    await waitFor(() =>
      expect(screen.getByRole('button', { name: /request from suppliers/i })).toBeDisabled(),
    )
  })

  it('surfaces server validation errors instead of swallowing them', async () => {
    const user = userEvent.setup()
    supplierRequestsApi.createSupplierRequests.mockRejectedValue({
      response: { data: { errors: { items: ['Item 1 is inactive and cannot be ordered.'] } } },
    })

    render(<InventoryPage />)
    await screen.findByText('Ballpoint Pens')

    await selectItem(user, 'Ballpoint Pens')
    await user.click(screen.getByRole('button', { name: /request from suppliers/i }))

    const dialog = await screen.findByRole('dialog')
    await user.click(within(dialog).getByRole('button', { name: /submit request/i }))

    expect(await screen.findByText(/is inactive and cannot be ordered/i)).toBeInTheDocument()
  })

  it('blocks submission until a supplier is chosen for an item that has none', async () => {
    const user = userEvent.setup()
    render(<InventoryPage />)
    await screen.findByText('Orphan Item')

    await selectItem(user, 'Orphan Item')
    await user.click(screen.getByRole('button', { name: /request from suppliers/i }))

    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByRole('button', { name: /submit request/i })).toBeDisabled()

    const picker = await within(dialog).findByLabelText(/supplier for Orphan Item/i)
    await user.selectOptions(picker, '9')

    expect(within(dialog).getByRole('button', { name: /submit request/i })).toBeEnabled()
  })

  it('keeps the cart when a search filters the row out of view', async () => {
    const user = userEvent.setup()
    render(<InventoryPage />)
    await screen.findByText('Ballpoint Pens')

    await selectItem(user, 'Ballpoint Pens')
    await user.type(screen.getByLabelText(/search inventory/i), 'Notebook')

    await waitFor(() => expect(screen.queryByText('Ballpoint Pens')).not.toBeInTheDocument())

    await user.click(screen.getByRole('button', { name: /request from suppliers/i }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText('Ballpoint Pens')).toBeInTheDocument()
  })
})
