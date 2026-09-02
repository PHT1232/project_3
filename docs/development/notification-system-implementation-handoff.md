# Notification System Implementation Handoff

**Branch:** `feat/M4-notifications` (off `main`). **Status:** implemented, built, tested —
61/61 backend tests, 90/90 frontend tests passing.

## What this is

The 6-trigger notification feed named in Plan §4.2 as `[SPEC]` and explicitly "not
deferrable": request submitted, approved, rejected, withdrawn, cancelled, and password
changed, each firing to exactly two recipients (Plan: "Every trigger inserts exactly 2
rows"). Backed by a persisted `Notifications` table, polled from the frontend — no
SignalR/real-time push (`[CUT]` per Plan §2.4/§11), no email (`[CUT]`).

Most of the request-lifecycle workflow this hooks into (`RequestService`, `ApprovalController`,
the 8-status state machine) already existed before this work — this pass adds the
notification layer on top of it, not the workflow itself.

## Architecture

- **Write side** (`INotificationService` / `NotificationService`): `NotifyRequestEventAsync`
  and `NotifyPasswordChangedAsync` build the two `Notification` rows for a trigger and stage
  them on the `DataContext` — they deliberately do **not** call `SaveChangesAsync` for request
  events, per the Plan's explicit `[DECISION]`:

  > Implement notifications via a single `INotificationService.NotifyAsync(eventType,
  > request, actor)` called **inside** the same transaction as the state change. Rejected
  > alternative: EF `SaveChanges` interceptors or a domain-event bus.

  `RequestService`'s five call sites (`SubmitAsync`, `ApproveAsync`, `WithdrawAsync`,
  `ApproveCancellationAsync`) each stage the notification rows, then their own existing
  `SaveChangesAsync()` call commits everything atomically — status change, history row, and
  notification rows all succeed or all roll back together.

  **`NotifyPasswordChangedAsync` is the one exception** — it calls `SaveChangesAsync` itself.
  Password changes go through ASP.NET Core Identity's `UserManager`, which persists via its
  own `SaveChangesAsync` call before `AuthService.ChangePasswordAsync` ever gets to the
  notification step, so there's no still-open unit of work left to join. A failed
  notification write here never rolls back an already-successful password change, which is
  the correct behavior anyway — you wouldn't want someone's password change silently undone
  because a notification insert hiccupped.

- **Read side** (`INotificationQueries` / `NotificationQueries`): `GetForUserAsync` (paged,
  newest first) and `GetUnreadCountAsync` (backed by the `(RecipientEmployeeNumber, IsRead)`
  composite index — Plan §3.3: "must be a single indexed COUNT").

- **Endpoints** (`NotificationsController`, matching Plan §4.2's "Notifications — Member 4"
  table exactly): `GET /api/v1/notifications`, `GET /api/v1/notifications/unread-count`,
  `POST /api/v1/notifications/{id}/read`, `POST /api/v1/notifications/read-all`.

- **Frontend**: `useNotifications(enabled)` polls only the unread-count endpoint every 30s,
  paused while `document.hidden` and refreshed immediately on `visibilitychange` (Plan §11
  risk: "Notification polling hammers the DB" — this plus the indexed count query is the
  mitigation). The full feed is fetched on demand, only when the bell dropdown opens.
  `NotificationBell.jsx` renders the badge + dropdown (loading/error/empty states, mark-read
  on click, mark-all-read), replacing the disabled placeholder that was already sitting in
  `Header.jsx` with a comment naming this exact feature as what would eventually replace it.

## Two design decisions worth knowing about

Both are called out in code comments at the point of use, but are easy to miss if you're
just skimming the diff:

**1. Recipient pairing isn't literally "actor and their superior."** The Plan's own wording
(§4.2) is "the actor and their superior," echoing the source spec's "popped up to the person
and his superior." Taken completely literally, that breaks for approve/reject: the actor
there is the *approver*, and the approver's own manager has no reason to hear about it. What
actually needs to happen is that **the requestor and the approver** both find out, regardless
of which of the two performed the action. In this app's hierarchy model those are the same
pairing anyway — `Request.ApproverEmployeeNumber` is set from the requestor's
`SuperiorEmployeeNumber` at creation time (`RequestService.CreateAsync`) — so
`NotifyRequestEventAsync` always uses `{request.RequestorEmployeeNumber,
request.ApproverEmployeeNumber}` as the recipient pair for all 5 request-related triggers.
Password-changed has no request involved, so it's the only trigger that uses the literal
`{actor, actor's superior}` reading.

