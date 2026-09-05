import { render, screen } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'

import ErrorBoundary from './ErrorBoundary.jsx'

function Boom() {
  throw new Error('render exploded')
}

describe('ErrorBoundary', () => {
  beforeEach(() => {
    // React logs the caught error itself; silence it so a passing run stays readable.
    vi.spyOn(console, 'error').mockImplementation(() => {})
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('renders its children when nothing throws', () => {
    render(
      <ErrorBoundary>
        <p>All good</p>
      </ErrorBoundary>,
    )

    expect(screen.getByText('All good')).toBeInTheDocument()
  })

  it('shows a recoverable screen instead of unmounting to a blank page', () => {
    render(
      <ErrorBoundary>
        <Boom />
      </ErrorBoundary>,
    )

    expect(screen.getByRole('heading', { name: /something broke/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /reload the page/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /go to dashboard/i })).toBeInTheDocument()
  })

  it('reassures the user that nothing was saved', () => {
    render(
      <ErrorBoundary>
        <Boom />
      </ErrorBoundary>,
    )

    expect(screen.getByText(/nothing was saved or changed/i)).toBeInTheDocument()
  })
})
