import { useEffect, useState } from 'react'
import Modal from '../../../components/ui/Modal.jsx'
import Button from '../../../components/ui/Button.jsx'
import { ROLES } from '../roles.js'

const EMPTY_FORM = {
  employeeNumber: '',
  name: '',
  email: '',
  role: ROLES[0],
  superiorEmployeeNumber: '0',
  grade: '',
  location: '',
  initialPassword: '',
}

function toFormState(user) {
  if (!user) return EMPTY_FORM
  return {
    employeeNumber: String(user.employeeNumber),
    name: user.name,
    email: user.email,
    role: user.role,
    superiorEmployeeNumber: String(user.superiorEmployeeNumber ?? 0),
    grade: user.grade ?? '',
    location: user.location ?? '',
    initialPassword: '',
  }
}

/** Create/edit form. Superior is a plain <select> — the directory tops out at 1000 users. */
export default function UserFormModal({ open, onClose, onSubmit, user, users, error }) {
  const isEdit = Boolean(user)
  const [form, setForm] = useState(EMPTY_FORM)
  const [submitting, setSubmitting] = useState(false)

  useEffect(() => {
    if (open) setForm(toFormState(user))
  }, [open, user])

  function update(field, value) {
    setForm((current) => ({ ...current, [field]: value }))
  }

  async function handleSubmit(event) {
    event.preventDefault()
    setSubmitting(true)
    try {
      const payload = {
        name: form.name,
        email: form.email,
        role: form.role,
        superiorEmployeeNumber: Number(form.superiorEmployeeNumber),
        grade: form.grade || null,
        location: form.location || null,
      }
      if (isEdit) {
        await onSubmit(payload)
      } else {
        await onSubmit({
          ...payload,
          employeeNumber: Number(form.employeeNumber),
          initialPassword: form.initialPassword,
        })
      }
    } finally {
      setSubmitting(false)
    }
  }

  const candidateSuperiors = users.filter((u) => u.employeeNumber !== user?.employeeNumber)

  return (
    <Modal open={open} onClose={onClose} title={isEdit ? 'Edit user' : 'Create user'}>
      <form className="space-y-4" onSubmit={handleSubmit} noValidate>
        {!isEdit && (
          <div>
            <label htmlFor="employeeNumber" className="block text-sm font-medium text-ink">
              Employee number
            </label>
            <input
              id="employeeNumber"
              type="number"
              min="1"
              max="1000"
              required
              value={form.employeeNumber}
              onChange={(e) => update('employeeNumber', e.target.value)}
              className="mt-1 w-full rounded-md border border-surface-border bg-surface-card px-3 py-2 text-sm text-ink"
            />
          </div>
        )}

        <div>
          <label htmlFor="name" className="block text-sm font-medium text-ink">
            Name
          </label>
          <input
            id="name"
            type="text"
            required
            maxLength={15}
            value={form.name}
            onChange={(e) => update('name', e.target.value)}
            className="mt-1 w-full rounded-md border border-surface-border bg-surface-card px-3 py-2 text-sm text-ink"
          />
        </div>

        <div>
          <label htmlFor="email" className="block text-sm font-medium text-ink">
            Email
          </label>
          <input
            id="email"
            type="email"
            required
            maxLength={25}
            value={form.email}
            onChange={(e) => update('email', e.target.value)}
            className="mt-1 w-full rounded-md border border-surface-border bg-surface-card px-3 py-2 text-sm text-ink"
          />
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div>
            <label htmlFor="role" className="block text-sm font-medium text-ink">
              Role
            </label>
            <select
              id="role"
              value={form.role}
              onChange={(e) => update('role', e.target.value)}
              className="mt-1 w-full rounded-md border border-surface-border bg-surface-card px-3 py-2 text-sm text-ink"
            >
              {ROLES.map((role) => (
                <option key={role} value={role}>
                  {role}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label htmlFor="superior" className="block text-sm font-medium text-ink">
              Superior
            </label>
            <select
              id="superior"
              value={form.superiorEmployeeNumber}
              onChange={(e) => update('superiorEmployeeNumber', e.target.value)}
              className="mt-1 w-full rounded-md border border-surface-border bg-surface-card px-3 py-2 text-sm text-ink"
            >
              <option value="0">None (top of hierarchy)</option>
              {candidateSuperiors.map((u) => (
                <option key={u.employeeNumber} value={u.employeeNumber}>
                  {u.name} (#{u.employeeNumber})
                </option>
              ))}
            </select>
          </div>
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div>
            <label htmlFor="grade" className="block text-sm font-medium text-ink">
              Grade
            </label>
            <input
              id="grade"
              type="text"
              value={form.grade}
              onChange={(e) => update('grade', e.target.value)}
              className="mt-1 w-full rounded-md border border-surface-border bg-surface-card px-3 py-2 text-sm text-ink"
            />
          </div>
          <div>
            <label htmlFor="location" className="block text-sm font-medium text-ink">
              Location
            </label>
            <input
              id="location"
              type="text"
              value={form.location}
              onChange={(e) => update('location', e.target.value)}
              className="mt-1 w-full rounded-md border border-surface-border bg-surface-card px-3 py-2 text-sm text-ink"
            />
          </div>
        </div>

        {!isEdit && (
          <div>
            <label htmlFor="initialPassword" className="block text-sm font-medium text-ink">
              Initial password
            </label>
            <input
              id="initialPassword"
              type="password"
              required
              minLength={8}
              value={form.initialPassword}
              onChange={(e) => update('initialPassword', e.target.value)}
              className="mt-1 w-full rounded-md border border-surface-border bg-surface-card px-3 py-2 text-sm text-ink"
            />
            <p className="mt-1 text-xs text-ink-subtle">
              Min. 8 characters, an uppercase letter, a lowercase letter, and a digit.
            </p>
          </div>
        )}

        {error && (
          <p role="alert" className="text-sm text-status-danger">
            {error}
          </p>
        )}

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" disabled={submitting}>
            {submitting ? 'Saving…' : isEdit ? 'Save changes' : 'Create user'}
          </Button>
        </div>
      </form>
    </Modal>
  )
}
