import { Construction } from 'lucide-react'
import PageHeader from './layout/PageHeader.jsx'
import Card from './ui/Card.jsx'

/**
 * Placeholder for pages owned by other developers.
 *
 * Deliberately contains no forms, tables, API calls or mock data — the owning developer
 * replaces this component wholesale. See `docs/development/page-map.md` for each page's
 * scope, required endpoints and related entities.
 */
export default function PagePlaceholder({ title, owner }) {
  return (
    <>
      <PageHeader title={title} />
      <Card className="flex flex-col items-center justify-center gap-3 px-6 py-16 text-center">
        <Construction className="h-7 w-7 text-ink-subtle" aria-hidden="true" />
        <p className="text-sm font-semibold text-ink">This page is under development.</p>
        <p className="max-w-md text-sm text-ink-muted">
          {owner ? `Owned by ${owner}. ` : ''}
          See <code className="font-mono text-xs">docs/development/page-map.md</code> for the
          scope, endpoints and entities for this page.
        </p>
      </Card>
    </>
  )
}
