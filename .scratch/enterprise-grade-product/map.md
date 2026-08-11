# Enterprise-Grade Product

Wayfinder map. Charted 2026-08-03.

## Destination

A **locked set of architectural decisions** plus **one implementable spec per capability bundle**
(Trust & control · Durability & data · Operability & evidence · Surface quality) and a sequenced
roadmap — enough that someone can build Motiv's rules SDK and a flagship self-hosted
rules-governance app on top of it **without further architectural discovery**.

Nothing is built on this map. Reaching the destination means the way is clear, not that the road
has been walked.

### ✅ Destination reached — 2026-08-11

All **22 tickets resolved**; all four bundles closed. The locked decisions are the tickets in
`issues/`; the four implementable bundle specs are in [`specs/`](specs/README.md). What remains is
building, not deciding — plus three named follow-ups (the telemetry-PII opt-out, `JustificationTree`'s
fate under ticket 06, and promoting the `ChangeRequest` domain model to a repo `CONTEXT.md` + ADR).

## Notes

**Domain.** Motiv's rules stack: `Motiv.Serialization` (JSON rule documents ⇄ specs),
`Motiv.Serialization.AspNetCore` (minimal-API endpoints), `@motiv/rules-core` (headless TS: schema,
DSL, validation, client), `@motiv/rules-react` (hooks + trees), `ui/apps/demo` (the SPA), and
`src/examples/Motiv.RulesEngine.Sample` (the host).

**Skills every session should consult.** `/grilling` and `/domain-modeling` by default;
`/research` for the research tickets; `/prototype` where the question is "how should this look or
behave"; `/codebase-design` when a ticket is about where a seam goes.

**Standing constraints — every ticket inherits these.**

| | |
|---|---|
| Product | SDK **and** flagship app together. The packages are the adoptable artefact; the app proves them |
| Users | Two personas with roles — engineers author the guardrails, analysts compose within them |
| Licence | MIT, public, enterprise-*ready*. Not commercial |
| Deployment | Self-hosted, single-tenant, customer's IdP — with tenancy-shaped seams so multi-tenant stays additive |
| Two-sidedness | Every capability lands as an **abstraction in the SDK** + a **reference implementation in the app**. This is the rule that makes this map longer than a single-track one |
| Authz unit | **Namespace prefix** (`pricing.*`, `fraud.*`), reusing the dotted-name projection `namespaceTree.ts` already builds |
| Approval | Required, but **admin-configurable**, and expressed as a **Motiv rule over a `ChangeRequest`** — the product governs itself with its own engine |
| Decision log | **Opt-in per rule; total when on.** Sampling is worthless for "why was *this* customer declined?" |

**Measurements taken while charting** (facts later tickets lean on):

- `ui/apps/demo/src` is **6,819 lines**; `packages/rules-core/src` is 2,079; `packages/rules-react/src`
  is **218**. The demo is 3× the SDK it consumes. The packages currently draw the line at
  *protocol and state*; the whole authoring experience is app-side.
- `IPropositionStore` is a public, pluggable persistence seam. `RuleSet` is a `sealed class` over a
  private `Dictionary<string, RuleBase>` with **no store interface at all** — the thing an enterprise
  governs is the one thing that cannot survive a restart.
- `IPropositionStore` is **synchronous by design**: implementations are "called while the publish
  lock is held, so they must be quick". A database-backed store collides with this head-on.
- **Saving *is* publishing.** A `PUT` validates, binds, and hot-swaps atomically. There is no
  server-side draft.
- `BindingScope` / `ScopeClaim` already guarantee that republishing a proposition rebinds every rule
  referencing it — **all or none**. Any durable store must preserve that. **Corrected by ticket 02:**
  a rebind re-binds a rule's *existing* document, so it never writes a rule row — the rule and
  proposition stores are never written in the same transaction.
- `PropositionSet` already has a **quarantine** concept for stored rows that no longer bind:
  *"quarantine exists so a bad row costs its own row"*. Rules inherit it.
- There is **no authentication on any endpoint** today.

## Decisions so far

<!-- one line per closed ticket: enough to judge relevance, then open the ticket for the detail -->

