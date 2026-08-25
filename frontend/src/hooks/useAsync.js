import { useCallback, useEffect, useState } from 'react'

/**
 * Runs an async service call and exposes real request state.
 *
 * Deliberately has no artificial delay and no simulated failure — `loading` is true only while
 * the promise is genuinely pending, and `error` is set only when it genuinely rejects. When the
 * mock data source is swapped for axios (see `src/api/`), this hook needs no changes.
 */
export default function useAsync(fn, deps = []) {
  const [state, setState] = useState({ data: null, error: null, loading: true })

  // eslint-disable-next-line react-hooks/exhaustive-deps
  const run = useCallback(fn, deps)

  const load = useCallback(() => {
    let cancelled = false
    setState({ data: null, error: null, loading: true })

    run()
      .then((data) => {
        if (!cancelled) setState({ data, error: null, loading: false })
      })
      .catch((error) => {
        if (!cancelled) setState({ data: null, error, loading: false })
      })

    return () => {
      cancelled = true
    }
  }, [run])

  useEffect(() => load(), [load])

  return { ...state, reload: load }
}
