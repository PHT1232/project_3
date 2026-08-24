# Frontend Context

Source of truth for architecture/process decisions: `__ai_agents/Stationery_Management_System_Project_Plan.md` (the Plan). This file is a quick-reference summary — if it and the Plan disagree, the Plan wins.

## Architecture
The frontend is a React Single Page Application in `frontend/`, bundled and served by the backend WebApi. The repo currently has a bare Vite JS scaffold (`main.js`, `counter.js`) — this needs to become the React 18 + Vite + Tailwind app described below (Plan Milestone M0, track T0.5); do not treat the current scaffold as the target state.

## Tech Stack
- **Build Tool**: Vite (Node.js ecosystem)
- **UI**: React 18, function components + hooks only. One component per file, `PascalCase.jsx`.
- **Styling**: Tailwind CSS. Utility classes in markup; extract repeated clusters into a component, not `@apply` soup. No inline `style={{}}`. Design tokens (colours, spacing, typography) are fixed in `tailwind.config.js` and documented in `docs/GUI-Standards.md`.
- **Routing**: React Router
- **State**: Local `useState` first; `AuthContext` for the session. **No Redux** — unjustified for this project's scope.
- **Data access**: All API calls go through `src/api/*.js` using a shared axios instance with a JWT interceptor. No `fetch` scattered through components.
- **Build Output**: `npm run build` generates production assets in `dist/`.

## Component conventions (Plan §9.2)
- Over ~150 lines, or more than 3 levels of JSX nesting → extract.
- Every data-fetching component needs a loading state, an error state, and an empty state — a happy-path-only component is incomplete and will be sent back in review.
- Controlled inputs on forms; validate client-side for UX, but trust only the server.
- Accessibility: labels on inputs, keyboard-reachable buttons, visible focus ring, ≥4.5:1 contrast.
- Nothing sensitive in `import.meta.env` — anything shipped to the browser is public.

## Auth & token storage
**[DECISION — Plan §9.2]** JWT is stored in `localStorage` and read by the axios interceptor, for time reasons. This is a known, documented trade-off: `localStorage` is readable by any injected script, so an XSS bug becomes full account takeover. The production-correct answer (`httpOnly` + `Secure` + `SameSite=Strict` cookie + CSRF token) was consciously not built here — don't "fix" this unilaterally; it's a scoped decision, not an oversight.

## Key screens (see Plan §4.2 for the full endpoint catalogue)
Login/change-password, role-filtered stationery catalogue, new multi-line request + eligibility view, My Requests with status, approvals queue (approve/reject/return), withdraw/two-step cancellation, notification feed + polled unread-count badge (30s poll — no SignalR/websockets), three manager cost reports, Help Q&A, and the AI Request Assistant (natural-language input → editable draft request, never auto-submitted).

## Deployment Integration
The application uses a unified deployment model via Docker:
1. The frontend is built inside a Node.js Docker stage.
2. The resulting `dist` folder is copied directly into the .NET WebApi's `wwwroot` directory.
3. The .NET backend acts as the static file server for the frontend (`UseStaticFiles()` + `MapFallbackToFile("index.html")`), ensuring both exist in a single container.

## Testing
Vitest + React Testing Library, scoped to ~5 critical components given the project's 3-week timebox (Plan §10.2) — not a general coverage target.
