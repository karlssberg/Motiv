---
title: Runtime Propositions
description: Documentation for runtime propositions in Motiv — named, versioned, persisted compositions that shadow compiled specs, are referenceable by name from rules and from one another, and rebind every dependent transactionally when edited.
---

Propositions are the building blocks rules are made of. Compiled ones are registered in C# through a
[`SpecRegistry`](../live-rules/AspNetCore.md); **runtime propositions** add named, versioned,
persisted compositions authored while the application is running. Either kind is referenced from a
rule document by name, and neither the binders nor the evaluation path can tell them apart &mdash; a
reference compiles to the spec instance itself, so nothing is added to the hot path.

Editing a runtime proposition rebinds everything that references it &mdash; rules and other
propositions, transitively &mdash; in one transaction: an edit that would break a dependent is
refused whole, and nothing is published.

Runtime propositions ship in the `Motiv.Serialization` package; the HTTP endpoints and DI wiring
ship in `Motiv.Serialization.AspNetCore`.

## Composition Only

**This is the one surprising constraint, so it comes first.** A runtime proposition is a *derived*
fact: its document is a composition of specs that already exist. It cannot introduce a new
*primitive* fact, because primitive facts come from predicates, and a predicate is C#.

Every leaf of a rule document is either a `spec` reference or an `expression`, and expression leaves
do not bind &mdash; they are rejected with `ExpressionsNotEnabled`, because the package that would
evaluate them does not exist. So every runtime proposition necessarily bottoms out in something
already registered:

```jsonc
// Valid: composes two registered specs.
{ "rule": { "andAlso": [{ "spec": "customer.is-active" }, { "spec": "customer.is-adult" }] } }

// Invalid: an expression leaf never binds.
{ "rule": { "expression": "Age >= 18" } }
```

There is consequently **no such thing as an empty proposition** to start from. The demo UI's New /
Derive / Override dialog reflects this literally: it carries a *Starts from* picker and keeps
**Create disabled until a source is chosen**, because the smallest thing it can create is a
reference to one existing spec. `Override` is likewise offered only where the compiled spec's model
has at least one *other* spec available to compose from &mdash; a lone predicate over a scalar field
has nothing to be rebuilt out of, and the UI must not imply otherwise.

New primitive facts continue to come only from C#. What runtime propositions add is the ability to
name and reuse combinations of them.

## Names and Namespaces

A name is one or more dot-separated segments. Each segment starts with an ASCII letter, then ASCII
letters, digits, `-` or `_`:

| Name | Verdict |
|---|---|
| `is-active` | valid &mdash; root-level; every pre-existing name stays legal |
| `customer.eligibility.is-active` | valid |
| `customer..is-active`, `.is-active`, `is-active.` | invalid &mdash; no leading, trailing, or doubled dot |
| `customer.1st-order` | invalid &mdash; a segment must start with a letter |

The grammar is exposed as `SpecRegistry.IsValidName(name)`. Namespacing is *purely* a naming
convention: there is no stored folder structure, so a tree view is a projection of the names and a
rename moves a proposition with nothing else to keep in step. The DSL lexes a dotted name as a
single word, so `customer.eligibility.is-active` is one token in rule text.

## Origin: Compiled, Overridden, Authored

Resolution is layered &mdash; the authored overlay is consulted first, then the compiled
`SpecRegistry`. Three states follow, all derived rather than stored:

| Origin | Condition | What `DELETE`/`Withdraw` does |
|---|---|---|
| `Compiled` | in the registry, no authored document | nothing to withdraw |
| `Overridden` | in both; the authored document wins | restores the compiled spec |
| `Authored` | authored only | removes it |

Creating an override is an ordinary create against a name that today exists only as a compiled spec.
Reverting is simply the removal of the overlay entry, so the compiled spec is *never* copied or
shadow-stored and `SpecRegistry` stays an honest record of what the developer compiled in.

Versions count the authored document's revisions and start at 1. A purely compiled proposition
reports version `0` &mdash; it has no authored document to revise.

**Asyncness and metadata are derived, not declared.** A proposition whose document references an
async spec is itself async and binds through the async binder (which lifts sync references).
Authored propositions bind with `string` metadata; referencing a compiled spec whose metadata type
is not `string` is a validation error rather than a silent fallback.

## The Cascade

Publishing a proposition rebinds its transitive dependents, under one write lock, and either all of
it lands or none of it does:

1. The caller's `expectedVersion` is compared against the current one &mdash; a mismatch is a
   version conflict, exactly as for a rule.
2. The document is parsed and its outgoing `spec` references extracted.
3. The prospective graph is checked for a cycle; a back-edge is rejected with `CycleDetected` and
   the cycle path in the message.
