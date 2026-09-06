# Spec 3E follow-up — The budget that bounded one fold — Plan

**Date:** 2026-09-04
**Ticket:** [#202](https://github.com/karlssberg/Motiv/issues/202)
**Source:** bundle spec
[3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
§2 Structural safety (19) — *"a **result-size bound counted in the traversal loop** replaces the crash
that used to cap the amplification finding"* — and §7, *"a 100k-deep composition no longer crashes and
is bounded by the result-size limit."*

## Why this slice exists

`MotivLimits.MaxEvaluationSize` is the bound Spec 3E put in place of the crash. It documents itself as
*"the maximum number of nodes **a single evaluation** may compose"*, and names exactly one exclusion:
work done *inside* a node, such as a higher-order proposition over a collection.

`EvaluationFold.Fold` holds its running count in a local (`var size = 1;`), and `AsyncEvaluationFold`
does the same. A decorator between two operator layers is not folded — it is evaluated through
`EvaluateInternal`, which **re-enters the fold with a fresh count**. So the bound is per fold, not per
evaluation. #145's measurement found it while bisecting the decorator ceiling, and
`DecoratorSeamTests` has been pinning it as behaviour since:

| Composition | Nodes | `MaxEvaluationSize = 100` |
|---|---|---|
| flat `And` chain of 200 | 399 | refused, as documented |
| 50 decorator layers × 10 operands | > 1,000 | **accepted** |

This is reachable from a rule document — `RuleBinder.Decorate` wraps every node carrying a `name` or a
`whenTrue`, so a document composes alternating operator and decorator layers by construction.

## The decision the ticket left open

#202 says the obvious repair changes what is counted in a second way:

> A higher-order proposition evaluates its inner spec once per element through the same entry point, so
> an ambient counter would charge those too, and the documented exclusion above promises it does not. A
> 250,000-element collection would start tripping the default bound. That is a breaking change, not a
> bug fix.
>
> So the budget has to distinguish *the composition tree* from *work inside a node*, and the fold
> cannot tell a decorator's `EvaluateInternal` from a higher-order proposition's.

**The fold cannot, and does not have to.** Both premises about the codebase were checked before
designing against them, and one of them is out of date:

1. **Every higher-order per-element call already funnels through two helpers.** All twenty higher-order
   propositions — boolean-predicate, boolean-result, policy-result and expression-tree — resolve their
   elements through `HigherOrderResults.Materialize` or `HigherOrderShortCircuit.Evaluate`. Those were
   centralised for allocation reasons, not for this, but they are the seam the ticket wanted: the fold
   stays ignorant, and the two funnels declare that what happens beneath them is work inside a node.
2. **There are no asynchronous higher-order propositions.** `HigherOrderProposition/` contains no async
   type at all, so on the asynchronous fold the exclusion has nothing to protect.

So the design is: an **ambient budget entered by the outermost fold and inherited by nested ones**
(which charges decorator layers — the fix), **suppressed for the duration of one higher-order
element's resolution** (which preserves the documented exclusion).

## What this slice ships, and what it cuts

The carrier differs between the folds, and that is where the slice is cut.

- **Synchronous (`Evaluate`, `Matches`) — in.** A `[ThreadStatic]` counter is correct: the synchronous
  folds never leave the thread. It is also free — no allocation, which `Matches` needs, since its
  contract is that it allocates nothing and Spec 3E went to the trouble of a per-thread frame buffer to
  keep it.
- **Asynchronous (`EvaluateAsync`, `MatchesAsync`) — out, filed as a follow-up.** A thread-static is
  *wrong* here: a continuation may resume on a thread whose slot holds a suspended evaluation's count.
  The correct carrier is `AsyncLocal`, whose write is an `ExecutionContext` copy — affordable next to
  the state machine an async evaluation already allocates per operand, but it drags in two further
  decisions this PR should not smuggle: how a concurrent operator's fan-out shares one counter, and how
  an `AsyncLocal` budget meets the thread-static one at `SyncSpecAsyncAdapter`. The ticket anticipates
  this cut in as many words.

That split is the same call Spec 3E made for the frame buffer, in the same direction and for the same
reason: per-thread reuse on the synchronous fold, fresh allocation on the asynchronous one.

## Approach

TDD, against the three assertions `DecoratorSeamTests` already left as a hand-over.

1. **Flip the two synchronous characterization tests** to `ShouldThrow`, and watch them fail for the
   right reason — the composition currently evaluates.
2. **Add the exclusion's guard first, red.** A higher-order proposition over a collection larger than
   `MaxEvaluationSize` must still evaluate. Without this test the fix has nothing stopping it from
   over-charging, and the over-charge is the breaking change the ticket warns about.
3. **`EvaluationBudget`** — a thread-static counter with three operations: `Enter` (outermost claims,
   nested inherits), `Charge` (throws the existing `SpecException`), `Suppress` (the higher-order
   funnels). Scopes are `ref struct`s so nothing allocates.
4. **Wire it**: `EvaluationFold.Fold` enters and charges; `HigherOrderResults.Materialize` and
   `HigherOrderShortCircuit.Evaluate` suppress per element.
5. **Correct the documentation the defect was written into** — `MotivLimits`' remarks and
   `docs/limits/index.md` both currently publish the hole as behaviour.
6. **Re-scope the asynchronous case**: keep its characterization test, repoint its remarks at the new
   follow-up ticket rather than at #202.

## Expected fallout

Recorded before building, so the design doc can say which predictions survived.

- **Something in the existing suite will trip the tightened bound.** The tests that lower
  `MaxEvaluationSize` are collection-serialised, so a leaked budget would show up as a neighbouring
  class failing rather than as a failure where the bug is. This is the prediction most likely to cost
  time.
- **`Matches` stays allocation-free**, and no benchmark moves measurably: the change is one
  thread-static read per fold entry and one increment per node that was already being incremented.
- **The higher-order exclusion will need suppression on both funnels, not one.** `Materialize` serves
  the result-composing path and `HigherOrderShortCircuit.Evaluate` the `Matches` path; a fix to one
  would pass the test written against the other.
- The example projects' suites (`Motiv.Poker.Tests` and the two others) should be untouched — none of
  them sets a limit — but they are run, because CLAUDE.md requires it and the failure mode above is
  precisely a cross-class one.
