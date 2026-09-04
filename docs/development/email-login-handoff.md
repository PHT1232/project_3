# Sign in by email address — Implementation Handoff

> Written 2026-09-04 on branch `khang`. Adds email as a second sign-in identifier alongside the
> employee number. Small change, but it is in the authentication path, so **two reviewers**
> (CLAUDE.md §5).

## 1. Scope note — this is an addition to the Plan, not an implementation of it

The Plan is explicit that the employee number *is* the login:

- §3.1 field table: "`EmployeeNumber` 1–1000, primary key **and** login" `[SPEC]`
- §4.2: `POST /api/v1/auth/login` — "Employee number + password → JWT + profile"
- M1 acceptance: "Login with employee number + password returns a JWT valid for 8 hours"

Signing in by email is **not** in the Plan. It was requested directly by the product owner on
2026-09-04. It is safe to add because ASP.NET Identity is configured with
`options.User.RequireUniqueEmail = true` (`WebApi/Program.cs`), so an address maps to exactly one
account — but it should be called out in the PR and, if kept, written into the Plan's revision
history the way K7 and K8 were, rather than left as an undocumented divergence.

## 2. How it works

```
Login page ── "Employee number or email" (one field) ──▶ api/auth.js
                                                              │  all digits?
                                                    yes ──────┴────── no
                                              {employeeNumber:N}   {email:"..."}
                                                              │
                                                POST /api/v1/auth/login
                                                              ▼
                                     LoginRequestValidator  (exactly one identifier?)
                                                              ▼
                                                       AuthService
                                          EmployeeNumber ?  VerifyCredentialsAsync
                                                       :    VerifyCredentialsByEmailAsync
                                                              ▼
                                        IdentityAccountAdapter.VerifyAsync  (shared)
                                     IsActive gate → CheckPasswordSignInAsync(lockout: true)
                                                              ▼
                                          AccountProjection → same JWT as before
```

The decision of *which* identifier was used stops at the lookup. Everything after it — the
`IsActive` gate, the lockout counter, the projection, the token, the `/auth/me` payload — is
literally the same code, because both paths call one private `VerifyAsync`. That is the main
design point: email sign-in cannot drift away from employee-number sign-in later.

### Where the split lives

The client decides the wire shape, not the server. `frontend/src/api/auth.js` tests the trimmed
input against a digits-only regex. The rationale: this module already owns the API contract, so
the page and `AuthContext` need no knowledge of it, and the server keeps a typed, unambiguous
request rather than a stringly-typed "identifier" it has to guess at.

## 3. Security posture — the rule that matters

**A 400 means the request was structurally unanswerable. Everything else is a generic 401.**

| Request | Result |
|---|---|
| No identifier at all | 400 |
| Both `employeeNumber` and `email` | 400 |
| Empty password | 400 |
| Employee number that does not exist (including out of the 1–1000 range) | **401**, generic |
| Email that is not registered | **401**, generic |
| Email that is not even well-formed | **401**, generic |
| Right identifier, wrong password | **401**, generic |
| Deactivated account, either identifier | **401**, generic |
| Locked-out account | **401**, generic |

All the 401s are byte-identical apart from ProblemDetails' per-request `traceId`:
`{"title":"Invalid credentials","status":401,"detail":"Those sign-in details are incorrect."}`.
The detail deliberately names neither identifier — it used to say "Employee number or password is
incorrect."

**This cost one iteration and is the most important thing to understand about the change.** The
first version of `LoginRequestValidator` also enforced the Plan's 1–1000 range and an email-format
rule. That looked like helpful validation and was actually a leak: employee number `999999` began
returning 400 while `999` returned 401, which tells an attacker which identifiers are worth
trying. It also broke the pre-existing contract test
`Login_UnknownEmployeeNumber_ReturnsSameGeneric401AsWrongPassword` — that test exists precisely to
catch this, and it did. Both rules were removed. **Do not add "helpful" identifier validation to
the login endpoint.** Range and format belong on user *creation*, where they already are.

### What is deliberately unchanged

- Lockout: `CheckPasswordSignInAsync(user, password, lockoutOnFailure: true)` on both paths, so
  email attempts feed the same 5-attempt / 15-minute counter.
