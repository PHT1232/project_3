import client from './client.js'

export async function getUsers({ page = 1, pageSize = 20, role, location } = {}) {
  const { data } = await client.get('/users', {
    params: {
      page,
      pageSize,
      role: role || undefined,
      location: location || undefined,
    },
  })
  return data
}

export async function createUser(payload) {
  const { data } = await client.post('/users', payload)
  return data
}

export async function updateUser(employeeNumber, payload) {
  const { data } = await client.put(`/users/${employeeNumber}`, payload)
  return data
}

export async function setUserStatus(employeeNumber, isActive) {
  const { data } = await client.patch(`/users/${employeeNumber}/status`, { isActive })
  return data
}

export async function getSubordinates(employeeNumber) {
  const { data } = await client.get(`/users/${employeeNumber}/subordinates`)
  return data
}

/**
 * The caller's own spending eligibility — role limits, month-to-date committed spend and
 * what's left this month (Plan §4.2 `GET /users/me/eligibility`). Any authenticated user.
 */
export async function getMyEligibility() {
  const { data } = await client.get('/users/me/eligibility')
  return data
}
