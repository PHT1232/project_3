# Revision 3 handoff — rejection comment guard (M5) and 401 session handling (L3)

> Branch `khang`, 2026-09-05, on top of `765ffb9`. Written so another team member can explain
> both changes in a PR review without reading this session's transcript.
> Companion documents: [PROJECT_AUDIT.md](../../PROJECT_AUDIT.md) (findings and their history)
> and the dated entry in [AI_usage_report.md](../../AI_usage_report.md).

---

## 1. What this revision did

Two confirmed implementation gaps were closed. Both implement rules that already exist in the
project documents — **no requirement, diagram or business rule was changed**.

| ID | Gap | Where the rule comes from |
|---|---|---|
| **M5** | A request could be rejected outright with no reason recorded | Plan §3.6 transition table: `Pending → Rejected · Guard: Comment required` |
| **L3** | An expired JWT surfaced as a raw 401 on every page, with the dead token still stored and no route back to login | Plan §9.2 (token in `localStorage`, 8-hour expiry) — the failure mode was never handled |

Everything else outstanding was left alone deliberately; see §6.

---

## 2. M5 — a rejection now needs a reason

### The defect

Plan §3.6 states the guard on the `Pending → Rejected` edge as "Comment required". Nothing
enforced it. `ApproveRequestCommandValidator` only capped the comment's length, so an approver
could reject someone's entire request and `Request.DecisionComment` would store `null`. The
requestor saw a rejected request with no explanation, and the audit trail recorded none either.

