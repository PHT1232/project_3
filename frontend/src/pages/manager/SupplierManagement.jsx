import { useState } from 'react'
import { Pencil, Power, Truck as TruckPlus } from 'lucide-react'

import PageHeader from '../../components/layout/PageHeader.jsx'
import Card from '../../components/ui/Card.jsx'
import Button from '../../components/ui/Button.jsx'
import Badge from '../../components/ui/Badge.jsx'
import Modal from '../../components/ui/Modal.jsx'
import { ErrorState, EmptyState } from '../../components/ui/StateBlock.jsx'
import { SkeletonTable } from '../../components/ui/Skeleton.jsx'
import useAsync from '../../hooks/useAsync.js'
import useSortableTable from '../../hooks/useSortableTable.js'
import usePagination from '../../hooks/usePagination.js'
import Pagination from '../../components/ui/Pagination.jsx'
import SortableHeader from '../../components/ui/SortableHeader.jsx'
import { getSuppliers, createSupplier, updateSupplier, deactivateSupplier } from '../../api/suppliers.js'

const EMPTY_FORM = { name: '', leadTimeDays: '' }

/** Lead time sorts numerically (the cell renders "5 days"); status sorts active first. */
const SUPPLIER_SORT_COLUMNS = {
  name: { type: 'string' },
  leadTimeDays: { type: 'number' },
  isActive: { type: 'boolean' },
}

