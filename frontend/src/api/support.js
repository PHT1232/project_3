import client from './client.js'

/**
 * In-app support inbox (Help page "message the team"). Messages are stored, not emailed —
 * SMTP is on the Plan's [CUT] list. Manager+ triage them at /support-inbox.
 */

export async function sendSupportMessage({ area, subject, body, diagnostics }) {
  const { data } = await client.post('/support/messages', { area, subject, body, diagnostics })
  return data
}

export async function getSupportMessages({ status, page = 1, pageSize = 50 } = {}) {
  const { data } = await client.get('/support/messages', {
    params: { status: status || undefined, page, pageSize },
  })
  return data
}

export async function setSupportMessageResolved(id, resolved) {
  const { data } = await client.patch(`/support/messages/${id}/status`, { resolved })
  return data
}

export async function getSupportOpenCount() {
  const { data } = await client.get('/support/messages/open-count')
  return data
}
