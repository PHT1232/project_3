import { useState } from 'react'
import { Calendar, User, Clock, AlertCircle, CheckCircle2, Send, Undo2, XCircle, Trash2 } from 'lucide-react'

import Modal from '../../../components/ui/Modal.jsx'
import Button from '../../../components/ui/Button.jsx'
import { formatCurrency, formatDate } from '../../../lib/format.js'
import RequestStatusBadge from './RequestStatusBadge.jsx'

/**
 * Checks if a request in Pending status has already been submitted for approval.
 * Submission writes a status history row with FromStatus="Pending" and ToStatus="Pending".
 */
export function isRequestSubmitted(request) {
  if (!request) return false
  if (request.status !== 'Pending') return true
  return Boolean(
    request.statusHistory?.some(
      (h) => h.fromStatus === 'Pending' && h.toStatus === 'Pending',
    ),
  )
}

export default function RequestDetailModal({
  open,
  request,
  onClose,
  onSubmit,
  onWithdraw,
  onRequestCancellation,
  onDelete,
  isActioning = false,
}) {
  if (!open || !request) return null

  const isSubmitted = isRequestSubmitted(request)
  const isPending = request.status === 'Pending'
  const isApprovedOrPartial = request.status === 'Approved' || request.status === 'PartiallyApproved'

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={
        <div className="flex flex-wrap items-center gap-3">
          <span>Request #{request.requestId}</span>
          <RequestStatusBadge status={request.status} />
        </div>
      }
      footer={
        <div className="flex w-full flex-wrap items-center justify-between gap-2">
          <div className="flex items-center gap-2">
            {isPending && !isSubmitted && onDelete && (
              <Button
                variant="danger"
                size="sm"
                disabled={isActioning}
                onClick={() => onDelete(request)}
              >
                <Trash2 className="h-4 w-4" aria-hidden="true" />
                Delete Draft
              </Button>
            )}
            {isPending && isSubmitted && onWithdraw && (
              <Button
                variant="secondary"
                size="sm"
                disabled={isActioning}
                onClick={() => onWithdraw(request)}
              >
                <Undo2 className="h-4 w-4" aria-hidden="true" />
                Withdraw Request
              </Button>
            )}
            {isApprovedOrPartial && onRequestCancellation && (
              <Button
                variant="secondary"
                size="sm"
                disabled={isActioning}
                onClick={() => onRequestCancellation(request)}
              >
                <XCircle className="h-4 w-4" aria-hidden="true" />
                Request Cancellation
              </Button>
            )}
          </div>

          <div className="flex items-center gap-2">
            <Button variant="secondary" onClick={onClose} disabled={isActioning}>
              Close
            </Button>
            {isPending && !isSubmitted && onSubmit && (
              <Button
                variant="primary"
                disabled={isActioning}
                onClick={() => onSubmit(request)}
              >
                <Send className="h-4 w-4" aria-hidden="true" />
                {isActioning ? 'Submitting…' : 'Submit for Approval'}
              </Button>
            )}
          </div>
        </div>
      }
    >
      <div className="space-y-6">
        {/* Request Header Summary */}
        <div className="grid grid-cols-1 gap-4 rounded-lg bg-surface-muted/60 p-4 sm:grid-cols-2 lg:grid-cols-4">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wider text-ink-muted">Requestor</p>
            <p className="mt-0.5 text-sm font-medium text-ink">
              {request.requestorName ? `${request.requestorName} (#${request.requestorEmployeeNumber})` : `#${request.requestorEmployeeNumber}`}
            </p>
          </div>

          <div>
            <p className="text-xs font-semibold uppercase tracking-wider text-ink-muted">Approver</p>
            <p className="mt-0.5 text-sm font-medium text-ink">
              {request.approverName
                ? `${request.approverName} (#${request.approverEmployeeNumber})`
                : request.approverEmployeeNumber
                ? `#${request.approverEmployeeNumber}`
                : 'None assigned'}
            </p>
          </div>

          <div>
            <p className="text-xs font-semibold uppercase tracking-wider text-ink-muted">Date Created</p>
            <p className="mt-0.5 text-sm font-medium text-ink">{formatDate(request.createdAtUtc)}</p>
          </div>

          <div>
            <p className="text-xs font-semibold uppercase tracking-wider text-ink-muted">Required By</p>
            <p className="mt-0.5 text-sm font-medium text-ink">
              {request.requiredByDate ? formatDate(request.requiredByDate) : 'Not specified'}
            </p>
          </div>
        </div>

        {/* Decision Comment / Approval Note if present */}
        {request.decisionComment && (
          <div className="rounded-md border border-surface-border bg-surface-card p-3.5 text-sm">
            <p className="text-xs font-semibold uppercase tracking-wider text-ink-muted">
              Approver Note ({formatDate(request.decidedAtUtc)})
            </p>
            <p className="mt-1 text-ink">{request.decisionComment}</p>
          </div>
        )}

        {/* Line Items Table */}
        <div>
          <h4 className="mb-2 text-sm font-semibold text-ink">Requested Items ({request.items?.length ?? 0})</h4>
          <div className="overflow-x-auto rounded-md border border-surface-border">
            <table className="w-full text-left text-sm">
              <thead>
                <tr className="border-b border-surface-border bg-surface-muted text-xs uppercase tracking-wider text-ink-muted">
                  <th className="px-3 py-2 font-semibold">Item</th>
                  <th className="px-3 py-2 font-semibold">Category</th>
                  <th className="px-3 py-2 font-semibold">Supplier</th>
                  <th className="px-3 py-2 text-right font-semibold">Qty</th>
                  <th className="px-3 py-2 text-right font-semibold">Unit Price</th>
                  <th className="px-3 py-2 text-right font-semibold">Total</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-surface-border">
                {request.items?.map((item) => (
                  <tr key={item.requestItemId}>
                    <td className="px-3 py-2.5 font-medium text-ink">{item.itemName}</td>
                    <td className="px-3 py-2.5 text-ink-muted">{item.categoryName ?? '—'}</td>
                    <td className="px-3 py-2.5 text-ink-muted">{item.supplierName ?? '—'}</td>
                    <td className="px-3 py-2.5 text-right font-mono text-ink">{item.quantity}</td>
                    <td className="px-3 py-2.5 text-right font-mono text-ink-muted">{formatCurrency(item.unitCostSnapshot)}</td>
                    <td className="px-3 py-2.5 text-right font-mono font-medium text-ink">{formatCurrency(item.lineTotal)}</td>
                  </tr>
                ))}
              </tbody>
              <tfoot>
                <tr className="border-t border-surface-border bg-surface-muted/40 font-semibold text-ink">
                  <td colSpan={5} className="px-3 py-2.5 text-right">
                    Total Estimated Cost:
                  </td>
                  <td className="px-3 py-2.5 text-right font-mono text-base text-brand-700">
                    {formatCurrency(request.totalEstimatedCost)}
                  </td>
                </tr>
              </tfoot>
            </table>
          </div>
        </div>

        {/* Status History / Audit Trail Timeline */}
        {request.statusHistory && request.statusHistory.length > 0 && (
          <div>
            <h4 className="mb-3 text-sm font-semibold text-ink">Status History & Audit Trail</h4>
            <div className="relative border-l-2 border-surface-border pl-4 space-y-4">
              {request.statusHistory.map((history, idx) => (
                <div key={history.historyId ?? idx} className="relative">
                  <div className="absolute -left-[21px] top-1 h-2.5 w-2.5 rounded-full border-2 border-surface-card bg-brand-600" />
                  <div className="text-sm">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="font-semibold text-ink">
                        {history.fromStatus ? `${history.fromStatus} → ${history.toStatus}` : history.toStatus}
                      </span>
                      <span className="text-xs text-ink-muted">· {formatDate(history.createdAtUtc)}</span>
                    </div>
                    <p className="text-xs text-ink-muted">
                      By {history.actorName ? `${history.actorName} (#${history.actorEmployeeNumber})` : `#${history.actorEmployeeNumber}`}
                    </p>
                    {history.comment && (
                      <p className="mt-1 text-xs italic text-ink-muted">"{history.comment}"</p>
                    )}
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>
    </Modal>
  )
}
