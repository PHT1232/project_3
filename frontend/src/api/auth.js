import client from './client.js'

export async function login(employeeNumber, password) {
  const { data } = await client.post('/auth/login', { employeeNumber, password })
  return data
}

export async function fetchCurrentUser() {
  const { data } = await client.get('/auth/me')
  return data
}

export async function changePassword(currentPassword, newPassword) {
  await client.post('/auth/change-password', { currentPassword, newPassword })
}
