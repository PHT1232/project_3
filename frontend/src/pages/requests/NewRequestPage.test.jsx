import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { MemoryRouter } from 'react-router-dom'
import NewRequestPage from './NewRequestPage.jsx'
import * as catalogueApi from '../../api/catalogue.js'
import * as requestsApi from '../../api/requests.js'

vi.mock('../../api/catalogue.js')
vi.mock('../../api/requests.js')
vi.mock('../../contexts/AuthContext.jsx', () => ({
  useAuth: () => ({
    user: {
      employeeNumber: 42,
      name: 'Arthur Dent',
      role: 'Engineer',
      rankLevel: 1,
      // CurrentUserDto always carries this, and an Engineer necessarily reports to someone.
      // Omitting it made the mock claim a state the API cannot produce for a rank-1 user.
      superiorEmployeeNumber: 7,
    },
    token: 'mock-token',
    login: vi.fn(),
    logout: vi.fn(),
  }),
}))

const SAMPLE_ITEMS = [
  {
    itemId: 101,
    itemName: 'Ballpoint Pen Blue',
    categoryName: 'Writing Instruments',
    supplierId: 1,
    supplierName: 'Office Depot',
    unitCost: 1.5,
    quantityAvailable: 100,
    isActive: true,
  },
  {
    itemId: 102,
    itemName: 'A4 Notebook Grid',
    categoryName: 'Paper Products',
    supplierId: 1,
    supplierName: 'Office Depot',
    unitCost: 3.0,
    quantityAvailable: 50,
    isActive: true,
  },
]

function renderNewRequestPage() {
  return render(
    <MemoryRouter>
      <NewRequestPage />
    </MemoryRouter>,
  )
}

describe('NewRequestPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    // getItems() unwraps the paged envelope and resolves to the item array itself
    // (api/catalogue.js). Mocking it as { items: [...] } encoded a contract the real client
    // never had, which is how the "0 available" picker bug reached the running app.
    catalogueApi.getItems.mockResolvedValue(SAMPLE_ITEMS)
  })

  it('renders request header, requestor info, and item selector', async () => {
    renderNewRequestPage()

    expect(screen.getByRole('heading', { name: /new stationery request/i })).toBeInTheDocument()
    expect(screen.getByText(/Arthur Dent/)).toBeInTheDocument()
    expect(await screen.findByText(/-- Select an item/i)).toBeInTheDocument()
  })

  it('allows adding items to the request and calculates estimated total', async () => {
    renderNewRequestPage()

    // Select the first item
    const select = await screen.findByRole('combobox')
    await userEvent.selectOptions(select, '101')

    // Click Add to Request
    const addBtn = screen.getByRole('button', { name: /add to request/i })
    await userEvent.click(addBtn)

    // Verify item appears in table
    expect(screen.getByText('Ballpoint Pen Blue')).toBeInTheDocument()
    expect(screen.getByText(/Total distinct items/i)).toBeInTheDocument()

    // Add second item
    await userEvent.selectOptions(select, '102')
    await userEvent.click(addBtn)

    expect(screen.getByText('A4 Notebook Grid')).toBeInTheDocument()
  })

  it('allows modifying quantity and removing an item', async () => {
    renderNewRequestPage()

    const select = await screen.findByRole('combobox')
    await userEvent.selectOptions(select, '101')
    await userEvent.click(screen.getByRole('button', { name: /add to request/i }))

    expect(screen.getByText('Ballpoint Pen Blue')).toBeInTheDocument()

    // Change quantity
    const qtyInput = screen.getByRole('spinbutton', { name: /quantity for ballpoint pen blue/i })
    await userEvent.clear(qtyInput)
    await userEvent.type(qtyInput, '5')

    // Remove item
    const removeBtn = screen.getByRole('button', { name: /remove ballpoint pen blue/i })
    await userEvent.click(removeBtn)

    expect(screen.queryByText('Ballpoint Pen Blue')).not.toBeInTheDocument()
    expect(screen.getByText(/no items in your request/i)).toBeInTheDocument()
  })

  it('saves request as draft (createRequest only)', async () => {
    requestsApi.createRequest.mockResolvedValue({
      requestId: 55,
      rowVersion: 'mock-guid',
      status: 'Pending',
    })

    renderNewRequestPage()

    const select = await screen.findByRole('combobox')
    await userEvent.selectOptions(select, '101')
    await userEvent.click(screen.getByRole('button', { name: /add to request/i }))

    const draftBtn = screen.getByRole('button', { name: /save as draft/i })
    await userEvent.click(draftBtn)

    await waitFor(() => {
      expect(requestsApi.createRequest).toHaveBeenCalledWith({
        items: [{ itemId: 101, quantity: 1 }],
        requiredByDate: null,
      })
      expect(requestsApi.submitRequest).not.toHaveBeenCalled()
    })
  })

  it('submits request immediately (createRequest then submitRequest)', async () => {
    requestsApi.createRequest.mockResolvedValue({
      requestId: 56,
      rowVersion: 'mock-guid',
      status: 'Pending',
    })
    requestsApi.submitRequest.mockResolvedValue({
      requestId: 56,
      status: 'Pending',
    })

    renderNewRequestPage()

    const select = await screen.findByRole('combobox')
    await userEvent.selectOptions(select, '101')
    await userEvent.click(screen.getByRole('button', { name: /add to request/i }))

    const submitBtn = screen.getByRole('button', { name: /submit request/i })
    await userEvent.click(submitBtn)

    await waitFor(() => {
      expect(requestsApi.createRequest).toHaveBeenCalledWith({
        items: [{ itemId: 101, quantity: 1 }],
        requiredByDate: null,
      })
      expect(requestsApi.submitRequest).toHaveBeenCalledWith(56, 'mock-guid')
    })
  })
})
