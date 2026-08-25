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
 * NOTE: no role-based filtering is applied. Capability is determined by role and rank
 * (Plan §3, §4.2), but the auth context that would expose the current user's rank is M1's
 * work (Plan T1.8) and does not exist yet. The wireframes show the full nav to every user.
 * Do not invent permission rules here — server-side authorisation is the real control
 * (Plan §2.5), and hiding a nav item is not authorisation.
 */
export const navItems = [
  { to: '/', label: 'Dashboard', icon: LayoutGrid, end: true },
  { to: '/catalogue', label: 'Catalogue', icon: BookOpen },
  { to: '/new-request', label: 'New Request', icon: PlusCircle },
  { to: '/my-requests', label: 'My Requests', icon: History },
  { to: '/approvals', label: 'Approvals', icon: ClipboardCheck },
  { to: '/reports', label: 'Reports', icon: BarChart3 },
  { to: '/inventory', label: 'Inventory', icon: Package },
  { to: '/suppliers', label: 'Suppliers', icon: Truck },
  { to: '/user-management', label: 'User Management', icon: Users },
  { to: '/help', label: 'Help', icon: HelpCircle },
]
