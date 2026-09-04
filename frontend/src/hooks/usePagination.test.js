import { renderHook, act } from '@testing-library/react'
import { describe, it, expect } from 'vitest'
import usePagination, { DEFAULT_PAGE_SIZE } from './usePagination.js'

const rows = (n) => Array.from({ length: n }, (_, i) => i)

describe('usePagination', () => {
  it('defaults to 12 rows per page', () => {
    expect(DEFAULT_PAGE_SIZE).toBe(12)

    const { result } = renderHook(() => usePagination(rows(30)))

    expect(result.current.pageRows).toHaveLength(12)
    expect(result.current.totalPages).toBe(3)
    expect(result.current.total).toBe(30)
  })

  it('slices the requested page and reports the last one short', () => {
    const { result } = renderHook(() => usePagination(rows(30)))

    act(() => result.current.setPage(3))

    expect(result.current.page).toBe(3)
    expect(result.current.pageRows).toEqual([24, 25, 26, 27, 28, 29])
  })

  it('reports one page and no slicing for an empty list', () => {
    const { result } = renderHook(() => usePagination([]))

    expect(result.current.totalPages).toBe(1)
    expect(result.current.pageRows).toEqual([])
    expect(result.current.page).toBe(1)
  })

  it('resets to page 1 when the row count changes, e.g. a filter is applied', () => {
    const { result, rerender } = renderHook(({ data }) => usePagination(data), {
      initialProps: { data: rows(30) },
    })

    act(() => result.current.setPage(3))
    expect(result.current.page).toBe(3)

    rerender({ data: rows(5) })

    expect(result.current.page).toBe(1)
    expect(result.current.pageRows).toEqual([0, 1, 2, 3, 4])
  })

  it('never leaves the page past the end', () => {
    const { result, rerender } = renderHook(({ data }) => usePagination(data), {
      initialProps: { data: rows(30) },
    })

    act(() => result.current.setPage(3))
    rerender({ data: rows(13) })

    expect(result.current.page).toBeLessThanOrEqual(result.current.totalPages)
    expect(result.current.pageRows.length).toBeGreaterThan(0)
  })

  it('isOnPage marks exactly the current window, for tables that must still print in full', () => {
    const { result } = renderHook(() => usePagination(rows(30)))

    expect(result.current.isOnPage(0)).toBe(true)
    expect(result.current.isOnPage(11)).toBe(true)
    expect(result.current.isOnPage(12)).toBe(false)

    act(() => result.current.setPage(2))

    expect(result.current.isOnPage(11)).toBe(false)
    expect(result.current.isOnPage(12)).toBe(true)
    expect(result.current.isOnPage(23)).toBe(true)
    expect(result.current.isOnPage(24)).toBe(false)
  })

  it('honours an explicit page size', () => {
    const { result } = renderHook(() => usePagination(rows(10), 4))

    expect(result.current.totalPages).toBe(3)
    expect(result.current.pageRows).toEqual([0, 1, 2, 3])
  })
})
