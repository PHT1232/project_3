import { MOCK_ITEMS, MOCK_CATEGORIES } from './mock/catalogue.mock.js'

/**
 * Catalogue data access. Components import from here and never touch the data source directly.
 *
 * ---------------------------------------------------------------------------
 * EXPECTED BACKEND CONTRACT (Plan §4.2 — M2's work, not implemented yet)
 *
 *   GET /api/v1/items
 *     auth  : any authenticated user
 *     query : ?page=&pageSize=  (Plan §4.1 paging envelope)
 *     note  : the server applies the role filter — it returns only items whose
 *             MinRankLevelToRequest <= the caller's RankLevel ([ASK] #3 default), and only
 *             items where IsActive = true.
 *     200   : { items: ItemDto[], page, pageSize, totalCount }
 *
 *   GET /api/v1/categories
 *     auth  : any authenticated user
 *     200   : CategoryDto[]
 *
 *   ItemDto     { itemId, itemName, categoryId, categoryName, unitOfMeasure,
 *                 unitCost, quantityAvailable, reorderLevel, minRankLevelToRequest }
 *   CategoryDto { categoryId, name }
 *
 * TO GO LIVE: replace each function body with the `client` call shown in the comment and
 * delete `./mock/catalogue.mock.js`. No component changes are required.
 * ---------------------------------------------------------------------------
 */

// import client from './client.js'

export async function getCategories() {
  // return (await client.get('/categories')).data
  return MOCK_CATEGORIES
}

export async function getItems() {
  // return (await client.get('/items')).data.items
  return MOCK_ITEMS
}
