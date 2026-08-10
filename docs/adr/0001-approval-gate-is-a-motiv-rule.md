# The approval gate is a Motiv rule over ChangeRequest

Status: accepted

Motiv's rules stack needs an admin-configurable approval gate that decides whether a change to a rule or
proposition may be published. We express that gate as a Motiv single-value `may-publish` Policy evaluated
over a `ChangeRequest` model — rather than as bespoke approval configuration — so the product governs its
own changes with its own engine, and a blocked publish explains itself through the same `Justification`
the product exists to provide. Admins compose the gate from a small catalogue of built-in specs
(`change.approver-count-at-least(n)`, `change.author-is-approver`, `change.in-namespace(prefix)`, …).

## Considered options

- **A bespoke approval-config DSL or rules table.** Rejected: it would reimplement — worse — the
  composition and explanation Motiv already does, and forfeit the dogfooding that makes the gate
  credible.
- **A fixed, non-configurable approval policy.** Rejected: enterprises need per-namespace approval rules
  (stricter for `fraud.*` than for `docs.*`), which a fixed policy cannot express.

## Consequences

- **The gate can govern changes to itself** — a genuine lockout hazard. Resolved by making gate
  *reconfiguration* an authorization act (the `administer` grant), not a `may-publish` act; a permissive
  default gate; a sound-but-incomplete lockout pre-check (evaluate a candidate gate against a synthetic
  maximally-approvable change and refuse if even that is blocked); and a deploy-time, fully-audited
  break-glass as the floor.
- **The gate must be synchronous** — an async spec would couple publish availability to an external
  system and defeat the pre-check. Enforced at bind time.
- **`register-spec` is out of scope** — registering a spec is a compile/deploy-time act, not a runtime
  ChangeRequest, so it never passes through the gate.