This was tracked as **PC5** in the previous audit ("Potential — confirm whether the Plan's guard
should be enforced"). It is not a document conflict: the Plan states the guard, and the code
simply had not implemented it, so it was implemented rather than escalated.

### The fix — server side

`Application/Validators/Requests/ApproveRequestCommandValidator.cs`:

```csharp
RuleFor(x => x.Comment)
    .NotEmpty()
    .When(IsOutrightRejection)
    .WithMessage("A comment is required when rejecting a request.");

private static bool IsOutrightRejection(ApproveRequestCommand command) =>
    command.LineDecisions is { Count: > 0 }
    && command.LineDecisions.All(d =>
        string.Equals(d.Decision, "rejected", StringComparison.OrdinalIgnoreCase));
```

**Why the validator and not the service.** The header status is derived inside
`RequestService.ApproveAsync` as *"every line rejected → `Rejected`"*, but that same condition is
decidable from the command alone, before any database work happens. Putting it in the validator
means the request is refused with a **400 and a field-level error** — the shape every other
validation failure in the app already has — and the service is never entered, so no partial state
can be written. It also keeps `RequestService` free of a rule FluentValidation expresses better.

**Why only an outright rejection.** Plan §3.6 puts the guard on one edge. A `PartiallyApproved`
outcome — a rejected line among approved ones, or a reduced quantity — is a different transition
and the Plan attaches no comment guard to it. Requiring one there would be stricter than the
specification, i.e. inventing a requirement. If the team decides a partial approval should also
demand a reason, it is a one-line change to `IsOutrightRejection`.

**Case-insensitivity** matches the service, which lowercases the decision before switching on it,
so `"REJECTED"` cannot slip past the guard and then be treated as a rejection downstream.

### The fix — client side

`frontend/src/pages/requests/components/RequestReviewModal.jsx` mirrors the rule so the approver
learns about it on the field instead of after pressing Submit:

- `isOutrightRejection` is derived from the same "every line rejected" condition.
- The label flips between `Comment (optional)` and `Comment (required)`; the textarea gets
  `required` / `aria-required`, and a hint reads *"Rejecting every line rejects the request, so
  the requestor needs a reason."*
- `canSubmit` gains `(!isOutrightRejection || comment.trim().length > 0)`, so **Submit decision**
  is disabled until a reason is typed.

The server remains the control (Plan §2.5, CLAUDE.md #9): the UI half is only ergonomics, and the
integration test proves the API refuses the call regardless of what the client does.

---

## 3. L3 — session expiry now returns the user to login

### The defect

Tokens live `Jwt:ExpiryHours` (default 8). `client.js` had a request interceptor only — the
response side was untouched. A tab left open overnight therefore hit 401 on every call and
rendered each page's generic error state ("Request failed with status code 401"), with the dead
token still in `localStorage` and nothing sending the user to `/login`.

### The fix

`frontend/src/api/client.js` gained a response interceptor and a `setUnauthorizedHandler` export:

```js
client.interceptors.response.use(
  (response) => response,
  (error) => {
    const isLoginCall = error.config?.url?.includes('/auth/login')
    if (error.response?.status === 401 && !isLoginCall) {
      clearStoredAccessToken()
      onUnauthorized?.()
    }
    return Promise.reject(error)
  },
)
```

`AuthContext` registers `clearSession` as that handler in an effect and unregisters on unmount.

Three design points worth being able to defend in review:

1. **The login call is excluded.** A 401 from `/auth/login` means *wrong password*, not *expired
   session*. Without the exclusion, a failed sign-in would clear a session the user never had and
   the Login page's own "Those sign-in details are incorrect." message would be competing with a
   session-cleared redirect.
2. **`client.js` never imports `AuthContext`.** The handler is registered by the consumer, so the
   dependency stays one-way and every `api/*.js` module can keep importing `client` without
   pulling React in. That is also what makes the interceptor unit-testable in isolation.
3. **No `window.location` assignment.** `clearSession()` sets `isAuthenticated` to false and
   `ProtectedRoute` redirects from there, so the redirect stays inside the router and does not
   throw away SPA state or trigger a full page reload.

The error is still re-thrown (`Promise.reject(error)`), so any caller with its own 401 handling
continues to see it.

---

## 4. Files changed

| File | Change |
|---|---|
| `Application/Validators/Requests/ApproveRequestCommandValidator.cs` | Comment required on an outright rejection; doc comment updated |
| `frontend/src/api/client.js` | 401 response interceptor + `setUnauthorizedHandler` export |
| `frontend/src/contexts/AuthContext.jsx` | Registers `clearSession` as the 401 handler |
| `frontend/src/pages/requests/components/RequestReviewModal.jsx` | Required/optional comment label, hint, `required`, submit gating |
| `Tests/Application.UnitTests/Requests/ApproveRequestCommandValidatorTests.cs` | **New** — 11 cases |
| `Tests/WebApi.IntegrationTests/RequestsTests.cs` | +2 end-to-end cases |
| `frontend/src/api/client.test.js` | **New** — 4 cases |
| `frontend/src/pages/requests/ApprovalsPage.test.jsx` | +3 cases |

**APIs changed:** none added or removed. `POST /api/v1/approvals/{id}/approve` gains one
validation rule and can now return **400** where it previously returned 200.

**Database changes:** none. No entity, configuration or migration was touched.

**Shared files touched:** `frontend/src/api/client.js` and `frontend/src/contexts/AuthContext.jsx`
are used by every page — both changes are additive (a new interceptor, a new effect) and no
existing export changed signature.

---

## 5. Tests actually run

| Command | Result |
|---|---|
| `dotnet test Project.slnx` (before any edit) | 230 passed — baseline, no regression from earlier work |
| `npx vitest run --pool=threads` (before any edit) | 147 passed |
| `dotnet test Project.slnx` (after) | **243 passed** (107 unit + 136 integration), 0 failed |
| `npx vitest run --pool=threads` (after) | **154 passed** across 25 files, 0 failed |

The new tests are written from Plan §3.6, not from the implementation — deliberately, because the
first audit found nine logical errors sitting under a green suite whose tests had all been written
from what the code did. The validator suite therefore asserts both halves of the rule: that a
rejection is refused **and** that an approval or partial approval is not.

### Live verification (SQLEXPRESS `StationeryManagementSystem.Dev` + Vite dev server)

Not only tests — every flow below was exercised against the running application:

| Check | Result |
|---|---|
| Reject request #26, `comment: null` | **400**, `"A comment is required when rejecting a request."` |
| Re-read #26 afterwards | still `Pending`, `decision: null` — nothing written |
| Partially approve #26, no comment | **200**, `PartiallyApproved` |
| Reject #27 with a comment (API) | **200**, `Rejected`, `decisionComment` persisted |
| Reject #28 through the browser as the real approver | Label flipped to "Comment (required)", Submit disabled; after typing a reason it submitted and the request came back `Rejected` with the comment stored |
| Corrupt the stored JWT, navigate in-app | Redirected to `/login`, token cleared, no raw error |
| Wrong password on the login form | Still shows "Those sign-in details are incorrect." — the interceptor does not swallow it |
| Manager sidebar | Still shows Support Inbox (M2 fix intact) |

**Wireframe fidelity:** the review modal's layout is unchanged; only the comment label's text,
a one-line hint below the field, and the disabled state of an existing button differ. No new
page, control or navigation entry was introduced.

**Test data:** employees 410 (Manager) and 411 (Engineer) were created on the dev database to get
a clean approver/requestor pair and have since been deactivated (`IsActive = false`). Requests
26–28 remain as evidence. Nothing was deleted: `Users` is soft-delete only and a submitted
request is never removed (CLAUDE.md #7).

---

## 6. Known issues and reviewer follow-ups

Left open on purpose:

- **M4 — reports are open to every authenticated user.** `ReportsController` is `[Authorize]`
  only, while Plan §4.2, T5.2 and TC-18 all say Manager+. Data is row-scoped, so nothing leaks,
  but the code and the Plan disagree **in writing**. Whichever side wins, the other document must
  be updated — a team ruling, not a silent code change.
- **PC1 — `SupplierRequest` has no `RowVersion`.** Duplicate arrival confirmation is blocked by a
  status check inside the transaction (sequential double-clicks are covered and tested), but two
  confirmations racing in separate transactions could in principle both read `PendingArrival`.
  Closing it needs a new column and a migration, and CLAUDE.md §5 allows only one open PR with a
  migration at a time — announce it before starting.
- **PC5 is now closed** by this revision; the audit's other PC items are unchanged.
- **L1, L2, L4, L5, L8** unchanged. L1 (`/new-request` absent from `navigation.js`) was left
  alone specifically: the sidebar order is wireframe-derived, and the page already has two working
  entry points (the Catalogue's "Proceed" button and the My Requests header).

Follow-ups a reviewer may want to raise:

1. Decide whether a **partial** approval containing a rejected line should also require a comment.
   The Plan does not say; the current reading is documented in §2 and is a one-line change.
2. `CLAUDE.md` §1 is still badly stale (**L7**) — it claims "everything after M2 is unbuilt",
   which is wrong by five milestones. It is the first file every teammate and AI agent reads.
