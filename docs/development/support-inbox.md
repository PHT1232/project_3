# In-app Support Inbox

## Why this shape

The Help page needs a "contact the team" path. A `mailto:` link is inert on any machine
without a configured desktop mail client, which is most dev boxes. A server that *sends*
email means SMTP — and **email/SMTP is on the Plan's `[CUT]` list** (§1.3), so building a
sender is a scope breach that needs Project-Leader sign-off (`[ASK] #4`).

**Option B** was chosen: the message is **stored**, not sent. Any authenticated user submits
from a dialog; Manager+ read and resolve them on an in-app triage screen. No network
dependency — it works with the internet unplugged, matching CLAUDE.md principle #10.

If the team later wants real delivery to `antsconst84@gmail.com`, it layers on top without
rework: the DB insert stays the source of truth and an SMTP send (MailKit + a Gmail App
Password in `dotnet user-secrets`, never committed) becomes a best-effort step after it.

## Data

`SupportMessage` (table `SupportMessages`, migration `20260903181104_AddSupportMessages`):

| Column | Notes |
|---|---|
| `Id` | PK, identity |
| `SenderEmployeeNumber` | FK → `AspNetUsers.Id`, `Restrict` |
| `Area` | free text, ≤80 (UI constrains to a fixed list) |
| `Subject` | ≤200 |
| `Body` | ≤4000 |
| `Diagnostics` | ≤4000, nullable — client-supplied context (app version, page, browser, user). Free text only; never parsed server-side. |
| `Status` | `"New"` or `"Resolved"` |
| `CreatedAtUtc` | `datetime2`, `GETUTCDATE()` default |
| `ResolvedAtUtc` / `ResolvedByEmployeeNumber` | set on resolve, cleared on reopen |

Indexes on `CreatedAtUtc` and `Status` (triage list reads newest-first, filtered by status).
No navigation properties — same pattern as `Notification` / `AiInteractionLog`.

## API — `WebApi/Controllers/SupportController.cs`

| Method | Route | Auth | Purpose |
|---|---|---|---|
| POST | `/api/v1/support/messages` | any authenticated | Send a message → 201 |
| GET | `/api/v1/support/messages?status=&page=&pageSize=` | `RequireManager` | Triage list, newest-first |
| GET | `/api/v1/support/messages/{id}` | `RequireManager` | One message |
| GET | `/api/v1/support/messages/open-count` | `RequireManager` | Count still `New` (for a future badge) |
| PATCH | `/api/v1/support/messages/{id}/status` | `RequireManager` | `{ resolved: bool }` — resolve / reopen. **400 if the actor is the message's own sender** — the reporter doesn't triage their own ticket. |

Write side: `ISupportMessageService` (`Infrastructure/Services/SupportMessageService.cs`).
Read side: `ISupportMessageQueries` (`Infrastructure/Queries/SupportMessageQueries.cs`).
Validation: `CreateSupportMessageCommandValidator` (FluentValidation → `ValidationException`
→ 400 via `ExceptionHandlingMiddleware`).

## Frontend

- `api/support.js` — client calls.
- `pages/help/components/ContactModal.jsx` — the dialog (Area / Subject / Message +
  collapsible "session details we'll attach"). Success screen; API `detail` surfaced on error.
- `pages/help/components/ContactCard.jsx` — "Message the team" opens the dialog; the email
  address is now just a plain fallback line; "Copy diagnostics" retained.
- `pages/support/SupportInboxPage.jsx` — Manager+ triage. Open / Resolved / All filter, one
  card per message, show/hide diagnostics, Mark resolved / Reopen. A message the viewer sent
  themselves shows a "You sent this" badge instead of the action button (the server enforces
  the same rule). Card skeleton while the list loads.
- Route `/support-inbox` sits inside `App.jsx`'s `requireManager` group; nav item in
  `navigation.js` with `minRankLevel: 2` (UX only — the server 403 is the real control).
- `config/support.js` — `SUPPORT_EMAIL`, `SUPPORT_AREAS`, `buildDiagnostics(user)`.
  `__APP_VERSION__` / `__BUILD_TIME__` are injected by `vite.config.js`.

## Tests

- `Tests/WebApi.IntegrationTests/SupportTests.cs` — 7: anon 401; engineer send → stored New;
  blank body 400; engineer list/resolve → 403; manager list newest-first + status filter;
  manager resolve flips status + records resolver + drops open-count.
- `frontend` — `Help.test.jsx` (dialog flow, no mailto), `SupportInboxPage.test.jsx` (list,
  resolve, filter switch, diagnostics reveal).
- `dotnet test Project.slnx` 128 passed · `npx vitest run --pool=threads` 110 passed · builds clean.

## Reviewer follow-ups

1. **Migration** — `AddSupportMessages` is the only pending one; announce before a second
   migration branch opens (CLAUDE.md git rules).
2. Confirm "no email delivery" is acceptable for the deliverable, or take `[ASK] #4` to the
   instructor for the SMTP add.
3. Statuses are New / Resolved only — add assignment / priority / "notify sender on resolve"
   only if asked.
4. `open-count` endpoint exists but nothing shows a badge yet — wire it into the sidebar or
   dashboard if wanted.
5. `SupportMessages` is net-new and not in the Plan's 12-table ERD (like `Notifications` /
   `AiInteractionLogs`) — fold it into the ERD/`StationerySchema.sql` reconciliation.
