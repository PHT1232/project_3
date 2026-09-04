/**
 * Where "contact the team" on the Help page sends people. One shared inbox for now
 * (team members aren't individually named in the repo — CLAUDE.md K6).
 *
 * Email is a `mailto:` hand-off to the user's own mail client — the app never sends mail
 * itself (SMTP is on the Plan's [CUT] list).
 */
export const SUPPORT_EMAIL = 'antsconst84@gmail.com'

/** The §4.2 feature areas, offered as the "Area" line in a bug report. */
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

/**
 * Build a `mailto:` URL with a pre-filled subject and a body scaffold the reporter can
 * fill in. `diagnostics` is a plain string block appended verbatim so support can see the
 * reporter's context without asking.
 */
export function buildSupportMailto({ subject, area, diagnostics }) {
  const body = [
    'What happened:',
    '',
    '',
    'What I expected instead:',
    '',
    '',
    'Steps to reproduce:',
    '1. ',
    '2. ',
    '',
    area ? `Area: ${area}` : null,
    '',
    '--- diagnostics (please keep) ---',
    diagnostics,
  ]
    .filter((line) => line !== null)
    .join('\n')

  return `mailto:${SUPPORT_EMAIL}?subject=${encodeURIComponent(subject)}&body=${encodeURIComponent(body)}`
}

/** The context block shared by the mailto body and the "copy diagnostics" button. */
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