4. The new document binds against a *prospective* source &mdash; the overlay with this document
   substituted, then the registry.
5. The transitive dependent closure is computed from the reverse-dependency index and ordered
   **dependencies before dependents**.
6. Each member rebinds against the prospective source, folding its result into the prospective
   overlay as it goes.
7. Any failure rejects the whole edit, attributed to the dependent that broke.
8. Otherwise the store is written, the bound specs are swapped in, and the edited proposition's
   version is bumped.

Step 5's ordering is load-bearing rather than tidiness: rebinding a referrer *before* its dependency
would bind it against the old definition and report no error at all.

**Dependents do not get a version bump.** A version tracks the *document*, not the binding, so a
dependent whose text did not change must not start conflicting with a colleague's open draft.

### Why a valid edit can break a dependent

The clearest case is asyncness. If `A` gains a reference to an async spec then `A` becomes async, and
a *synchronous* rule bound over `A` can no longer bind at all. Model-type and metadata-type changes
behave the same way. Step 6 catches exactly this at save time, and reports it as a broken dependent
rather than as an error at a path inside the document being saved &mdash; the break is somewhere
else, and a JSON pointer into this document could not address it.

### Removal and reverting

- **Reverting an override is permitted even while referenced**, because referrers keep resolving
  &mdash; to the compiled spec. It runs the same steps 5&ndash;8, so it is refused if the compiled
  spec fails to satisfy a dependent.
- **Removing an authored proposition with no compiled counterpart is refused while referenced**,
  listing the referrers, because removal would leave them pointing at nothing.
- **Rules are pure sinks.** Documents reference specs, never rules, so the graph is rooted and a rule
  can never take part in a cycle.
- **Blast radius is a plain read.** Who references `A` is a fact about *other* documents and does not
  depend on `A`'s pending edit, so it can be fetched and shown accurately while the user is still
  typing.

## Startup: Quarantine, Don't Crash

This deliberately breaks symmetry with [`RuleSet.Add`](../live-rules/RuleSet.md), which fails fast.

A compiled default failing to bind is a *developer* error, and catching it at startup is correct. A
persisted document failing to bind is an *operational* reality: a redeploy renames or removes a C#
spec that a saved proposition referenced. Refusing to boot would turn a stale row in a JSON file
into an outage.

So [`Load()`](PropositionSet.md) binds every stored document in dependency order, and anything that
fails to bind &mdash; or that depends on something which did &mdash; is **quarantined**: excluded
from the effective set, with its binding errors recorded and its document retained for repair.

- Lookups fall through to the compiled spec wherever one exists.
- A rule whose document is quarantined falls back to its compiled default, already proven to bind.
- Quarantined entries are still listed, carrying their errors, so they can be repaired or deleted in
  place.

Quarantine is orthogonal to origin, not a fourth value of it: an overridden *or* an authored
proposition can be quarantined.

Note that repairing a quarantined proposition does not automatically un-quarantine its dependents,
because a quarantined node installs no graph edges. Repair a chain dependencies-first.

## Wiring

```csharp
builder.Services.AddMotivRules(registry, options)
    .AddPropositions(new JsonFilePropositionStore("propositions.json"))
    .AddRule<CanCheckoutRule>();

var app = builder.Build();
app.MapMotivRules("/api/rules");
```

`AddPropositions()` enables the [proposition endpoints](AspNetCore.md) and points them at an
[`IPropositionStore`](IPropositionStore.md); omitting the argument uses `InMemoryPropositionStore`.
`JsonFilePropositionStore` above is the sample host's own implementation of that interface, not a
library type &mdash; durability stays outside the library, exactly as transport does.
Propositions load before rule defaults bind, so a rule's compiled-in default document may reference
one.

The `PropositionSet` and the `RuleSet` share one coordinator, so a proposition edit and a rule
update can never interleave.

## Available Types and Methods

| Page | Description |
|---|---|
| [PropositionSet](PropositionSet.md) | `AddModel()`, `Create()`, `Update()`, `Withdraw()`, `Load()`, `Dependents()` &mdash; the write path and its outcome contract. |
| [IPropositionStore](IPropositionStore.md) | The persistence seam, `StoredProposition`, and `InMemoryPropositionStore`. |
| [ASP.NET Core Integration](AspNetCore.md) | `AddPropositions()` and the six `/propositions` endpoints. |

## Next Steps

- See [Live Rules](../live-rules/index.md) for the rules that reference propositions by name.
- See [ASP.NET Core Integration](AspNetCore.md) for the HTTP contract.
- See [Asynchronous Propositions](../async/index.md) for the hierarchy an async proposition binds into.
