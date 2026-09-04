import { describe, it, expect } from 'vitest'
import { navItems } from './navigation.js'

/**
 * Navigation is the only way most pages are discoverable, so a missing or mis-ranked entry is a
 * feature nobody can reach rather than a cosmetic slip. Support Inbox was routed, built and
 * tested but absent from this list entirely, which made every message sent from the Help page
 * invisible in the UI (audit finding M2).
 *
 * The Sidebar filter these mirror is:
 *   navItems.filter(item => !item.minRankLevel || (user?.rankLevel ?? 0) >= item.minRankLevel)
 */
const RANK = { ENGINEER: 1, MANAGER: 2, BUSINESS_MANAGER: 3, MANAGING_DIRECTOR: 4 }

function visibleTo(rankLevel) {
  return navItems
    .filter((item) => !item.minRankLevel || rankLevel >= item.minRankLevel)
    .map((item) => item.to)
}

describe('navigation', () => {
  it('gives every entry a destination, label and icon', () => {
    for (const item of navItems) {
      expect(item.to, `entry ${JSON.stringify(item)} needs a "to"`).toMatch(/^\//)
      expect(item.label, `${item.to} needs a label`).toBeTruthy()
      expect(item.icon, `${item.to} needs an icon`).toBeTruthy()
    }
  })

  it('has no duplicate destinations', () => {
    const destinations = navItems.map((item) => item.to)
    expect(new Set(destinations).size).toBe(destinations.length)
  })

  describe('Support Inbox', () => {
    const entry = navItems.find((item) => item.to === '/support-inbox')

    it('is present in the navigation', () => {
      expect(entry).toBeDefined()
      expect(entry.label).toBe('Support Inbox')
    })

    // "Manager account interface only" — matches SupportController's RequireManager policy
    // (rank >= 2) and the /support-inbox route guard in App.jsx. All three must agree.
    it('is Manager+ only', () => {
      expect(entry.minRankLevel).toBe(RANK.MANAGER)
    })

    it('is hidden from an Engineer', () => {
      expect(visibleTo(RANK.ENGINEER)).not.toContain('/support-inbox')
    })

    it('is shown to a Manager, Business Manager and Managing Director', () => {
      expect(visibleTo(RANK.MANAGER)).toContain('/support-inbox')
      expect(visibleTo(RANK.BUSINESS_MANAGER)).toContain('/support-inbox')
      expect(visibleTo(RANK.MANAGING_DIRECTOR)).toContain('/support-inbox')
    })
  })

  it('keeps the existing rank floors intact', () => {
    // Regression guard for the neighbours touched while adding Support Inbox.
    const engineer = visibleTo(RANK.ENGINEER)
    expect(engineer).toContain('/')
    expect(engineer).toContain('/catalogue')
    expect(engineer).toContain('/my-requests')
    expect(engineer).toContain('/help')
    expect(engineer).not.toContain('/inventory')
    expect(engineer).not.toContain('/suppliers')

    const manager = visibleTo(RANK.MANAGER)
    expect(manager).toContain('/inventory')
    expect(manager).toContain('/suppliers')
    expect(manager).not.toContain('/user-management', 'user management is Business Manager+')
    expect(manager).not.toContain('/catalogue/manage', 'item management is Business Manager+')

    expect(visibleTo(RANK.BUSINESS_MANAGER)).toContain('/user-management')
  })
})
