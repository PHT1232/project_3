import { useState, useMemo, useEffect } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { Plus, Trash2, Send, Save, AlertCircle, ShoppingCart, Calendar, CheckCircle2 } from 'lucide-react'

import PageHeader from '../../components/layout/PageHeader.jsx'
import Card from '../../components/ui/Card.jsx'
import Button from '../../components/ui/Button.jsx'
import SearchInput from '../../components/ui/SearchInput.jsx'
import { LoadingState, ErrorState } from '../../components/ui/StateBlock.jsx'
import { useAuth } from '../../contexts/AuthContext.jsx'
import useAsync from '../../hooks/useAsync.js'
import { getItems } from '../../api/catalogue.js'
import { createRequest, submitRequest } from '../../api/requests.js'
import { formatCurrency } from '../../lib/format.js'

/**
 * Maps a catalogue item onto a requisition line. Shared by the item picker below and by items
 * handed over from the Catalogue page, so both routes produce identical line objects.
 */
function toRequisitionLine(item) {
  return {
    itemId: item.itemId,
    itemName: item.itemName,
    categoryName: item.categoryName,
    supplierName: item.supplierName,
    unitCost: item.unitCost,
    quantity: 1,
    maxStock: item.quantityAvailable,
  }
}

export default function NewRequestPage() {
  const navigate = useNavigate()
  const location = useLocation()
  const { user } = useAuth()

  // Catalogue items for picker
  const { data: catalogueItems, error: loadError, loading: itemsLoading, reload } = useAsync(
    // getItems() already unwraps the paged envelope and returns the item array (see
    // api/catalogue.js). Reading `.items` off that array yielded undefined, so the picker fell
    // back to [] and always showed "0 available" — no request could be built at all.
    async () => (await getItems()) ?? [],
    [],
  )

  // Form state
  const [requiredByDate, setRequiredByDate] = useState('')
  // Seeded once from whatever the Catalogue's "Proceed" handed over (router state); empty when
  // the page is opened directly, so the picker below stays the other way in.
  const [selectedItems, setSelectedItems] = useState(() =>
    (location.state?.items ?? []).map(toRequisitionLine),
  )
  const [pickerItemIds, setPickerItemIds] = useState([])
  const [itemSearch, setItemSearch] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [submitMode, setSubmitMode] = useState(null) // 'draft' | 'submit'
  const [errorMessage, setErrorMessage] = useState(null)
  const [successMessage, setSuccessMessage] = useState(null)

  // Filter available items for dropdown (exclude already added items)
  const availableItems = useMemo(() => {
    if (!catalogueItems) return []
    const addedIds = new Set(selectedItems.map((i) => i.itemId))
    return catalogueItems.filter((i) => !addedIds.has(i.itemId))
  }, [catalogueItems, selectedItems])

  const filteredItems = useMemo(() => {
    if (!itemSearch.trim()) return availableItems
    const search = itemSearch.toLowerCase()
    return availableItems.filter(
      (item) =>
        item.itemName.toLowerCase().includes(search) ||
        (item.categoryName && item.categoryName.toLowerCase().includes(search)),
    )
  }, [availableItems, itemSearch])

  // A requestor at the top of the hierarchy has no superior, so there is nobody who could approve
  // the request and the server rejects it (Plan §14 [ASK] #11 — "Can the MD (no superior) raise a
  // request at all?", default: no). Surfaced up front rather than letting the user fill in the
  // whole form and only then hit a validation error on submit.
  const canRaiseRequest = user?.superiorEmployeeNumber != null

  // Computed summary
  const totalEstimatedCost = useMemo(() => {
    return selectedItems.reduce((sum, item) => sum + (item.quantity || 0) * (item.unitCost || 0), 0)
  }, [selectedItems])

  const totalQuantity = useMemo(() => {
    return selectedItems.reduce((sum, item) => sum + (Number(item.quantity) || 0), 0)
  }, [selectedItems])

  function handlePickerItemToggle(itemId) {
    setPickerItemIds((previousIds) =>
      previousIds.includes(itemId)
        ? previousIds.filter((id) => id !== itemId)
        : [...previousIds, itemId],
    )
  }

  function handleAddItems() {
    if (pickerItemIds.length === 0) return

    const itemsToAdd = catalogueItems.filter((item) => pickerItemIds.includes(item.itemId))
    setSelectedItems((previousItems) => [
      ...previousItems,
      ...itemsToAdd.map(toRequisitionLine),
    ])
    setPickerItemIds([])
  }

  function handleQuantityChange(itemId, qtyStr) {
    const qty = parseInt(qtyStr, 10)
    setSelectedItems((prev) =>
      prev.map((item) => {
        if (item.itemId === itemId) {
          return { ...item, quantity: isNaN(qty) ? '' : Math.max(1, Math.min(9999, qty)) }
        }
        return item
      }),
    )
  }

  function handleRemoveItem(itemId) {
    setSelectedItems((prev) => prev.filter((item) => item.itemId !== itemId))
  }

  function handleClearAll() {
    setSelectedItems([])
    setRequiredByDate('')
    setErrorMessage(null)
  }

  async function handleSubmit(asDraft = false) {
    if (selectedItems.length === 0) {
      setErrorMessage('Please add at least one item to your request.')
      return
    }

    const invalidQty = selectedItems.find((i) => !i.quantity || i.quantity <= 0)
    if (invalidQty) {
      setErrorMessage(`Please enter a valid quantity (> 0) for ${invalidQty.itemName}.`)
      return
    }

    setSubmitting(true)
    setSubmitMode(asDraft ? 'draft' : 'submit')
    setErrorMessage(null)
    setSuccessMessage(null)

    try {
      const payload = {
        items: selectedItems.map((i) => ({
          itemId: i.itemId,
          quantity: Number(i.quantity),
        })),
        requiredByDate: requiredByDate ? new Date(requiredByDate).toISOString() : null,
      }

      const created = await createRequest(payload)

      if (!asDraft) {
        // Immediately submit
        await submitRequest(created.requestId, created.rowVersion)
        setSuccessMessage(`Request #${created.requestId} submitted for approval successfully!`)
      } else {
        setSuccessMessage(`Request #${created.requestId} saved as draft (Pending).`)
      }

      // Redirect to my requests after short delay
      setTimeout(() => {
        navigate('/my-requests')
      }, 1200)
    } catch (err) {
      const problem = err.response?.data
      // `errors` first: on a validation failure the API also sends `detail`, but that is
      // FluentValidation's raw dump ("Validation failed: \r\n -- requestorEmployeeNumber: …
      // Severity: Error"), which leaked internal field names and severity text into the UI.
      // `errors` holds the same messages already formatted for a human.
      const message =
        (problem?.errors ? Object.values(problem.errors).flat().join(', ') : null) ||
        problem?.detail ||
        problem?.title ||
        problem?.error ||
        err.message ||
        'Failed to save stationery request.'
      setErrorMessage(message)
    } finally {
      setSubmitting(false)
      setSubmitMode(null)
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="New Stationery Request"
        description="Select items from the stationery catalogue and submit for approval."
      />

      {!canRaiseRequest && (
        <div className="flex items-start gap-3 rounded-lg border border-surface-border bg-surface-muted p-4 text-sm text-ink">
          <AlertCircle className="mt-0.5 h-5 w-5 shrink-0 text-ink-muted" aria-hidden="true" />
          <div className="flex-1">
            <p className="font-medium">You cannot raise a stationery request</p>
            <p className="mt-1 text-ink-muted">
              Requests are approved by your superior, and your account is at the top of the
              reporting hierarchy, so there is nobody to approve one. Ask an employee who reports
              to you to raise it instead.
            </p>
          </div>
        </div>
      )}

      {/* Messages */}
      {errorMessage && (
        <div className="flex items-start gap-3 rounded-lg border border-status-dangerBorder bg-status-dangerBg p-4 text-sm text-status-danger">
          <AlertCircle className="mt-0.5 h-5 w-5 shrink-0" aria-hidden="true" />
          <div className="flex-1 font-medium">{errorMessage}</div>
        </div>
      )}

      {successMessage && (
        <div className="flex items-start gap-3 rounded-lg border border-status-successBorder bg-status-successBg p-4 text-sm text-status-success">
          <CheckCircle2 className="mt-0.5 h-5 w-5 shrink-0" aria-hidden="true" />
          <div className="flex-1 font-medium">{successMessage}</div>
        </div>
      )}

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        {/* Left 2 Cols: Form details & Item selection */}
        <div className="space-y-6 lg:col-span-2">
          {/* Metadata Card */}
          <Card className="p-5">
            <h3 className="text-base font-semibold text-ink">Request Details</h3>
            <div className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div>
                <label className="block text-xs font-semibold uppercase tracking-wider text-ink-muted">
                  Requestor
                </label>
                <div className="mt-1 text-sm font-medium text-ink">
                  {user?.name ?? 'Current User'} {user?.employeeNumber ? `(#${user.employeeNumber})` : ''}
                </div>
              </div>

              <div>
                <label htmlFor="requiredByDate" className="block text-xs font-semibold uppercase tracking-wider text-ink-muted">
                  Required By Date (Optional)
                </label>
                <div className="mt-1 flex items-center">
                  <input
                    type="date"
                    id="requiredByDate"
                    value={requiredByDate}
                    min={new Date().toISOString().split('T')[0]}
                    onChange={(e) => setRequiredByDate(e.target.value)}
                    className="h-9 w-full rounded-md border border-surface-border bg-surface-card px-3 text-sm text-ink focus:border-brand-500 focus:outline-none"
                  />
                </div>
              </div>
            </div>
          </Card>

          {/* Item Selector Card */}
          <Card className="p-5">
            <h3 className="text-base font-semibold text-ink">Add Items from Catalogue</h3>
            <p className="mt-1 text-sm text-ink-muted">
              Choose stationery items to add to your requisition list.
            </p>

            {itemsLoading ? (
              <div className="py-4">
                <LoadingState label="Loading catalogue items…" />
              </div>
            ) : loadError ? (
              <div className="py-4">
                <ErrorState error={loadError} onRetry={reload} />
              </div>
            ) : (
              <div className="mt-4 space-y-3">
                <SearchInput
                  value={itemSearch}
                  onChange={setItemSearch}
                  placeholder="Search by item or category..."
                  label="Search catalogue items"
                />

                {filteredItems.length === 0 ? (
                  <p className="text-sm text-ink-muted">
                    {itemSearch.trim()
                      ? `No catalogue items match “${itemSearch}”. Try a different item or category.`
                      : 'All available catalogue items have been added to this request.'}
                  </p>
                ) : (
                  <div className="overflow-x-auto rounded-md border border-surface-border">
                    <table className="w-full text-left text-sm">
                      <thead>
                        <tr className="border-b border-surface-border bg-surface-muted text-xs uppercase tracking-wider text-ink-muted">
                          <th className="w-12 px-3 py-2.5 text-center">
                            <span className="sr-only">Select</span>
                          </th>
                          <th className="px-3 py-2.5 font-semibold">Item</th>
                          <th className="px-3 py-2.5 font-semibold">Category</th>
                          <th className="px-3 py-2.5 text-right font-semibold">Unit Price</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-surface-border">
                        {filteredItems.map((item) => (
                          <tr key={item.itemId}>
                            <td className="px-3 py-3 text-center">
                              <input
                                type="checkbox"
                                aria-label={`Select ${item.itemName}`}
                                checked={pickerItemIds.includes(item.itemId)}
                                onChange={() => handlePickerItemToggle(item.itemId)}
                                className="h-4 w-4 rounded border-surface-border text-brand-600 focus:ring-brand-500"
                              />
                            </td>
                            <td className="px-3 py-3 font-medium text-ink">{item.itemName}</td>
                            <td className="px-3 py-3 text-ink-muted">{item.categoryName ?? 'General'}</td>
                            <td className="px-3 py-3 text-right font-mono text-ink-muted">
                              {formatCurrency(item.unitCost)}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}

                <Button
                  type="button"
                  variant="primary"
                  disabled={pickerItemIds.length === 0}
                  onClick={handleAddItems}
                >
                  <Plus className="h-4 w-4" aria-hidden="true" />
                  Add selected items{pickerItemIds.length > 0 ? ` (${pickerItemIds.length})` : ''}
                </Button>
              </div>
            )}
          </Card>

          {/* Selected Items Table Card */}
          <Card className="p-5">
            <div className="flex items-center justify-between">
              <h3 className="text-base font-semibold text-ink">
                Requisition Items ({selectedItems.length})
              </h3>
              {selectedItems.length > 0 && (
                <button
                  type="button"
                  onClick={handleClearAll}
                  className="text-xs font-semibold text-status-danger hover:underline"
                >
                  Clear all
                </button>
              )}
            </div>

            {selectedItems.length === 0 ? (
              <div className="mt-4 rounded-lg border-2 border-dashed border-surface-border p-8 text-center">
                <ShoppingCart className="mx-auto h-8 w-8 text-ink-muted" aria-hidden="true" />
                <p className="mt-2 text-sm font-medium text-ink">No items in your request</p>
                <p className="mt-1 text-xs text-ink-muted">
                  Select one or more stationery items from the catalogue table above.
                </p>
              </div>
            ) : (
              <div className="mt-4 overflow-x-auto rounded-md border border-surface-border">
                <table className="w-full text-left text-sm">
                  <thead>
                    <tr className="border-b border-surface-border bg-surface-muted text-xs uppercase tracking-wider text-ink-muted">
                      <th className="px-3 py-2.5 font-semibold">Item</th>
                      <th className="px-3 py-2.5 font-semibold">Unit Price</th>
                      <th className="px-3 py-2.5 font-semibold text-center w-28">Quantity</th>
                      <th className="px-3 py-2.5 text-right font-semibold">Est. Total</th>
                      <th className="px-3 py-2.5 text-center w-12" aria-label="Actions"></th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-surface-border">
                    {selectedItems.map((item) => (
                      <tr key={item.itemId}>
                        <td className="px-3 py-3">
                          <p className="font-medium text-ink">{item.itemName}</p>
                          <p className="text-xs text-ink-muted">
                            {item.categoryName ?? 'General'} · {item.supplierName ?? 'Preferred Supplier'}
                          </p>
                        </td>
                        <td className="px-3 py-3 font-mono text-sm text-ink-muted">
                          {formatCurrency(item.unitCost)}
                        </td>
                        <td className="px-3 py-3 text-center">
                          <input
                            type="number"
                            min="1"
                            max="9999"
                            aria-label={`Quantity for ${item.itemName}`}
                            value={item.quantity}
                            onChange={(e) => handleQuantityChange(item.itemId, e.target.value)}
                            className="h-8 w-20 rounded border border-surface-border bg-surface-card px-2 text-center text-sm font-medium text-ink focus:border-brand-500 focus:outline-none"
                          />
                        </td>
                        <td className="px-3 py-3 text-right font-mono font-medium text-ink">
                          {formatCurrency((item.quantity || 0) * (item.unitCost || 0))}
                        </td>
                        <td className="px-3 py-3 text-center">
                          <button
                            type="button"
                            aria-label={`Remove ${item.itemName}`}
                            onClick={() => handleRemoveItem(item.itemId)}
                            className="rounded p-1 text-ink-muted hover:bg-surface-muted hover:text-status-danger"
                          >
                            <Trash2 className="h-4 w-4" aria-hidden="true" />
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </Card>
        </div>

        {/* Right 1 Col: Summary & Actions */}
        <div className="space-y-6">
          <Card className="p-5">
            <h3 className="text-base font-semibold text-ink">Summary</h3>

            <dl className="mt-4 divide-y divide-surface-border text-sm">
              <div className="flex justify-between py-2.5">
                <dt className="text-ink-muted">Total distinct items</dt>
                <dd className="font-medium text-ink">{selectedItems.length}</dd>
              </div>
              <div className="flex justify-between py-2.5">
                <dt className="text-ink-muted">Total unit quantity</dt>
                <dd className="font-medium text-ink">{totalQuantity}</dd>
              </div>
              <div className="flex justify-between py-2.5">
                <dt className="text-ink-muted">Delivery deadline</dt>
                <dd className="font-medium text-ink">
                  {requiredByDate ? new Date(requiredByDate).toLocaleDateString() : 'Not specified'}
                </dd>
              </div>
              <div className="flex justify-between py-3 font-semibold">
                <dt className="text-base text-ink">Est. Total Cost</dt>
                <dd className="font-mono text-lg text-brand-700">
                  {formatCurrency(totalEstimatedCost)}
                </dd>
              </div>
            </dl>

            <div className="mt-6 space-y-3">
              <Button
                type="button"
                variant="primary"
                className="w-full justify-center"
                disabled={!canRaiseRequest || selectedItems.length === 0 || submitting}
                onClick={() => handleSubmit(false)}
              >
                <Send className="h-4 w-4" aria-hidden="true" />
                {submitting && submitMode === 'submit' ? 'Submitting…' : 'Submit Request'}
              </Button>

              <Button
                type="button"
                variant="secondary"
                className="w-full justify-center"
                disabled={!canRaiseRequest || selectedItems.length === 0 || submitting}
                onClick={() => handleSubmit(true)}
              >
                <Save className="h-4 w-4" aria-hidden="true" />
                {submitting && submitMode === 'draft' ? 'Saving…' : 'Save as Draft'}
              </Button>
            </div>

            <p className="mt-4 text-center text-xs text-ink-muted">
              Submitting sends this requisition directly to your superior for review.
            </p>
          </Card>
        </div>
      </div>
    </div>
  )
}
