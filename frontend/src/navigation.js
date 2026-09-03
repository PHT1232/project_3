import {
  LayoutGrid,
  BookOpen,

  History,
  ClipboardCheck,
  BarChart3,
  Package,
  Truck,
  Users,
  PackagePlus,
  HelpCircle,
} from 'lucide-react'

/**
 * Primary navigation, in the order shown on every approved wireframe in `docs/Wireframe/`.
 *
 * SHARED FILE — adding a page means adding one entry here plus a <Route> in App.jsx.
 *
 * `minRankLevel` hides an item below that rank (Sidebar.jsx filters using AuthContext) — this
 * is UX only, never the real control (Plan §2.5); the server-side policy on the route is.
 * Inventory, Suppliers, Item Management and User Management are Manager+ (Plan §4.2,
 * page-map.md §9–12). Reports is open to everyone — its data is row-scoped per role
 * server-side (a requestor sees only their own spend), so there's no rank floor on the tab.
 */
export const navItems = [
  { to: '/', label: 'Dashboard', icon: LayoutGrid, end: true },
  { to: '/catalogue', label: 'Catalogue', icon: BookOpen },

  { to: '/my-requests', label: 'My Requests', icon: History },
  { to: '/approvals', label: 'Approvals', icon: ClipboardCheck },
  { to: '/reports', label: 'Reports', icon: BarChart3 },
  { to: '/inventory', label: 'Inventory', icon: Package, minRankLevel: 2 },
  { to: '/suppliers', label: 'Suppliers', icon: Truck, minRankLevel: 2 },
  { to: '/catalogue/manage', label: 'Item Management', icon: PackagePlus, minRankLevel: 2 },
  { to: '/user-management', label: 'User Management', icon: Users, minRankLevel: 2 },
  { to: '/help', label: 'Help', icon: HelpCircle },
]
