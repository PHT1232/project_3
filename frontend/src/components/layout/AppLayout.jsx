import { useState } from 'react'
import { Outlet } from 'react-router-dom'
import Sidebar from './Sidebar.jsx'
import Header from './Header.jsx'

/**
 * Application shell: fixed sidebar + sticky header + routed content.
 * SHARED COMPONENT — every page inside the app renders through this Outlet.
 */
export default function AppLayout() {
  const [navOpen, setNavOpen] = useState(false)

  return (
    <div className="min-h-screen">
      <Sidebar open={navOpen} onNavigate={() => setNavOpen(false)} />

      <div className="lg:pl-64">
        <Header onMenuClick={() => setNavOpen(true)} />
        <main className="mx-auto w-full max-w-[1400px] px-4 py-6 sm:px-6">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
