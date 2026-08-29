import {
  LayoutGrid,
  BookOpen,
  PlusCircle,
  History,
  ClipboardCheck,
  BarChart3,
  Package,
  Truck,
  Users,
  HelpCircle,
} from 'lucide-react'

/**
 * Primary navigation, in the order shown on every approved wireframe in `docs/Wireframe/`.
 *
 * SHARED FILE — adding a page means adding one entry here plus a <Route> in App.jsx.
 *
 * `minRankLevel` hides an item below that rank (Sidebar.jsx filters using AuthContext) — this
 * is UX only, never the real control (Plan §2.5); the server-side policy on the route is.
 * Reports and User Management are Manager+ (Plan §4.2, page-map §9 / §12).
 */
export const navItems = [
  { to: '/', label: 'Dashboard', icon: LayoutGrid, end: true },
  { to: '/catalogue', label: 'Catalogue', icon: BookOpen },
  { to: '/new-request', label: 'New Request', icon: PlusCircle },
  { to: '/my-requests', label: 'My Requests', icon: History },
  { to: '/approvals', label: 'Approvals', icon: ClipboardCheck },
  { to: '/reports', label: 'Reports', icon: BarChart3, minRankLevel: 2 },
  { to: '/inventory', label: 'Inventory', icon: Package },
  { to: '/suppliers', label: 'Suppliers', icon: Truck },
  { to: '/user-management', label: 'User Management', icon: Users, minRankLevel: 2 },
  { to: '/help', label: 'Help', icon: HelpCircle },
]
