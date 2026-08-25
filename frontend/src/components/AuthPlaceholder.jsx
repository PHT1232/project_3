import { Files, Construction } from 'lucide-react'
import Card from './ui/Card.jsx'

/**
 * Placeholder for the authentication screens, which render outside the app shell (no sidebar).
 *
 * Contains no form, no fields and no auth logic — authentication is M1's work
 * (Plan M1: `POST /api/v1/auth/login`, JWT, `AuthContext`, `ProtectedRoute`).
 */
export default function AuthPlaceholder({ title, note }) {
  return (
    <div className="flex min-h-screen items-center justify-center px-4 py-12">
      <div className="w-full max-w-sm">
        <div className="mb-6 flex items-center justify-center gap-2">
          <Files className="h-7 w-7 text-brand-700" aria-hidden="true" />
          <span className="text-xl font-bold tracking-tight text-brand-700">StationeryMS</span>
        </div>
        <Card className="px-6 py-10 text-center">
          <Construction className="mx-auto h-7 w-7 text-ink-subtle" aria-hidden="true" />
          <h1 className="mt-3 text-lg font-bold tracking-tight text-ink">{title}</h1>
          <p className="mt-2 text-sm text-ink-muted">This page is under development.</p>
          {note && <p className="mt-3 text-xs text-ink-subtle">{note}</p>}
        </Card>
      </div>
    </div>
  )
}
