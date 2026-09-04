/**
 * Support-contact config for the Help page.
 *
 * The primary channel is an in-app message stored in the support inbox (a Manager+ triages
 * it) — the app never sends email (SMTP is on the Plan's [CUT] list). `SUPPORT_EMAIL` is
 * kept only as a manual fallback shown on the card. Team members aren't named individually in
 * the repo (CLAUDE.md K6), so it's one shared address.
 */
export const SUPPORT_EMAIL = 'antsconst84@gmail.com'

/** Offered as the "Area" of a message; mirrors the §4.2 feature list. */
export const SUPPORT_AREAS = [
  'Login / account',
  'Catalogue',
  'New request',
  'My Requests',
  'Approvals',
  'Budget / eligibility',
  'Notifications',
  'Reports',
  'Inventory',
  'AI assistant',
  'Something else',
]

/** Session context attached to every message and offered by "Copy diagnostics". */
export function buildDiagnostics(user) {
  return [
    `App version: ${__APP_VERSION__}`,
    `Built: ${__BUILD_TIME__}`,
    `Page: ${typeof window !== 'undefined' ? window.location.href : '(unknown)'}`,
    `User: #${user?.employeeNumber ?? '?'} ${user?.name ?? ''} (${user?.role ?? 'unknown role'})`,
    `Browser: ${typeof navigator !== 'undefined' ? navigator.userAgent : '(unknown)'}`,
    `When: ${new Date().toISOString()}`,
  ].join('\n')
}
