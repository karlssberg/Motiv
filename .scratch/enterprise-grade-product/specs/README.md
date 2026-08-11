# Bundle Specs — the destination artefact

The map's destination: **a locked set of architectural decisions plus one implementable spec per
capability bundle, enough to build Motiv's rules SDK and a flagship self-hosted rules-governance app
without further architectural discovery.**

The 22 resolved tickets in `../issues/` are the locked decisions. These four specs synthesise them into
build-ready form — **no new decisions, only assembly**, each traceable back to its tickets.

| Spec | Bundle | Tickets |
|---|---|---|
| [1](1-trust-and-control.md) | **Trust & Control** — authenticate → authorize → govern → publish, with layered recovery | 03, 05, 12, 13, 14 |
| [2](2-durability-and-data.md) | **Durability & Data** — versioned, async, multi-instance-safe storage behind a dumb-sink seam | 02, 09, 10, 16, 20, 21 |
| [3](3-operability-and-evidence.md) | **Operability & Evidence** — telemetry, the decision log, stack-safe evaluation | 04, 15, 19 |
| [4](4-surface-quality.md) | **Surface Quality** — `Motiv.Studio` on headless packages, non-React story, WCAG 2.1 AA | 01, 07, 08, 17, 18, 22 |

Each spec is structured: capability → SDK surface → app surface → invariants → new machinery → build
sequence → verification obligations.

## Cross-cutting threads (present in more than one spec)

- **Two-sidedness** — every capability is an SDK abstraction + a `Motiv.Studio` reference implementation.
- **Fail-closed & loud** — the dev identity (1), dev grant source (1), and break-glass (1) all refuse to
  enable by omission; the same discipline governs the PII-input default (3).
- **The pinned schema `$id`** (ticket 06) keeps the TS and C# DSL cores in sync — load-bearing for both
  the Blazor story (4) and versioning (cross-cutting).
- **Explainability as affordance** — Motiv's generated `Reason`/`Justification` is reused as the gate's
  refusal message (1), the accessible description of a composition (4), and the trace/decision payload
  (3).
- **Structural constraints, not semantic ones** — the dumb-sink store keeps identity/structural
  constraints (2), and one of them (the `(Name,Version)` PK) delivers cross-process write coordination
  for free (2).

## Known follow-ups beyond the plan

- The **telemetry-PII opt-out** is the one additive code change to *published* `Motiv` (spec 3 / ticket
  04) — its own branch + release.
- Whether `JustificationTree` survives is ticket 06's call and decides if *any* a11y is package-inherited
  (spec 4 / ticket 18).
- Promoting the `ChangeRequest` domain model to a repo `CONTEXT.md` + ADR (ticket 13) is offered but not
  done (plan-only).