- [Does `RuleSet` get a persistence seam?](issues/02-rule-persistence-seam.md) — **Yes.** `IRuleStore`
  in `Motiv.Serialization` beside `IPropositionStore`; two symmetrical stores, never written in the
  same transaction. Record is a head row `(Name, Version, DocumentJson?)` — `Version` persisted in its
  own right because `Revert` moves it forward while nulling the document, and `null` means "on the
  compiled default at this version". A stored document that no longer binds is **quarantined**, with
  fail-fast left to the host as policy; falling back to the compiled default was rejected because a
  silent revert to unapproved behaviour is indefensible under an approval gate. A stored document
  always beats the compiled default — and a code-defined rule tracks code with no version bump, which
  is unfixable (delegates cannot be fingerprinted), so the decision log must pin the build.
- [Inventory the demo's UI for promotion](issues/01-demo-promotion-inventory.md) — **65% product-generic**
  (4,459/6,819). Excluding stylesheets only **4.6% of TypeScript is genuinely demo-specific**. Model
  coupling is a *single constant* (`MODEL_TYPE = 'customer'`) in five files; `Order` and the rule names
  appear nowhere in `src/`. The real blockers to promotion are unscoped class names against a 1,537-line
  global stylesheet, and 1,058 lines behind six `@codemirror/*` packages — though completion and lint
  are **type-only** CodeMirror consumers and `dslTokens.ts` proves a CM-free highlighter is possible.
  Verdict on the 218-line question: **small because nothing pushed on it** — `RuleTree`, the flagship
  component `@motiv/rules-react` exports, is imported by nothing in the demo.
- [npm scope ownership — is `@motiv` ours?](issues/22-npm-scope-ownership.md) — **No.** The `@motiv`
  scope is held by a third party, dormant with zero published packages, and npm has no org-dispute
  process — treat it as unreclaimable. (Unscoped `motiv` v1.0.1 also exists but is a different
  namespace and irrelevant.) Renaming to **`@motiv-rules/core`** and **`@motiv-rules/react`**, chosen
  from a fully-available field. A scope beats unscoped names because ticket 07 makes companion
  packages likely. Costs nothing — neither package was ever published. **Org created 2026-08-08; the
  scope is held.** No reserving publish is needed: a scope is claimed by owning the org, not by
  publishing — as `@motiv` itself proves, holding zero packages. First publish should *follow*
  ticket 06 and the ticket 07 promotion, not precede them. NuGet `Motiv` is unaffected.
- [SDK public-API stability and semver policy](issues/06-api-stability-policy.md) — **Only `Motiv` is
  published** (v8.0.0, 22 versions); `Motiv.Serialization`, `.AspNetCore`, `Analyzer`, `CodeFix` and
  both npm packages have **never shipped** — so every breaking change this map has taken costs
  nothing, and tickets 03 and 09 are corrected accordingly. **Two version trains**: `Motiv` keeps v8;
  the rules stack gets its own tag prefix at 0.x, so its deliberate churn stops dragging the core's
  major. **Curate `rules-core`'s barrel before ticket 07's promotion** — `export *` publishes 100
  unchosen symbols today and would auto-publish everything promoted. Approved-API snapshots for both
  ecosystems. **LTS-only TFMs**: drop the already-EOL `net9.0`, add `net8.0` to AspNetCore behind
  `#if NET9_0_OR_GREATER` (the client already degrades when the catalog lacks `modelTypes`), keep
  `netstandard2.0`. HTTP: **pin the schema `$id`** — it points at `main`, so a file named `v1` is
  mutable — fix the `/api/rules/rules` stutter and the `DELETE`-that-reverts now; defer `/v1` paths
  to 1.0. `RuleTree` removed, not deprecated.
- [Authentication — what does the SDK owe, and what does the app wire?](issues/03-authentication-two-sided.md)
  — **The whole HTTP surface is authoring, so it is all authenticated.** There is no machine execution
  path: no endpoint evaluates a *named* rule, and the sample runs rules **in-process** via DI.
  `POST /evaluate` tests a draft, nothing more. `MapMotivRules` becomes **secure by default** with a
  visible, greppable opt-out — affordable only because ticket 08's dev identity supplies a principal,
  so `docker compose up` still works. `ClaimsPrincipal` at the edge; authorization in the AspNetCore
  package; ticket 12 designs a namespace-grant evaluator over a principal, **not** a new identity
  abstraction. **Tenancy is a separate seam** — it selects the `BindingScope`, identity authorizes
  within it. OIDC demonstrated via an opt-in Keycloak compose profile, **e2e-tested**, because this
  session's two bugs both lived in untested seams. Corrects ticket 19's premise.
- [New app, or evolve the demo?](issues/08-new-app-or-evolve-demo.md) — **Evolve in place.** The demo
  becomes the flagship app, renamed **`Motiv.Studio`** (`src/Motiv.Studio`, `ui/apps/studio`), out of
  `src/examples/`. The forcing-function case for a second app does not survive ticket 07: it buys
  detection of one failure (`RuleTree`), and a second full UI is a very expensive detector for
  something that becomes greppable once the boundary is behaviour — **logic leaves a trace, an unused
  component does not**. A **fail-closed dev-mode identity** keeps `docker compose up` working with no
  IdP, so no separate demo is needed — the product is the evaluation surface. The backend does not
  split (285 lines, one host). Gap to close deliberately: `src/examples/` loses its only hosted
  rules-engine example.
- [Where does the SDK/app boundary sit?](issues/07-sdk-app-boundary.md) — **Headless behaviour: the
  packages own the logic of authoring and render nothing.** The codebase had already run the
  promote-a-component experiment — `RuleTree` — and its only consumer rejected it for 292 lines of its
  own recursion. Scope is **domain *and* workflow** (the 574 lines of optimistic save, 409 recovery and
  blast-radius reporting), split across separate entry points so an adopter can take document logic
  without inheriting session opinions. CodeMirror is handled by **neutral shapes** — the packages
  declare their own completion/diagnostic/token-run types and take no CM dependency even at the type
  level; `dslTokens.ts` already proves it works. Packaging: framework-free logic → `rules-core`,
  bindings → `rules-react`, workflow → a subpath. **The styling blocker evaporates** — it only bit if
  components were promoted. Excluded: `shell/`, the CodeMirror editor integration, and `RuleTree`
  itself, which is now inconsistent with the boundary → ticket 06.
- [Multi-instance: whole-rebuild refresh and change notification](issues/20-multi-instance-refresh.md)
  — **Snapshot isolation within a replica; eventual consistency across replicas, made detectable.**
  Cross-rule straddle is *incoherence*, not staleness — a combination that never existed — so it is
  ruled out; propositions get it nearly free via the whole-overlay copy-and-swap `PropositionOverlay`
  already documents but `CommitClosure` does not do. `RefreshAsync()` in `Motiv.Serialization`, opt-in
  `IHostedService` poller in the AspNetCore package, polling a cheap store-derived monotonic generation
  and rebuilding only when it moves. That same sequence is the client-facing **fencing token** giving
  monotonic-read consistency. Startup keeps its synchronous `Load()`, so the DI factory wall costs
  nothing. Cross-process write coordination deferred → ticket 21.
- [The store contract under the publish lock — sync or async?](issues/09-store-contract-under-publish-lock.md)
  — **Two-tier exclusion.** An outer `SemaphoreSlim` on `BindingScope` serialises whole operations
  await-safely; the inner Monitor is left untouched for data-structure mutation, because all five
  `Enrol`/`Withdraw` sites are reentrant and a pure swap would self-deadlock at startup. The
  authoring write path goes async — seven breaking signatures, done pre-1.0 while that is cheap. The
  reason is **cancellation** (`WaitAsync(ct)` answers the hung-store problem), not thread-time: the
  critical section is mostly CPU, so async frees ~5 ms of ~45 ms. **Scoped to the authoring path
  only** — ticket 15's `IDecisionSink` must not inherit it. Sub-question 4 spun out as ticket 20.
- [Threat model — the DSL parser and rule binder as untrusted-input surfaces](issues/05-untrusted-input-threat-model.md)
  — **⚠️ CRITICAL, live:** an unauthenticated ~49 KB `POST /api/rules/evaluate` with a flat operand
  array kills the host with an uncatchable `StackOverflowException` (verified: survives k=1625, dies
  at k=1640). The depth guard counts *nesting*; the crash comes from *width* folded left-deep by
  `RuleBinder` and walked by non-tail recursion in `BooleanResultBase`. Also HIGH: higher-order
  amplification, 146 KB → 200 MB in ~14s, no timeout. Client DSL is client-only (LOW). Six
  hypotheses cleared — see the ticket before re-investigating.
  **Exposure corrected by ticket 06:** the remote vector needs `Motiv.Serialization.AspNetCore`,
  which has **never been published** — no consumer was ever remotely exposed, and earlier notes here
  overstated the urgency. But the fix landed wholly in unpublished code while the **root cause sits in
  published `Motiv` v8.0.0**: `UnderlyingAssertionSources` recurses non-tail, so
  `specs.Aggregate((a,b) => a.And(b))` over ~1,000 specs then reading `.Assertions` crashes an ASP.NET
  request thread. A release would **not** fix that — only ticket 19's iterative-traversal rewrite will.
  **Resolved:** the ~19 recursion sites (three families, audited) collapse to **one stack-safe
  traversal the properties delegate to, differential-tested against the current recursive code as a
  perfect oracle** — chosen over recursion-plus-guard because only a stack-safe walk makes every
  property behave identically at depth (guards let fat-framed `Justification` trip before `Assertions`,
  reviving the "what but not why" split), and the oracle test converts the real short-circuit fold risk
  into a checked invariant. `MaxCompositionDepth` kept but re-derived against result-tree size and
  raised. Allocation: plain right-sized buffer, fix `UnderlyingMetadataSources`' missing memoization,
  measure before pooling. One item left to the implementer: a result-size bound in the traversal loop,
  since iteration *raises* the amplification concern by removing the crash that capped it.
- [The namespace-prefix RBAC grant model](issues/12-namespace-rbac-grant-model.md) — **A three-verb
  ladder `read` → `author` → `publish` over a dotted prefix shared by rules *and* propositions.**
  `evaluate` folds (it tests an arbitrary draft, not a named rule — not prefix-scoped); `approve` folds
  into `publish` (maker-checker is ticket 13's *workflow*, not a grant); `register-spec` is **not a
  runtime verb** — it is compile/deploy-time, so the "engineers author the guardrails" persona is
  enforced by who ships the host. Grant-only, prefix-covering, **no denies**. **Read is unfiltered,
  write is grant-gated** (user call): everyone composes from the whole spec catalogue, grants gate only
  where a rule *lands* — so the write grant is a function of the artefact's own name, never of what it
  references, and the evaluator stays off the read/`/catalog` path. Grants come from a swappable
  **`IGrantSource`** (user call): app-owned store by default (mutable, queryable), a shipped
  **IdP-claims** implementation as an alternative (claims→prefix mapping in app config), and a
  **dev-only single-user source** — the authorization twin of ticket 08's dev identity, closing the
  empty-store gap (`docker compose up` would otherwise authenticate a powerless user) as a source that
  *evaporates when the switch is off* rather than a persisted seed that could leak to production.
  `administer` present only when the active source is mutable. Composite sources permitted, not v1.
  Downstream: ticket 13 owns maker-checker over the author/publish split.
- [Rule version history and rollback — what is the record?](issues/10-version-history-and-rollback.md)
  — **An append-only log of immutable version rows, one per published change**, each carrying the
  document plus who/when/why. Grounded: `RuleSet.Revert` *already* moves the version forward, never
  back, so append-only rollback is the codebase's existing semantics generalised — restoring vN writes
  vN+1 (a copy of N), which also *records that a rollback happened*. Version is **both** a permanent
  identity (each number names one immutable row, so ticket 15's "rule as it stood at v5" is stable) and
  the head-is-max concurrency token — no tension. Row:
  `(Name, Version, DocumentJson?, Author, TimestampUtc, ChangeNote?, ApprovalRef?, BuildId?)`; null doc
  = "on the compiled default"; `BuildId` is a build stamp not a hash (delegates are unfingerprintable).
  Kept **forever** (~1,800 docs/5yr is nothing); any future pruning is governed by decision-log
  references. **The fog graduates into three records** — version history (*what it said*) is the spine
  that the **audit trail** (*who did what*, incl. denials/reads — separate, FK in) and the **decision
  log** (15, *what it decided*) both reference; never merged (cardinality, subject, lifecycle all
  differ). **Symmetric across rules and propositions**, and a proposition bump does *not* bump dependent
  rules' versions — which is why replay must pin proposition versions at the *decision* (→15), not the
  rule version. Downstream: 02/16 head row stays the *binding* record with the log alongside (atomic
  append in ticket 09's gate); distinct from ticket 20's store-wide generation. Unblocks 11 and 16.
- [The reference persistence implementation — schema, migrations, backup](issues/16-reference-persistence-implementation.md)
  — **EF Core for the authoring store; the decision log is a *separate* database, never an EF table.**
  Grounded: the existing `Motiv.EntityFramework.Tests` is a red herring (it proves *spec→SQL query
  translation*, not persistence — no store to reuse), and `IPropositionStore` is a **dumb sink**
  (*"validates nothing, enforces no invariants"*) — which decides the schema: no FKs/cascades *encoding
  binding legality* (the SDK owns those), EF as thin typed-SQL + migrations. **But "no constraints" is
  too broad** — three kinds with three risk profiles: **identity/structural constraints are KEPT** (PK,
  unique `Name`, `NOT NULL`, `(Name,Version)` PK — free, portable, the SDK assumes them, and the
  `(Name,Version)` PK doubles as the cross-replica append guard); cross-aggregate/cross-DB FKs omitted
  with compensating controls; semantic invariants caught by **quarantine-on-load** (ticket 02). Head/log
  divergence is *designed out*: `StoredRule` is a slim identity table and current `(Version, Document)`
  is **projected from `max(RuleVersion.Version)`**, not a stored duplicate — deciding the head-vs-projection
  latitude and refining ticket 02's head row. Dapper weighed and
  lost on provider-agnosticism + adopter familiarity (change-tracking is irrelevant at human write-rate).
  **Three providers** (SQLite dev / Postgres / SQL Server), document stored as **portable `text`, not
  native `jsonb`** — the sink never queries *into* the document, so native JSON is dead weight that would
  fork the schema per provider. Authoring `DbContext`: `StoredRule`, `RuleVersion`, `StoredProposition`,
  `PropositionVersion`, `Grant`, single-row `StoreGeneration` — head + version append + generation bump
  in **one transaction**. Migrations: the **ASP.NET Identity pattern** — derivable `DbContext`,
  adopter-owned migrations, so custom columns never conflict and an SDK field addition breaks at
  *compile time* (loud, not silent). Ships as `Motiv.Serialization.EntityFrameworkCore` (0.x, ticket 06).
  SQLite `EnsureCreated` bootstrap for zero-config `compose up`; a one-way **propositions-only** importer
  from `JsonFilePropositionStore`. DR hazard documented: a restore must not move the generation backward
  while replicas live (breaks ticket 20's fencing token). Provisional pending 11/13: `Draft`,
  `ChangeRequest`. Feeds the health-probes fog (readiness = store answers `GetGenerationAsync`).
- [The draft/published split — replacing save-is-publish](issues/11-draft-published-split.md) — **The
  split *factors* save-is-publish, it does not replace it.** Today `PUT` does two atomic things (persist
  the edit, swap the live rule); the split names them `author`/`publish` so a gate can slot between, and
  **with no gate configured they re-fuse into today's behaviour — save-is-publish is the degenerate case
  of the general model, and the dev-mode default** (single superuser, no gate). Grounded: a draft
  already exists *client-side only* (the code names *"an editor's open draft"*); this makes it durable
  and server-side. **Drafts are mutable, in their own `Draft` table; only publish mints an immutable
  version** — refining ticket 10's "draft = unpublished version" hint (immutability is for *published*
  versions; freezing keystrokes bloats the log and gaps version numbers), and keeping ticket 16's
  projection filter-free. States: status enum `Draft/Published/Superseded` + reserved `Approved/Rejected`;
  v1 operates two-state (approval publishes immediately, per 12). **Live path protected by construction**
  — drafts never bind, so evaluation can't see them and a draft save can't tear a result. Drafts
  evaluated via `/evaluate` (against *published* references; draft-vs-draft is 13's coordinated change).
  **Many concurrent drafts per artefact, single published head**; `409`/`baseVersion` resolves at
  *publish* time ("rebase your change"). Downstream: 13 owns ChangeRequest/gate/scheduled-release; 16's
  Draft table confirmed (mutable, keyed per-change). **Unblocks 13.**
- [The `ChangeRequest` model and its built-in approval specs](issues/13-change-request-model.md) —
  **A `ChangeRequest` is the governance *envelope* around one-or-more `ProposedChange`s; the gate is a
  Motiv `may-publish` Policy over it, so a blocked publish explains itself via `Justification`** — the
  product governs its own changes with its own engine. Domain-modeling sharpening: the coordinated-change
  scenario (add a proposition *and* the rule using it, publish atomically) forces ChangeRequest to be
  1:many over artefacts, collapsing ticket 11/16's provisional `Draft` into a `ProposedChange` child —
  one noun, not two. Approvals accumulate (`(approver, timestamp)`); Rejection is a terminal transition;
  the ChangeRequest workflow status is distinct from the version-row status (10/11). Classification is
  **derived from the diff** (`IsCreation/IsDeletion/IsMetadataOnly/TouchesAsyncSpec`) except stored
  intent (`IsRollback`+source). Built-in specs are all scalar/nullary so `RuleParameterResolver` covers
  them; **maker-checker = `approver-count-at-least(1) & !author-is-approver`** (12's deferral lands here).
  `may-publish` (not `requires-review`) so refusal carries the reasons. **Default gate permissive** —
  preserves hot-swap *and* is the only lockout-safe seed (→14); auth still locked (03/12), only approval
  ceremony is opt-in. Covers **propositions** (higher-stakes, gate stricter) but **not spec-registration**
  (12: deploy-time, not runtime). SDK owns model+specs; app owns active-gate config; the gate is itself a
  governed rule → lockout hazard to **14**. New machinery: a document **structural diff** for
  `is-metadata-only`. Offered (plan-only): a `CONTEXT.md` entry + an ADR. **Unblocks 14.**
- [Bootstrapping and lockout for a self-governing gate](issues/14-bootstrapping-and-lockout.md) —
  **Every lockout surface is recovered from the layer beneath it, bottoming out at infrastructure
  access** — so "ungovernable" is impossible while someone can set an env var / redeploy. The
  self-governance paradox **dissolves**: reconfiguring the gate is an **`administer` action
  (authorization layer)**, not a `may-publish` action (workflow layer), so the gate never governs
  itself — no new mechanism (administer already exists, 12), no model hole, same editor. A
  **sound-but-incomplete pre-check** evaluates a candidate gate against a *synthetic maximally-approvable
  gate-change* and refuses if even that is blocked (general SAT undecidable with arbitrary/async specs,
  so a footgun-catcher not a proof — the engine detecting its own lockout). **Gate restricted to
  synchronous specs, enforced at bind time** (`IsAsync` is bind-visible) — authoring availability must
  not depend on an external system; distinct from 13's sync `change.touches-async-spec` predicate. Cold
  start: a **config-designated bootstrap identity** (subject or IdP claim) elevated to administer *only
  while no admin grant exists* — a conditional seed, not a standing superuser; open first-run flow
  rejected (08's default-credentials hazard); dev covered by 12's dev single-user source. Break-glass: a
  **deploy-time config flag** disabling the gate, loud + continuously warned + every publish stamped in
  the audit trail (10), living at the infra layer above any in-app grant; recommend time-boxing.
  Downstream: 12 needs a "cannot remove the last administer" invariant; new machinery = the synthetic
  ChangeRequest builder + the bind-time sync-gate check. **Closes the governance spine (10→11→13→14).**
- [The decision log — record, retention, PII, and the write path](issues/15-decision-log-record.md) —
  **Opt-in per rule via an `audited` flag *on the rule document*** (versioned + governed) — which
  *discharges ticket 02's "audited ⟹ stored document" for free*, since a compiled-default rule has no
  document to hold the flag, so marking it audited transcribes a stored one. Grounded: the existing
  `RuleEvaluationResult` payload carries only the *outcome* — the envelope (version, build, caller,
  timestamp, correlation) is new; nothing exists yet. A record pins behaviour with **three anchors** —
  stored document + `BuildId` (02: compiled specs unfingerprintable) + **referenced proposition
  versions** (10's replay pin lands here, a property of the evaluation). **Input is an adopter-chosen
  seam** — `StoreWhole` (dev) / `Redact` / **`ReferenceOnly` (GDPR-clean prod default)** — never a
  silent whole-model default (the 08 default-credentials trap applied to PII); the strategy sets the
  replay ceiling. Off the hot path via **`IDecisionSink`** (SDK interface + default bounded-channel
  sink; app writes raw-append to the *separate* decision DB per 16), **`FailClosed` by default** (an
  audited decision that wasn't logged didn't happen); `Drop` allowed but **never silent** — gap-marker +
  telemetry (04). **Retention mandatory** (unbounded otherwise), adopter-set, background purge. Graduates
  the *replay* and *audit-vs-version* fog patches; this is the *decision* record of 10's three-records
  model. New machinery: the `audited` field, `DecisionRecord`, input strategies, the channel sink, the
  purge job.
- [Cross-process write coordination — moving optimistic concurrency into the store](issues/21-cross-process-write-coordination.md)
  — **Ticket 16 already built the fix; this ticket is a deletion plus a mapping, not a coordination
  subsystem.** 16's head-as-projection means publishing v6 is an `INSERT RuleVersion(Name, 6, …)`, so
  the `(Name, Version)` PK **is** the cross-process compare-and-set: two replicas at v5 both compute
  next=6, the PK lets one win and rejects the other → the lost update (two publishes both claiming v6)
  becomes impossible and the audit is correct (one v6, one rejected attempt). Grounded: the CAS is
  `Interlocked.CompareExchange(ref _state,…)` at `Rule.cs:170`/`AsyncRule.cs:175`, blind to the other
  replica. **The dumb-sink tension dissolves** — the PK is a *structural* constraint (16 kept it, not
  semantic legality), so the store still validates nothing semantic. **Ordering survives** — the
  conflict surfaces at persist, before mutate/commit (09's fallible-prefix). **Remove the in-memory
  CAS** (redundant intra-process via 09's gate, blind inter-process). Record unchanged; `SaveAsync`
  gains the **existing `VersionConflict`** outcome (CAS-produced → PK-produced). Same pattern for
  propositions, independent (02: never co-written). **Optimistic beats a write-lease** (bottleneck +
  failover machinery for a low-write-rate product). 20's store-wide generation is complementary
  (skew/refresh), not the per-rule conflict key. **Closes the Durability & data bundle and the last
  architecturally-open ticket.**
- [The OpenTelemetry contract — what does the SDK emit?](issues/04-opentelemetry-contract.md) —
  **Premise stale: core evaluation telemetry already ships in published `Motiv`** — a `motiv.evaluate`
  span (one per top-level eval, tags proposition/satisfied/reason/assertions), `motiv.evaluations` +
  `motiv.evaluation.duration` instruments, `System.Diagnostics.DiagnosticSource` (in-box, not the OTel
  SDK), source+meter both `"Motiv"`, zero-alloc when unsubscribed. So the ticket ratifies + freezes core
  as contract and adds what it lacks. **Structural decision mirrors 06: two surfaces on two trains** —
  frozen core `Motiv`, and a new **`Motiv.Serialization`** source/meter for `motiv.rules.*` (bind
  failures, publish_conflicts=21's 409s, store.duration=09, catalog.size, generation/replica_lag/
  refreshes/rebuild.duration=20, decisions.dropped/queue.depth=15's promised backpressure visibility,
  break_glass=14). **Live finding:** the shipped span emits `motiv.reason`/`motiv.assertions`
  *unconditionally* (`EvaluationScope.cs:64`) — author-templated assertions can carry PII into traces in
  published v8/v9 with no opt-out; the one *additive* core change is a PII control (`full`/`reason-only`/
  `none`) **coupled to 15's redaction posture — decided once**. Granularity: one span/eval default;
  per-node opt-in via `audited`, but the tree's durable home is the decision log (15). Sub-4 answered by
  shipped code. App adds: readiness=`GetGenerationAsync` (16); authoring→evaluation correlation via
  `motiv.rules.name`/`version` tags in the rules-stack layer. **Closes the Operability & evidence
  bundle.**
- [Do the packages owe a non-React story?](issues/17-non-react-story.md) — **A two-runtime story, and
  both cores already exist.** Grounded (sub-1 verified): `editor.ts`'s store is genuinely framework-free
  — `subscribe`/`getState` with no `useSyncExternalStore` caching baked in, no `react` import; the React
  adaptation lives in the 218-line adapter. The DSL/schema exists in **both** TS (`rules-core`) and C#
  (`Motiv.Serialization`), already synced by 06's pinned schema `$id`. So: **React is the supported JS
  adapter; Vue/Svelte are cheap DIY (~200 bindings-only lines) on the verified-neutral core — one second
  adapter optional as a credibility signal; .NET/Blazor uses `Motiv.Serialization` directly (no
  rules-core needed) — the better fit for the actual buyer.** Web components rejected (07 headless + taxes
  the React consumer). The deliverable is the honest support-tier table (sub-4: the illegitimate thing is
  leaving it undocumented).
- [The accessibility target, and what enforces it](issues/18-accessibility-target.md) — **WCAG 2.1 AA
  floor; a VPAT is the procurement deliverable.** Grounded: `axe-core` is *not* wired up (a gap to close,
  not ratify). **The key move: Motiv's generated `Reason`/`Justification` text IS the accessible
  description of the composition** — so the accordion builder is **not `role=tree`** (wrong model for an
  editing surface with interactive nodes) but nested labeled groups + disclosure, with the explainability
  output carrying "understand the structure". CodeMirror a11y **inherited not invented**; command palette
  via `listbox`/`activedescendant`. Enforcement: **`axe-core` in Playwright (mechanical ~50%) + a
  required manual screen-reader pass** (focus order, announcement quality, labels on generated content) —
  VPAT from both. SDK carries none (07 headless — stated honestly as a cost); `JustificationTree` is the
  read-only tractable exception and the lone package-inherited a11y case *if* it survives 06. **Closes
  the Surface quality bundle.**

## Not yet specified

<!-- in-scope fog: real, but not yet sharp enough to phrase as a question -->

- **Environments and promotion** — whether dev/staging/prod is a first-class axis or an adopter
  concern. Hangs on the draft/published split and the RBAC grant model.
- **Audit trail vs version history** — one record or two? Version history answers "what did the rule
  say?"; audit answers "who changed it and why?". They may be the same table or deliberately not.
- **Replay against historical rule versions** — re-running a past decision against the rule as it
  stood. Needs the decision-log record and the version record to exist first.
- **Where the tenancy seam sits relative to `BindingScope`** — it cannot be a filter bolted onto
  reads, or an all-or-none rebind spans tenants. Needs the store contract settled.
- **Theming, white-label, i18n** — shape depends entirely on whether the app is new or the demo grown.
- **Docs, adoption, and the upgrade path** — needs the API stability policy and the SDK/app boundary.
- **Health/readiness probes, configuration, and secrets** — small, but shaped by the reference
  persistence implementation.
- **The four bundle specs themselves** — the destination artefact. Nearly every ticket blocks these;
  they graduate last.

## Out of scope

<!-- ruled beyond the destination; never graduates -->

- **Licensing, entitlements, pricing, support tiers, open-core boundaries** — the product stays MIT
  and enterprise-*ready* rather than commercial, so go-to-market is a different effort.
- **SaaS hosting and multi-tenant isolation** — deployment is self-hosted single-tenant. Only the
  *seam* is in scope; per-tenant isolation, encryption, and noisy-neighbour limits are not.
- **Building any of it** — the destination is decisions plus specs. Implementation is downstream.