- `IsActive`: checked before the password, on both paths.
- JWT contents, expiry, and the `OnTokenValidated` active-user check.
- `GET /auth/me` and `POST /auth/change-password` still key on the employee number from `sub`.

## 4. Files

| File | Change |
|---|---|
| `Application/DTOs/Auth/LoginRequest.cs` | `(int? EmployeeNumber, string? Email, string Password)`. Both nullable so existing callers sending only `employeeNumber` still bind — 16 call sites across the integration tests, unmodified. |
| `Application/Validators/Auth/LoginRequestValidator.cs` | **New.** XOR on the identifiers, non-empty password. Nothing else, on purpose. |
| `Application/Interfaces/Auth/IAccountStore.cs` | `+ VerifyCredentialsByEmailAsync(string, string)`. |
| `Infrastructure/Identity/IdentityAccountAdapter.cs` | Both public verify methods now delegate to one private `VerifyAsync(ApplicationUser?, string)`. Email lookup via `FindByEmailAsync` (matches `NormalizedEmail`, so case- and whitespace-insensitive without hand-rolled normalisation). |
| `Application/Services/Auth/AuthService.cs` | Validates the request, then branches on the identifier. Constructor gained `IValidator<LoginRequest>`. |
| `WebApi/Controllers/AuthController.cs` | 401 detail no longer names an identifier. |
| `frontend/src/api/auth.js` | `login(identifier, password)` chooses `employeeNumber` vs `email`. |
| `frontend/src/contexts/AuthContext.jsx` | Parameter renamed to `identifier`. |
| `frontend/src/pages/Login.jsx` | One field, `type="text"`, label "Employee number or email", distinct 400 vs 401 messages. |

No database, migration, DI, policy or route changes. `AddValidatorsFromAssemblyContaining` already
picks the new validator up.

## 5. Tests actually run (2026-09-04)

```
dotnet build Project.slnx                 0 errors
dotnet test Project.slnx                  133/133   (unit 53, integration 80)
npx vitest run --pool=threads             104/104   across 18 files
npm run build                             passed
```

New coverage:

- **Unit** (`AuthServiceTests`): email identifier routes to the email path and never touches the
  employee-number one; unknown email returns null like every other failure; malformed requests
  (neither identifier, both identifiers) throw `ValidationException` without touching the store.
- **Integration** (`AuthTests`): email sign-in returns the same JWT and profile; case- and
  whitespace-insensitive; wrong password → 401 whose detail does not mention "password";
  unregistered email returns the same title/detail as a wrong password; deactivated user cannot
  sign in by email; a token obtained by email works on `/auth/me`; ambiguous or identifier-less
  payloads → 400.
- **Frontend**: `Login.test.jsx` covers both identifiers through the one field plus the 400 and
  401 messages; new `api/auth.test.js` pins the wire-shape decision (digits → `employeeNumber`,
  else `email`, whitespace trimmed, bare numbers still accepted).

Manually, against the live API and the running SPA: email login, mixed-case/padded email,
employee-number login, unregistered email, and both-identifiers-at-once all behaved as the table
in §3 says.

## 6. Known issue for the reviewer

**Timing side-channel.** An unregistered identifier returns before any password hashing happens;
a registered one pays for a PBKDF2 verification. The difference is measurable and could in
principle enumerate valid addresses. This is *not* a regression — the employee-number path has had
the same shape since M1 — but email addresses are guessable in a way that employee numbers are
not, so the exposure is more real now. The standard fix is to hash against a dummy password when
the lookup misses, so every request costs the same work. Left undone deliberately: it is a
decision about the M1 auth design, not a detail of this change.

## 7. How to explain it at the whiteboard

> "Login takes either identifier in one field. The browser decides which one it is — digits mean
> an employee number — and the server accepts exactly one. After the lookup, both routes run the
> same verification code, so email sign-in gets the same active-account check, the same lockout,
> and the same token. And every way of failing looks identical from outside: you can't tell an
> unknown email from a wrong password, which is why the validator doesn't range-check or
> format-check the identifier."
