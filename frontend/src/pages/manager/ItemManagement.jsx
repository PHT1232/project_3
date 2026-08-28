import { useState } from 'react'
import { Pencil, Power, PackagePlus } from 'lucide-react'

import PageHeader from '../../components/layout/PageHeader.jsx'
import Card from '../../components/ui/Card.jsx'
import Button from '../../components/ui/Button.jsx'
import Badge from '../../components/ui/Badge.jsx'
import Modal from '../../components/ui/Modal.jsx'
import { LoadingState, ErrorState, EmptyState } from '../../components/ui/StateBlock.jsx'
import useAsync from '../../hooks/useAsync.js'
import { formatCurrency } from '../../lib/format.js'
import { getCategories, getItems, createItem, updateItem, deactivateItem } from '../../api/catalogue.js'
import { getSuppliers } from '../../api/suppliers.js'

const RANK_LABELS = { 1: 'Engineer', 2: 'Manager', 3: 'Business Manager', 4: 'Managing Director' }

function emptyForm(categories) {
  return {
    itemName: '',
    categoryId: String(categories[0]?.categoryId ?? ''),
    unitOfMeasure: '',
    unitCost: '',
    reorderLevel: '0',
    minRankLevelToRequest: '1',
    supplierId: '',
  }
}

function ItemFormModal({ open, onClose, onSubmit, item, categories, suppliers, error }) {
  const isEdit = Boolean(item)
  const [form, setForm] = useState(
    item
      ? {
          itemName: item.itemName,
          categoryId: String(item.categoryId),
          unitOfMeasure: item.unitOfMeasure,
          unitCost: String(item.unitCost),
          reorderLevel: String(item.reorderLevel),
          minRankLevelToRequest: String(item.minRankLevelToRequest),
          supplierId: item.supplierId ? String(item.supplierId) : '',
        }
      : emptyForm(categories),
  )
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(event) {
    event.preventDefault()
    setSubmitting(true)
    try {
      await onSubmit({
        itemName: form.itemName,
        categoryId: Number(form.categoryId),
        unitOfMeasure: form.unitOfMeasure,
        unitCost: Number(form.unitCost),
        reorderLevel: Number(form.reorderLevel),
        minRankLevelToRequest: Number(form.minRankLevelToRequest),
        supplierId: form.supplierId ? Number(form.supplierId) : null,
      })
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Modal open={open} onClose={onClose} title={isEdit ? 'Edit item' : 'New item'}>
      <form className="space-y-4" onSubmit={handleSubmit} noValidate>
        <div>
          <label htmlFor="item-name" className="block text-sm font-medium text-ink">
            Item name
          </label>
          <input
            id="item-name"
            type="text"
            required
            maxLength={200}
            value={form.itemName}
            onChange={(e) => setForm((f) => ({ ...f, itemName: e.target.value }))}
            className="mt-1 w-full rounded-md border border-surface-border bg-surface-card px-3 py-2 text-sm text-ink"
          />
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div>
            <label htmlFor="item-category" className="block text-sm font-medium text-ink">
              Category
            </label>
            <select
              id="item-category"
              value={form.categoryId}
              onChange={(e) => setForm((f) => ({ ...f, categoryId: e.target.value }))}
              className="mt-1 w-full rounded-md border border-surface-border bg-surface-card px-3 py-2 text-sm text-ink"
            >
              {categories.map((c) => (
                <option key={c.categoryId} value={c.categoryId}>
                  {c.name}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label htmlFor="item-uom" className="block text-sm font-medium text-ink">
              Unit of measure
            </label>
            <input
              id="item-uom"
              type="text"
              required
              maxLength={50}
              value={form.unitOfMeasure}
              onChange={(e) => setForm((f) => ({ ...f, unitOfMeasure: e.target.value }))}
              className="mt-1 w-full rounded-md border border-surface-border bg-surface-card px-3 py-2 text-sm text-ink"
            />
          </div>
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div>
            <label htmlFor="item-cost" className="block text-sm font-medium text-ink">
              Unit cost
            </label>
            <input
              id="item-cost"
              type="number"
              min={0}
              step="0.01"
              required
              value={form.unitCost}
              onChange={(e) => setForm((f) => ({ ...f, unitCost: e.target.value }))}
              className="mt-1 w-full rounded-md border border-surface-border bg-surface-card px-3 py-2 text-sm text-ink"
            />
          </div>
          <div>
            <label htmlFor="item-reorder" className="block text-sm font-medium text-ink">
              Reorder level
            </label>
            <input
              id="item-reorder"
              type="number"
              min={0}
              required
              value={form.reorderLevel}
              onChange={(e) => setForm((f) => ({ ...f, reorderLevel: e.target.value }))}
              className="mt-1 w-full rounded-md border border-surface-border bg-surface-card px-3 py-2 text-sm text-ink"
            />
          </div>
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div>
            <label htmlFor="item-rank" className="block text-sm font-medium text-ink">
              Minimum rank to request
            </label>
            <select
              id="item-rank"
              value={form.minRankLevelToRequest}
              onChange={(e) => setForm((f) => ({ ...f, minRankLevelToRequest: e.target.value }))}
              className="mt-1 w-full rounded-md border border-surface-border bg-surface-card px-3 py-2 text-sm text-ink"
            >
              {Object.entries(RANK_LABELS).map(([rank, label]) => (
                <option key={rank} value={rank}>
                  {label}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label htmlFor="item-supplier" className="block text-sm font-medium text-ink">
              Preferred supplier
            </label>
            <select
              id="item-supplier"
              value={form.supplierId}
              onChange={(e) => setForm((f) => ({ ...f, supplierId: e.target.value }))}
              className="mt-1 w-full rounded-md border border-surface-border bg-surface-card px-3 py-2 text-sm text-ink"
            >
              <option value="">None</option>
              {suppliers.map((s) => (
                <option key={s.supplierId} value={s.supplierId}>
                  {s.name}
                </option>
              ))}
            </select>
          </div>
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
            {submitting ? 'Saving…' : isEdit ? 'Save changes' : 'Create item'}
          </Button>
        </div>
      </form>
    </Modal>
  )
}

export default function ItemManagement() {
  const [formState, setFormState] = useState({ open: false, item: null, error: null })
  const [deactivateTarget, setDeactivateTarget] = useState(null)
  const [deactivateError, setDeactivateError] = useState(null)

  const { data, error, loading, reload } = useAsync(
    () =>
      Promise.all([getItems(), getCategories(), getSuppliers()]).then(([items, categories, suppliersPage]) => ({
        items,
        categories,
        suppliers: suppliersPage.items,
      })),
    [],
  )

  const items = data?.items ?? []
  const categories = data?.categories ?? []
  const suppliers = data?.suppliers ?? []

  function openCreate() {
    setFormState({ open: true, item: null, error: null })
  }

  function openEdit(item) {
    setFormState({ open: true, item, error: null })
  }

  async function handleFormSubmit(payload) {
    try {
      if (formState.item) {
        await updateItem(formState.item.itemId, { ...payload, rowVersion: formState.item.rowVersion })
      } else {
        await createItem(payload)
      }
      setFormState({ open: false, item: null, error: null })
      reload()
    } catch (err) {
      setFormState((current) => ({
        ...current,
        error: err.response?.data?.detail ?? 'Could not save this item.',
      }))
      throw err
    }
  }

  async function handleDeactivate() {
    setDeactivateError(null)
    try {
      await deactivateItem(deactivateTarget.itemId)
      setDeactivateTarget(null)
      reload()
    } catch (err) {
      setDeactivateError(err.response?.data?.detail ?? 'Could not deactivate this item.')
    }
  }

  return (
    <>
      <PageHeader
        title="Item Management"
        description="Create and maintain the stationery items available in the catalogue."
        actions={
          <Button onClick={openCreate} disabled={categories.length === 0}>
            <PackagePlus className="h-4 w-4" aria-hidden="true" />
            New item
          </Button>
        }
      />

      <Card>
        {loading && <LoadingState label="Loading items…" />}
        {!loading && error && <ErrorState error={error} onRetry={reload} />}
        {!loading && !error && items.length === 0 && (
          <EmptyState title="No items yet" description="Create the first item to get started." />
        )}
        {!loading && !error && items.length > 0 && (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead>
                <tr className="border-b border-surface-border text-xs uppercase tracking-wide text-ink-muted">
                  <th className="px-4 py-3 font-semibold">Item</th>
                  <th className="px-4 py-3 font-semibold">Category</th>
                  <th className="px-4 py-3 font-semibold">Unit cost</th>
                  <th className="px-4 py-3 font-semibold">Min. rank</th>
                  <th className="px-4 py-3 font-semibold">Status</th>
                  <th className="px-4 py-3 font-semibold text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-surface-border">
                {items.map((item) => (
                  <tr key={item.itemId}>
                    <td className="px-4 py-3 font-medium text-ink">{item.itemName}</td>
                    <td className="px-4 py-3 text-ink-muted">{item.categoryName}</td>
                    <td className="px-4 py-3 text-ink-muted">{formatCurrency(item.unitCost)}</td>
                    <td className="px-4 py-3 text-ink-muted">{RANK_LABELS[item.minRankLevelToRequest]}</td>
                    <td className="px-4 py-3">
                      <Badge tone={item.isActive ? 'plain' : 'danger'}>
                        {item.isActive ? 'Active' : 'Inactive'}
                      </Badge>
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex justify-end gap-1">
                        <Button variant="ghost" size="sm" onClick={() => openEdit(item)} aria-label={`Edit ${item.itemName}`}>
                          <Pencil className="h-4 w-4" aria-hidden="true" />
                        </Button>
                        {item.isActive && (
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => setDeactivateTarget(item)}
                            aria-label={`Deactivate ${item.itemName}`}
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
          </div>
        )}
      </Card>

      <ItemFormModal
        open={formState.open}
        item={formState.item}
        categories={categories}
        suppliers={suppliers}
        error={formState.error}
        onClose={() => setFormState({ open: false, item: null, error: null })}
        onSubmit={handleFormSubmit}
      />

      <Modal
        open={Boolean(deactivateTarget)}
        onClose={() => setDeactivateTarget(null)}
        title={`Deactivate ${deactivateTarget?.itemName}?`}
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
          This item will be hidden from the catalogue but preserved in historical requests.
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
