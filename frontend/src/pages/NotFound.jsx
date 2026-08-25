import { Link, useLocation } from 'react-router-dom'
import { FileQuestion } from 'lucide-react'
import Card from '../components/ui/Card.jsx'
import Button from '../components/ui/Button.jsx'

export default function NotFound() {
  const { pathname } = useLocation()

  return (
    <Card className="flex flex-col items-center justify-center gap-4 px-6 py-20 text-center">
      <FileQuestion className="h-8 w-8 text-ink-subtle" aria-hidden="true" />
      <p className="text-4xl font-bold tracking-tight text-brand-700">404</p>
      <div>
        <h1 className="text-lg font-bold tracking-tight text-ink">Page not found</h1>
        <p className="mt-2 max-w-md text-sm text-ink-muted">
          We couldn&apos;t find <code className="font-mono text-xs">{pathname}</code>. The link may
          be out of date, or the page may not exist yet.
        </p>
      </div>
      <Button as={Link} to="/">
        Back to Dashboard
      </Button>
    </Card>
  )
}
