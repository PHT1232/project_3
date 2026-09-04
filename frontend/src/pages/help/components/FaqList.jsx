import { useMemo, useState } from 'react'
import { ChevronDown } from 'lucide-react'

import Card from '../../../components/ui/Card.jsx'
import SearchInput from '../../../components/ui/SearchInput.jsx'
import { EmptyState } from '../../../components/ui/StateBlock.jsx'
import { faqEntries } from '../faqData.js'

/**
 * Searchable Q&A. Entries are grouped by area; typing in the box filters across question
 * and answer text and auto-opens what matches. Uses native <details> so it stays keyboard-
 * and screen-reader-friendly without per-row state.
 */
export default function FaqList() {
  const [query, setQuery] = useState('')

  const term = query.trim().toLowerCase()

  const groups = useMemo(() => {
    const matched = term
      ? faqEntries.filter(
          (e) =>
            e.question.toLowerCase().includes(term) || e.answer.toLowerCase().includes(term),
        )
      : faqEntries

    const byArea = new Map()
    for (const entry of matched) {
      if (!byArea.has(entry.area)) byArea.set(entry.area, [])
      byArea.get(entry.area).push(entry)
    }
    return [...byArea.entries()]
  }, [term])

  const matchCount = groups.reduce((n, [, entries]) => n + entries.length, 0)

  return (
    <Card className="p-5">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <h2 className="text-base font-semibold text-ink">Frequently asked questions</h2>
        <SearchInput
          value={query}
          onChange={setQuery}
          label="Search help"
          placeholder="Search help…"
          className="sm:w-72"
        />
      </div>

      {term && (
        <p className="mt-3 text-xs text-ink-muted">
          {matchCount === 0
            ? 'No answers match that search.'
            : `${matchCount} ${matchCount === 1 ? 'answer' : 'answers'} matching “${query.trim()}”`}
        </p>
      )}

      {matchCount === 0 && !term ? null : matchCount === 0 ? (
        <EmptyState
          title="Nothing found"
          description="Try a different word, or email the team using the card on the right."
        />
      ) : (
        <div className="mt-4 space-y-6">
          {groups.map(([area, entries]) => (
            <section key={area}>
              <h3 className="text-xs font-semibold uppercase tracking-wide text-ink-muted">
                {area}
              </h3>
              <ul className="mt-2 divide-y divide-surface-border border-y border-surface-border">
                {entries.map((entry) => (
                  <li key={entry.question}>
                    <details open={Boolean(term)} className="group">
                      <summary className="flex cursor-pointer list-none items-center justify-between gap-3 py-3 text-sm font-medium text-ink marker:content-none">
                        {entry.question}
                        <ChevronDown
                          className="h-4 w-4 shrink-0 text-ink-subtle transition-transform group-open:rotate-180"
                          aria-hidden="true"
                        />
                      </summary>
                      <p className="pb-3 text-sm leading-relaxed text-ink-muted">
                        {entry.answer}
                      </p>
                    </details>
                  </li>
                ))}
              </ul>
            </section>
          ))}
        </div>
      )}
    </Card>
  )
}
