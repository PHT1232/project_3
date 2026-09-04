import { useState } from 'react'
import { UserPlus } from 'lucide-react'

import PageHeader from '../../components/layout/PageHeader.jsx'
import Card from '../../components/ui/Card.jsx'
import Button from '../../components/ui/Button.jsx'
import { ErrorState, EmptyState } from '../../components/ui/StateBlock.jsx'
import { SkeletonTable } from '../../components/ui/Skeleton.jsx'
import useAsync from '../../hooks/useAsync.js'
import { getUsers, createUser, updateUser, setUserStatus } from '../../api/users.js'

import { ROLES } from './roles.js'
import UserTable from './components/UserTable.jsx'
import UserFormModal from './components/UserFormModal.jsx'
import StatusConfirmModal from './components/StatusConfirmModal.jsx'
import SubordinatesModal from './components/SubordinatesModal.jsx'

const PAGE_SIZE = 20

export default function UserManagementPage() {
  const [page, setPage] = useState(1)
  const [role, setRole] = useState('')
  const [location, setLocation] = useState('')

  const [formState, setFormState] = useState({ open: false, user: null, error: null })
  const [statusUser, setStatusUser] = useState(null)
  const [subordinatesUser, setSubordinatesUser] = useState(null)

  const { data, error, loading, reload } = useAsync(
    () => getUsers({ page, pageSize: PAGE_SIZE, role, location }),
    [page, role, location],
  )

  const users = data?.items ?? []
  const totalCount = data?.totalCount ?? 0
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE))
  const hasFilters = role !== '' || location !== ''

  function openCreate() {
    setFormState({ open: true, user: null, error: null })
  }

  function openEdit(user) {
    setFormState({ open: true, user, error: null })
  }

  async function handleFormSubmit(payload) {
    try {
      if (formState.user) {
        await updateUser(formState.user.employeeNumber, payload)
      } else {
        await createUser(payload)
      }
      setFormState({ open: false, user: null, error: null })
      reload()
    } catch (err) {
      setFormState((current) => ({
        ...current,
        error: err.response?.data?.detail ?? 'Could not save this user. Please check the form and try again.',
      }))
      throw err
    }
  }

  async function handleStatusConfirm(nextActive) {
    await setUserStatus(statusUser.employeeNumber, nextActive)
    setStatusUser(null)
    reload()
  }

  return (
    <>
      <PageHeader
        title="User Management"
        description="Create employees, assign roles and superiors, and activate or deactivate accounts."
        actions={
          <Button onClick={openCreate}>
            <UserPlus className="h-4 w-4" aria-hidden="true" />
            New user
          </Button>
        }
      />

      <Card className="mb-4 flex flex-wrap items-center gap-3 px-4 py-3">
        <select
          aria-label="Filter by role"
          value={role}
          onChange={(e) => {
            setPage(1)
            setRole(e.target.value)
          }}
          className="h-10 rounded-md border border-surface-border bg-surface-card px-3 text-sm text-ink"
        >
          <option value="">All roles</option>
          {ROLES.map((r) => (
            <option key={r} value={r}>
              {r}
            </option>
          ))}
        </select>

        <input
          type="text"
          aria-label="Filter by location"
          placeholder="Filter by location…"
          value={location}
          onChange={(e) => {
            setPage(1)
            setLocation(e.target.value)
          }}
          className="h-10 rounded-md border border-surface-border bg-surface-card px-3 text-sm text-ink placeholder:text-ink-subtle"
        />

        {hasFilters && (
          <Button
            variant="ghost"
            size="sm"
            onClick={() => {
              setPage(1)
              setRole('')
              setLocation('')
            }}
          >
            Clear filters
          </Button>
        )}
      </Card>

      <Card>
        {loading && (
          <SkeletonTable
            label="Loading users…"
            rows={8}
            columns={[6, 6, 11, 8, 5, { width: 5, height: 'h-6' }, { width: 9, align: 'right', height: 'h-8' }]}
          />
        )}
        {!loading && error && <ErrorState error={error} onRetry={reload} />}
        {!loading && !error && users.length === 0 && (
          <EmptyState
            title="No users found"
            description={hasFilters ? 'Try clearing filters.' : 'Create the first user to get started.'}
          />
        )}
        {!loading && !error && users.length > 0 && (
          <>
            <UserTable
              users={users}
              onEdit={openEdit}
              onToggleStatus={setStatusUser}
              onViewSubordinates={setSubordinatesUser}
            />
            <div className="flex items-center justify-between border-t border-surface-border px-4 py-3 text-sm text-ink-muted">
              <span>
                Page {page} of {totalPages} · {totalCount} user{totalCount === 1 ? '' : 's'}
              </span>
              <div className="flex gap-2">
                <Button
                  variant="secondary"
                  size="sm"
                  disabled={page <= 1}
                  onClick={() => setPage((p) => p - 1)}
                >
                  Previous
                </Button>
                <Button
                  variant="secondary"
                  size="sm"
                  disabled={page >= totalPages}
                  onClick={() => setPage((p) => p + 1)}
                >
                  Next
                </Button>
              </div>
            </div>
          </>
        )}
      </Card>

      <UserFormModal
        open={formState.open}
        user={formState.user}
        users={users}
        error={formState.error}
        onClose={() => setFormState({ open: false, user: null, error: null })}
        onSubmit={handleFormSubmit}
      />

      <StatusConfirmModal
        open={Boolean(statusUser)}
        user={statusUser}
        onClose={() => setStatusUser(null)}
        onConfirm={handleStatusConfirm}
      />

      <SubordinatesModal
        open={Boolean(subordinatesUser)}
        user={subordinatesUser}
        onClose={() => setSubordinatesUser(null)}
      />
    </>
  )
}
