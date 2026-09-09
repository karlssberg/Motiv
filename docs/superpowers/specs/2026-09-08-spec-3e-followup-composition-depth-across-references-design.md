# Design — the caps that stopped at a `spec` leaf

Source: bundle spec [3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
§2 *Structural safety (19)*, §4 (*"no uncatchable crash, bounded work"*), §7.
Ticket [#201](https://github.com/karlssberg/Motiv/issues/201) · measurement
[#145](https://github.com/karlssberg/Motiv/issues/145) · budget
[#202](https://github.com/karlssberg/Motiv/issues/202).

## What was wrong

Spec 3E left decorator nesting recursive on an argument: decorator depth "comes from how many
propositions an author wraps around each other, which is bounded by the catalogue", where operator
depth is attacker-controlled through a rule document's operand array. #145 asked for the measurement.
The measurement refuted the argument — a catalogue *composes* the alternating operator/decorator
shape one publish at a time, and that shape aborts the process at 1,047 layers synchronously and 261
asynchronously on a 1 MB thread, with a stack overflow no `catch` can see.

Every cap read small while it happened. The one named for the job, `MaxCompositionDepth`, was computed
in `RuleDocumentParser`, which holds no `ISpecSource` — so it stopped at a `spec` leaf and scored a
reference as 0 however deep the proposition it named. `PropositionChainDepthTests` stated that as
behaviour: a 200-link chain accepted with the cap set to **1**.

## Decision 1 — measure at bind time, against the source

The depth check moves to where an `ISpecSource` exists: the four binders' `Bind` entry points
(`RuleBinder`, `AsyncRuleBinder`, `MetadataRuleBinder`, `AsyncMetadataRuleBinder`). `CompositionDepth`
walks a `RuleNode` tree and resolves a `spec` leaf to the depth carried on its `SpecRegistryEntry`.

The check runs **before** anything binds. A cap exists to refuse a composition; composing it first
and then refusing it would pay exactly the cost the cap declined.

The parser's own source-blind check stays. It is a cheap pre-filter on a path that has already parsed
the JSON and has not yet resolved a name, and it is what refuses a single oversized document before a
registry lookup happens. It under-counts, and that is now its job rather than its defect.

## Decision 2 — the depth is carried, not re-walked

`SpecRegistryEntry` gains an internal `CompositionMeasure Depth`. `PropositionSet`'s bind delegate
stamps it on every authored entry.

This is what keeps the walk finite. `CompositionDepth.Of` recurses over **one document**, bounded by
`MaxDocumentDepth`, which the parser has already enforced — a chain adds depth to the *result* of the
walk, never to the walk itself, because a referenced proposition's measure is read off its entry
rather than recomputed from its document. Bounding an arbitrarily long chain therefore needs no
stack-safe walk of the very structure being bounded.

## Decision 3 — two numbers, because two resources

The ticket's sketch asked `MaxCompositionDepth` to count decorator levels as well as operator ones.
It now does, and one number would still have been wrong, because after Spec 3E the two are no longer
the same resource:

| level | folded? | cost | bound by |
|---|---|---|---|
| operator | yes, onto the heap | time + retained result | `MaxCompositionDepth` (4,096) |
| decorator | no — re-enters the fold | **stack**, one frame per level | `MaxDecoratorDepth` (128, new) |

Lowering `MaxCompositionDepth` to a stack-safe number would have contradicted its own published
derivation, which is a cost-per-evaluation budget for compositions that are provably iterative. So
`MaxCompositionDepth` keeps 4,096 and `MaxDecoratorDepth` takes the stack half, defaulted to 128 —
half of #145's lower measured ceiling (261, async, 1 MB thread), leaving the rest of a request stack
to the caller's own frames. Under the defaults a reference chain is refused at its 129th link.

`RuleSerializerOptionsTests` pins both defaults with the derivation in the assertion name, so changing
either is an edit to a test and to the XML docs rather than a silent number change.

## Decision 4 — a compiled entry scores as a leaf

This is #201's stated open question: a spec registered through the public `SpecRegistry.Register` has
an unknown depth, and Motiv has no stack-safe walk of a finished spec tree (decorators expose no
common seam to their underlying spec). The ticket offered assuming 1 (under-counts) or requiring a
caller to declare it (a public API change).

Neither is needed, because the question dissolves once the boundary is drawn correctly. A compiled
entry carries `CompositionMeasure.Leaf`. Spec 3E's original argument — that this depth is what a
developer wrote, bounded by their source rather than by a request — is **true of compiled code**; it
was only false when applied to a catalogue that a publish surface can extend. So the caps bound what
documents compose and leave what was compiled in to the developer's own budget, and the public API is
untouched.

The same reasoning covers the adapters a binder inserts at a leaf (`ToExplanationSpec`, the async
lift): a constant per leaf, not a level that accumulates along a chain, so they are not counted.

## Verification

- `PropositionChainDepthTests` — the two cases that stated the defect are inverted: a chain is refused
  past `MaxCompositionDepth` and past `MaxDecoratorDepth`, and refused at the 129th link under the
  shipped defaults with nothing configured. The evaluate case and #202's evaluation-size case are kept
  at a chain length the defaults admit, so they still test what they were written to test.
- `RuleSerializerOptionsTests` — both defaults, both range guards, and a single document nesting more
  decorators than the cap (decorator levels are counted within a document, not only across
  references).
- Full solution suite green on net8.0, net9.0 and net10.0.

## Not done here

Folding decorators into the evaluation driver — spec 3E design decision 3 explains why the driver's
type parameters cannot admit `ChangeModelTypeSpec` or `MetadataToExplanationAdapterSpec`, and #145
asked for the measurement rather than the rewrite. A cap at the edge is the cheaper half and the one
that matches where the other caps live.
