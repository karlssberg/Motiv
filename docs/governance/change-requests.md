---
title: Change Requests
---

A `ChangeRequest` is the governance envelope: one or more `ProposedChange`s — edits to rules or
propositions — that are reviewed, approved, and published together, atomically, through the
[approval gate](approval-gate.md).

## The envelope

```csharp
public sealed class ChangeRequest
{
    public Guid Id { get; }
    public string Author { get; }
    public string ChangeNote { get; }
    public IReadOnlyList<ProposedChange> ProposedChanges { get; }
    public IReadOnlyList<Approval> Approvals { get; }
    public ChangeRequestStatus Status { get; }
    public string? RejectionReason { get; }
    public bool PublishedUnderBreakGlass { get; }
}

public enum ChangeRequestStatus { Draft, InReview, Approved, Published, Rejected, Withdrawn }
```

A request starts in `Draft`; the first approval moves it to `InReview`; `Publish`, `Reject`, or
`Withdraw` moves it to one of the three terminal states. `Approved` is declared for a future explicit
approval-threshold transition — the current workflow evaluates the [gate](approval-gate.md) at publish
time rather than flipping a separate "became approved" status, so it is never produced today.

Each `ProposedChange` targets one artefact:

```csharp
public sealed record ChangeTarget(ChangeTargetKind Kind, string Name); // Kind: Rule | Proposition

public sealed record ProposedChange(
    ChangeTarget Target, string? ProposedDocumentJson, int BaseVersion,
    ChangeClassification Classification, string? ModelTypeId = null, string? Description = null);
```

`ProposedDocumentJson: null` means delete — withdraw the proposition, or revert the rule to its
default. `Classification` (`IsCreation`, `IsDeletion`, `IsMetadataOnly`, `TouchesAsyncSpec`,
`IsRollback`, `RollbackOfVersion`) is **derived**, not supplied: computed once, at creation time,
against the target's live document — because that is what the gate reasons over and what reviewers
saw. A base document that moves underneath the request afterwards is caught by the version check at
publish, not by silently reclassifying.

## The workflow

```csharp
public sealed class ChangeRequestSet
{
    public ChangeRequestSet(ApprovalGate gate, RuleSet rules, PropositionSet? propositions);
    public IReadOnlyList<ChangeRequest> All { get; }
    public ChangeRequest? Find(Guid id);
    public ChangeRequestResult Create(string author, string changeNote, IReadOnlyList<NewProposedChange> changes);
    public ChangeRequestResult Approve(Guid id, string approver, IReadOnlyList<string> roles);
    public ChangeRequestResult Reject(Guid id, string reason);
    public ChangeRequestResult Withdraw(Guid id, string caller);
    public ChangeRequestResult Publish(Guid id, bool breakGlassActive);
}
```

`AddGovernance()` constructs this over the same `RuleSet` (and `PropositionSet`, if
`AddPropositions()` was also called) that `MapMotivRules()` maps — both must share one `BindingScope`,
so an envelope spanning a rule and a proposition publishes under one lock and can never interleave with
someone else's publish.

An envelope must not be empty, and may target each artefact at most once. Approvals are one-per-approver:
a second approval from the same subject replaces the first rather than appending, so a gate counting
approvals (`change.approver-count-at-least`) cannot be satisfied by one person pressing the button
twice. Only the request's own author may withdraw it — checked as workflow state, not a grant.

## Publishing

`Publish` evaluates the [gate](approval-gate.md) (unless `breakGlassActive`), then applies every edit
in the envelope under one lock: **all-validate-then-all-apply**, so a refusal leaves nothing
half-published. Propositions coming into existence or changing apply first (so a rule in the same
envelope may reference one), then rules, then propositions going away (so nothing may still reference
them by the time they are removed).

## HTTP surface

Mounted under `{basePath}/change-requests`, only when `AddGovernance()` was called:

| Method & path | Grant | Body | Success | Failure |
|---|---|---|---|---|
| `GET {basePath}/change-requests` | none (unfiltered read) | — | `200` array of `ChangeRequestResponse` | — |
| `GET {basePath}/change-requests/{id}` | none | — | `200 ChangeRequestResponse` | `404` |
| `POST {basePath}/change-requests` | `Author` on every target | `{ changeNote, changes: [{ kind, name, document, baseVersion, rollbackOfVersion?, modelTypeId?, description? }] }` | `201 ChangeRequestResponse` | `400` |
| `POST {basePath}/change-requests/{id}/approvals` | `Publish` on every target | — | `200 ChangeRequestResponse` | `404`, `409` closed |
| `POST {basePath}/change-requests/{id}/rejection` | `Publish` on every target | `{ reason }` | `200 ChangeRequestResponse` | `404`, `409` closed |
| `POST {basePath}/change-requests/{id}/withdrawal` | author only | — | `200 ChangeRequestResponse` | `404`, `409` not the author, or closed |
| `POST {basePath}/change-requests/{id}/publish` | `Publish` on every target | — | `200 { request, publishedVersions }` | `403` gate blocked; `404`; `409` version conflict or closed |

`kind` is `"rule"` or `"proposition"`. Removal is spelled `"document": null` explicitly — an *omitted*
`document` is refused with `400`, because a forgotten field and a deliberate deletion are the same
bytes on the wire otherwise, and deletion is the destructive one.

A publish the gate blocks answers `403` in the gate's own words —
`{ "reason", "assertions", "justification" }` — see [The Approval Gate](approval-gate.md) for a worked
refusal. `publishedVersions` keys each published target's name to its new version; a withdrawn
proposition reports `0`.

## Next Steps

- See [The Approval Gate](approval-gate.md) for what decides whether `Publish` succeeds.
- See [Namespace Grants](grants.md) for the `Author`/`Publish` verbs this surface checks.
- See [Break-Glass](break-glass.md) for bypassing the gate on this same `Publish` route.
