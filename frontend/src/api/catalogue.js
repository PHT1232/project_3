import client from './client.js'

/**
 * Catalogue data access. Components import from here and never touch the data source directly.
 *
 * GET /api/v1/categories — any authenticated user.
 * GET /api/v1/items — any authenticated user; server applies the role filter
 *   (MinRankLevelToRequest <= caller's RankLevel) and returns only active items.
 */

export async function getCategories() {
  return (await client.get('/categories')).data
}

export async function getItems() {
  const { data } = await client.get('/items', { params: { pageSize: 500 } })
  return data.items
}

export async function createItem(payload) {
  return (await client.post('/items', payload)).data
}

export async function updateItem(itemId, payload) {
  return (await client.put(`/items/${itemId}`, payload)).data
}

export async function deactivateItem(itemId) {
  await client.patch(`/items/${itemId}/deactivate`)
}

export async function createCategory(name) {
  return (await client.post('/categories', { name })).data
}

export async function updateCategory(categoryId, name) {
  return (await client.put(`/categories/${categoryId}`, { name })).data
}

export async function deactivateCategory(categoryId) {
  await client.patch(`/categories/${categoryId}/deactivate`)
}
