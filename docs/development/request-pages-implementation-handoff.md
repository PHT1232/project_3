# Request Pages Implementation Handoff (New Request & My Requests)

**Date:** 2026-08-31  
**Scope:** Complete Backend and Frontend implementation for the **New Request** and **My Requests** pages (Plan M3/M4, §3.4–§4.2).  
**Test Status:** ✅ 70/70 Backend tests passed (26 Unit + 44 Integration) | ✅ 67/67 Frontend tests passed (12 suites).

---

## 1. Summary of What Was Built

### Entry flow update — 2026-09-03

- Removed **New Request** from the primary sidebar in `frontend/src/navigation.js`. The request route remains registered at `/new-request`; it is entered from the Catalogue selection bar's **Proceed** action, which passes selected items through React Router state.
- `frontend/src/pages/requests/NewRequestPage.jsx` remains usable when opened directly, so a requestor can still add items without returning to Catalogue. The picker now exposes a search box for item name and category, filters the unadded eligible catalogue items, and gives a clear no-results state.
- The picker is a compact catalogue table with an accessible checkbox per row. A requestor can select multiple eligible items and add them in one action. Added lines leave the picker; removing a line makes it available in the picker again.
- No request API, server-side authorization, request lifecycle, database entity, or migration changed. `GET /api/v1/items` remains the source of role-filtered active catalogue items.
- `frontend/src/pages/requests/NewRequestPage.test.jsx` covers name/category search, multi-item picker selection, removal, and request submission.

### Backend (`WebApi`, `Application`, `Infrastructure`)
1. **`WebApi/Controllers/RequestsController.cs`**:
   - `GET /api/v1/requests` — Paginated list of visible requests (with optional `status` filter).
   - `GET /api/v1/requests/mine` — Paginated list of caller's own requests (with optional `status` filter).
   - `GET /api/v1/requests/{id}` — Single request details with lines and audit trail history. Returns 404 (ownership-aware) if the caller is neither the requestor, the approver, nor a Manager+.
   - `POST /api/v1/requests` — Creates a new stationery request (in `Pending` status) with line item snapshots.
   - `POST /api/v1/requests/{id}/submit` — Submits a `Pending` request for approval.
   - `POST /api/v1/requests/{id}/withdraw` — Withdraws a `Pending` request (caller must be requestor).
   - `POST /api/v1/requests/{id}/request-cancellation` — Initiates cancellation request on an `Approved` / `PartiallyApproved` request.
   - `DELETE /api/v1/requests/{id}` — Deletes an unsubmitted `Pending` request.
   - `GET /api/v1/requests/dashboard-summary` — Status counts for dashboard widgets.
2. **Query & Interface Updates**:
   - `Application/Interfaces/Requests/IRequestQueries.cs` and `Infrastructure/Queries/RequestQueries.cs` updated with `statusFilter` support in `GetByRequestorAsync`.

### Frontend (`frontend/src/`)
1. **API Client (`frontend/src/api/requests.js`)**:
   - Implemented `getRequests`, `getMyRequests`, `getRequest`, `createRequest`, `submitRequest`, `withdrawRequest`, `requestCancellation`, `deletePendingRequest`, `getRequestSummary`.
2. **New Request Page (`frontend/src/pages/requests/NewRequestPage.jsx`)**:
   - Item picker dropdown loading active catalogue items from `getItems()`.
   - Requisition cart with item details (name, category, supplier, unit cost snapshot, quantity adjustment `1–9999`, and line subtotal).
   - Summary panel with total distinct items, total units count, delivery deadline, and estimated total cost.
   - Dual submission modes:
     - **Save as Draft**: Calls `createRequest()` only, leaving it in unsubmitted `Pending` state.
     - **Submit Request**: Calls `createRequest()` and immediately calls `submitRequest(id, rowVersion)`.
   - Comprehensive error and success state handling.
3. **My Requests Page (`frontend/src/pages/requests/MyRequestsPage.jsx`)**:
   - Paged requests table showing Request ID, Date Created, Required By, Approver, Item Count, Estimated Cost, Status badge (`RequestStatusBadge`), and contextual action buttons.
   - Status filter dropdown across all 8 statuses (`Pending`, `Approved`, `PartiallyApproved`, `Rejected`, `Withdrawn`, `CancellationPending`, `Cancelled`, `Fulfilled`).
   - Contextual actions:
     - View Details (`RequestDetailModal`)
     - Submit (for unsubmitted `Pending` requests)
     - Withdraw (for submitted `Pending` requests awaiting decision)
     - Request Cancellation (for `Approved` / `PartiallyApproved` requests)
     - Delete Draft (for unsubmitted `Pending` requests)
4. **Shared Components**:
   - `frontend/src/pages/requests/components/RequestDetailModal.jsx`: Displays header info, snapshotted line items table, and status history timeline audit trail with action buttons.
   - `frontend/src/pages/requests/components/CancellationModal.jsx`: Reason prompt modal for requesting order cancellation.
5. **App Routing**:
   - Updated `frontend/src/App.jsx` to route `/new-request` and `/my-requests` to `NewRequestPage` and `MyRequestsPage`.

---

## 2. Testing & Verification

### Backend Tests
- **Integration Tests (`Tests/WebApi.IntegrationTests/RequestsTests.cs`)**:
  - `CreateRequest_ValidPayload_ReturnsCreatedWithPendingStatus`
  - `SubmitRequest_PendingRequest_TransitionsToSubmitted`
  - `WithdrawRequest_PendingRequest_TransitionsToWithdrawn`
  - `DeletePendingRequest_Unsubmitted_DeletesSuccessfully`
  - `GetMine_ReturnsOnlyCallersRequests`
  - `GetById_UnauthorizedUser_Returns404`
- **Result:** `dotnet test Project.slnx` passed **70/70** tests (26 Unit + 44 Integration).

### Frontend Tests
- **`frontend/src/pages/requests/NewRequestPage.test.jsx`**:
  - Renders request header, requestor info, and item selector.
  - Adding items to the request and calculating estimated totals.
  - Modifying quantity and removing items.
  - Save as draft vs. immediate submit.
- **`frontend/src/pages/requests/MyRequestsPage.test.jsx`**:
  - Loading, error, and empty states.
  - Requests table rendering with status badges.
  - Detail modal opening.
  - Submit, withdraw, and cancellation request actions.
- **Result:** `npm test -- --pool=threads` passed **67/67** tests across 12 test suites.

---

## 3. Architecture & Security Compliance

- **CLAUDE.md Principle #1 & #2 (Dependencies point inward, Controllers thin):** `RequestsController` is thin and delegates all business logic to `IRequestService` and `IRequestQueries`.
- **CLAUDE.md Principle #4 & #8 (Optimistic Concurrency & Snapshots):** Every state change checks `Guid RowVersion`. `UnitCostSnapshot` and `LineTotal` are frozen at creation time.
- **CLAUDE.md Principle #9 (Authorization & Ownership):** Endpoints enforce server-side row-level checks. Accessing an unpermitted request returns 404 (not 403) to avoid leaking existence.
- **State Machine Alignment:** Correctly identifies unsubmitted draft vs. submitted `Pending` states via `StatusHistory` audit entries.
