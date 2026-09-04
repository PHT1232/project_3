import client from './client.js'

/** An identifier made only of digits is an employee number; anything else is treated as an email. */
const EMPLOYEE_NUMBER_PATTERN = /^\d+$/

/**
 * Signs in with either identifier. The server accepts `employeeNumber` or `email` (never both),
 * so the wire shape is decided here rather than in the page or the context — this module owns
 * the API contract.
 *
 * @param {string|number} identifier employee number or email address
 */
export async function login(identifier, password) {
  const trimmed = String(identifier).trim()
  const credentials = EMPLOYEE_NUMBER_PATTERN.test(trimmed)
    ? { employeeNumber: Number(trimmed) }
    : { email: trimmed }

  const { data } = await client.post('/auth/login', { ...credentials, password })
  return data
}

export async function fetchCurrentUser() {
  const { data } = await client.get('/auth/me')
  return data
}

export async function changePassword(currentPassword, newPassword) {
  await client.post('/auth/change-password', { currentPassword, newPassword })
}