function SupplierFormModal({ open, onClose, onSubmit, supplier, error }) {
  const isEdit = Boolean(supplier)
  const [form, setForm] = useState(
    supplier ? { name: supplier.name, leadTimeDays: String(supplier.leadTimeDays) } : EMPTY_FORM,
  )
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(event) {
    event.preventDefault()
    setSubmitting(true)
    try {
      await onSubmit({ name: form.name, leadTimeDays: Number(form.leadTimeDays) })
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Modal open={open} onClose={onClose} title={isEdit ? 'Edit supplier' : 'New supplier'}>
      <form className="space-y-4" onSubmit={handleSubmit} noValidate>
        <div>
          <label htmlFor="supplier-name" className="block text-sm font-medium text-ink">
            Name
          </label>
          <input
            id="supplier-name"
            type="text"
            required
            maxLength={200}
            value={form.name}
            onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
            className="mt-1 w-full rounded-md border border-surface-border bg-surface-card px-3 py-2 text-sm text-ink"
          />
        </div>
        <div>
          <label htmlFor="supplier-lead-time" className="block text-sm font-medium text-ink">
            Lead time (days)
          </label>
          <input
            id="supplier-lead-time"
            type="number"
            min={0}
            required
            value={form.leadTimeDays}
            onChange={(e) => setForm((f) => ({ ...f, leadTimeDays: e.target.value }))}
            className="mt-1 w-full rounded-md border border-surface-border bg-surface-card px-3 py-2 text-sm text-ink"
          />
        </div>

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
            {submitting ? 'Saving…' : isEdit ? 'Save changes' : 'Create supplier'}
          </Button>
        </div>
      </form>
    </Modal>
  )
}

export default function SupplierManagement() {
  const [formState, setFormState] = useState({ open: false, supplier: null, error: null })
  const [deactivateTarget, setDeactivateTarget] = useState(null)
  const [deactivateError, setDeactivateError] = useState(null)

  const { data, error, loading, reload } = useAsync(() => getSuppliers(), [])
  const suppliers = data?.items ?? []
  const { sortedRows: sortedSuppliers, headerProps } = useSortableTable(suppliers, SUPPLIER_SORT_COLUMNS, {
    key: 'name',
    dir: 'asc',
  })
  const { page, setPage, totalPages, total, pageRows } = usePagination(sortedSuppliers)

  function openCreate() {
    setFormState({ open: true, supplier: null, error: null })
  }

  function openEdit(supplier) {
    setFormState({ open: true, supplier, error: null })
  }

  async function handleFormSubmit(payload) {
    try {
      if (formState.supplier) {
        await updateSupplier(formState.supplier.supplierId, { ...payload, rowVersion: formState.supplier.rowVersion })
      } else {
        await createSupplier(payload)
      }
      setFormState({ open: false, supplier: null, error: null })
      reload()
    } catch (err) {
      setFormState((current) => ({
        ...current,
        error: err.response?.data?.detail ?? 'Could not save this supplier.',
      }))
      throw err
    }
  }

  async function handleDeactivate() {
    setDeactivateError(null)
    try {
      await deactivateSupplier(deactivateTarget.supplierId)
      setDeactivateTarget(null)
      reload()
    } catch (err) {
      setDeactivateError(
        err.response?.data?.detail ?? 'Could not deactivate this supplier.',
      )
    }
  }

  return (
    <>
      <PageHeader
        title="Suppliers"
        description="Manage the suppliers stationery items are sourced from."
        actions={
          <Button onClick={openCreate}>
            <TruckPlus className="h-4 w-4" aria-hidden="true" />
            New supplier
          </Button>
        }
      />

      <Card>
        {loading && (
          <SkeletonTable
            label="Loading suppliers…"
            rows={6}
            columns={[6, 2, { width: 2, height: 'h-6' }, { width: 3, align: 'right', height: 'h-8' }]}
          />
        )}
        {!loading && error && <ErrorState error={error} onRetry={reload} />}
        {!loading && !error && suppliers.length === 0 && (
          <EmptyState title="No suppliers yet" description="Create the first supplier to get started." />
        )}
        {!loading && !error && suppliers.length > 0 && (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead>
                <tr className="border-b border-surface-border text-xs uppercase tracking-wide text-ink-muted">
                  <SortableHeader {...headerProps('name')} className="font-semibold">
                    Name
                  </SortableHeader>
                  <SortableHeader {...headerProps('leadTimeDays')} className="font-semibold">
                    Lead time
                  </SortableHeader>
                  <SortableHeader {...headerProps('isActive')} className="font-semibold">
                    Status
                  </SortableHeader>
                  <th className="px-4 py-3 font-semibold text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-surface-border">
                {pageRows.map((supplier) => (
                  <tr key={supplier.supplierId}>
                    <td className="px-4 py-3 font-medium text-ink">{supplier.name}</td>
                    <td className="px-4 py-3 text-ink-muted">{supplier.leadTimeDays} days</td>
                    <td className="px-4 py-3">
                      <Badge tone={supplier.isActive ? 'plain' : 'danger'}>
                        {supplier.isActive ? 'Active' : 'Inactive'}
                      </Badge>
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex justify-end gap-1">
                        <Button variant="ghost" size="sm" onClick={() => openEdit(supplier)} aria-label={`Edit ${supplier.name}`}>
                          <Pencil className="h-4 w-4" aria-hidden="true" />
                        </Button>
                        {supplier.isActive && (
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => setDeactivateTarget(supplier)}
                            aria-label={`Deactivate ${supplier.name}`}
                          >
                            <Power className="h-4 w-4" aria-hidden="true" />
                          </Button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            <Pagination
              page={page}
              totalPages={totalPages}
              total={total}
              onPageChange={setPage}
              noun="supplier"
            />
          </div>
        )}
      </Card>

      <SupplierFormModal
        open={formState.open}
        supplier={formState.supplier}
        error={formState.error}
        onClose={() => setFormState({ open: false, supplier: null, error: null })}
        onSubmit={handleFormSubmit}
      />

      <Modal
        open={Boolean(deactivateTarget)}
        onClose={() => setDeactivateTarget(null)}
        title={`Deactivate ${deactivateTarget?.name}?`}
        footer={
          <>
            <Button variant="secondary" onClick={() => setDeactivateTarget(null)}>
              Cancel
            </Button>
            <Button onClick={handleDeactivate}>Deactivate</Button>
          </>
        }
      >
        <p className="text-sm text-ink-muted">
          This supplier will no longer be selectable for new items or stock receipts.
        </p>
        {deactivateError && (
          <p role="alert" className="mt-2 text-sm text-status-danger">
            {deactivateError}
          </p>
        )}
      </Modal>
    </>
  )
}
