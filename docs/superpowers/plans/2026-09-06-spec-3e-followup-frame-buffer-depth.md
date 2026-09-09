# Spec 3E follow-up — The contract that was free only at the top — Plan

**Date:** 2026-09-06
**Ticket:** [#205](https://github.com/karlssberg/Motiv/issues/205)
**Source:** bundle spec
[3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
§2 Structural safety (19) — *"Allocation: a reused, closure-free working stack"*.
**Lineage:** Spec 3E ([#144](https://github.com/karlssberg/Motiv/pull/144)) →
[#145](https://github.com/karlssberg/Motiv/issues/145) (the measurement) →
[#202](https://github.com/karlssberg/Motiv/issues/202) (the budget made per-evaluation,
[#206](https://github.com/karlssberg/Motiv/pull/206)) → this, found while building #202's allocation
guard.

## Why this slice exists

Spec 3's §2 does not say "a working stack". It says **a reused** working stack, and the reuse is the
whole clause — an allocated-per-fold stack is what the recursion it replaced already had for free on
the thread's own stack. `SpecBase<TModel>.Matches` is where that clause is cashed: its contract is
that it allocates nothing, and `EvaluationFold`'s per-thread frame buffer exists for that reason and
no other. Its own remarks say so.

The reuse was one slot per thread, taken rather than borrowed, because a nested fold handed the same
array would overwrite the frames its caller was still unwinding. The remarks named the consequence and
dismissed it:

> A nested fold finds nothing and allocates, which is correct and rare.

It is correct. It is not rare, and #145's measurement is what makes that visible: `RuleBinder.Decorate`
wraps every node carrying a `name` or a `whenTrue`, so **a rule document produces one nested fold per
decorator layer**, and an authored proposition referencing another composes the alternating shape a
link at a time. Measured warm, `Matches` over that shape at four operands per layer:

| Layers | Allocated |
|---|---|
| flat chain of 16 | 0 bytes |
| 4 | 456 bytes |
| 8 | 1,064 bytes |

Exactly `(layers − 1) × 152` at both points: one frame array per *nested* fold, the outermost reusing
the cached one. So "allocates nothing" was a per-**fold** claim, not a per-**evaluation** one.

That is the same shape of defect [#202](https://github.com/karlssberg/Motiv/issues/202) fixed in
`MotivLimits.MaxEvaluationSize`, in the sibling property, reachable by the same composition, and
missed for the same reason: `EvaluationBudgetTests.Should_carry_the_budget_without_allocating` holds
the flat case at 0 and nothing held the nested one.

## Why it is #205 rather than #204, #208 or #201

The frontier query over the build ledger [#169](https://github.com/karlssberg/Motiv/issues/169)
returns nothing agent-actionable — its only open child is #172, the manual screen-reader pass,
assigned to a human by design. The work is in the ledger's second table, the follow-ups shipped slices
raised on their way through, and four of those were open.

[#211](https://github.com/karlssberg/Motiv/issues/211) — filed the day before, and a naming change
that touches the same files — states the order and asks not to jump it:

> It should not jump the queue ahead of those four. #205 in particular is a *shipped contract that is
> false*.

That is the tiebreak, and it holds up on its own terms. #205 is a false published claim whose fix is
contained in one private nested class. #204 needs three unmade decisions about an `AsyncLocal` carrier;
#208 is nineteen call sites and says in its own body that it deserves its own PR; #201 has an open
question — the depth of a *compiled* spec registered through `SpecRegistry.Register` — that it must
settle before any of it can be built.

## Approach

TDD, red first. The ticket already carries the measurement, so the failing test is the measurement
restated as an assertion, and the expected red is a number the ticket predicts rather than one the
test discovers.

1. **Red.** A new `FrameBufferTests` stating the contract at three nesting depths, with the flat chain
   as a control so the cases cannot pass on a build where `Matches` had stopped folding at all.
   Expected failure: 152 / 456 / 1,064 bytes.
2. **Green.** Replace the single thread-static slot with a per-thread **stack** of buffers, contained
   entirely in `FrameBuffer` — no change to `Fold`'s control flow.
3. **Bound the retention**, because the depth the reuse now spans is bounded by nothing else: #201
   measured the alternating ceiling at over a thousand layers, and a retained buffer per layer at the
   width bound is precisely the megabytes-per-thread that `MaxCachedCapacity` exists to refuse.
4. **Prove the guards by mutation**, not by their passing — the aliasing invariant especially, since
   it is the one the fix could break and the one no new test states directly.

## Expected fallout

- **The retention cap is a new number, and new numbers are where this series has gone wrong before.**
  It must be chosen from the memory it costs, not from the depth the tests happen to use — a cap that
  exactly accommodates the deepest case in the suite is a cap fitted to its tests.
- **`Array.Resize` is the reason the ticket's second option will not work.** #205 sketches a
  region-partitioned single buffer and says both options are contained in `FrameBuffer`. That is worth
  checking rather than assuming.
- **The existing suite may already refuse a borrowed buffer.** If it does, no bespoke aliasing test is
  needed — but "may already" is not evidence, and a green suite is not a live guard until a mutant has
  been run against it.

## What actually shipped, and where it diverges

Step 2 above is wrong, and it is left standing because the plan is the plan. A **stack** serves every
fold up to the cap and refuses the outermost past it — folds return innermost-first, so a full pool
refuses the ones that arrive last — and the outermost is the one level whose operand run a caller can
make wide. The review round caught it; the shipped pool is indexed by **nesting level** instead. See
the design doc's *review round* section, which is the part of this slice worth reading.

The third expected-fallout bullet held too: the existing suite does refuse a borrowed buffer, so no
bespoke aliasing test was written. The first bullet held in the way it was meant to — the cap's memory
arithmetic was wrong in the first draft, in the flattering direction, and the review corrected it.

## Out of scope

The asynchronous fold, which has no frame buffer at all and allocates one array per fold by design;
Spec 3E's reasoning for that is separate and still holds. The `IOperationFold` rename #211 asks to be
folded into whichever budget slice is next open is deliberately left out — see the design doc.

> **Landed since.** The rename shipped standalone after all
> ([#211](https://github.com/karlssberg/Motiv/issues/211)) — every slice it could have ridden along
> with declined it for the reviewer-cost reason recorded here, and there were no candidates left.
