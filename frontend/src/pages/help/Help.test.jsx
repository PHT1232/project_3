import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'

import Help from '../Help.jsx'
import { faqEntries, FAQ_AREAS } from './faqData.js'
import * as supportApi from '../../api/support.js'

vi.mock('../../api/support.js')
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
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('renders the FAQ, contact card and system info', () => {
    render(<Help />)
    expect(screen.getByRole('heading', { name: /frequently asked questions/i })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: /contact the team/i })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: /system info/i })).toBeInTheDocument()
  })

  it('filters questions as you type', async () => {
    const user = userEvent.setup()
    render(<Help />)

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

  it('sends a message through the in-app dialog, not a mailto link', async () => {
    const user = userEvent.setup()
    supportApi.sendSupportMessage.mockResolvedValue({ id: 1, status: 'New' })
    render(<Help />)

    await user.click(screen.getByRole('button', { name: /message the team/i }))

    // Dialog is open.
    expect(screen.getByRole('dialog', { name: /message the team/i })).toBeInTheDocument()
    const send = screen.getByRole('button', { name: /^send$/i })
    expect(send).toBeDisabled() // nothing typed yet

    await user.type(screen.getByPlaceholderText(/short summary/i), 'Approve button broken')
    await user.type(screen.getByPlaceholderText(/what happened/i), 'It spins forever on request 5.')
    await user.click(send)

    await waitFor(() =>
      expect(supportApi.sendSupportMessage).toHaveBeenCalledWith(
        expect.objectContaining({
          subject: 'Approve button broken',
          body: 'It spins forever on request 5.',
          area: expect.any(String),
          diagnostics: expect.stringContaining('User: #42'),
        }),
      ),
    )
    expect(await screen.findByText(/the team can see this now/i)).toBeInTheDocument()
  })

  it('shows the signed-in user in system info', () => {
    render(<Help />)
    expect(screen.getByText(/#42 · Arthur Dent/)).toBeInTheDocument()
  })
})
