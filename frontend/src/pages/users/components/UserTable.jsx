import { Pencil, Power, Users as UsersIcon } from 'lucide-react'
import Badge from '../../../components/ui/Badge.jsx'
import Button from '../../../components/ui/Button.jsx'

export default function UserTable({ users, onEdit, onToggleStatus, onViewSubordinates }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-left text-sm">
        <thead>
          <tr className="border-b border-surface-border text-xs uppercase tracking-wide text-ink-muted">
            <th className="px-4 py-3 font-semibold">Employee #</th>
            <th className="px-4 py-3 font-semibold">Name</th>
            <th className="px-4 py-3 font-semibold">Email</th>
            <th className="px-4 py-3 font-semibold">Role</th>
            <th className="px-4 py-3 font-semibold">Superior</th>
            <th className="px-4 py-3 font-semibold">Status</th>
            <th className="px-4 py-3 font-semibold text-right">Actions</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-surface-border">
          {users.map((user) => (
            <tr key={user.employeeNumber}>
              <td className="px-4 py-3 font-mono text-xs text-ink-muted">{user.employeeNumber}</td>
              <td className="px-4 py-3 font-medium text-ink">{user.name}</td>
              <td className="px-4 py-3 text-ink-muted">{user.email}</td>
              <td className="px-4 py-3 text-ink-muted">{user.role}</td>
              <td className="px-4 py-3 text-ink-muted">
                {user.superiorEmployeeNumber ? `#${user.superiorEmployeeNumber}` : '—'}
              </td>
              <td className="px-4 py-3">
                <Badge tone={user.isActive ? 'plain' : 'danger'}>
                  {user.isActive ? 'Active' : 'Inactive'}
                </Badge>
              </td>
              <td className="px-4 py-3">
                <div className="flex justify-end gap-1">
                  <Button
                    variant="ghost"
                    size="sm"
                    aria-label={`View direct reports for ${user.name}`}
                    onClick={() => onViewSubordinates(user)}
                  >
                    <UsersIcon className="h-4 w-4" aria-hidden="true" />
                  </Button>
                  <Button
                    variant="ghost"
                    size="sm"
                    aria-label={`Edit ${user.name}`}
                    onClick={() => onEdit(user)}
                  >
                    <Pencil className="h-4 w-4" aria-hidden="true" />
                  </Button>
                  <Button
                    variant="ghost"
                    size="sm"
                    aria-label={`${user.isActive ? 'Deactivate' : 'Activate'} ${user.name}`}
                    onClick={() => onToggleStatus(user)}
                  >
                    <Power className="h-4 w-4" aria-hidden="true" />
                  </Button>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
