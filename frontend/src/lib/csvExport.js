/**
 * Client-side CSV download. Pure frontend utility — no API involved.
 *
 * Callers pass values already formatted as display strings; this module only
 * handles RFC-4180 quoting and the browser download. A value containing a comma,
 * double-quote, CR or LF is wrapped in double-quotes with embedded quotes doubled
 * (e.g. `Lever Arch Files, A4, Pack of 5` → `"Lever Arch Files, A4, Pack of 5"`).
 */

function escapeCell(value) {
  const text = value == null ? '' : String(value)
  return /[",\r\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text
}

/** Serialise headers + rows to an RFC-4180 CSV string (CRLF line endings). */
export function toCsv(headers, rows) {
  return [headers, ...rows].map((cells) => cells.map(escapeCell).join(',')).join('\r\n')
}

/**
 * Build the CSV and trigger a download via a temporary anchor.
 * A UTF-8 BOM is prepended so Excel opens non-ASCII item names correctly.
 *
 * @param {string} filename   e.g. "cost-by-item-2026-05-30-2026-08-28.csv"
 * @param {string[]} headers
 * @param {string[][]} rows   each row's values already formatted as strings
 */
export function exportToCsv(filename, headers, rows) {
  const bom = String.fromCharCode(0xfeff)
  const blob = new Blob([bom, toCsv(headers, rows)], {
    type: 'text/csv;charset=utf-8;',
  })
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = filename
  document.body.appendChild(anchor)
  anchor.click()
  anchor.remove()
  URL.revokeObjectURL(url)
}
