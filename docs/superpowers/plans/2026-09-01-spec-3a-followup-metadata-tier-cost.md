# Spec 3A follow-up — the cost that was a cycle — Plan

**Date:** 2026-09-01
**Status:** Shipped
**Source:** bundle spec [3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
build step 1 — the second of the two findings Spec 3A
([PR #134](https://github.com/karlssberg/Motiv/pull/134)) measured and deferred. The first was the
parent-vs-child divergence, which became
[#136](https://github.com/karlssberg/Motiv/issues/136) → [#188](https://github.com/karlssberg/Motiv/issues/188)
→ [#189](https://github.com/karlssberg/Motiv/issues/189). This is the other: cost. Tracked as
[#137](https://github.com/karlssberg/Motiv/issues/137).

Not a build-map slice: #137 is a bug ticket spawned by 3A, not a child of the build map
[#169](https://github.com/karlssberg/Motiv/issues/169), so it takes no row in that map's slice table —
the same call #136, #188 and #189 made. It is recorded on #169 under the follow-ups the shipped
slices spawned.

## The debt being paid

#137 reported two things, both measured by 3A while proving it had not caused them:

1. Reading `RootValues` over a left-deep, fully-causal `And` chain grew as roughly `n^2.6` — 46 s at
   2,000 operands, 163 s at 3,000.
2. A chain of 300 **identically-named** propositions exhausted memory outright.

The consequence 3A had to live with is in `DeepCompositionTests`: its `RootValues` depth-ceiling
regression runs on a short-circuiting `OrElse` chain rather than the `And` chain every other case in
that file uses, because the `And` chain took minutes. The ticket ends by asking for that to be
revisited *"so the case can go back on the `And` chain"*.

## The decision

**Measure before implementing.** Three follow-ups have landed in this area since #137 was written, all
of them in the same three files, and the ticket's numbers predate every one of them. So the first step
is not a fix but a differential measurement against `08f81f5c` — the commit before #136's repair.

If the defect is gone, the slice ships **no production change**: it ships the regression cover that
was missing, and the ticket's explicit ask. A ticket that fixed itself is only closable with evidence,
and the evidence has to be a test that fails against the code that had the bug — otherwise nothing
holds it fixed and the next reader has to re-derive all of this.

The ticket names a suspect — `MetadataNode.Resolve`'s collapse comparison — and, as with #189, expect
it to be where the cost is paid rather than what causes it.

## Explicitly out of scope

**Any change to `MetadataNode.Resolve`, `Underlying` or the collapse rule.** #189 established the
collapse is a load-bearing contract, with a 148-test refutation of the plausible alternative.

**Whatever quadratic remains.** If the extra factor is gone but a square is left, it needs
establishing whose square it is before anyone tries to remove it — and a lazily-materialised tier is
what 3A made eager to stop `Values` overflowing the stack, so unpicking it is a laziness-contract
change and not a walk optimisation. Measure it, characterise it, file it.

**A wall-clock assertion.** CI runs Windows and this repo has been bitten by timing-sensitive tests.
If a cost bound is wanted it has to be stated structurally.

## Steps

1. **Reproduce both findings against `08f81f5c`**, on a 1 MB thread in Release, at the ticket's own
   operand counts — not against prose.
2. **Measure the same probes against `main`.**
3. **Widen past the shapes the ticket named.** It reports `And`; check `Or`, `XOr`, `AndAlso` and
   `OrElse`, and each with and without a higher-order proposition in the chain. #189's review pass
   ended on exactly this lesson: a corpus establishes a boundary, it does not enumerate the shapes
   outside one.
4. **Establish the mechanism** well enough to assert it, rather than asserting the symptom alone.
5. **Write the regression cover**, and prove it red by checking `src/Motiv` out at `08f81f5c` and
   running it there — the only available form of "watch it fail for the right reason" when the fix
   already shipped.
6. **Move `Should_read_RootValues_of_a_deep_composition` back to `DeepAnd()`**, and rewrite the remark
   that explains why it was not.
7. **Full solution suite**, then the `code-simplifier` pass per `CLAUDE.md` — and if it changes the
   test rig, re-prove the cases red against `08f81f5c` afterwards, since a simplification that quietly
   makes a regression test pass against the bug is the one failure mode this slice cannot survive.
8. **File the residual** with its measurement, so the next session inherits it rather than re-deriving
   it.

## Verification

- Every new case fails against `08f81f5c` and passes on `main`, with the failure modes recorded.
- `DeepCompositionTests` green with the `RootValues` case on the fully-causal chain at 3,000 operands.
- Full solution suite green.
