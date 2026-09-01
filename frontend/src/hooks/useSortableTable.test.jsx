import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect } from 'vitest'

import useSortableTable from './useSortableTable.js'
import SortableHeader from '../components/ui/SortableHeader.jsx'

const COLUMNS = {
  name: { type: 'string' },
  qty: { type: 'number' },
  status: { type: 'order', order: ['REORDER_NOW', 'WATCH', 'OK'] },
  isActive: { type: 'boolean' },
}

const ROWS = [
  { id: 1, name: 'Charlie', qty: 5, status: 'OK', isActive: false, note: 'b' },
  { id: 2, name: 'alpha', qty: 100, status: 'REORDER_NOW', isActive: true, note: null },
  { id: 3, name: 'Bravo', qty: 20, status: 'WATCH', isActive: true, note: 'a' },
]

function Harness({ rows = ROWS, initial = null }) {
  const { sortedRows, headerProps } = useSortableTable(rows, { ...COLUMNS, note: { type: 'string' } }, initial)
  return (
    <table>
      <thead>
        <tr>
          <SortableHeader {...headerProps('name')}>Name</SortableHeader>
          <SortableHeader {...headerProps('qty')}>Qty</SortableHeader>
          <SortableHeader {...headerProps('status')}>Status</SortableHeader>
          <SortableHeader {...headerProps('isActive')}>Active</SortableHeader>
          <SortableHeader {...headerProps('note')}>Note</SortableHeader>
        </tr>
      </thead>
      <tbody>
        {sortedRows.map((row) => (
          <tr key={row.id}>
            <td data-testid="cell">{row.name}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

const order = () => screen.getAllByTestId('cell').map((c) => c.textContent)

describe('useSortableTable + SortableHeader', () => {
  it('leaves rows untouched until a header is clicked', () => {
    render(<Harness />)
    expect(order()).toEqual(['Charlie', 'alpha', 'Bravo'])
  })

  it('sorts ascending on first click and descending on the second', async () => {
    const user = userEvent.setup()
    render(<Harness />)

    await user.click(screen.getByRole('button', { name: 'Name' }))
    expect(order()).toEqual(['alpha', 'Bravo', 'Charlie'])

    await user.click(screen.getByRole('button', { name: 'Name' }))
    expect(order()).toEqual(['Charlie', 'Bravo', 'alpha'])
  })

  it('sorts numbers numerically, not lexically', async () => {
    const user = userEvent.setup()
    render(<Harness />)

    await user.click(screen.getByRole('button', { name: 'Qty' }))
    expect(order()).toEqual(['Charlie', 'Bravo', 'alpha']) // 5, 20, 100
  })

  it('sorts an ordered column by its declared severity, not alphabetically', async () => {
    const user = userEvent.setup()
    render(<Harness />)

    await user.click(screen.getByRole('button', { name: 'Status' }))
    // REORDER_NOW -> WATCH -> OK, which A-Z would never produce.
    expect(order()).toEqual(['alpha', 'Bravo', 'Charlie'])
  })

  it('sorts booleans active-first', async () => {
    const user = userEvent.setup()
    render(<Harness />)

    await user.click(screen.getByRole('button', { name: 'Active' }))
    expect(order().at(-1)).toBe('Charlie') // the only inactive row
  })

  it('keeps only one column active at a time', async () => {
    const user = userEvent.setup()
    render(<Harness />)

    await user.click(screen.getByRole('button', { name: 'Name' }))
    await user.click(screen.getByRole('button', { name: 'Qty' }))

    const headers = screen.getAllByRole('columnheader')
    expect(headers.filter((h) => h.getAttribute('aria-sort') !== 'none')).toHaveLength(1)
  })

  it('exposes aria-sort reflecting the active column and direction', async () => {
    const user = userEvent.setup()
    render(<Harness />)

    const nameHeader = screen.getByRole('columnheader', { name: /Name/ })
    expect(nameHeader).toHaveAttribute('aria-sort', 'none')

    await user.click(screen.getByRole('button', { name: 'Name' }))
    expect(nameHeader).toHaveAttribute('aria-sort', 'ascending')

    await user.click(screen.getByRole('button', { name: 'Name' }))
    expect(nameHeader).toHaveAttribute('aria-sort', 'descending')
  })

  it('is keyboard operable', async () => {
    const user = userEvent.setup()
    render(<Harness />)

    await user.tab()
    expect(screen.getByRole('button', { name: 'Name' })).toHaveFocus()

    await user.keyboard('{Enter}')
    expect(order()).toEqual(['alpha', 'Bravo', 'Charlie'])

    await user.keyboard(' ')
    expect(order()).toEqual(['Charlie', 'Bravo', 'alpha'])
  })

  it('sinks blank values to the bottom in both directions', async () => {
    const user = userEvent.setup()
    render(<Harness />)

    await user.click(screen.getByRole('button', { name: 'Note' }))
    expect(order().at(-1)).toBe('alpha') // note: null

    await user.click(screen.getByRole('button', { name: 'Note' }))
    expect(order().at(-1)).toBe('alpha')
  })

  it('honours an initial sort', () => {
    render(<Harness initial={{ key: 'name', dir: 'asc' }} />)
    expect(order()).toEqual(['alpha', 'Bravo', 'Charlie'])
  })
})
