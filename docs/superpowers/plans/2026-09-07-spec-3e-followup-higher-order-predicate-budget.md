# Spec 3E follow-up — The elements that were excluded, and the answer they were resolved for — Plan

**Date:** 2026-09-07
**Ticket:** [#208](https://github.com/karlssberg/Motiv/issues/208)
**Source:** bundle spec
[3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
§2 Structural safety (19) — *"a result-size bound counted in the traversal loop"* — and §4's invariant
that a public property behave identically at every depth.
**Lineage:** Spec 3E ([#144](https://github.com/karlssberg/Motiv/pull/144)) →
[#145](https://github.com/karlssberg/Motiv/issues/145) (the ceiling measured) →
[#202](https://github.com/karlssberg/Motiv/issues/202) (the budget made per-evaluation,
[#206](https://github.com/karlssberg/Motiv/pull/206)) →
[#209](https://github.com/karlssberg/Motiv/issues/209) (telemetry excluded,
[#210](https://github.com/karlssberg/Motiv/pull/210)) → this. Like #209, it was found by #206's own
review round rather than by a failing test.

## Why this slice exists

#202 made `MaxEvaluationSize` bound one *evaluation* rather than one *fold* by making the budget
ambient. Ambient means every re-entry into the fold from inside a node spends the enclosing
composition's allowance unless something declares it excluded — so the correctness of the whole scheme
rests on a hand-maintained list of declaration sites. #209 added the fourth entry to that list.
**#208 is not a fifth entry; it is a hole inside the first one.**

`HigherOrderResults.Materialize` excluded the enumeration and the projection. The predicate applied to
the materialized results was outside the scope:

```csharp
var results = HigherOrderResults.Materialize(models, resultResolver, /* excluded */);
return (results, higherOrderPredicate(results));   // <- charged
```

For the built-in quantifiers that costs nothing — `AsAllSatisfied` and its siblings count booleans. For
a predicate supplied through `As(...)` it can cost an entire evaluation:

```csharp
Spec.Build(elem)
    .As(results => results.Count(r => r.Satisfied) >= threshold.Evaluate(config).Value)
    .Create("quorum")
```

`threshold.Evaluate` re-enters the fold with the composition's count in force, so its whole subtree is
charged to a composition it has nothing to do with.

The line as it stood was not one anybody could have described: **the elements were excluded, and the
answer they were resolved for was not.**

## Scope, and what is deliberately not in it

The ticket names three delegate families. Only the first is in this PR.

| Family | When it runs | In scope |
|---|---|---|
| `higherOrderPredicate` via `EvaluateModels` | eagerly, during the evaluation | **yes** |
| `causeSelector` from a result's `field ??=` | lazily, at first property read | no — [#213](https://github.com/karlssberg/Motiv/issues/213) |
| `whenTrue` / `whenFalse` from a result | lazily, at first property read | no — #213 |

The eager family is nineteen copies of one expression with a seam available to hide behind. The lazy
families are ~19 result classes with no shared seam, and they raise a question the ticket does not
answer: **is reading a result property part of the evaluation that produced it?** A result may be read
long after its evaluation ended, in which case there is nothing to charge, or during an unrelated one,
in which case the charge lands nowhere near the cause. That answer should be written down before
nineteen classes are edited to match it. Cutting it is the same call #202 made about the asynchronous
carrier (#204) and #206 made about this ticket.

## The two candidate fixes

#208 offers two and predicts the second wins:

1. **A scope per `EvaluateModels`** — `using var exclusion = EvaluationBudget.Exclude();` at the top of
   each of the nineteen. *"Mechanical, but nineteen edits and nineteen chances to miss one — with
   nothing that would go red if one were missed."*
2. **A combined helper** — `MaterializeAndDecide(source, state, project, predicate)`, so the exclusion
   becomes structural rather than repeated.

Option 2, with one addition the ticket does not make: **`Materialize` becomes private.** Option 2 as
written still leaves the footgun loaded — a twentieth family could call the materializing half and
apply its predicate outside. Removing the half-seam is what turns "nineteen chances to miss one" into
"the mistake is not expressible".

## The invariant nothing enforces

The ticket's third ask, and the one worth the most:

> `EvaluationBudget`'s remarks name the seams that declare the exclusion. Nothing checks the list is
> complete — #206 shipped with a claim that there were two seams when there were three, and that was
> found by reading rather than by CI.

A private `Materialize` makes the mistake hard, not impossible: a class can still hand-roll
`models.Select(...).ToArray()`. So the slice ships a gate that reads the **IL** of every
`EvaluateModels` in the assembly and asserts it calls the seam. Source text can be satisfied by a
comment; IL is what runs.

## Steps

1. **Red.** A theory over all four higher-order families — `.As(pred)` where `pred` evaluates a
   proposition of nineteen nodes, composed into a three-node composition against a limit of twenty — on
   both the `Evaluate` and `Matches` funnels. Plus the IL gate, red because the seam does not exist.
2. **Green.** Add `MaterializeAndDecide`; make `Materialize` a private core; collapse all nineteen
   `EvaluateModels` bodies to a single expression-bodied call.
3. **Red-prove the gate.** Rewrite one site to reach its decision without the seam and confirm the gate
   names that method.
4. **Move the documented line.** Both `MotivLimits.MaxEvaluationSize`'s remarks and
   `docs/limits/index.md` currently list the `As(...)` predicate as *counted*. That becomes false;
   both move together, since they are the only statement of the line a caller can read.
5. **File the deferred half** as #213, cited from `EvaluationBudget`'s remarks so the next reader of
   the seam list finds it.
6. Full solution suite, then the mandatory `code-simplifier` pass.

## Expected fallout

- **A behaviour change wearing a bug fix's clothes, in the permissive direction.** A composition that
  used to be refused will now be admitted. That is the correct direction for a backstop — #206's
  change went the other way and was the risky one — but it is still a documented promise being
  withdrawn, which is why step 4 is not optional.
- **The `Materialize`-only unit test** (`HigherOrderResultsTests`) must move to the new seam; its five
  cases cover the array / `IReadOnlyList` / buffered branches and should keep doing so.
- **The IL gate's naivety is a fair criticism to raise in review.** It scans for `0x28` without
  decoding operand lengths. The asymmetry is what makes it acceptable and should be stated in the test
  rather than defended later: a false *positive* needs the seam's own metadata token to appear by
  accident inside another instruction's operand; a missing call — the thing being detected — cannot be
  missed by an over-eager scan.
- **net472 cannot be run on this machine** (no `mono`), so the gate's reflection is exercised on
  net8/9/10 locally and on net472 only in CI.
