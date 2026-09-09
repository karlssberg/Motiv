# Spec 3E follow-up — The delegate that was excluded when it ran and charged when it didn't — Plan

**Date:** 2026-09-08
**Ticket:** [#213](https://github.com/karlssberg/Motiv/issues/213)
**Source:** bundle spec
[3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
§2 Structural safety (19) — *"a result-size bound counted in the traversal loop"* — and §4's invariant
that every public result-tree property behave identically at every depth.
**Lineage:** Spec 3E ([#144](https://github.com/karlssberg/Motiv/pull/144)) →
[#145](https://github.com/karlssberg/Motiv/issues/145) (the ceiling measured) →
[#202](https://github.com/karlssberg/Motiv/issues/202) (the budget made per-evaluation,
[#206](https://github.com/karlssberg/Motiv/pull/206)) →
[#209](https://github.com/karlssberg/Motiv/issues/209) (telemetry excluded,
[#210](https://github.com/karlssberg/Motiv/pull/210)) →
[#208](https://github.com/karlssberg/Motiv/issues/208) (the `As(...)` predicate excluded,
[#214](https://github.com/karlssberg/Motiv/pull/214)) → this. Filed by #208 as the half it cut
deliberately.

## Why this slice exists

#202 made `MaxEvaluationSize` bound one *evaluation* rather than one *fold*, by making the budget
ambient. Ambient means the correctness of the whole scheme rests on a hand-maintained list of places
that declare themselves outside it. #209 added telemetry to that list; #208 fixed a hole *inside* the
higher-order entry, where the elements were excluded and the predicate they were resolved for was not.

**#213 is the same node's other half.** #208 fixed the *eager* path — `EvaluateModels`, which runs
during the fold. Every higher-order **result** additionally defers two kinds of user delegate to first
property read:

```csharp
private BooleanResult<TModel, TUnderlyingMetadata>[] CausesInternal =>
    field ??= causeSelector(Satisfied, underlyingResults).ToArray();

private IEnumerable<TMetadata> MetadataValues =>
    field ??= (Satisfied ? whenTrue(Evaluation) : whenFalse(Evaluation)).ToArray();
```

Neither ran during `Evaluate`. Both ran on whatever evaluation happened to be in flight at the moment
of the first read.

The cause selector is the sharper case, because it makes the state self-contradictory rather than
merely incomplete. The default selector for `As(...)` is `Causes.Get(…, higherOrderPredicate)`, and
`Causes.Resolve` re-invokes that predicate up to twice to decide which elements to name as causes — so
**#208 excluded a delegate in one place and left the same delegate charged in another.**

## The question the ticket left open

> Is reading a result property part of the evaluation that produced it?

The ticket declined to answer it and said the answer should be written down before nineteen classes
were edited to match it. It is answered here.

**Excluded — because these delegates run after `Satisfied` is fixed.** Every one of the nineteen result
classes takes `isSatisfied` as a constructor argument. A cause selector picks which already-evaluated
elements to *name* as the cause of a decision already made; a `WhenTrue` renders text for an outcome
already chosen. Neither can change what the node decided. That is the same property #209 relied on for
telemetry — *rendering an explanation must not change what an evaluation decides* — and it is exactly
what distinguishes them from the per-item leaf predicate that stays counted, because a leaf predicate
**is** how its node reaches an answer.

The ticket's counter-argument — *it is code the caller did write* — does not separate the two cases: so
is the telemetry-tagged `WhenTrue` that #209 already excluded, and it is the same delegate.

## The argument that does not need the decision

The decision above settles which way to move the line. A second observation says the current state is
wrong whichever way you would have drawn it.

These properties are **memoized** — `field ??=`, and the single-value ones a `_hasValue` flag. So the
delegate runs exactly once, on the *first* read. Whether it is charged is therefore a fact about **who
read the property first**, not about the composition:

```csharp
var cold = quorum.Evaluate(models);       // nothing resolved
var warm = quorum.Evaluate(models);
_ = warm.Values;                          // resolved here, with no budget in force — free

// the same rule, the same limit, the same models:
reads(cold).And(other).Evaluate(models);  // refused
reads(warm).And(other).Evaluate(models);  // accepted
```

And it is reachable without writing a delegate at all. Motiv's own telemetry tags its span with the
result's explanation, which resolves `WhenTrue` — so **attaching a listener warms the memo and makes a
later refusal disappear.** #209 established that subscribing must not change what an evaluation
decides; this is that rule with the sign reversed, and the same fix answers both.

## Approach

TDD, in the order #208 and #209 used.

1. **Red.** A new `HigherOrderLazyResultBudgetTests`, over all four higher-order families, in three
   theories: the cause selector charged, the value delegate charged, and the cold/warm parity case
   above. Plus a leak canary that fails if the fix *discards* the reader's count rather than parking
   it — the failure mode that would make the first three pass on an implementation that had removed
   the bound instead of placing the work outside it.
2. **Green.** Three seams on `HigherOrderResults`, beside #208's `MaterializeAndDecide`:
   `ResolveCauses`, `ResolveValue`, `ResolveValues`, each owning one `EvaluationBudget.Exclude()`
   scope. All nineteen result classes route through them.
3. **The gate.** Extend `HigherOrderSeamGateTests` — the IL gate #208 shipped — from propositions to
   results, with the population discovered rather than listed, and red-prove it by mutation.
4. **The documented line.** `MotivLimits.MaxEvaluationSize`'s remarks and `docs/limits/index.md` both
   currently say these delegates **are** counted. That is accurate today and must move in the same
   commit; they are the only statement of the line a caller can read.

## Expected fallout

- `ResolveValues` has to materialize *inside* the scope: a yielding `WhenTrue` is an iterator block, so
  invoking it runs none of the caller's code and enumerating it runs all of it. One expression-tree
  class stores the sequence unmaterialized today, so it will change from lazy to eager — a deliberate
  normalization onto what its five siblings already do, and worth calling out rather than burying.
- Three of the six yielding sites tolerate a null return with `?.ToArray()!` and three do not. One
  seam has to stand for both; the tolerant form is the wider.
- Two sites nest an exclusion inside another (a value delegate reads `Evaluation`, which resolves the
  cause selector). Nested `Exclude()` is already well defined — the inner scope parks a zero — but it
  is worth confirming rather than assuming.

## Out of scope

The asynchronous higher-order results, if their lazy shape differs, and any change to `Causes.Resolve`
itself. This slice moves *where* the delegates run, not what they compute.
