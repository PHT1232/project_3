import client from './client.js'

export async function getSuppliers({ page = 1, pageSize = 50, includeInactive = true } = {}) {
  const { data } = await client.get('/suppliers', { params: { page, pageSize, includeInactive } })
  return data
}

export async function createSupplier(payload) {
  return (await client.post('/suppliers', payload)).data
}

export async function updateSupplier(supplierId, payload) {
  return (await client.put(`/suppliers/${supplierId}`, payload)).data
}

export async function deactivateSupplier(supplierId) {
  await client.patch(`/suppliers/${supplierId}/deactivate`)
}
