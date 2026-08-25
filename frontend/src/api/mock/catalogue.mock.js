/**
 * ============================ TEMPORARY MOCK DATA ============================
 * Stand-in for `GET /api/v1/items` and `GET /api/v1/categories` (Plan §4.2), which are
 * M2's backend work and do not exist yet.
 *
 * DELETE THIS FILE once the endpoints are live. Nothing outside `src/api/catalogue.js`
 * imports it, so removing it is a one-file change.
 *
 * Field names mirror the approved ERD (`docs/Diagrams/ERD_project.png`) so the shape does not
 * change when the API lands. Item names and values are taken from the approved Catalogue
 * wireframe so the page renders as designed.
 * ============================================================================
 */

export const MOCK_CATEGORIES = [
  { categoryId: 1, name: 'Tech & Accessories' },
  { categoryId: 2, name: 'Paper & Notebooks' },
  { categoryId: 3, name: 'Writing Instruments' },
  { categoryId: 4, name: 'Organization' },
]

export const MOCK_ITEMS = [
  {
    itemId: 1,
    itemName: 'Ergonomic Wireless Mouse',
    categoryId: 1,
    categoryName: 'Tech & Accessories',
    unitOfMeasure: 'EA',
    unitCost: 24.99,
    quantityAvailable: 124,
    reorderLevel: 20,
    minRankLevelToRequest: 1,
    // UNSPECIFIED: the "MGR APPROVAL REQ" badge appears on the approved wireframe but has no
    // entity, endpoint or rule behind it anywhere in the Plan or ERD (tracked as K5 in
    // CLAUDE.md §6). Carried as a plain data flag so the UI can render the wireframe without
    // inventing a client-side rule for it. Confirm with the team before relying on it.
    requiresManagerApproval: false,
  },
  {
    itemId: 2,
    itemName: 'Mechanical Keyboard',
    categoryId: 1,
    categoryName: 'Tech & Accessories',
    unitOfMeasure: 'EA',
    unitCost: 89.0,
    quantityAvailable: 45,
    reorderLevel: 15,
    minRankLevelToRequest: 1,
    requiresManagerApproval: false,
  },
  {
    itemId: 3,
    itemName: 'USB-C Docking Station',
    categoryId: 1,
    categoryName: 'Tech & Accessories',
    unitOfMeasure: 'EA',
    unitCost: 145.5,
    quantityAvailable: 3,
    reorderLevel: 10,
    minRankLevelToRequest: 2,
    requiresManagerApproval: false,
  },
  {
    itemId: 4,
    itemName: 'Noise Cancelling Headset',
    categoryId: 1,
    categoryName: 'Tech & Accessories',
    unitOfMeasure: 'EA',
    unitCost: 65.0,
    quantityAvailable: 0,
    reorderLevel: 10,
    minRankLevelToRequest: 1,
    requiresManagerApproval: false,
  },
  {
    itemId: 5,
    itemName: '27-inch 4K Monitor',
    categoryId: 1,
    categoryName: 'Tech & Accessories',
    unitOfMeasure: 'EA',
    unitCost: 320.0,
    quantityAvailable: 12,
    reorderLevel: 5,
    minRankLevelToRequest: 3,
    requiresManagerApproval: true,
  },
  {
    itemId: 6,
    itemName: 'USB Flash Drive 64GB',
    categoryId: 1,
    categoryName: 'Tech & Accessories',
    unitOfMeasure: 'EA',
    unitCost: 12.5,
    quantityAvailable: 240,
    reorderLevel: 40,
    minRankLevelToRequest: 1,
    requiresManagerApproval: false,
  },
  {
    itemId: 7,
    itemName: 'Standard A4 Copy Paper, 500 Sheets',
    categoryId: 2,
    categoryName: 'Paper & Notebooks',
    unitOfMeasure: 'REAM',
    unitCost: 6.4,
    quantityAvailable: 450,
    reorderLevel: 100,
    minRankLevelToRequest: 1,
    requiresManagerApproval: false,
  },
  {
    itemId: 8,
    itemName: 'Spiral Bound Notebooks, A5, Ruled',
    categoryId: 2,
    categoryName: 'Paper & Notebooks',
    unitOfMeasure: 'EA',
    unitCost: 3.2,
    quantityAvailable: 85,
    reorderLevel: 40,
    minRankLevelToRequest: 1,
    requiresManagerApproval: false,
  },
  {
    itemId: 9,
    itemName: 'Blue Ballpoint Pens, Box of 50',
    categoryId: 3,
    categoryName: 'Writing Instruments',
    unitOfMeasure: 'BOX',
    unitCost: 11.75,
    quantityAvailable: 25,
    reorderLevel: 30,
    minRankLevelToRequest: 1,
    requiresManagerApproval: false,
  },
  {
    itemId: 10,
    itemName: 'Highlighters, Assorted Colors, Pack of 4',
    categoryId: 3,
    categoryName: 'Writing Instruments',
    unitOfMeasure: 'PACK',
    unitCost: 4.8,
    quantityAvailable: 210,
    reorderLevel: 50,
    minRankLevelToRequest: 1,
    requiresManagerApproval: false,
  },
  {
    itemId: 11,
    itemName: 'Whiteboard Markers, Black, Box of 12',
    categoryId: 3,
    categoryName: 'Writing Instruments',
    unitOfMeasure: 'BOX',
    unitCost: 9.9,
    quantityAvailable: 18,
    reorderLevel: 20,
    minRankLevelToRequest: 1,
    requiresManagerApproval: false,
  },
  {
    itemId: 12,
    itemName: 'Lever Arch Files, A4, Pack of 5',
    categoryId: 4,
    categoryName: 'Organization',
    unitOfMeasure: 'PACK',
    unitCost: 14.25,
    quantityAvailable: 60,
    reorderLevel: 25,
    minRankLevelToRequest: 1,
    requiresManagerApproval: false,
  },
  {
    itemId: 13,
    itemName: 'Desk Organizer Tray, Mesh',
    categoryId: 4,
    categoryName: 'Organization',
    unitOfMeasure: 'EA',
    unitCost: 18.0,
    quantityAvailable: 32,
    reorderLevel: 15,
    minRankLevelToRequest: 1,
    requiresManagerApproval: false,
  },
]
