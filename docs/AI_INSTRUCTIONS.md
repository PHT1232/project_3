# AI_INSTRUCTIONS.md — AI-Assisted Development Standard

Applies to **every** AI coding agent used on this project: Claude, Claude Code, Cursor,
GitHub Copilot, Gemini, Gemini CLI, and any other agent. Applies equally to the human
developer driving the agent.

---

## 1. Purpose

Five developers work on this repository in parallel, each using different AI tools. Without
a shared standard, agents produce inconsistent architecture, silently overwrite each other's
work, invent requirements that were never approved, and claim tests that never ran.

This file exists so that every agent behaves like the same disciplined teammate:
small scoped changes, no invented requirements, no unreviewed code, no fabricated reports.

**AI is an assistant, not a decision-maker.** It never redefines requirements, business rules,
architecture, or the database schema on its own initiative.

---

## 2. Project Context

Web-based **Stationery Management System** for HMT Technologies, replacing manual
email/Excel/register tracking of stationery requests.

- **Roles:** Requestor (every employee), Approver (anyone with direct reports), Manager and
  above (adds cost reports). Hierarchy: Engineer → Manager → Business Manager → MD; routing
  by self-referencing superior link, `0` = top of hierarchy.
- **Stack:** .NET 10 Web API in Clean Architecture (`Core` / `Application` / `Infrastructure` /
  `WebApi`), Vite SPA frontend served as static files by `WebApi`, Jenkins + Docker CI/CD,
  NGINX + Tailscale Funnel for public exposure.
- **Stage:** design and analysis complete; implementation starting. The .NET solution is still
  template scaffolding (`Class1.cs`, `WeatherForecastController`).
- **Database engine, ORM, auth mechanism, test framework, and the AI product feature are
  NOT SPECIFIED** in the current documents.

---

## 3. Source of Truth

When documents disagree or information is missing, resolve in this order:

1. Official project requirements / assignment — `web_based_Stationery_Management_System.docx`
2. Project specification / course deliverables — `ProjectSpecification.docx`
3. Approved system diagrams (Use Case, DFD, ERD, Activity, Sequence, Architecture)
4. Approved database design
5. Approved Figma wireframes and UI documentation
6. Existing source code
7. Project plan / roadmap — `Phân_chia_công_việc.xlsx`, `Stationery_Management_System_Roadmap.md`
8. `README.md` and supporting docs (`cau_truc_du_an.md`, `cau_hinh_funnel_tailscale.md`,
   `Startup_Product_Kickoff.pdf`)
9. General software engineering practice

**Conflict rule.** If two sources conflict, **do not silently pick one and do not invent a
rule to reconcile them.** Stop, report both positions and where each came from, and let a
human decide.

**Missing rule.** If something cannot be determined from the repository, write
**NOT SPECIFIED** and ask. Never fill the gap with a plausible-sounding invention.

Note: `Stationery_Management_System_Roadmap.md` is *analysis of* the requirements, and it
labels its own content (required / best practice / recommendation). Its recommendations are
not approved requirements.

---

## 4. Requirement Rules

AI must:
- Implement only what the approved requirements state.
- Follow the approved diagrams for behaviour and data flow.
- Follow the approved wireframes for UI.
- **Never invent** business rules, entities, fields, statuses, endpoints, roles, or pages.
- Report — not resolve — anything unclear, contradictory, or absent.

Business rules that are explicit in the requirements and must not be "simplified":
- An approver can also be a requestor; they are not separate user types.
- Requests route to the requestor's direct superior.
- **Withdraw** applies only to a request that is still pending; the requestor does it alone.
- **Cancel** applies to an already-approved request and requires the superior's approval —
  a two-step workflow, not a status flip and not a delete.
- Eligibility thresholds are per **role**, not per individual.
- Superior number `0` means top of hierarchy; hierarchy traversal must terminate there.
- Passwords are stored hashed ("cryptic form"), never in plain text.
- Notifications fire to **both** the actor and their superior, on all six events:
  request created, cancelled, withdrawn, approved, rejected, password changed.