**2. Only the *final* cancellation outcome notifies.** The two-step cancellation flow has two
decision points: `RequestCancellationAsync` (Approved/PartiallyApproved → CancellationPending)
and `ApproveCancellationAsync` (CancellationPending → Cancelled, or back to
Approved/PartiallyApproved on denial). The Plan names exactly 6 triggers, and "cancelled" is
the only cancellation-related one on that list — not "cancellation requested," not
"cancellation denied." So `RequestCancellationAsync` fires no notification at all, and
`ApproveCancellationAsync` only fires one when `approved == true`. If the two-step flow's
*other* transitions ever need their own notifications, that would be adding a 7th/8th trigger
beyond what's specified — flag it as a scope change, don't just add it quietly.

## Setup / migration

Nothing beyond the normal flow — `dotnet.ef migrations add`/`Program.cs`'s existing
`dbContext.Database.MigrateAsync()` on startup applies `20260902112636_AddNotifications`
automatically. No new environment variables, no new Jenkins/Docker changes.

One thing to know if you're running `dotnet ef` commands locally: it boots the real
`Program.cs`, which means it tries to reach Elasticsearch if `Elasticsearch:Uri` is
configured. If ES isn't running, you'll see `[Serilog]` connection-refused noise in the
`dotnet ef` output — harmless, the migration still completes; that's just the ES sink failing
to register its index template on a host that doesn't exist locally.

## Files changed

**Backend:**
- `Core/Entities/Notification.cs`, `Core/Enums/NotificationEventType.cs`
- `Infrastructure/Data/Configurations/NotificationConfiguration.cs`
- `Infrastructure/Data/Migrations/20260902112636_AddNotifications.cs` (+ Designer, snapshot)
- `Infrastructure/DataContext.cs` (new `DbSet<Notification>`)
- `Infrastructure/Services/NotificationService.cs`
- `Infrastructure/Queries/NotificationQueries.cs`
- `Application/DTOs/Notifications/{NotificationDto,UnreadCountDto}.cs`
- `Application/Interfaces/Notifications/{INotificationService,INotificationQueries}.cs`
- `Infrastructure/Services/RequestService.cs` (5 new call sites)
- `Application/Services/Auth/AuthService.cs` (1 new call site)
- `WebApi/Controllers/NotificationsController.cs`
- `WebApi/Program.cs` (DI registration)
- `Tests/WebApi.IntegrationTests/{NotificationServiceTests,NotificationsControllerTests}.cs`
- `Tests/Application.UnitTests/Auth/AuthServiceTests.cs` (constructor signature update only)

**Frontend:**
- `frontend/src/api/notifications.js`
- `frontend/src/hooks/useNotifications.js` (+ test)
- `frontend/src/components/layout/NotificationBell.jsx` (+ test)
- `frontend/src/components/layout/Header.jsx` (wired in, replacing the disabled placeholder)

## Tests actually run

- `dotnet test Project.slnx` — 61/61 passed (50 pre-existing + 11 new).
- `npx vitest run --pool=threads` (frontend) — 90/90 passed (75 pre-existing + 15 new).
- Both backend and frontend builds run cleanly (`dotnet build Project.slnx`, `npm run build`).

## Known gaps / explicitly out of scope for this pass

- **No toast-on-action.** Plan's T4.8 also mentions a toast for the acting user's own screen
  at the moment of the action (e.g. "Request submitted" right after clicking Submit). Not
  added here — every action-triggering page already has its own success/error handling, and
  a toast layer is a separate, smaller UI concern from the persisted feed itself. If wanted,
  it's a small addition on top of what's here, not a redesign.
- **No notification retention/cleanup policy.** Rows are never deleted, matching the Plan's
  "persisted notification feed" framing. Not requested, not added — flag before this becomes
  a real, long-lived deployment with years of notification history.
- **No click-through navigation from a notification to its request.** Clicking a notification
  in the dropdown marks it read; it doesn't navigate to the request's detail view. Adding that
  would mean picking a target route/modal pattern consistent with how `MyRequestsPage`/
  `ApprovalsPage` already handle request detail — reasonable follow-up, not done here to avoid
  scope creep beyond what Plan §4.2/T4.8 actually specifies.
