import Badge from '../../../components/ui/Badge.jsx'
import { INVENTORY_STATUS } from '../../../api/inventory.js'

/**
 * Status chip. The band itself is decided by the server (see the note in
 * `src/api/mock/inventory.mock.js`) — this only maps a value to its wireframe styling.
 */
const STATUS_STYLE = {
  [INVENTORY_STATUS.OK]: { tone: 'outline', label: 'OK' },
  [INVENTORY_STATUS.WATCH]: { tone: 'muted', label: 'WATCH' },
  [INVENTORY_STATUS.REORDER_NOW]: { tone: 'solid', label: 'REORDER NOW' },
}

export default function StockStatusBadge({ status }) {
  const style = STATUS_STYLE[status]
  if (!style) return <span className="text-xs text-ink-subtle">—</span>
  return <Badge tone={style.tone}>{style.label}</Badge>
}
