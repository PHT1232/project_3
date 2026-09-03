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
    reorderLevel: 20,
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
    reorderLevel: 50,
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
    expect(await screen.findByRole('checkbox', { name: /select ballpoint pen blue/i })).toBeInTheDocument()
    expect(screen.getByText('100 available')).toBeInTheDocument()
  })

  it('searches available catalogue items by name or category before adding', async () => {
    const user = userEvent.setup()
    renderNewRequestPage()

    const search = await screen.findByRole('searchbox', { name: /search catalogue items/i })
    await user.type(search, 'notebook')

    expect(screen.getByText('A4 Notebook Grid')).toBeInTheDocument()
    expect(screen.queryByText('Ballpoint Pen Blue')).not.toBeInTheDocument()

    await user.clear(search)
    await user.type(search, 'writing instruments')

    expect(screen.getByText('Ballpoint Pen Blue')).toBeInTheDocument()
  })

  it('filters catalogue items to low stock only', async () => {
    const user = userEvent.setup()
    renderNewRequestPage()

    await user.selectOptions(await screen.findByLabelText(/stock status/i), 'low-stock')

    expect(screen.getByText('A4 Notebook Grid')).toBeInTheDocument()
    expect(screen.queryByText('Ballpoint Pen Blue')).not.toBeInTheDocument()
  })

  it('paginates the catalogue picker table', async () => {
    const user = userEvent.setup()
    catalogueApi.getItems.mockResolvedValue(
      Array.from({ length: 11 }, (_, index) => ({
        ...SAMPLE_ITEMS[0],
        itemId: index + 1,
        itemName: `Stationery Item ${index + 1}`,
      })),
    )
    renderNewRequestPage()

    expect(await screen.findByText('Stationery Item 1')).toBeInTheDocument()
    expect(screen.getByText('Stationery Item 5')).toBeInTheDocument()
    expect(screen.queryByText('Stationery Item 6')).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /next/i }))
    expect(screen.getByText('Stationery Item 6')).toBeInTheDocument()
    expect(screen.queryByText('Stationery Item 1')).not.toBeInTheDocument()
  })

  it('allows selecting and adding multiple items to the request', async () => {
    const user = userEvent.setup()
    renderNewRequestPage()

    await user.click(await screen.findByRole('checkbox', { name: /select ballpoint pen blue/i }))
    await user.click(screen.getByRole('checkbox', { name: /select a4 notebook grid/i }))
    await user.click(screen.getByRole('button', { name: /add selected items \(2\)/i }))

    expect(screen.getByText('Ballpoint Pen Blue')).toBeInTheDocument()
    expect(screen.getByText('A4 Notebook Grid')).toBeInTheDocument()
    expect(screen.getByText(/Total distinct items/i)).toBeInTheDocument()
  })

  it('allows modifying quantity and removing an item', async () => {
    renderNewRequestPage()

    await userEvent.click(await screen.findByRole('checkbox', { name: /select ballpoint pen blue/i }))
    await userEvent.click(screen.getByRole('button', { name: /add selected items \(1\)/i }))

    expect(screen.getByText('Ballpoint Pen Blue')).toBeInTheDocument()

    // Change quantity
    const qtyInput = screen.getByRole('spinbutton', { name: /quantity for ballpoint pen blue/i })
    await userEvent.clear(qtyInput)
    await userEvent.type(qtyInput, '5')

    // Remove item
    const removeBtn = screen.getByRole('button', { name: /remove ballpoint pen blue/i })
    await userEvent.click(removeBtn)

    expect(screen.queryByRole('button', { name: /remove ballpoint pen blue/i })).not.toBeInTheDocument()
    expect(screen.getByText(/no items in your request/i)).toBeInTheDocument()
    expect(screen.getByRole('checkbox', { name: /select ballpoint pen blue/i })).toBeInTheDocument()
  })

  it('saves request as draft (createRequest only)', async () => {
    requestsApi.createRequest.mockResolvedValue({
      requestId: 55,
      rowVersion: 'mock-guid',
      status: 'Pending',
    })

    renderNewRequestPage()

    await userEvent.click(await screen.findByRole('checkbox', { name: /select ballpoint pen blue/i }))
    await userEvent.click(screen.getByRole('button', { name: /add selected items \(1\)/i }))

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

    await userEvent.click(await screen.findByRole('checkbox', { name: /select ballpoint pen blue/i }))
    await userEvent.click(screen.getByRole('button', { name: /add selected items \(1\)/i }))

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
