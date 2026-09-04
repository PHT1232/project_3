import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, it, expect } from 'vitest'

import DashboardKpis from './DashboardKpis.jsx'

function renderKpis(props) {
  return render(
    <MemoryRouter>
      <DashboardKpis
        pendingApprovals={0}
        lowStockCount={0}
        isManager={false}
        eligibility={null}
        {...props}
      />
    </MemoryRouter>,
  )
}

describe('DashboardKpis', () => {
  it('links the Pending Approvals card straight to the approvals page', () => {
    renderKpis({ pendingApprovals: 3 })

    const link = screen.getByRole('link', { name: /review approvals/i })
    expect(link).toHaveAttribute('href', '/approvals')
  })

  it('shows the quick link even when there is nothing pending', () => {
    renderKpis({ pendingApprovals: 0 })

    expect(screen.getByRole('link', { name: /review approvals/i })).toBeInTheDocument()
  })
})
