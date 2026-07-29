# Runtime Propositions — Design

**Date:** 2026-07-30
**Status:** Approved (pending spec review)
**Scope:** `Motiv.Serialization`, `Motiv.Serialization.AspNetCore`,
`src/examples/Motiv.RulesEngine.Sample`, `@motiv/rules-core`, `ui/apps/demo`.

> **Companion spec.** Reference-site parameter arguments — `{ "spec": "x", "args": { "n": 5 } }` —
> are **deliberately excluded** here and will be specified separately. See
> [Deferred: parameterised propositions](#deferred-parameterised-propositions).

## Problem

Specs are the building blocks rules are made of, but today they exist only in
compiled C#. `SpecRegistry` is populated once at startup and is documented as
read-only thereafter (`SpecRegistry.cs:7`). So a user of the rules UI can compose
a *rule* from the blocks a developer shipped, but cannot mint a new block — even
when the new block is nothing more than a named combination of existing ones.

That asymmetry is arbitrary. A rule is already a named `RuleDocument` bound into
a `SpecBase`; the only thing distinguishing it from a spec is that a rule has a
compile-time C# handle injected by type. Remove that requirement and the same
machinery yields **runtime propositions**: named, versioned, persisted
compositions that are referenceable from rules and from each other.

Two smaller gaps follow from it. There is no namespace grammar — `IsIdentifierLike`
(`SpecRegistry.cs:110`) rejects `.` — so a catalog of any size is a flat list. And
the UI has one page, whose breadcrumb leaf is a rule picker, with no surface for
browsing or authoring blocks.

## Goals

- Author, persist, and derive propositions from the UI, composed from any spec in
  scope — compiled or UI-authored.
- Namespace propositions, and browse them in a searchable tree.
- Let a UI document **override** a compiled spec, and revert to the compiled one.
- Keep the invariant that **every rule and proposition in the store binds, at all
  times** — an edit that would break a dependent is refused whole.
- Leave the evaluation hot path byte-for-byte unchanged.

## Non-Goals

- Expression leaves (`` `Age >= 21` ``). They parse but refuse to bind
  (`RuleBinder.cs:98` — *"expression nodes require the Motiv.Serialization.Expressions
  package"*), and that package does not exist. New **primitive** facts continue to
  come only from C#; this spec adds new **derived** facts.
- Reference-site parameter arguments (see [Deferred](#deferred-parameterised-propositions)).
- Non-`string` metadata authoring.
- Version history / pinning. Propositions carry a version for optimistic
  concurrency only; superseded documents are not retained.

## Design Decisions (settled during brainstorming)

| Axis | Decision |
|---|---|
| Store location | `PropositionSet` in `Motiv.Serialization`, mirroring `RuleSet` |
| Durability | In-memory by default, behind an `IPropositionStore` seam; the sample wires a JSON file |
| Namespacing | **Dotted names** — the tree is a projection of the name, nothing to keep in sync |
| Derivation grammar | Composition only: operators, higher-order quantifiers, name/whenTrue/whenFalse decoration |
| Reference binding | **Direct** — a reference compiles to the spec instance itself, no indirection |
| Change propagation | **Live cascade** — transitive dependents rebound eagerly at publish, transactionally |
| Compiled specs | Overridable defaults, symmetric with `RuleSet`; revert restores the compiled spec |
| Metadata type | UI-authored propositions are `string`-metadata (explanation) |
| Startup failure policy | **Quarantine, don't crash** — asymmetric with `RuleSet.Add` on purpose |
| Explorer hierarchy | Namespace only; model type shown as a pill and offered as a filter |
| Derive affordance | `Derive` (pre-seeded) **and** plain `New`, sharing one dialog |
| Navigation | Appbar tabs + hash routes, no router dependency |
| Propositions page | Left tree rail + the same Editor / JSON / Evaluate body, panes unmodified |

### Rejected alternatives

**Indirection nodes with lazy memoised binding.** A reference would compile to a
delegating spec resolving through a memo, making publish cheap and pre-adapting to
args-keyed caching. Rejected because it taxes every evaluation of every reference
to make a rare operation cheap — directly against the sustained allocation- and
closure-elimination work in 3e28b314, e1cbf47d, 87f9e65f, 7dc2ca39 — and because
lazy invalidation defers the discovery of a broken dependent to its first
evaluation in production, abandoning the fail-fast posture stated at
`MotivRulesEndpoints.cs:126`.

**Mutable `SpecRegistry`.** Folding runtime propositions into the registry with a
lock. Rejected because it breaks the type's documented contract, and because
conflating the compiled catalog with a mutable store makes "what did the developer
compile in?" unanswerable — which in turn makes revert-to-compiled impossible
without a shadow copy, i.e. layering, rebuilt worse.

## Name Grammar

`IsIdentifierLike` gains the dot as a **segment separator**. Each segment keeps
today's rule — an ASCII letter, then ASCII letters, digits, `-` or `_` — joined by
single dots, with no leading, trailing, or doubled dot.

| Name | Verdict |
|---|---|
| `is-active` | valid (root-level; every existing name stays legal) |
| `customer.eligibility.is-active` | valid |
| `customer..is-active`, `.is-active`, `is-active.` | invalid |
| `customer.1st-order` | invalid — segment must start with a letter |

The DSL mirrors this by admitting `.` into `WORD_REST` (`ui/packages/rules-core/src/dsl/lexer.ts:9`).
Safe because numeric literals are lexed before words (`lexer.ts:70-76`), so a dot
can never be stolen from `2.5`; and `wordKind` matches whole words, so a dotted
word can never collide with a keyword or quantifier.

The change is purely additive: no existing name, document, or DSL text changes
meaning.

## Resolution & Layering

The four binders take `SpecRegistry` concretely (`RuleSerializer.cs:74`, `:122`,
`:173`, `:227`). They change to take a new internal seam:

```csharp
internal interface ISpecSource
{
    SpecRegistryEntry? Find(string name);
}
```

One method, at exactly the existing lookup shape. `SpecRegistryEntry` already
carries `(Name, ModelType, MetadataType, IsAsync, Spec, Description)` — every fact
the binders type-check against — so the binders never learn that propositions
exist. `SpecRegistry` implements `ISpecSource` unchanged; `LayeredSpecSource`
consults the proposition overlay first, then the registry.

**That layering is the override mechanism**, and it gives revert for free: a
proposition with no overlay entry simply falls through to the compiled entry.
Nothing is copied or shadow-stored, and `SpecRegistry` alone remains an honest
record of what the developer compiled in.

Three states, all derived rather than stored:

| State | Condition | Delete/revert action |
|---|---|---|
| Compiled | in `SpecRegistry`, no overlay entry | n/a |
| Overridden | in both; overlay wins | restores the compiled spec |
| UI-authored | overlay only | removes it |

**Asyncness is derived.** An overlay entry's `IsAsync` follows from its document:
if any referenced entry is async, the proposition is async and binds via
`AsyncRuleBinder` (which lifts sync references) rather than `RuleBinder`.

**Metadata.** UI-authored propositions bind with `string` metadata. Referencing a
compiled spec whose `MetadataType` is not `string` from a UI-authored proposition
is a validation error, not a silent fallback to string assertions.

## Publish, Cascade & Integrity

### The shared coordinator

Cascade must be atomic across both stores, because a live rule can sit in a
proposition's dependent closure. An internal `BindingScope` owns the four things
that must agree:

1. the layered `ISpecSource`,
2. one write lock,
3. the reverse-dependency index,
4. the set of rebind participants.

`PropositionSet` and `RuleSet` are both constructed against it by
`AddMotivRules`. This supersedes `RuleSet`'s present per-rule-CAS-only
concurrency, which cannot remain sufficient once a rule may be rebound by someone
else's proposition edit.

The write lock and the version check solve different problems and both are
required. The **lock** is machine-scale: it stops two publishes interleaving their
graph walks for the milliseconds a rebind takes. The **version check**
(compare-and-swap, as `RuleBase.TryUpdate` already does) is human-scale: it stops
a save from silently discarding an edit made while a browser tab sat open.

### Algorithm

`PropositionSet.Update(name, documentJson, expectedVersion)`, entirely under the
write lock:

1. Compare `expectedVersion` against the current version; mismatch →
   `VersionConflict` (semantics identical to rules today).
2. Parse the document; extract its outgoing `spec` references.
3. **Cycle check** against the prospective graph — depth-first from `name`; a
   back-edge yields `Invalid` with the cycle path in the message.
4. Bind the new document against a **prospective source** (overlay with this
   document substituted, then registry), collecting errors.
5. Compute the transitive dependent closure from the reverse index, and
   topologically order it — **dependencies before dependents**.
6. Rebind each member of the closure against the prospective source, folding each
   result into the prospective overlay as it goes.
7. **Any failure rejects the whole edit.** Errors are attributed to the dependent
   that broke.
8. All succeeded → swap the bound specs in and bump `name`'s version. Persist via
   `IPropositionStore.Save`.

Step 5's ordering is load-bearing, not tidiness: rebinding a referrer before its
dependency would bind it against the *old* definition and report no error at all.

**Dependents do not get a version bump.** Version tracks the *document*, not the
binding. A dependent whose text did not change must not spuriously conflict with a
colleague's open draft — otherwise editing one shared proposition would invalidate
every open draft in the app and produce a wave of false 409s.

### Why a valid edit breaks a dependent

The primary case is asyncness. If `A` gains a reference to an async spec, `A`
becomes async, and a **sync** rule bound over `A` can no longer bind. Model-type
and metadata-type changes behave the same way. Step 6 catches exactly this at save
time; it is the concrete failure that justifies the transactional design.

### Integrity rules

- **`DELETE` means revert when a compiled default exists, and remove when it does
  not.** The two cases differ in what they can do to referrers, so they are ruled
  separately:
  - *Overridden* → publishes the compiled entry in place of the overlay entry and
    runs the same steps 5–8. Referrers still resolve — to the compiled spec — so
    this is **permitted even when referenced**, and refused only if the compiled
    spec fails to satisfy a dependent (the same async/model/metadata breaks as any
    other edit), with the same attribution.
  - *UI-authored, no compiled counterpart* → removal would leave referrers
    dangling, so it is **refused with 409** listing them (`PropositionReferenced`)
    whenever the referrer set is non-empty.
- **Rules are pure sinks** — documents reference specs, never rules — so the graph
  is rooted and a rule can never participate in a cycle.
- **Blast radius is a plain `GET`.** Who references `A` is a fact about *other*
  documents and does not depend on `A`'s pending edit, so it can be fetched
  accurately while the user types.

## Persistence

### Stored record

A proposition's model type is not in the document (rules take theirs from the C#
class), so it is carried explicitly:

```csharp
public sealed record StoredProposition(
    string Name, string ModelType, string DocumentJson, int Version, string? Description);
```

### Seam

```csharp
public interface IPropositionStore
{
    IReadOnlyList<StoredProposition> Load();
    void Save(StoredProposition proposition);
    void Delete(string name);
}
```

Synchronous, matching `RuleSet`'s synchronous publish, and called under the write
lock — implementations must be quick. The default is in-memory. The sample host
wires a JSON-file implementation, keeping durable storage outside the library
exactly as transport and serialization already are.

### Startup: quarantine, don't crash

This deliberately breaks symmetry with `RuleSet.Add`, which fails fast.

A compiled default failing to bind is a **developer** error, caught at startup —
correctly. A persisted document failing to bind is an **operational** reality: a
redeploy renames or removes a C# spec that a saved proposition referenced.
Refusing to boot would turn a stale row in a JSON file into a production outage.

So startup loads every stored document and binds in topological order. Anything
that fails to bind — or depends on something that failed — is **quarantined**:
excluded from the overlay, with its document retained for repair. Consequences:

- Lookups fall through to the compiled spec where one exists.
- A rule whose document is quarantined falls back to its compiled default, which
  is already known to bind because `RuleSet.Add` proved it moments earlier.
- Quarantined entries surface in the explorer with their binding errors, and are
  repairable or deletable in place.

The two fallback layers compose, so a bad deploy degrades toward *the behaviour
the developer shipped* — the most defensible resting state available.

## HTTP Surface

Under the existing `/api/rules` group:

| Verb | Path | Body / query | Success | Failure |
|---|---|---|---|---|
| `GET` | `/propositions` | — | 200 list | — |
| `GET` | `/propositions/{name}` | — | 200 | 404 |
| `POST` | `/propositions` | `{ name, modelType, document, description? }` | 201 | 400, 409 taken |
| `PUT` | `/propositions/{name}` | `{ document, baseVersion }` | 200 | 400, 409 conflict, 404 |
| `DELETE` | `/propositions/{name}` | `?baseVersion=n` | 200 | 400, 409 referenced, 404 |
| `GET` | `/propositions/{name}/dependents` | — | 200 transitive closure | 404 |

List entries carry `name`, `modelType`, `metadataType`, `isAsync`, `origin`
(`compiled` \| `overridden` \| `authored`), `version`, `description`, and a
`quarantine` field. **Quarantine is orthogonal to origin, not a fourth value of
it** — an overridden *or* an authored proposition can be quarantined, and the
explorer renders the two facts as separate marks.

Create is `POST`, not `PUT`, because `MotivRulesEndpoints.cs:222` already reserves
`baseVersion` as strictly positive — *"versions start at 1"* — leaving no spare
value meaning "expect absent" without overloading a field that has one clear
meaning today.

**`POST` is also how an override is created**, so "taken" is scoped precisely: 409
`PropositionNameTaken` means *an overlay entry already exists* under that name. A
name that exists only as a compiled spec is **accepted** and creates an override,
whose overlay entry starts at version 1 — versions count the overlay document's
revisions and are unrelated to the compiled spec, which has none. Only `PUT` and
`DELETE` require a `baseVersion`; `POST` does not, since there is nothing yet to
conflict with.

### `GET /catalog` must return the effective list

Today the catalog projects `registry.Entries` (`MotivRulesEndpoints.cs:41`) and is
computed **once at `MapMotivRules` time**, closed over as a constant. That is
sound while the catalog is immutable and wrong the moment propositions can be
authored — a new proposition would never appear until restart.

The catalog becomes a projection of the layered source, each spec entry tagged
with its origin, and is either built inside the handler or held as a snapshot the
`BindingScope` republishes on each publish. Without this the builder's spec picker
cannot offer UI-authored propositions as operands, and composability is the entire
feature.

## UI

### Navigation & shell

`useHashRoute` (~30 lines, no dependency, `hashchange`-driven) parses
`#/rules/{name}` and `#/propositions/{name}`. The appbar gains a
`Rules | Propositions` tablist between the brand and the breadcrumb.

The dotted name renders directly as breadcrumb segments, reusing `.breadcrumb-sep`
and `.breadcrumb-current`: `Motiv / Propositions / customer / eligibility / is-active`.
The Rules page keeps its `ListboxPicker` leaf exactly as landed in 21e37e16; on the
Propositions page the tree rail is the selector, so the leaf is a label.

`.shell-body` gains a leading rail column on the Propositions page only. The
Editor / JSON / Evaluate panes are reused **unmodified** — they read from the
shared `RuleEditorStore` and never ask what the document represents.

### Where explorer logic lives

Split along the seam the workspace already uses. Pure data shaping goes in
`@motiv/rules-core` — `buildNamespaceTree(entries)` and
`filterTree(tree, query, models)`, no React, directly unit-testable. Rendering
stays in the demo as app-shell furniture, alongside the other panes.

### Explorer behaviour

- One search input filtering on substring of the full dotted name, revealing
  matches with ancestors expanded, and reporting a match count.
- Model filter chips, reusing `.model-pill` styling.
- Per-leaf origin badge (compiled · overridden · authored), with quarantined
  rendered distinctly and carrying its binding errors.
- Node actions: `Derive`, `Override`, `Revert`, `Delete`.
- `Override` is offered only where the compiled spec's model has at least one
  other spec available to compose from. Because UI propositions are
  composition-only, a raw predicate over a scalar field may have nothing to
  compose it from; the UI must not imply otherwise.

### New & Derive

Both flows share one dialog: name (with namespace autocomplete drawn from existing
segments), model select, description. `Derive` pre-seeds the name to the source
node's namespace and the document to `{ spec: <source> }` as root, so derivation
is a shortcut into ordinary authoring rather than a separate concept or a
separate persisted shape.

### Blast radius

A dependents strip under the header, populated from
`GET /propositions/{name}/dependents`, with the count echoed on the Save button.
Not a modal: the information belongs in view while editing, not sprung at the
moment of saving.

## Error Handling

Existing validation continues to flow through the store's debounced `/validate`
loop, untouched. New `RuleErrorCode` members:

| Code | Meaning |
|---|---|
| `InvalidSpecName` | name violates the dotted grammar |
| `CycleDetected` | message carries the cycle path |
| `PropositionNameTaken` | 409 on create |
| `PropositionReferenced` | 409 on delete; lists referrers |

Cascade failures get their own response shape rather than being forced into
`RuleError`, whose `path` is a JSON pointer into *this* document and cannot
address a break in another:

```jsonc
{
  "errors": [],
  "brokenDependents": [
    { "name": "can-checkout", "kind": "rule", "errors": [ /* RuleError[] */ ] }
  ]
}
```

The UI renders that as *"this change would break rule can-checkout"* with the
underlying errors beneath — the difference between a usable feature and an
inscrutable 400.

## Testing

Test-driven throughout, per CLAUDE.md: failing test first, confirm it fails for
the right reason, minimum code to pass.

**Backend unit (`Motiv.Serialization.Tests`)**

- Dotted-name grammar: accept/reject table including every invalid form above.
- Layered resolution; fall-through on revert; origin derivation.
- Cycle detection, direct and transitive.
- Topological rebind order — **including the negative test**: rebinding a referrer
  before its dependency must be shown to miss the error. This is the only test
  that fails if the ordering is later "simplified" away; every other cascade test
  still passes, because wrong-order rebinding reports *fewer* errors, not
  different ones.
- Transactional rejection via the sync→async break, asserting nothing was published.
- Dependents' versions not bumping.
- Startup quarantine, and both fallback layers.
- Delete protection; store round-trip.

**Integration (`Motiv.RulesEngine.Sample.Tests`, `WebApplicationFactory`)**

- Every endpoint, including both 409 paths.
- `/catalog` reflecting a just-created proposition — the regression guard for the
  closed-over-constant catalog.

**`@motiv/rules-core` unit** — dotted names surviving lexer → parser → printer
round-trip; `buildNamespaceTree` / `filterTree`.

**Demo vitest** — explorer render and filtering, derive prefill, hash routing.

**Playwright e2e** — the cascade end to end: author a proposition, reference it
from a rule, evaluate, then edit the proposition and observe the rule's result
change without touching the rule. That single test proves the feature's central
claim. Safe to run from a worktree now that 58fae21a stops the suite adopting a
host it did not start.

Per CLAUDE.md, the **full solution** suite runs before completion — the
Poker/ECommerce/SmartHome example tests assert on justification strings and break
if result formatting shifts.

## Deferred: parameterised propositions

Reference-site arguments — `{ "spec": "orders.at-least-n-large", "args": { "n": 5 } }` —
make a proposition a reusable template bound differently per call site. This was
chosen during brainstorming as the desired end state, and is deliberately held
back to its own spec because it inverts an invariant this design depends on:
resolution here returns a **bound spec instance**, whereas args require resolution
to return a **document to bind on demand**, cached by name+args.

That reaches into `RuleBinder`, the overlay cache, the cycle guard, and both
authoring surfaces. It composes cleanly onto this design — the reverse-dependency
index and closure walk are exactly what args-keyed invalidation is built on — but
it does not belong in the same plan as tree-view styling.

Parameters as they exist today are bind-time: `RuleParameterSubstituter.Apply`
folds values into `n` and interpolates whenTrue/whenFalse text once, when the
document binds. Documents may continue to declare and use parameters as they do
now; what this spec adds no support for is *supplying arguments at a reference*.

## Documentation

Per CLAUDE.md, user-facing documentation lands outside this file: a brief example
under Core Features in `README.md`, and detailed pages under `docs/` following the
existing structure (`docs/{feature}/index.md`, method pages, `toc.yml`, plus
entries in `docs/toc.yml` and `docs/Overview.md`).
