import { render, screen } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import ItemManagement from './ItemManagement.jsx'
import * as catalogueApi from '../../api/catalogue.js'
import * as suppliersApi from '../../api/suppliers.js'

vi.mock('../../api/catalogue.js')
vi.mock('../../api/suppliers.js')

describe('ItemManagement', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('shows a loading state while fetching', () => {
    catalogueApi.getItems.mockReturnValue(new Promise(() => {}))
    catalogueApi.getCategories.mockReturnValue(new Promise(() => {}))
    suppliersApi.getSuppliers.mockReturnValue(new Promise(() => {}))

    render(<ItemManagement />)

    expect(screen.getByText(/loading items/i)).toBeInTheDocument()
  })

  it('shows an empty state when there are no items', async () => {
    catalogueApi.getItems.mockResolvedValue([])
    catalogueApi.getCategories.mockResolvedValue([])
    suppliersApi.getSuppliers.mockResolvedValue({ items: [], page: 1, pageSize: 50, totalCount: 0 })

    render(<ItemManagement />)

    expect(await screen.findByText(/no items yet/i)).toBeInTheDocument()
  })

  it('renders a populated table of items', async () => {
    catalogueApi.getItems.mockResolvedValue([
      {
        itemId: 1,
        itemName: 'Stapler',
        categoryId: 1,
        categoryName: 'Office',
        unitOfMeasure: 'Each',
        unitCost: 4.5,
        quantityAvailable: 10,
        reorderLevel: 2,
        minRankLevelToRequest: 1,
        isActive: true,
        supplierId: null,
        rowVersion: 'v1',
      },
    ])
    catalogueApi.getCategories.mockResolvedValue([{ categoryId: 1, name: 'Office', isActive: true }])
    suppliersApi.getSuppliers.mockResolvedValue({ items: [], page: 1, pageSize: 50, totalCount: 0 })

    render(<ItemManagement />)

    expect(await screen.findByText('Stapler')).toBeInTheDocument()
  })
})
