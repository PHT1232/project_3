import client from './client.js'

/**
 * AI feature API client (Plan §4.2, §5.2 — A1 Request Assistant).
 * Backed by WebApi/Controllers/AiController.cs.
 */

/**
 * Turn free text into a validated draft request. The draft is never persisted — the user
 * reviews it on the New Request page and submits through the normal createRequest() path.
 *
 * @param {string} text
 * @returns {Promise<{
 *   items: Array<{ itemId, itemName, categoryName, supplierName, unitOfMeasure, unitCost, quantity, quantityAvailable }>,
 *   requiredByDate: string | null,
 *   note: string | null,
 *   totalEstimatedCost: number,
 *   warnings: string[],
 *   wasFallback: boolean,
 *   model: string,
 * }>}
 */
export async function draftRequestFromText(text) {
  return (await client.post('/ai/request-assistant', { text })).data
}

/**
 * Manager+ only: paged AI interaction log (Plan T5.6 [RUBRIC]).
 */
export async function getAiUsageReport({ page = 1, pageSize = 20 } = {}) {
  return (await client.get('/ai/usage-report', { params: { page, pageSize } })).data
}
