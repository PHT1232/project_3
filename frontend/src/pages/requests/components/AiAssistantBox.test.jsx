import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import AiAssistantBox from './AiAssistantBox.jsx'
import * as aiApi from '../../../api/ai.js'

vi.mock('../../../api/ai.js')

const DRAFT = {
  items: [
    {
      itemId: 101,
      itemName: 'Ballpoint Pen Black',
      categoryName: 'Writing Instruments',
      supplierName: 'Office Depot',
      unitOfMeasure: 'Each',
      unitCost: 1.5,
      quantity: 2,
      quantityAvailable: 40,
    },
  ],
  requiredByDate: '2999-01-10T00:00:00Z',
  note: null,
  totalEstimatedCost: 3,
  warnings: [],
  wasFallback: false,
  model: 'gemini-3.5-flash-lite',
}

describe('AiAssistantBox', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('keeps the draft button disabled until something is typed', async () => {
    const user = userEvent.setup()
    render(<AiAssistantBox onApplyDraft={vi.fn()} />)

    const button = screen.getByRole('button', { name: /draft with ai/i })
    expect(button).toBeDisabled()

    await user.type(screen.getByLabelText(/describe the stationery/i), '2 black pens')
    expect(button).toBeEnabled()
  })

  it('shows the draft and hands it to the page on "Add to request"', async () => {
    const user = userEvent.setup()
    const onApplyDraft = vi.fn()
    aiApi.draftRequestFromText.mockResolvedValue(DRAFT)
    render(<AiAssistantBox onApplyDraft={onApplyDraft} />)

    await user.type(screen.getByLabelText(/describe the stationery/i), '2 black pens')
    await user.click(screen.getByRole('button', { name: /draft with ai/i }))

    expect(aiApi.draftRequestFromText).toHaveBeenCalledWith('2 black pens')
    expect(await screen.findByText('Ballpoint Pen Black')).toBeInTheDocument()
    expect(screen.queryByText(/ai assistant unavailable/i)).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /add to request/i }))
    expect(onApplyDraft).toHaveBeenCalledWith(DRAFT)
    expect(screen.queryByTestId('ai-draft')).not.toBeInTheDocument()
  })

  it('shows an honest notice and the warnings when the server used the keyword fallback', async () => {
    const user = userEvent.setup()
    aiApi.draftRequestFromText.mockResolvedValue({
      ...DRAFT,
      wasFallback: true,
      model: 'keyword-fallback',
      warnings: [
        'The AI assistant was unavailable, so items were matched by keyword. Please check the items and quantities before submitting.',
        'Only 1 of Ballpoint Pen Black in stock; you asked for 2.',
      ],
    })
    render(<AiAssistantBox onApplyDraft={vi.fn()} />)

    await user.type(screen.getByLabelText(/describe the stationery/i), '2 black pens')
    await user.click(screen.getByRole('button', { name: /draft with ai/i }))

    expect(await screen.findByText(/ai assistant unavailable/i)).toBeInTheDocument()
    expect(screen.getByText(/only 1 of ballpoint pen black in stock/i)).toBeInTheDocument()
  })

  it('surfaces an API error and leaves the page usable', async () => {
    const user = userEvent.setup()
    aiApi.draftRequestFromText.mockRejectedValue({ response: { status: 429 } })
    render(<AiAssistantBox onApplyDraft={vi.fn()} />)

    await user.type(screen.getByLabelText(/describe the stationery/i), 'pens')
    await user.click(screen.getByRole('button', { name: /draft with ai/i }))

    expect(await screen.findByRole('alert')).toHaveTextContent(/used the assistant a lot/i)
    expect(screen.getByRole('button', { name: /draft with ai/i })).toBeEnabled()
  })

  it('disables input when the page says the user cannot raise a request', () => {
    render(<AiAssistantBox disabled onApplyDraft={vi.fn()} />)

    expect(screen.getByLabelText(/describe the stationery/i)).toBeDisabled()
  })
})