- Name ≤ 15 chars with no underscores/special characters; EmailId ≤ 25 chars and unique;
  EmployeeNumber in 1–1000 and used as the login.

---

## 5. Coding Rules

- Respect the Clean Architecture dependency direction:
  `WebApi → Application + Infrastructure`, `Infrastructure → Core`, `Application → Core`,
  `Core → nothing`. Domain logic does not reach outward.
- Data access goes through the repository abstraction in `Core/Interfaces/IRepository.cs`.
  No raw data access from controllers.
- Read the existing code before writing new code; reuse what is there rather than adding a
  parallel implementation.
- No new NuGet/npm dependency without a stated reason and team approval.
- No refactoring outside the assigned task. Formatting-only churn is a review burden.
- Keep naming consistent with existing code. If a convention is not yet established, follow
  standard .NET (PascalCase members, `I`-prefixed interfaces, async methods suffixed `Async`)
  and standard JS/TS conventions on the frontend, and say in your report that you set a
  precedent.
- Validate input on the **server**, not only in the browser. UI-only checks are not validation.
- Handle errors explicitly: no swallowed exceptions, no generic 500 for expected states,
  no leaking stack traces or connection strings to clients.
- Comment non-obvious logic. The course standard requires code to be explainable.

---

## 6. Page-Based Development

The unit of work is a **page/feature**, not a milestone. Developers work in parallel, each
owning their page end-to-end.

Pages recorded in `Phân_chia_công_việc.xlsx`: Register & Login · Dashboard (availability) ·
Request Form · Approval Page · My Requests · Cancel and Withdraw · Manager's Cost Report ·
View Eligibility *(marked optional)* · Automatic Notifications *(marked "done later")*.

Required by the specification but missing from that sheet: **Change Password**, **Help / Q&A**.
Page owners are **NOT SPECIFIED** — the assignment columns are empty.

Do not build pages that appear in no project document (e.g. product browsing, inventory,
product management, supplier management). They are not part of this system.

Every task must state, before work begins:
- **Page** — which page/feature
- **Role** — which role(s) use it (Requestor / Approver / Manager)
- **Feature** — the specific behaviour being added
- **Backend requirements** — layer, services, business rules involved
- **Frontend requirements** — screens, states, validation, wireframe reference
- **Related database entities**
- **Related APIs** — existing endpoints reused, new endpoints proposed

If any of these is missing, ask before coding. Work only within the assigned scope unless
explicitly told otherwise.

---

## 7. Multi-Agent Collaboration

With five developers on different agents, the main risk is unrequested collateral change.

AI must:
- Minimize any change outside the assigned page.
- Never modify another developer's feature without permission.
- Treat these as **shared surfaces** requiring extra care and an explicit callout:
  `Core` entities/interfaces · `DataContext` · `Repository` · DI registration in `Program.cs` ·
  auth middleware · shared frontend layout, routing, API client, shared UI components ·
  `appsettings*.json` · Dockerfile · Jenkinsfile · database schema/migrations.
- Prefer additive changes to shared code over changing existing signatures or behaviour.
- Avoid large refactors entirely during a page task.
- **Explicitly report** every shared file touched, every API contract change, and every
  database change — these need to reach the whole team, not just the reviewer.

---

## 8. Human Review

AI output is never assumed correct. The owning developer reviews, and must be able to
explain, all of:
- **Logic** — does it match the approved requirement and diagram?
- **Security** — auth, role checks, password handling, no secrets in code.
- **Validation** — server-side, matching the spec's field constraints.
- **Database operations** — correct entities, no unintended writes/deletes, no N+1 surprises.
- **API behaviour** — routes, verbs, status codes, payload shape, error responses.
- **UI behaviour** — matches wireframe, handles loading/empty/error states.
- **Tests** — that they exist and that they actually exercise the behaviour.

A developer who cannot explain their AI-generated code has not finished the task.

---

## 9. Testing

