---
title: The Approval Gate
---

The `ApprovalGate` is a **may-publish `Policy`** over a `ChangeRequest` — Motiv governing its own
changes with its own engine, so a blocked publish explains itself through the same `Reason`,
`Assertions`, and `Justification` every evaluation produces (see
[ADR 0001](../adr/0001-approval-gate-is-a-motiv-rule.md)).

## Permissive by default

```csharp
public sealed class ApprovalGate
{
    public const string NoGateConfiguredReason = "no approval gate is configured";
    public ApprovalGate(IGateStore? store = null);
    public string? DocumentJson { get; }
    public GateDecision Evaluate(ChangeRequest change);
    public GateUpdateResult SetGate(string? documentJson, IReadOnlyCollection<string> knownRoles);
}
```

With no document configured, `Evaluate` always returns `MayPublish == true`, and `Reason`,
`Assertions`, and `Justification` all carry `NoGateConfiguredReason` (`"no approval gate is
configured"`). This is the only lockout-safe bootstrap: access is still gated by
[namespace grants](grants.md) — only the review ceremony is opt-in.

## The built-in `change.*` catalogue

`GateSpecs.CreateRegistry()` — the registry every `ApprovalGate` binds gate documents against — carries
ten reusable predicates over `ChangeRequest`, each an unnamed explanation proposition so a refusal reads
as prose rather than a bare `== true`/`== false` suffix:

| Spec | Args | `WhenTrue` / `WhenFalse` |
|---|---|---|
| `change.in-namespace` | `prefix: string` | `"change touches namespace '{prefix}'"` / `"change does not touch namespace '{prefix}'"` |
| `change.target-is-proposition` | — | `"change targets a proposition"` / `"change targets no proposition"` |
| `change.approver-count-at-least` | `n: int` | `"change has at least {n} approvals"` / `"change has fewer than {n} approvals"` |
| `change.author-is-approver` | — | `"the author approved their own change"` / `"no self-approval"` |
| `change.approver-has-role` | `role: string` | `"an approver holds role '{role}'"` / `"no approver holds role '{role}'"` |
| `change.is-rollback` | — | `"change is a rollback"` / `"change is not a rollback"` |
| `change.is-creation` | — | `"change creates an artefact"` / `"change creates nothing"` |
| `change.is-deletion` | — | `"change deletes an artefact"` / `"change deletes nothing"` |
| `change.is-metadata-only` | — | `"change is metadata-only"` / `"change alters logic"` |
| `change.touches-async-spec` | — | `"change touches an async spec"` / `"change touches no async spec"` |

Compose them with the ordinary rule-document operators (`and`, `or`, `not`, ...) exactly as any other
rule document. A gate document **must be synchronous** — a reference to an async spec is rejected at
bind time with `RuleErrorCode.GateMustBeSynchronous`, because an async gate would couple publish
availability to an external system and defeat the lockout pre-check below.

## Maker-checker

The canonical example: publish requires at least one approval, and the author may not approve their
own change.

```jsonc
{
  "rule": { "and": [
    { "spec": "change.approver-count-at-least", "args": { "n": 1 } },
    { "not": { "spec": "change.author-is-approver" } }
  ]}
}
```

Evaluated against a request `alice` authored with no approvals recorded yet, the gate refuses:

```text
MayPublish:   false
Reason:       change has fewer than 1 approvals
Assertions:   ["change has fewer than 1 approvals"]
Justification:
AND
    change has fewer than 1 approvals
```

Only the failing operand appears. De-noising works here exactly as it does everywhere else in Motiv:
the "no self-approval" half of the `and` was satisfied (alice had not self-approved), so it did not
contribute to the refusal — a peer approval flips `MayPublish` to `true` without alice touching
anything else.

## Reconfiguring the gate

```csharp
public enum GateUpdateOutcome { Updated, Invalid, WouldLockOut }
public sealed record GateUpdateResult(
    GateUpdateOutcome Outcome, IReadOnlyList<RuleError> Errors, GateDecision? PreCheck);
```

`documentJson: null` resets to the permissive default — always safe, so no pre-check runs. A non-null
document is validated and bound first (`Invalid` on failure), then run through the lockout pre-check.

### Lockout pre-check

Before a valid candidate document is persisted, it judges
`SyntheticChangeRequests.MaximallyApprovable(knownRoles)` — the friendliest change imaginable: 100
distinct synthetic approvers, each holding every known role, none of them the (also synthetic) author,
proposing a plain edit. If the candidate would refuse even that, `SetGate` returns `WouldLockOut` with
the refusing `GateDecision` in `PreCheck`, and the gate is left unchanged.

This is **sound but incomplete**: it catches the common ways a document locks itself out (an
impossible role requirement, an unreachable approval threshold), but arbitrary predicates make general
satisfiability undecidable, so treat it as a footgun-catcher, not a proof that a document can never
lock out a real change.

## Persistence

```csharp
public interface IGateStore
{
    string? Load();
    void Save(string? documentJson);
}
```

A store is a **dumb sink** — it validates nothing; `ApprovalGate` validates before ever calling `Save`.
If a persisted document has been tampered with or corrupted (fails to bind at construction), the
`ApprovalGate` constructor throws rather than silently falling back to permissive: recovery is an
operational act — fix or clear the stored document, or fall back to [break-glass](break-glass.md) — not
something the constructor can safely paper over.

## Wiring and endpoints

```csharp
builder.Services.AddMotivRules(registry, options)
    .AddGovernance(gateStore); // omit, or pass null, to run without persistence — always permissive
```

Mounted only when `AddGovernance()` was called, under the same secured group:

| Method & path | Grant | Body | Responses |
|---|---|---|---|
| `GET {basePath}/gate` | none (unfiltered read) | — | `200 { document, permissiveDefault }` |
| `PUT {basePath}/gate` | `administer` | `{ document }` | `200 { document, permissiveDefault }`; `400 { errors }`; `422 { reason, assertions, justification }` |
| `DELETE {basePath}/gate` | `administer` | — | `204` (reset to the permissive default) |

The gate never governs its own reconfiguration — `PUT`/`DELETE /gate` are checked against the
`administer` grant at the endpoint boundary, never routed through a `ChangeRequest` and the very gate
being replaced. A gate that had to approve its own tightening could never be locked down further than
its current document already allows.

## No bypass

When governance is enabled, direct writes — `PUT`/`DELETE /rules/{name}` and
`POST`/`PUT`/`DELETE /propositions[/{name}]` — run through the gate too, as a transient, unrecorded
`ChangeRequest` that the ungoverned endpoint's own core then executes if the gate allows it. A refused
direct write gets an extra sentence pointing at the alternative:

```text
{reason}. Raise the edit as a change request (POST change-requests) and have it approved.
```

## Next Steps

- See [Change Requests](change-requests.md) for the `ChangeRequestSet` workflow the gate sits in front of.
- See [Break-Glass](break-glass.md) for the deploy-time escape when the gate itself is misconfigured.
- See [Namespace Grants](grants.md) for the `administer` grant these endpoints check.
