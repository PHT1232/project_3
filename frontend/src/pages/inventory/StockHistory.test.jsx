import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'

import InventoryPage from './InventoryPage.jsx'
import * as inventoryApi from '../../api/inventory.js'

vi.mock('../../api/inventory.js', async () => {
  const actual = await vi.importActual('../../api/inventory.js')
  return { ...actual, getInventory: vi.fn(), getTransactionHistory: vi.fn() }
})
vi.mock('../../api/supplierRequests.js')
vi.mock('../../api/suppliers.js')

const ROWS = [
  {
    itemId: 1,
    itemName: 'Ballpoint Pens',
    quantityAvailable: 43,
    reorderLevel: 20,
    unitCost: 5,
    status: 'REORDER_NOW',
    rowVersion: 'v1',
  },
  {
    itemId: 2,
    itemName: 'A5 Notebook',
    quantityAvailable: 80,
    reorderLevel: 20,
    unitCost: 3,
    status: 'OK',
    rowVersion: 'v2',
  },
]

const HISTORY = [
  {
    transactionId: 3,
    itemId: 1,
    txType: 'Issue',
    changeQuantity: -5,
    unitCostSnapshot: 5,
    reference: 'Request #12',
    createdAtUtc: '2026-08-20T10:00:00Z',
    createdByName: 'Mary Manager',
  },
  {
    transactionId: 1,
    itemId: 1,
    txType: 'Receipt',
    changeQuantity: 48,
    unitCostSnapshot: 5,
    reference: 'PO-9',
    createdAtUtc: '2026-08-01T10:00:00Z',
    createdByName: 'System Admin',
  },
]

async function openHistoryFor(user, itemName) {
  await user.click(screen.getByRole('button', { name: new RegExp(`Actions for ${itemName}`, 'i') }))
  await user.click(screen.getByRole('button', { name: /view stock history/i }))
}

beforeEach(() => {
  vi.clearAllMocks()
  inventoryApi.getInventory.mockResolvedValue({
    items: ROWS,
    summary: { totalItems: ROWS.length, lowStockAlerts: 1, totalValue: 100 },
  })
  inventoryApi.getTransactionHistory.mockResolvedValue(HISTORY)
})

describe('Inventory toolbar acts on the selected row', () => {
  it('disables Adjust and Receive until exactly one row is selected', async () => {
    render(<InventoryPage />)
    await screen.findByText('Ballpoint Pens')

    expect(screen.getByRole('button', { name: /adjust stock/i })).toBeDisabled()
    expect(screen.getByRole('button', { name: /receive goods/i })).toBeDisabled()
  })

  it('opens the dialog for the SELECTED item, not the first visible row', async () => {
    const user = userEvent.setup()
    render(<InventoryPage />)
    await screen.findByText('Ballpoint Pens')

    // Select the second row; the first row is the one that would previously have been used.
    await user.click(screen.getByRole('checkbox', { name: /select A5 Notebook/i }))
    await user.click(screen.getByRole('button', { name: /adjust stock/i }))

    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText('A5 Notebook')).toBeInTheDocument()
    expect(within(dialog).queryByText('Ballpoint Pens')).not.toBeInTheDocument()
  })

  it('disables the action again when several rows are selected', async () => {
    const user = userEvent.setup()
    render(<InventoryPage />)
    await screen.findByText('Ballpoint Pens')

    await user.click(screen.getByRole('checkbox', { name: /select A5 Notebook/i }))
    await user.click(screen.getByRole('checkbox', { name: /select Ballpoint Pens/i }))

    const adjust = screen.getByRole('button', { name: /adjust stock/i })
    expect(adjust).toBeDisabled()
    expect(adjust).toHaveAttribute('title', expect.stringMatching(/exactly one/i))
  })
})

describe('Stock ledger history', () => {
  it('does not call the history endpoint until the modal is opened', async () => {
    render(<InventoryPage />)
    await screen.findByText('Ballpoint Pens')

    expect(inventoryApi.getTransactionHistory).not.toHaveBeenCalled()
  })

  it('loads and lists the ledger for the chosen item', async () => {
    const user = userEvent.setup()
    render(<InventoryPage />)
    await screen.findByText('Ballpoint Pens')

    await openHistoryFor(user, 'Ballpoint Pens')

    await waitFor(() => expect(inventoryApi.getTransactionHistory).toHaveBeenCalledWith(1))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText('Issue')).toBeInTheDocument()
    expect(within(dialog).getByText('Receipt')).toBeInTheDocument()
    expect(within(dialog).getByText('Request #12')).toBeInTheDocument()
    expect(within(dialog).getByText(/Mary Manager/)).toBeInTheDocument()
  })

  it('shows a running balance derived from the current stock level', async () => {
    const user = userEvent.setup()
    render(<InventoryPage />)
    await screen.findByText('Ballpoint Pens')

    await openHistoryFor(user, 'Ballpoint Pens')

    // Current 43; the Issue of -5 leaves 43, and the Receipt before it left 48.
    const dialog = await screen.findByRole('dialog')
    const rows = within(dialog).getAllByRole('row')
    expect(within(rows[1]).getByText('43')).toBeInTheDocument()
    expect(within(rows[2]).getByText('48')).toBeInTheDocument()
    expect(within(dialog).getByText('−5')).toBeInTheDocument()
    expect(within(dialog).getByText('+48')).toBeInTheDocument()
  })

  it('warns when the ledger does not reconcile with the cached balance', async () => {
    const user = userEvent.setup()
    // Ledger nets to 43 but the item claims 50 — 7 unexplained units.
    inventoryApi.getInventory.mockResolvedValue({
      items: [{ ...ROWS[0], quantityAvailable: 50 }],
      summary: { totalItems: 1, lowStockAlerts: 0, totalValue: 100 },
    })
    render(<InventoryPage />)
    await screen.findByText('Ballpoint Pens')

    await openHistoryFor(user, 'Ballpoint Pens')

    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText(/does not reconcile/i)).toBeInTheDocument()
  })

  it('renders an empty state for an item with no movements', async () => {
    const user = userEvent.setup()
    inventoryApi.getTransactionHistory.mockResolvedValue([])
    render(<InventoryPage />)
    await screen.findByText('Ballpoint Pens')

    await openHistoryFor(user, 'Ballpoint Pens')

    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText(/no stock movements/i)).toBeInTheDocument()
  })

  it('surfaces a failed history fetch instead of showing an empty ledger', async () => {
    const user = userEvent.setup()
    inventoryApi.getTransactionHistory.mockRejectedValue(new Error('Network down'))
    render(<InventoryPage />)
    await screen.findByText('Ballpoint Pens')

    await openHistoryFor(user, 'Ballpoint Pens')

    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText(/something went wrong/i)).toBeInTheDocument()
    expect(within(dialog).getByText(/network down/i)).toBeInTheDocument()
  })
})
