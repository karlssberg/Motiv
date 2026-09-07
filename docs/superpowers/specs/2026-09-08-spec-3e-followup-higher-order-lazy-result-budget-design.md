# Spec 3E follow-up — The delegate that was excluded when it ran and charged when it didn't — Design

**Date:** 2026-09-08
**Ticket:** [#213](https://github.com/karlssberg/Motiv/issues/213)
**Plan:** [`2026-09-08-spec-3e-followup-higher-order-lazy-result-budget.md`](../plans/2026-09-08-spec-3e-followup-higher-order-lazy-result-budget.md)
**Source:** bundle spec
[3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
§2 Structural safety (19).
**Lineage:** Spec 3E ([#144](https://github.com/karlssberg/Motiv/pull/144)) →
[#145](https://github.com/karlssberg/Motiv/issues/145) →
[#202](https://github.com/karlssberg/Motiv/issues/202) / [#206](https://github.com/karlssberg/Motiv/pull/206)
→ [#209](https://github.com/karlssberg/Motiv/issues/209) / [#210](https://github.com/karlssberg/Motiv/pull/210)
→ [#208](https://github.com/karlssberg/Motiv/issues/208) / [#214](https://github.com/karlssberg/Motiv/pull/214)
→ this. Closes the half #208 cut.

## What the defect was

Every higher-order **result** defers two kinds of caller-supplied delegate to first property read:

```csharp
private BooleanResult<TModel, TUnderlyingMetadata>[] CausesInternal =>
    field ??= causeSelector(Satisfied, underlyingResults).ToArray();

private IEnumerable<TMetadata> MetadataValues =>
    field ??= (Satisfied ? whenTrue(Evaluation) : whenFalse(Evaluation)).ToArray();
```

Neither runs during `Evaluate`. Both run at the first read, on whatever evaluation is in flight then.

Two facts make that worse than "the wrong side of a defensible line".

**The same delegate was on both sides at once.** The default cause selector for `As(...)` is
`Causes.Get(…, higherOrderPredicate)`, and `Causes.Resolve` re-invokes that predicate up to twice to
pick which elements to name. #208 had just excluded that predicate where it decides. Here it was
charged.

**And whether it was charged depended on read order, not on the composition.** These properties are
memoized, so the delegate runs exactly once — on the *first* read. Two results of the same proposition
over the same models, read by the same rule against the same limit, differ only in whether someone else
looked first:

```
readsCold: refused        readsWarm: accepted
```

That is reachable with no delegate of the caller's at all: Motiv's telemetry tags its span with the
result's explanation, which resolves `WhenTrue`, so **attaching a listener warmed the memo and made a
later refusal disappear.** #209 shipped the rule that a listener must not change what an evaluation
decides; this was the same violation with the sign reversed, in the code #209 did not touch.

## The decision the ticket deferred

> Is reading a result property part of the evaluation that produced it?

**No — because these delegates run after `Satisfied` is fixed.** All nineteen result classes take
`isSatisfied` as a constructor argument. A cause selector chooses which already-evaluated elements to
*name* as the cause of a decision already made; `WhenTrue` renders text for an outcome already chosen.
Neither can change what the node decided, so they describe a decision rather than reach one.

That is the line, and it is not a new one — it is #209's, restated for code the caller *does* write.
The ticket's reason for hesitating was that "the caller wrote it" is what keeps the per-item leaf
predicate counted. It does not separate the two cases, because the caller also wrote the `WhenTrue`
that telemetry already resolves under exclusion. What separates them is **when** they run relative to
the decision: a leaf predicate runs before its node has an answer and *is* how it gets one; these run
after, and cannot affect it.

Stated as a rule for the next seam: **work a node does to reach its answer is inside the node; work
done to describe an answer already reached is outside the evaluation entirely.** #208 settled the
first clause. This settles the second.

## What shipped

Three seams on `HigherOrderResults`, beside #208's `MaterializeAndDecide`:

| Seam | Owns | Sites |
|---|---|---|
| `ResolveCauses` | the cause selector, materialized | 19 |
| `ResolveValue` | a single `WhenTrue`/`WhenFalse` | 8 |
| `ResolveValues` | a yielding pair, **materialized inside the scope** | 8 |

All nineteen result classes route through them; `Causes.Resolve` itself is untouched. The seams live
beside `MaterializeAndDecide` rather than on `Causes` (which the ticket suggested) so that one class
holds every higher-order budget declaration and one gate covers them all.

**Three seams rather than one, and that is not #208's mistake repeated.** #208 refused to offer
`Materialize` and the predicate separately, because half-using *that* seam left a hole with nothing to
say which half you had taken. These three are not halves of one act: the cause selector and the value
delegates are resolved by different properties at different times, and each seam owns the whole of the
delegate it is handed. There is no partial use to get wrong — which the gate then checks per property
rather than per class.

### Materializing inside the scope

`ResolveValues` calls `.ToArray()` *inside* the exclusion, and that is the load-bearing part rather
than a tidiness. A yielding `WhenTrue` is an iterator block: invoking it runs none of the caller's code
and enumerating it runs all of it. A seam that excluded the invocation and returned the sequence would
be the same defect the plan's prediction warned about and the same one #208's remarks record for
enumeration — *producing an element is part of resolving it*.

One consequence, called out because it is a behaviour change and not only a budget one:
`HigherOrderFromExpressionTreeMultiAssertionExplanationBooleanResult` stored its sequence
**unmaterialized**, alone among the six yielding classes. It is now eager like its five siblings. That
is a normalization onto the majority shape, and it is what makes the seam honest for that class rather
than merely applied to it.

`ResolveValues` is also null-tolerant (`?.ToArray()!`) because three of the six sites already were and
three were not; one seam has to stand for both, and the tolerant form is the wider.

### Nesting

The plan predicted nested exclusions where a value delegate reads `Evaluation`. It was wrong, and
harmlessly so: C# evaluates `Evaluation` as an *argument*, so `ResolveCauses`' scope opens and closes
before `ResolveValues`' opens. The scopes are sequential.

Genuine nesting still occurs — a `WhenTrue` that reads another higher-order result's property — and
`Exclude()` already handles it: the inner scope parks a zero and hands it back. No change was needed,
and `Should_resume_the_readers_own_count_after_the_read` is what says so rather than an assumption.

## How it was proved

### The red run

`HigherOrderLazyResultBudgetTests`, four families × three theories. Twelve red, on a three-node
composition against a limit of twenty with a nineteen-node threshold read inside the delegate:

```
Motiv.SpecException : The evaluation exceeded the maximum size of 20 nodes.
```

**The first red run was five cases short, and the shortfall was the test's fault.** Both weaknesses are
worth recording, because each is a test reporting a property it was not exercising:

- The vehicle read only `Assertions`. For a *named* higher-order proposition the assertions are
  `"{name} == true"`, rendered from the description without touching the value delegate at all — so
  three of the four value-delegate cases passed green while the delegate sat unresolved. Reading
  `Values` as well is what reaches them.
- The cold/warm parity case warmed through a *narrower* read than the vehicle made, so the
  boolean-predicate family's two arms agreed for the wrong reason. Warming through exactly what the
  vehicle reads is what makes the comparison mean anything.

Both were found by asking why a case was green rather than by trusting the count — the same discipline
#189's design records, one level down.

### The leak canary

`Should_resume_the_readers_own_count_after_the_read` is green before *and* after. Its job is the
implementation the other twelve cannot distinguish from a correct one: a scope that **discarded** the
reader's count instead of parking it would turn every property read into a free refill of the whole
budget, and all twelve would pass on a library that had removed the bound rather than moved the work
outside it. Twenty-three nodes against a bound of twenty must still be refused with the read in the
middle of them.

This is the shape [#208's own canary](2026-09-07-spec-3e-followup-higher-order-predicate-budget-design.md)
introduced, and the reason to keep writing it is that the permissive failure is invisible to every
other case in the file.

### The gate, extended and red-proved

#208 shipped an IL gate asserting that every higher-order **proposition** decides through
`MaterializeAndDecide`, with its population *discovered* rather than listed. It now carries the same
claim for **results**, with two additions:

- The population is discovered by name (`…Result`, arity-stripped) and pinned at 19, and the two
  sub-populations are pinned too — 19 deferring a cause selector, 16 a value delegate.
- Membership of each sub-population is read off the **shape** of a constructor parameter
  (`Func<bool, IEnumerable<T>, IEnumerable<T>>`; `Func<…Evaluation, …>`) rather than off a parameter
  name. That is deliberate: the expression-tree family spells its value delegates `trueBecause` where
  the other three spell them `whenTrue`, and a name-matched population absorbs exactly that drift in
  silence.

- The value gate is **split by delegate shape** rather than accepting either seam, and that split is the
  round's own finding — see below.

Red-proved by mutation, both halves at once — one `ResolveCauses` call and one `ResolveValue` call
reverted in place:

```
["MinimalHigherOrderFromExpressionTreeBooleanResult`1"]
["HigherOrderFromPolicyResultMetadataPolicyResult`3"]
Failed: 2, Passed: 4
```

Each gate went red and named its offender. Restored by re-editing rather than by `mv`, so the rebuild
was not skipped.

## The review round

The mandatory `code-simplifier` pass returned mostly consolidation inside the gate — one assembly
sweep behind a suffix parameter, one constructor-parameter walk behind a shape predicate, the
parameterless `Seam()` overload dropped now that there are four seams rather than one. It also
declined several consolidations for the right reason: ~50 lines are duplicated between this file's
fixture and `HigherOrderPredicateBudgetTests`', and the two classes are self-contained arguments that
cross-reference each other in prose rather than sharing machinery, which is CLAUDE.md's anti-over-DRY
guidance applied rather than quoted.

**Its one substantive finding was a defect in this slice's own gate**, and it is the family the ledger
already names: *a check reporting a property it is not actually checking is worse than no check.*

`ResolveValue` and `ResolveValues` have different names but **overlapping signatures**. A yielding
delegate binds to `ResolveValue` perfectly well, with `TValue` inferred as `IEnumerable<string>`. The
exclusion then closes over the *invocation* — which, for an iterator block, runs none of the caller's
code — and the body runs later, at enumeration, outside it. That is #213 reopened. The gate as first
written accepted **either** seam for any value-deferring class, so it would have reported success over
exactly that.

The finding was taken as a hypothesis and proved before being believed. One yielding site rerouted
through `ResolveValue`:

```
                          compiles: yes (no CS error)
12 behavioural budget cases:  green
                    the gate:  green      <- before the split
```

Two changes rather than one, because a structural claim and a behavioural one should not rest on each
other:

- **The gate is now split by shape.** A delegate whose return type is a constructed `IEnumerable<T>`
  must take `ResolveValues`; anything else must take `ResolveValue`. The two populations cannot
  overlap, because a single-valued delegate returns the class's own open `TMetadata`, or `string`, and
  neither is a constructed `IEnumerable<T>`. Both sub-counts — 8 and 8 — are pinned alongside the 19
  and the 16.
- **A behavioural case was added** with a real `yield` in the delegate body, which none of the original
  twelve had. It is red against the same mutation.

Restored by re-editing rather than by `mv`, so the rebuild was not skipped.

## The documented line

`MotivLimits.MaxEvaluationSize`'s remarks and `docs/limits/index.md` both said, accurately, that a
`WhenTrue`/`WhenFalse` or cause-selecting delegate resolved from a result **is** counted. Both moved in
this commit — they are the only statement of the line a caller can read, and a library whose limit is
*declared* rather than detected has no other way to be right about it.

The docs page gains the read-order argument in full, because it is the part a caller cannot infer: the
rule is not merely "these are excluded now" but "they never had a stable answer, and here is the code
shape that made that true".

## What was not done

- **`Causes.Resolve` still re-invokes the predicate up to twice.** That is a cost question, not a
  budget one, and moving where it runs was this slice's whole subject.
- **No asynchronous counterpart was needed.** All nineteen deferring result classes live in the four
  synchronous family namespaces; the repository-wide sweep for `causeSelector` finds nothing outside
  them.
- **The gate is still per-property, not per-delegate.** It asserts that a class calls the seam its
  shape requires, not that it calls it for *every* delegate it defers. A class deferring two cause
  selectors and routing one would pass. No such class exists, and inventing a stronger scan for a
  population of nineteen hand-written classes would buy less than the shape-derived population and the
  seam split already do.
