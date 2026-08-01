---
title: PropositionSet
---

`PropositionSet` is the set of runtime propositions an application resolves alongside its compiled
ones, and the write path for authoring them. It mirrors [`RuleSet`](../live-rules/RuleSet.md) &mdash;
validate, bind, then publish atomically, with optimistic version checks on writes &mdash; with one
addition: because a proposition is *referenceable*, publishing one also rebinds everything that
references it, all of it or none.

```csharp
PropositionSet AddModel<TModel>(string modelTypeId);

IReadOnlyCollection<PropositionEntry> Propositions { get; }
PropositionEntry? Find(string name);
string? DocumentJsonOf(string name);
IReadOnlyList<PropositionDependent> Dependents(string name);

PropositionUpdateResult Create(string name, string modelTypeId, string documentJson, string? description);
PropositionUpdateResult Update(string name, string documentJson, int expectedVersion);
PropositionUpdateResult Withdraw(string name, int expectedVersion);
void Load();
```

Instances are built by [`AddPropositions()`](AspNetCore.md) rather than constructed directly: the set
must share the `RuleSet`'s coordinator so a proposition edit and a rule update can never interleave.

## Registering Models

A proposition's model type is not in its document &mdash; a rule takes its model from the C# class,
and an authored proposition has no class &mdash; so each model that propositions may be written
against is registered against a stable id:

```csharp
propositions.AddModel<Customer>("customer");
```

In ASP.NET Core this is replayed automatically from
[`MotivRulesOptions.AddModel<TModel>()`](../live-rules/AspNetCore.md), so the id a client passes as
`modelType` is the same one the evaluate and validate endpoints already use.

## Authoring, Editing and Withdrawing

```csharp
var created = propositions.Create(
    "customer.eligibility.is-eligible",
    "customer",
    """{ "rule": { "andAlso": [ { "spec": "customer.is-active" }, { "spec": "customer.is-adult" } ] } }""",
    "Whether the customer may check out");

var edited = propositions.Update("customer.eligibility.is-eligible", replacementJson, expectedVersion: 1);
var gone = propositions.Withdraw("customer.eligibility.is-eligible", expectedVersion: 2);
```

`Create()` publishes version 1. A name already carrying an authored document is a conflict; a name
carrying only a compiled spec is accepted and creates an override.

The document must compose specs that already resolve &mdash; every leaf is a `spec` reference, never
a new predicate. See [Composition Only](index.md#composition-only).

`Withdraw()` means *revert* when a compiled spec lies beneath the name and *remove* when none does
&mdash; the two differ in what they may do to referrers, so they are ruled separately. See
[the integrity rules](index.md#removal-and-reverting).

Expected outcomes are values, not exceptions. `PropositionUpdateResult` carries the `Outcome`, the
`Version`, and whichever detail the outcome needs:

| `PropositionUpdateOutcome` | Meaning | Detail carried |
|---|---|---|
| `Created` | authored at version 1 | `Version` |
| `Updated` | replaced | new `Version` |
| `Removed` | withdrawn (reverted or removed) | `Version` is 0 |
| `VersionConflict` | the caller's `expectedVersion` was stale | current `Version` |
| `NameTaken` | an authored document already exists under that name | &mdash; |
| `Referenced` | removal refused; referrers would dangle | `Referrers` |
| `NotFound` | no authored document under that name | &mdash; |
| `Invalid` | the document did not bind, **or** the edit would break a dependent | `Errors`, `BrokenDependents` |

`Invalid` covers both a fault in *this* document (`Errors`, each with a JSON pointer into it) and a
break somewhere else (`BrokenDependents`, each naming the rule or proposition that stopped binding
and why). They are reported separately because a pointer into this document cannot address a break
in another.

## Reading

`Propositions` is the effective listing: every compiled entry, with authored entries layered over
the top. Each [`PropositionEntry`](index.md#origin-compiled-overridden-authored) carries its
`Origin`, its `Version` (0 when purely compiled), its derived `IsAsync` and `MetadataType`, and a
`Quarantine` list &mdash; empty unless an authored document failed to bind at startup.

`DocumentJsonOf(name)` returns the authored document behind a name, or null when the name carries
only a compiled spec. `Dependents(name)` returns the transitive closure of what references it, in
rebind order, each tagged `"rule"` or `"proposition"` &mdash; this is the blast radius, and it is a
plain read that does not depend on any pending edit.

## Loading

`Load()` reads every persisted proposition from the [store](IPropositionStore.md) and binds them in
dependency order, quarantining rather than throwing. It is called once, by `AddPropositions()`,
before rule defaults bind &mdash; so a rule's compiled-in default document may reference an authored
proposition. See [Startup: quarantine, don't crash](index.md#startup-quarantine-dont-crash).

## Remarks

- **Every write is serialized.** `Create`, `Update`, `Withdraw` and `Load` run under the shared write
  lock, which also covers rule updates. The lock is machine-scale &mdash; it stops two publishes
  interleaving their graph walks &mdash; and is a separate concern from the version check, which is
  human-scale and stops a save silently discarding an edit made while a tab sat open.
- **Nothing is published unless the outcome says so.** On any rejection neither the overlay, the
  dependency graph, nor the store is touched.
- **Persist first.** The store write is the only step that can fail after the point of no return, so
  it runs before the in-memory swap &mdash; a throwing store leaves no live proposition without a
  durable record.
- **The evaluation path is untouched.** A reference binds to the spec instance itself, so an
  evaluation of a proposition costs exactly what an evaluation of the equivalent compiled
  composition costs.

## Next Steps

- Serve `Create()`/`Update()`/`Withdraw()` over HTTP with
  [ASP.NET Core Integration](AspNetCore.md).
- Choose where authored documents persist with [`IPropositionStore`](IPropositionStore.md).
- See the [Runtime Propositions overview](index.md) for the name grammar, the cascade, and startup
  quarantine.
- See [`RuleSet`](../live-rules/RuleSet.md) for the rule-side write path this mirrors.
