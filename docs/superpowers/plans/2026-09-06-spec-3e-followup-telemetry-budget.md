# Spec 3E follow-up — The observability that could refuse a decision — Plan

**Date:** 2026-09-06
**Ticket:** [#209](https://github.com/karlssberg/Motiv/issues/209)
**Source:** bundle spec
[3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
§2 Telemetry (04) and §2 Structural safety (19) — the bundle that holds *both* halves of this defect,
which is why it is one ticket and not two.
**Lineage:** Spec 3E ([#144](https://github.com/karlssberg/Motiv/pull/144)) →
[#145](https://github.com/karlssberg/Motiv/issues/145) (the measurement) →
[#202](https://github.com/karlssberg/Motiv/issues/202) (the budget made per-evaluation,
[#206](https://github.com/karlssberg/Motiv/pull/206)) → this, found by that PR's review round.

## Why this slice exists

#202 made `MotivLimits.MaxEvaluationSize` bound one *evaluation* rather than one *fold*, by making the
budget ambient. Ambient means **any** re-entry into the fold from inside a node spends the enclosing
composition's allowance unless something declares it excluded. #206 declared three seams —
higher-order element resolution, `EnumerableExtensions.Where`, and `Tap` — and did not declare Motiv's
own telemetry, which re-enters the fold in two ways:

- **Explanation rendering.** `EvaluationScope.Complete` tags the span with the result's `Reason` and
  `Assertions`. Both are lazily resolved, and resolving them runs the author's `WhenTrue`/`WhenFalse`
  delegates — which may evaluate propositions of their own.
- **Listener dispatch.** `Activity.Dispose()` synchronously runs every `ActivityStopped` callback. An
  exporter or audit hook that evaluates a proposition runs on the composition's thread, while the
  composition is still in flight.

So **attaching a listener could change what an evaluation decides.** That is the invariant
`EvaluationScope` already keeps against exceptions and did not keep against the budget: its own remarks
say a throw from explanation resolution *"must never escape and turn an otherwise-succeeding evaluation
into a failing one"*, and there is no reading of that sentence under which a refusal caused by the same
resolution is acceptable.

Spec 3's §2 puts the two halves in one bundle and states the telemetry side plainly: the core signals
are *"zero-alloc when unsubscribed"*, and per-node spans are opt-in precisely so that observability
does not change the shape of the thing observed. §4's invariant *"every public result-tree property
behaves identically at every depth"* is the structural-safety twin.

## The shape of the failure, which is worse than the obvious one

The refusal is raised **inside telemetry's own `catch`**, so it is swallowed. The count it left behind
is *not* released, because a nested fold's `Ownership` deliberately does not release — that is what
makes the bound per-evaluation in the first place. So the exception surfaces at the next node the
composition charges: a node that did nothing wrong, in a stack frame that names neither telemetry nor
the delegate that spent the budget.

The ticket rated this PLAUSIBLE rather than CONFIRMED, on the grounds that *"each condition is
supported by the code, none is common, and the stack of them was not built end to end."* Building it
end to end is the first job of this slice.

## The two candidate fixes, and why only one of them is a fix

#209 offers three candidates and favours the first:

1. **Check before incrementing** — `if (_spent + 1 > Max) ThrowExceeded(); _spent++;` so a swallowed
   refusal leaves the count *at* the limit rather than over it.
2. **Reset on the throw path** — an EH region on a path that is currently free.
3. **Leave it and document it.**

**Candidate 1 changes nothing observable, and the plan is to prove that rather than assume it.** At
`_spent == Max` the next `Charge()` still evaluates `Max + 1 > Max` and throws; the overshoot is a lie
about how much was spent, but every subsequent charge refuses under both encodings, an `Exclude()`
parks and restores either value identically, and a root `Ownership` zeroes both. The expectation is
that the three red cases below stay red with candidate 1 applied — if they do, it does not ship, and
the TDD rule that no production code lands without a test that failed for it is what keeps it out.

The fix that does hold is the ticket's own aside, which it calls *"arguably the more important half"*:
telemetry is work inside a node by exactly the argument that already excludes `Tap`, and it should
declare itself so.

## Approach

1. **Three red cases**, in `src/Motiv.Tests/Traversal/TelemetryBudgetTests.cs`, each stated as a
   *difference*: the same composition, model and limit, evaluated once with nothing listening and once
   with a listener attached. The untraced call is the control — without it the case would pass on a
   limit that was merely generous.
   - explanation rendering, under `ExplanationDetail.Full`;
   - an `ActivityStopped` listener that evaluates, under `ExplanationDetail.None` so nothing else can
     be charged;
   - the same listener on the `Fail` path, where the caller catches a sub-evaluation's exception and
     carries on — #209's user-code shape.

   A fourth was added mid-slice when the *Expected fallout* prediction below turned out to be right;
   the design doc records it and the mutation that proves it earns its place.
2. **Declare the exclusion in `EvaluationScope`**, not at its four call sites. `SpecBase`,
   `PolicyBase`, `AsyncSpecBase` and `AsyncPolicyBase` all funnel through it, so one declaration per
   scope method covers every instrumented boundary and cannot be missed when a fifth is added. This is
   the "structural rather than repeated" altitude [#208](https://github.com/karlssberg/Motiv/issues/208)
   asks for on the higher-order side.
3. **Correct the seam list wherever it is written down** — `EvaluationBudget`'s remarks,
   `MotivLimits.MaxEvaluationSize`'s remarks and `docs/limits/index.md` all enumerate the three
   exclusions, and #208 records that a wrong seam list is itself a defect: #206 shipped claiming two
   seams when there were three, and that was found by reading rather than by CI.
4. Full solution suite, then the mandatory `code-simplifier` pass.

## Scope

**In:** the four `EvaluationScope` terminators; the three cases; the three prose locations that
enumerate seams.

**Out, and why:**

- **Candidate 1**, unless a case can be written that fails without it. Recorded either way.
- **A convention test over the seam list** ([#208](https://github.com/karlssberg/Motiv/issues/208)).
  #208 already owns it, and its shape depends on the 19-call-site question that ticket has to settle
  first. Widening here would put the enforcement in the wrong PR.
- **The asynchronous carrier** ([#204](https://github.com/karlssberg/Motiv/issues/204)). The four
  scope methods are synchronous and the exclusion is inside them, so the async boundaries get it for
  free — but the async fold's budget is still its own, which is #204's whole subject.

## Expected fallout

The exclusion parks rather than discards, so a listener whose *own* evaluation is oversized must still
be refused on its own account. If the suite goes green on a version that discards the count instead,
the cases are not stating enough — that is the failure mode
`Should_resume_the_compositions_count_after_a_higher_order_operand` exists for on the higher-order
side, and it was found there by deleting the restore and watching the suite stay green.
