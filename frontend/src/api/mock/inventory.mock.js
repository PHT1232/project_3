/**
 * ============================ TEMPORARY MOCK DATA ============================
 * Stand-in for `GET /api/v1/inventory` (Plan §4.2 — "Stock levels + reorder flags"),
 * which is M3's backend work and does not exist yet.
 *
 * DELETE THIS FILE once the endpoint is live. Only `src/api/inventory.js` imports it.
 *
 * Rows and figures are taken from the approved Inventory wireframe.
 *
 * TWO FIELDS TO BE AWARE OF:
 *
 * 1. `status` — OK / WATCH / REORDER_NOW is carried as DATA, not computed on the client.
 *    The Plan defines these bands in §5.3, but its formula needs 60 days of `Issue` ledger
 *    history (average daily consumption, lead-time demand, safety stock) that this page does
 *    not load. `GET /api/v1/inventory` is specified as returning "reorder flags", so the
 *    server owns this. Do not reverse-engineer a threshold on the client.
 *
 * 2. `sku` — appears as a column and a search field on the approved wireframe, but there is
 *    no SKU column in the ERD and the Plan lists "barcode/SKU" under M2 *future improvements*
 *    (tracked as K5 in CLAUDE.md §6). Display-only here; no logic depends on it.
 * ============================================================================
 */

export const INVENTORY_STATUS = {
  OK: 'OK',
  WATCH: 'WATCH',
  REORDER_NOW: 'REORDER_NOW',
}

export const MOCK_INVENTORY = [
  {
    itemId: 7,
    itemName: 'Standard A4 Copy Paper, 500 Sheets',
    sku: 'PAP-A4-500',
    quantityAvailable: 450,
    reorderLevel: 100,
    unitCost: 6.4,
    status: INVENTORY_STATUS.OK,
  },
  {
    itemId: 9,
    itemName: 'Blue Ballpoint Pens, Box of 50',
    sku: 'PEN-BL-50',
    quantityAvailable: 25,
    reorderLevel: 30,
    unitCost: 11.75,
    status: INVENTORY_STATUS.WATCH,
  },
  {
    itemId: 14,
    itemName: 'Ergonomic Mouse Pads, Black',
    sku: 'ACC-MP-BLK',
    quantityAvailable: 120,
    reorderLevel: 50,
    unitCost: 7.5,
    status: INVENTORY_STATUS.OK,
  },
  {
    itemId: 15,
    itemName: 'Laser Printer Toner Cartridge, Black (High Yield)',
    sku: 'TON-BLK-HY',
    quantityAvailable: 2,
    reorderLevel: 10,
    unitCost: 98.0,
    status: INVENTORY_STATUS.REORDER_NOW,
  },
  {
    itemId: 8,
    itemName: 'Spiral Bound Notebooks, A5, Ruled',
    sku: 'NBK-A5-RUL',
    quantityAvailable: 85,
    reorderLevel: 40,
    unitCost: 3.2,
    status: INVENTORY_STATUS.OK,
  },
  {
    itemId: 10,
    itemName: 'Highlighters, Assorted Colors, Pack of 4',
    sku: 'HLT-AST-4',
    quantityAvailable: 210,
    reorderLevel: 50,
    unitCost: 4.8,
    status: INVENTORY_STATUS.OK,
  },
  {
    itemId: 11,
    itemName: 'Whiteboard Markers, Black, Box of 12',
    sku: 'MRK-WB-BLK',
    quantityAvailable: 18,
    reorderLevel: 20,
    unitCost: 9.9,
    status: INVENTORY_STATUS.WATCH,
  },
]

/**
 * Summary tiles. Carried as data for the same reason as `status`: "Total Items" on the
 * wireframe reads 1,248 against 7 visible rows, so it is an organisation-wide figure the
 * server computes, not a count of the current page.
 */
export const MOCK_INVENTORY_SUMMARY = {
  totalItems: 1248,
  lowStockAlerts: 12,
  totalValue: 14520,
}
