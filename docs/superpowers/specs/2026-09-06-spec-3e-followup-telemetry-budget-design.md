# Spec 3E follow-up — The observability that could refuse a decision — Design

**Date:** 2026-09-06
**Ticket:** [#209](https://github.com/karlssberg/Motiv/issues/209)
**Plan:** [`2026-09-06-spec-3e-followup-telemetry-budget.md`](../plans/2026-09-06-spec-3e-followup-telemetry-budget.md)
**Source:** bundle spec
[3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
§2 Telemetry (04) and §2 Structural safety (19).
**Lineage:** Spec 3E ([#144](https://github.com/karlssberg/Motiv/pull/144)) →
[#145](https://github.com/karlssberg/Motiv/issues/145) →
[#202](https://github.com/karlssberg/Motiv/issues/202) / [#206](https://github.com/karlssberg/Motiv/pull/206)
→ this, found by #206's own review round.

## What the defect was

#206 made `MotivLimits.MaxEvaluationSize` bound one *evaluation* rather than one *fold*, by moving the
count from a fold-local into an ambient thread-static. Ambient is what makes a decorator's nested fold
spend its caller's budget — the whole point — and it is also what makes **every other** re-entry into
the fold spend it, including re-entries that are not composition at all. #206 declared three seams
excluded. It did not declare Motiv's own telemetry, which re-enters the fold in two ways:

| Where | What re-enters |
|---|---|
| `EvaluationScope.Complete` → `TrySetExplanationTags` | `result.Reason` / `result.Assertions` are lazily resolved, which runs the author's `WhenTrue`/`WhenFalse` delegates |
| `Complete` / `Fail` / `Cancel` → `Activity.Dispose()` | every `ActivityStopped` callback, synchronously, on the composition's thread |

So **attaching a listener could change what an evaluation decides** — the exact thing
`TrySetExplanationTags` already refuses to allow for exceptions, in a comment that says so:

> that throw must never escape and turn an otherwise-succeeding evaluation into a failing one

There is no reading of that sentence under which a *refusal* caused by the same resolution is
acceptable. The rule was written down; only one of its two failure modes was implemented.

### Why the shape is worse than "tracing costs budget"

The refusal is raised inside telemetry's own `catch`, so it is swallowed. The count it left behind is
not released, because a nested fold's `Ownership` deliberately does not release — that is what makes
the bound per-evaluation. So the exception surfaces at **the next node the composition charges**: a
node that did nothing wrong, in a stack that names neither telemetry nor the delegate that spent the
budget. The red run before the fix shows it exactly — the throw comes from the outer `AndSpec`'s own
fold, not from anywhere near the cause:

```
Motiv.SpecException : The evaluation exceeded the maximum size of 3 nodes.
   at Motiv.Traversal.EvaluationBudget.Charge()
   at Motiv.Traversal.EvaluationFold.Fold[...]
   at Motiv.And.AndSpec`2.EvaluateSpec(TModel model)     <- the innocent node
   at Motiv.SpecBase`2.EvaluateSpecInstrumented(TModel model)
```

## The candidate fix that is not a fix, and the evidence

#209 offers three candidates and favours the first — *check before incrementing*, so a swallowed
refusal leaves the count at the limit rather than over it, described as *"cheapest, and probably
enough."*

**It is not enough, and it changes nothing observable at all.** At `_spent == Max` the next `Charge()`
still evaluates `Max + 1 > Max` and throws. The overshoot is a lie about how much was spent, but every
subsequent charge refuses under both encodings, an `Exclude()` parks and restores either value
identically, and a root `Ownership` zeroes both. That argument was not trusted on its own: candidate 1
was applied to `Charge()` with the exclusion reverted, and **all three then-existing cases stayed
red**, with the same message from the same frame. It does not ship. Nothing in the suite would have
gone red for it either, which is the TDD rule doing its job rather than a formality — a production
change no test can distinguish is a change nobody can maintain.

The ticket's own aside is the fix, and it labels it correctly:

> That is arguably the more important half — explanation rendering is not composition, and #206's
> "declared, not detected" exclusion arguably ought to cover it.

## The fix

`EvaluationBudget.Exclude()` in all four `EvaluationScope` terminators. Nine lines, four of them the
same line.

**Why in `EvaluationScope` and not at its call sites.** Four instrumented boundaries funnel through it
— `SpecBase`, `PolicyBase`, `AsyncSpecBase`, `AsyncPolicyBase` — so one declaration per scope method
covers all of them and cannot be missed when a fifth arrives. This is the altitude
[#208](https://github.com/karlssberg/Motiv/issues/208) asks for on the higher-order side, where the
equivalent fix is nineteen repeated edits *"and nineteen chances to miss one — with nothing that would
go red if one were missed."* Here the structural version was available for free, so it was taken.

**Why `Exclude` and not `Ownership`.** They look alike and are opposites. `Ownership` asks *when I end,
does the evaluation end?*; `Exclude` asserts *this span of work was never part of the evaluation*.
`Exclude` parks the caller's count and hands it back, so a listener whose own evaluation is oversized
is still refused on its own account while costing the composition nothing. Zeroing instead would leak
in the permissive direction — see the fourth case below.

**Why `Start` and `Cancel` carry it too, though nothing tests them.** `Start` runs the sampler
callback; `Cancel` disposes the activity, running the same listener callbacks `Complete` does. Their
exposure is identical and the line is the same line, so leaving them out would be an inconsistency to
explain rather than a saving. Said plainly rather than implied: `Cancel` is the asynchronous path,
whose fold still holds its own budget ([#204](https://github.com/karlssberg/Motiv/issues/204)), so
this is symmetry rather than a tested claim — a distinction #208 records as worth making, because a
check reporting a property it is not actually checking is worse than no check.

## The four cases

In `src/Motiv.Tests/Traversal/TelemetryBudgetTests.cs`, each stated as a **difference** — the same
composition, model and limit, evaluated once with nothing listening and once with a listener attached.
The untraced call is the control; without it the case would pass on a limit that was merely generous.

| Case | What it holds |
|---|---|
| `Should_not_charge_explanation_rendering_to_the_composition` | the `Reason`/`Assertions` half, under `ExplanationDetail.Full` |
| `Should_not_charge_a_listeners_own_evaluation_to_the_composition` | the `ActivityStopped` half, under `ExplanationDetail.None` so nothing else can be charged |
| `Should_not_charge_a_failed_spans_listener_to_the_composition` | the same on the `Fail` path — #209's user-code shape, a caller catching a sub-evaluation's exception and carrying on |
| `Should_resume_the_compositions_count_after_a_traced_evaluation` | that the exclusion **parks** rather than discards |

**The fourth exists because the first three cannot see the permissive direction.** They all assert an
evaluation *succeeds*, and a discarding exclusion makes more evaluations succeed, not fewer — so a
version of `Exclusion.Dispose` that zeroed the count instead of restoring it would leave every one of
them green. The fourth asserts a *refusal* instead, and it was red-proved: with `Dispose` mutated to
discard, it fails alone while the other three pass. That is the same trap
`Should_resume_the_compositions_count_after_a_higher_order_operand` was written for on the
higher-order side, where it was found by deleting the restore and watching the suite stay green.

### The fixture, and the arithmetic that was wrong first

Reaching the defect needs a span opened **while a fold is already unwinding**, which only user code can
do: composed propositions take `EvaluateInternal` precisely so a composition emits one span at its root
rather than one per node. So the fixture is a two-proposition composition whose left operand's
*predicate* calls the public `Evaluate` on an inner proposition, and the inner proposition's
explanation costs nineteen nodes to render.

The inner proposition has to be an **unnamed** explanation proposition. Motiv's `== true`/`== false`
suffix rule means a named one renders `"{name} == false"` from the name alone and demotes the
delegate's value to `Values`, which telemetry never reads — so naming it would make the fixture inert
while looking identical. The unnamed shape is the only one whose assertion text is produced by user
code at all, and `WhenTrue(string)` + `WhenFalse(delegate)` is the overload that permits a
parameterless `Create()`.

The limit was first written as 4, derived: three for the chain (`2n - 1`) plus one for the inner
proposition's own fold. **That derivation is wrong — the composition costs 3.** It was caught by the
fourth case's control failing to throw, and settled by bisecting the limit rather than by re-reasoning:
the fold charges a leaf before descending into it, and the leaf's *predicate* is what re-enters, so the
inner fold adds nothing the composition was not already charged. The published number is now the
measured one, and the comment says it was measured. Left at 4 the suite would still have been green
with a node of headroom nobody had accounted for — which is how a case that pins an exact cost quietly
becomes a case that pins a comfortable margin.

## What was corrected besides the code

Three places enumerate the excluded seams, and all three were wrong the moment the fourth existed:
`EvaluationBudget`'s remarks, `MotivLimits.MaxEvaluationSize`'s remarks, and `docs/limits/index.md`.
#208 records why that matters — #206 shipped claiming two seams when there were three, and it was found
by reading rather than by CI.

The public docs also carried a claim that is now half wrong. They said *"a `WhenTrue`/`WhenFalse`
delegate resolved while another evaluation is running **is** counted."* It still is when **you** resolve
it; it is not when telemetry does. The sentence now says *"a delegate you resolve yourself"*, and the
page gains a paragraph naming telemetry as the one entry on the excluded list a caller does not write.

Nothing enforces that these three lists agree with the code. That is #208's convention test, and it
stays #208's: its shape depends on the nineteen-call-site question that ticket has to settle first, and
putting the enforcement here would land it in the wrong PR.

## What this does not fix

- **[#208](https://github.com/karlssberg/Motiv/issues/208)** — a higher-order predicate through
  `As(...)` is still charged. Nineteen call sites, and the ticket already argues for the combined
  helper over the repeated scope.
- **[#204](https://github.com/karlssberg/Motiv/issues/204)** — the asynchronous fold still bounds one
  fold. The four scope methods are synchronous, so the async boundaries inherit this exclusion; their
  *budget* is still their own.
- **The `Charge()` overshoot.** Left as it is, deliberately and with the measurement above. If a case
  is ever found that can distinguish the two encodings, that case is the argument for changing it — and
  it will be a better argument than "one extra comparison on the hottest path in the library, probably
  enough."

## Verification

- `Motiv.Tests` — 5,968 on net8.0, net9.0 and net10.0 (5,964 before, plus these four).
- Full solution — 17 test projects, all green.
- `net472` **builds** but does not run here: the VSTest host needs `mono`, which is not installed on
  this machine. CI runs it. The change is framework-agnostic (a `using` over a `ref struct`, already
  used by the fold on every target) and `dotnet build` over all four TFMs succeeds.
