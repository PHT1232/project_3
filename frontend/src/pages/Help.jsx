import PageHeader from '../components/layout/PageHeader.jsx'
import FaqList from './help/components/FaqList.jsx'
import ContactCard from './help/components/ContactCard.jsx'
import SystemInfoCard from './help/components/SystemInfoCard.jsx'

/**
 * Help & support (`/help`, Plan T6.1). Static Q&A covering every feature, plus a mailto
 * hand-off to the shared support inbox and a read-only build/session panel for bug reports.
 */
export default function Help() {
  return (
    <>
      <PageHeader
        title="Help & support"
        description="Answers to common questions, and how to reach the team."
      />

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        <div className="lg:col-span-2">
          <FaqList />
        </div>
        <div className="space-y-6">
          <ContactCard />
          <SystemInfoCard />
        </div>
      </div>
    </>
  )
}
