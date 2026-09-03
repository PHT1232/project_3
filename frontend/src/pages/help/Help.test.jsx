import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi } from 'vitest'

import Help from '../Help.jsx'
import { faqEntries, FAQ_AREAS } from './faqData.js'
import { SUPPORT_EMAIL } from '../../config/support.js'

vi.mock('../../contexts/AuthContext.jsx', () => ({
  useAuth: () => ({
    user: {
      employeeNumber: 42,
      name: 'Arthur Dent',
      email: 'arthur@hmt.test',
      role: 'Engineer',
      rankLevel: 1,
      superiorEmployeeNumber: 7,
    },
    token: 'mock-token',
    login: vi.fn(),
    logout: vi.fn(),
  }),
}))

describe('faqData', () => {
  it('has at least 15 entries (Plan T6.1)', () => {
    expect(faqEntries.length).toBeGreaterThanOrEqual(15)
  })

  it('covers every declared feature area', () => {
    const covered = new Set(faqEntries.map((e) => e.area))
    for (const area of FAQ_AREAS) {
      expect(covered).toContain(area)
    }
  })

  it('every entry has a non-empty question and answer', () => {
    for (const e of faqEntries) {
      expect(e.question.trim().length).toBeGreaterThan(0)
      expect(e.answer.trim().length).toBeGreaterThan(0)
    }
  })
})

describe('Help page', () => {
  it('renders the FAQ, contact card and system info', () => {
    render(<Help />)
    expect(screen.getByRole('heading', { name: /frequently asked questions/i })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: /contact the team/i })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: /system info/i })).toBeInTheDocument()
  })

  it('filters questions as you type', async () => {
    const user = userEvent.setup()
    render(<Help />)

    // A term that only appears in the budget answers.
    await user.type(screen.getByLabelText(/search help/i), 'reset')

    expect(screen.getByText(/when does my budget reset/i)).toBeInTheDocument()
    expect(screen.queryByText(/how do i sign in/i)).not.toBeInTheDocument()
  })

  it('shows a friendly message when nothing matches', async () => {
    const user = userEvent.setup()
    render(<Help />)

    await user.type(screen.getByLabelText(/search help/i), 'zzzznotathing')
    expect(screen.getByText(/no answers match that search/i)).toBeInTheDocument()
  })

  it('points the contact buttons at the shared inbox with a prefilled subject', () => {
    render(<Help />)

    const bug = screen.getByRole('link', { name: /report a bug/i })
    expect(bug).toHaveAttribute('href', expect.stringContaining(`mailto:${SUPPORT_EMAIL}`))
    expect(bug).toHaveAttribute('href', expect.stringContaining('subject='))
    expect(bug.getAttribute('href')).toContain('bug%20report')
  })

  it('shows the signed-in user in system info', () => {
    render(<Help />)
    expect(screen.getByText(/#42 · Arthur Dent/)).toBeInTheDocument()
  })
})
