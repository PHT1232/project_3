import { Component } from 'react'
import { AlertOctagon } from 'lucide-react'

import Button from './ui/Button.jsx'

/**
 * Catches a render-time exception anywhere below it and shows a recoverable screen instead of
 * React unmounting the whole tree — which left a blank white page with no way back but a manual
 * refresh.
 *
 * Must be a class: `componentDidCatch` / `getDerivedStateFromError` have no hook equivalent.
 *
 * This is a last resort, not error handling. Failed API calls are already handled where they
 * happen (`useAsync` + `ErrorState`); what reaches here is a genuine bug — a bad render off
 * unexpected data — so the copy says "something broke" rather than pretending it is transient.
 */
export default class ErrorBoundary extends Component {
  state = { error: null }

  static getDerivedStateFromError(error) {
    return { error }
  }

  componentDidCatch(error, info) {
    // Nothing ships logs off the client (no Sentry — it is not in the Plan), so the console is
    // the only record. Keep the component stack: it is what makes the trace readable.
    console.error('Unhandled render error:', error, info?.componentStack)
  }

  render() {
    if (!this.state.error) {
      return this.props.children
    }

    return (
      <div className="flex min-h-screen items-center justify-center px-4 py-12">
        <div className="w-full max-w-md rounded-card border border-surface-border bg-surface-card p-6 text-center">
          <AlertOctagon className="mx-auto h-8 w-8 text-status-danger" aria-hidden="true" />
          <h1 className="mt-3 text-lg font-bold tracking-tight text-ink">Something broke</h1>
          <p className="mt-2 text-sm text-ink-muted">
            This page hit an unexpected error. Your data is safe — nothing was saved or changed by
            the failure.
          </p>

          <div className="mt-5 flex justify-center gap-2">
            {/* A full reload, not setState({error:null}): whatever state produced the bad render
                is still there, so re-rendering the same tree would just throw again. */}
            <Button onClick={() => window.location.reload()}>Reload the page</Button>
            <Button variant="secondary" onClick={() => { window.location.href = '/' }}>
              Go to Dashboard
            </Button>
          </div>

          {import.meta.env.DEV && (
            <pre className="mt-5 max-h-48 overflow-auto whitespace-pre-wrap rounded-md bg-surface-muted p-3 text-left text-[11px] text-ink-muted">
              {this.state.error?.stack ?? String(this.state.error)}
            </pre>
          )}
        </div>
      </div>
    )
  }
}