Before a task is reported complete:
- Build the affected project(s) — `dotnet build` for backend, the frontend build for SPA changes.
- Run the relevant tests. (No test project exists yet — if you add the first one, say so.)
- Manually verify the affected page against the requirement and the wireframe.
- Check for obvious regressions in pages that share the code you touched.

**Report only tests that were actually executed.** "Should work", "builds cleanly" when it was
not built, or invented test output are unacceptable. If you could not run something, say why.

---

## 10. Git

Workflow:
1. **Feature branch** off the integration branch, named for the page/feature.
2. **Develop** in small, focused commits with clear messages describing what and why.
3. **Test** locally — build + run + verify the page.
4. **Review** — the owning developer reviews AI-generated code before it leaves the branch.
5. **Pull Request** — describe the change, shared files touched, API/DB changes, tests run.
6. **Merge** after review.

Rules for AI agents:
- **Do not commit, push, merge, rebase, force-push, or create/switch branches unless
  explicitly instructed.**
- Never commit secrets, connection strings, API keys, `.env` files, `bin/`, `obj/`, or
  `node_modules/`.
- Never rewrite shared history.

Exact branch names and the Git conventions in use are **NOT SPECIFIED** — confirm with the
team lead before creating the first branch.

---

## 11. Security

- Enforce authentication and role-based authorization on the **server**, on every protected
  endpoint. Hiding a button is not authorization.
- An approver may only act on requests where they are the listed superior.
- Reports are restricted to Manager and above.
- Passwords: hashed with a modern algorithm, never logged, never returned by an API, never
  stored or compared in plain text.
- Validate and sanitize all input; use parameterized queries / ORM parameter binding.
- No secrets in source control or in the frontend bundle; use configuration/environment.
- Do not weaken or bypass authorization "temporarily" to make development easier. If a check
  blocks you, that is the design working — get the correct test data instead.

---

## 12. AI Feature

The course brief requires at least one **user-facing** AI feature in the product, distinct
from using AI tools to write code.

**The specific AI feature for this project is NOT SPECIFIED in any project document.**
Do not choose one, prototype one, or assume one. Escalate to the team lead and instructor.

When the feature is defined and approved, implement it under these rules:
- Isolate AI functionality behind a service/abstraction; keep it out of core domain logic.
- Validate all inputs sent to the AI provider.
- Handle failures, timeouts, rate limits, and malformed responses gracefully — the page must
  degrade, not crash.
- Never hardcode API keys; load from configuration/environment and keep them server-side.
- Never expose provider secrets or raw provider errors to the client.
- Apply the same authorization and validation controls to AI output as to any other data
  before it is stored or displayed.

---

## 13. AI Usage Reporting

The project requires an AI usage report at `docs/AI-Usage-Report.md` (not yet present in the
repository — create it when first needed). Missing or falsified AI reporting is a zero-point
trigger in the course rubric.

Record meaningful AI-assisted work: what was asked, which tool, what was generated, what the
developer changed or verified.

**Never fabricate** prompts, dates, results, screenshots, tests, or AI contributions. An
honest "AI drafted this, I rewrote the validation logic" is worth more than an invented log.

---

## 14. Output Expectations

Every completed AI task reports:

1. **What was implemented**
2. **Files changed** (full list)
3. **APIs added or modified** (route, verb, payload, status codes)
4. **Database changes** (entities, fields, migrations)
5. **Tests performed** — only those actually executed, with results
6. **Whether the wireframe was followed** — and any deviation, with reason
7. **Architecture concerns**
8. **Shared files modified**
9. **Remaining limitations, TODOs, and assumptions made**

Never claim functionality or test results that were not verified.

---

## 15. Restrictions

Do **not**:
- Rewrite the project, or large parts of it, without permission
- Change the architecture unnecessarily
- Introduce technologies, frameworks, or dependencies without approval
- Invent requirements, business rules, database entities, fields, or APIs
- Modify pages outside the assigned scope
- Perform broad refactoring during a page task
- Delete existing functionality without permission
- Commit or push automatically
- Silently resolve a conflict between documents
- Report unverified work as verified
