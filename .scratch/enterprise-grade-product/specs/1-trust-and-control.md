# Bundle Spec — Trust & Control

Status: draft — synthesis of resolved decisions; no new architecture.
Source tickets: [03](../issues/03-authentication-two-sided.md) · [05](../issues/05-untrusted-input-threat-model.md) · [12](../issues/12-namespace-rbac-grant-model.md) · [13](../issues/13-change-request-model.md) · [14](../issues/14-bootstrapping-and-lockout.md)

## 1. Capability

Every change to live behaviour passes through **authenticate → authorize (namespace grant) → govern
(approval gate) → publish**, and the system can always recover from a gate it can no longer satisfy.
The governing principle is **layered recovery**: each control's failure is escaped from the layer
beneath it, bottoming out at infrastructure access — so "ungovernable" is impossible while someone can
redeploy.

## 2. SDK surface

### Authentication (03)
- `MapMotivRules` applies `.RequireAuthorization()` to the whole endpoint group. Opening it needs an
  explicit, greppable `AllowAnonymous()` at the call site — **secure by default**.
- Identity is ASP.NET `ClaimsPrincipal` at the edge; **no new identity abstraction** for adopters.
- The entire HTTP surface is *authoring* and authenticated. Production evaluation is **in-process** (no
  endpoint evaluates a named live rule). `/evaluate` tests an arbitrary draft document only.

### Authorization / RBAC (12)
- Verbs `read → author → publish` (a ladder: publish⊃author⊃read) over **dotted namespace prefixes
  shared by rules and propositions**.
- **Grant-only, prefix-covering** (`pricing` covers `pricing.eu.vat`), **no deny rules**.
- **Unfiltered read / filtered write**: everyone composes from the whole catalogue; grants gate only
  where a rule *lands*. The write grant is a function of the artefact's own name, never of what it
  references — so the evaluator stays off the read/`/catalog` path.
- `IGrantSource` yields a principal's grants; the **grant evaluator** `(ClaimsPrincipal, verb, prefix)
  → allow/deny` (prefix-covering) lives in `.AspNetCore` on ASP.NET policies.
- `administer` is a *capability of the active source*, present only when the source is mutable.
- `register-spec` is **not** a runtime verb (compile/deploy-time).

### The approval gate (13)
- **`ChangeRequest`** = governance envelope containing 1..N **`ProposedChange`**(Target,
  ProposedDocument?, BaseVersion, Classification); **publish is atomic across all** its changes.
- Gate = a Motiv **`may-publish` Policy** over `ChangeRequest`; an *unsatisfied* result blocks and its
  `Justification` explains which conditions failed.
- **Built-in spec catalogue** (all scalar-parameterised or nullary, so `RuleParameterResolver` already
  covers them): `in-namespace(prefix)`, `target-is-proposition`, `approver-count-at-least(n)`,
  `author-is-approver`, `approver-has-role(role)`, `is-rollback`, `is-creation`, `is-deletion`,
  `is-metadata-only`, `touches-async-spec`.
- **Maker-checker** = `approver-count-at-least(1) & !author-is-approver` — a composition, not a grant.
- Classification is **derived from the diff** except stored intent (`is-rollback` + source version).
- Covers propositions (an admin may gate them stricter); **not** spec-registration.

### Bootstrapping & lockout (14)
- **Gate reconfiguration is an `administer` action** (authorization layer), *not* a `may-publish`
  action — so the gate never governs itself. No new mechanism, no model hole, same editor.
- **Lockout pre-check**: evaluate a candidate gate against a *synthetic maximally-approvable*
  gate-change; refuse publish (with `Justification`) if even that is blocked. Sound-but-incomplete
  (general SAT undecidable with arbitrary/async specs) — a footgun-catcher, not a proof.
- **Gate restricted to synchronous specs**, enforced at bind time (`IsAsync` is bind-visible).
- **Cold start**: a config-designated bootstrap identity (subject or IdP claim) elevated to
  `administer` **only while no admin grant exists** — a conditional seed, not a standing superuser.
- **Break-glass**: a deploy-time config flag disabling the gate, loud + continuously warned, every
  publish under it **audit-stamped**; an infra-layer privilege above any in-app grant; recommend
  time-boxing.

### Structural caps at the untrusted edge (05 → detailed in the Operability bundle / ticket 19)
- `MaxCompositionDepth` refuses over-deep documents at parse time (returns 400, not a crash). The
  uncatchable-crash class is closed structurally by ticket 19's stack-safe traversal.

## 3. App surface (`Motiv.Studio`)

- **Fail-closed dev identity** (08): refuses to start unless explicitly enabled, warns continuously,
  never the default in a release image; supplies the principal so `.RequireAuthorization()` and
  `docker compose up` coexist.
- **Dev single-user `IGrantSource`**: grants the dev principal everything and *evaporates when the
  switch is off* (not a persisted seed).
- **App-owned grant store** (default: mutable, queryable) + **IdP-claims source** (claims→prefix
  mapping in app config).
- OIDC via `docker compose --profile auth up` (Keycloak), **e2e-tested**.
- Admin surface renders only for a mutable grant source.

## 4. Invariants (must hold)

- No endpoint evaluates a named live rule — execution stays in-process.
- The gate never governs itself (administer governs gate config).
- The app-owned grant store enforces **"cannot remove the last `administer`."**
- Dev identity, dev grants, and break-glass are each **fail-closed and loud** — anything enable-able by
  omission is a default-credentials vulnerability.
- Every publish made under break-glass is audit-stamped.

## 5. New machinery to build

- `IGrantSource` + the namespace-grant evaluator + three implementations (app-owned, IdP-claims, dev).
- `ChangeRequest` / `ProposedChange` model + the built-in gate spec catalogue.
- The synthetic maximally-approvable `ChangeRequest` builder (pre-check).
- The bind-time "gate must be synchronous" check.
- A **document structural diff** for `is-metadata-only`.
- Break-glass flag + bootstrap-identity elevation.

## 6. Build sequence

1. `.RequireAuthorization()` + `ClaimsPrincipal` + fail-closed dev identity (03/08) — the floor.
2. `IGrantSource` + evaluator + app-owned store + dev source (12).
3. `ChangeRequest` + `may-publish` gate Policy + built-in specs (13).
4. Lockout: administer-gated gate config, pre-check, break-glass, bootstrap (14).

## 7. Verification obligations

- **e2e of the authenticated path** (Keycloak profile) — this session's two bugs both lived in
  untested seams; a documented-but-unexercised OIDC path would be the third.
- A gate referencing a nonexistent role is refused at publish (pre-check fires).
- A break-glass publish appears in the audit trail with its marker.
- A release-tagged image refuses to start on the dev identity unless explicitly enabled.
