import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi } from 'vitest'
import Pagination from './Pagination.jsx'

describe('Pagination', () => {
  it('summarises the position and the total', () => {
    render(<Pagination page={2} totalPages={4} total={40} onPageChange={vi.fn()} />)

    expect(screen.getByText(/Page 2 of 4 · 40 items/)).toBeInTheDocument()
  })

  it('uses the singular noun for one row and a custom noun when given', () => {
    const { rerender } = render(
      <Pagination page={1} totalPages={1} total={1} onPageChange={vi.fn()} />,
    )
    expect(screen.getByText(/1 item$/)).toBeInTheDocument()

    rerender(
      <Pagination page={1} totalPages={2} total={7} onPageChange={vi.fn()} noun="supplier" />,
    )
    expect(screen.getByText(/7 suppliers$/)).toBeInTheDocument()
  })

  it('steps forward and back', async () => {
    const onPageChange = vi.fn()
    const user = userEvent.setup()
    render(<Pagination page={2} totalPages={4} total={40} onPageChange={onPageChange} />)

    await user.click(screen.getByRole('button', { name: /next/i }))
    expect(onPageChange).toHaveBeenCalledWith(3)

    await user.click(screen.getByRole('button', { name: /previous/i }))
    expect(onPageChange).toHaveBeenCalledWith(1)
  })

  it('disables the ends', () => {
    const { rerender } = render(
      <Pagination page={1} totalPages={3} total={30} onPageChange={vi.fn()} />,
    )
    expect(screen.getByRole('button', { name: /previous/i })).toBeDisabled()
    expect(screen.getByRole('button', { name: /next/i })).toBeEnabled()

    rerender(<Pagination page={3} totalPages={3} total={30} onPageChange={vi.fn()} />)
    expect(screen.getByRole('button', { name: /previous/i })).toBeEnabled()
    expect(screen.getByRole('button', { name: /next/i })).toBeDisabled()
  })

  it('is marked so it never appears on a printed report', () => {
    const { container } = render(
      <Pagination page={1} totalPages={2} total={20} onPageChange={vi.fn()} />,
    )

    expect(container.firstChild).toHaveAttribute('data-print-hide')
  })
})
