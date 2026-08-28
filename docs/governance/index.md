---
title: Governance
description: Governance for Motiv's rules stack — authentication, namespace grants, the maker-checker approval gate, and break-glass recovery around live rules and runtime propositions.
---

Governance wraps the [live rules](../live-rules/index.md) and [runtime propositions](../propositions/index.md)
HTTP surface with four layers, applied in order on every write: **authenticate**, **authorize**,
**govern**, **publish**. Only the first is mandatory — `MapMotivRules()` is secure by default — the
rest are opt-in. Everything here ships in the `Motiv.Serialization` and `Motiv.Serialization.AspNetCore`
packages.

## Authenticate

`MapMotivRules(basePath, ...)` requires an authenticated caller on the whole mapped group:

```csharp
app.MapMotivRules("/api/rules"); // every route below requires authentication
```

Opening the group to unauthenticated callers is an explicit, greppable call at the mount site, never a
silent default:

```csharp
app.MapMotivRules("/api/rules", options => options.AllowAnonymous());
```

## Authorize

Once a caller is authenticated, [namespace grants](grants.md) decide *what* they may read and write.
Listings (`GET /catalog`, `GET /rules`, `GET /propositions`, `GET /change-requests`, `GET /gate`, ...)
are **unfiltered** — any authenticated caller sees the whole listing. Writes are **filtered**: each
write endpoint checks a `GrantVerb` (`Read < Author < Publish`) against the target's namespace, via a
registered `IGrantSource`. Without one, every check passes — grants are opt-in, so the surface stays
exactly as authenticated-only as it was before this feature existed.

## Govern

[`AddGovernance()`](change-requests.md) wires an [`ApprovalGate`](approval-gate.md) — a may-publish
`Policy` over a `ChangeRequest` — and a `ChangeRequestSet` workflow in front of the `RuleSet` (and the
`PropositionSet`, when authored propositions are enabled). The gate's default is **permissive**:
enabling governance changes no response until an admin installs a gate document. Once a document is
active, every write is evaluated against it — whether proposed through a [change request](change-requests.md)
or attempted directly against `PUT /rules/{name}` or `POST /propositions` — so the ungoverned endpoints
cannot be used to walk around the ceremony.

## Publish

A change request that satisfies the gate — or any write at all, while no gate document is active —
publishes exactly as it always did: an atomic, optimistically-concurrent update to the `RuleSet` or
`PropositionSet`. A refusal explains itself in the gate's own terms — `Reason`, `Assertions`, and
`Justification` — the same properties every Motiv evaluation produces.

## Layered recovery

Two safety nets sit under the gate, for when it is misconfigured:

- **The lockout pre-check** runs on every `PUT /gate`: before a candidate document is persisted, it is
  asked to judge the friendliest change imaginable (100 distinct approvers, every known role,
  none of them the author). A document that would refuse even that is refused itself — `422`, with the
  pre-check's own `GateDecision` — rather than locking out every future change. This is **sound but
  incomplete**: arbitrary predicates make satisfiability undecidable in general, so treat it as a
  footgun-catcher, not a proof that a document can never lock out a real change.
- **[Break-glass](break-glass.md)** is the floor beneath the pre-check: a deploy-time flag (an
  environment variable or `appsettings` entry, never an in-app toggle) that disables the gate entirely
  while active. Loud — every bypassed publish is logged under a fixed audit category — and
  time-boxable, so a forgotten break-glass window auto-expires rather than staying open indefinitely.

## Trying it with a real IdP

The repository's `docker-compose.yml` has an opt-in `auth` profile (`docker compose --profile auth up`)
that wires Studio to a real Keycloak instance instead of the zero-config dev identity, for
exercising authentication and namespace grants end to end. The compose file and realm import are a
sample-app concern, not part of the library surface documented on this page.

## Next Steps

- [Namespace Grants](grants.md) — `IGrantSource`, the verb ladder, and where each endpoint checks it.
- [Change Requests](change-requests.md) — the governance envelope and its HTTP surface.
- [The Approval Gate](approval-gate.md) — the may-publish policy, the built-in `change.*` specs, and
  the lockout pre-check.
- [Break-Glass](break-glass.md) — the deploy-time escape and its audit trail.
