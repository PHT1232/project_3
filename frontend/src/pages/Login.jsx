import { useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { Files, Eye, EyeOff } from 'lucide-react'
import Card from '../components/ui/Card.jsx'
import Button from '../components/ui/Button.jsx'
import { useAuth } from '../contexts/AuthContext.jsx'

export default function Login() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  // One field for both identifiers. api/auth.js decides which one to send: all digits is an
  // employee number, anything else is an email address.
  const [identifier, setIdentifier] = useState('')
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [error, setError] = useState(null)
  const [submitting, setSubmitting] = useState(false)

  const redirectTo = location.state?.from?.pathname ?? '/'

  async function handleSubmit(event) {
    event.preventDefault()
    setError(null)
    setSubmitting(true)

    try {
      await login(identifier, password)
      navigate(redirectTo, { replace: true })
    } catch (err) {
      // 400 means the identifier itself was malformed (not a number in range, not a valid
      // address); 401 stays deliberately vague about which half was wrong.
      setError(
        err.response?.status === 400
          ? 'Enter a valid employee number or email address.'
          : err.response?.status === 401
            ? 'Those sign-in details are incorrect.'
            : 'Could not sign in. Please try again.',
      )
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center px-4 py-12">
      <div className="w-full max-w-sm">
        <div className="mb-6 flex items-center justify-center gap-2">
          <Files className="h-7 w-7 text-brand-700" aria-hidden="true" />
          <span className="text-xl font-bold tracking-tight text-brand-700">StationeryMS</span>
        </div>

        <Card className="px-6 py-8">
          <h1 className="text-lg font-bold tracking-tight text-ink">Sign in</h1>

          <form className="mt-6 space-y-4" onSubmit={handleSubmit} noValidate>
            <div>
              <label htmlFor="identifier" className="block text-sm font-medium text-ink">
                Employee number or email
              </label>
              <input
                id="identifier"
                name="identifier"
                type="text"
                inputMode="email"
                required
                autoFocus
                autoComplete="username"
                placeholder="101 or you@hmt.local"
                value={identifier}
                onChange={(event) => setIdentifier(event.target.value)}
                className="mt-1 w-full rounded-md border border-surface-border bg-surface-card px-3 py-2 text-sm text-ink outline-none focus:border-brand-500"
              />
            </div>

            <div>
              <label htmlFor="password" className="block text-sm font-medium text-ink">
                Password
              </label>
              <div className="relative mt-1">
                <input
                  id="password"
                  name="password"
                  type={showPassword ? 'text' : 'password'}
                  required
                  autoComplete="current-password"
                  value={password}
                  onChange={(event) => setPassword(event.target.value)}
                  className="w-full rounded-md border border-surface-border bg-surface-card py-2 pl-3 pr-10 text-sm text-ink outline-none focus:border-brand-500"
                />
                {/* tabIndex -1 keeps Tab going straight from the password field to Sign in, so
                    the reveal is available to the mouse without interrupting a typed sign-in. */}
                <button
                  type="button"
                  tabIndex={-1}
                  onClick={() => setShowPassword((visible) => !visible)}
                  aria-label={showPassword ? 'Hide password' : 'Show password'}
                  aria-pressed={showPassword}
                  className="absolute inset-y-0 right-0 flex items-center rounded-r-md px-3 text-ink-muted hover:text-ink focus:outline-none focus-visible:text-ink"
                >
                  {showPassword
                    ? <EyeOff className="h-4 w-4" aria-hidden="true" />
                    : <Eye className="h-4 w-4" aria-hidden="true" />}
                </button>
              </div>
            </div>

            {error && (
              <p role="alert" className="text-sm text-status-danger">
                {error}
              </p>
            )}

            <Button type="submit" className="w-full" disabled={submitting}>
              {submitting ? 'Signing in…' : 'Sign in'}
            </Button>
          </form>
        </Card>
      </div>
    </div>
  )
}
