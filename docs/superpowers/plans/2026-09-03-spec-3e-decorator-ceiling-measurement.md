# Spec 3E follow-up — The decorator ceiling, measured — Plan

**Date:** 2026-09-03
**Ticket:** [#145](https://github.com/karlssberg/Motiv/issues/145), residual 1
**Source:** bundle spec
[3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
§4 ("every public result-tree property behaves identically at every depth") and §6 step 1.

## Why this slice exists

Spec 3E ([#144](https://github.com/karlssberg/Motiv/pull/144)) folded the logical-operator family onto
the heap and named two recursions it was leaving standing: decorator nesting, and the concurrent async
operators. It left both on an argument rather than a number — decorator depth "comes from how many
propositions an author wraps around each other, which is bounded by the catalogue" — and #145 was filed
to say that argument was sound but unmeasured.

The ticket is explicit about what it wants first: *"the measurement, not the rewrite: build the
alternating shape, bisect the ceiling on a 1 MB thread as Spec 3E did for the other three, and decide
against a number."* It also names the question the measurement has to answer — *"whether a proposition
catalogue with deep reference chains can approach it."*

## Approach

1. **Bisect out of process.** A `StackOverflowException` is uncatchable and aborts the process, so the
   ceiling can only be found by a child process's exit code. A scratch console harness builds one shape
   at one depth on an explicitly-sized 1 MB thread; a shell bisection walks it. Same method Spec 3E
   used for its three numbers.
2. **Measure both residuals, decide only the first.** Residual 2's ceiling is a few extra shapes in the
   same harness, and having the number costs nothing. Its *rewrite* is a different algorithm and stays
   out.
3. **Verify the reachability claim rather than reasoning about it.** Build the reference chain through
   `PropositionSet`'s public hosting path and evaluate it, rather than reading `RuleBinder` and
   concluding.
4. **Hold the numbers.** Regression cover at roughly a quarter of each Debug ceiling, so a fatter frame
   fails a test rather than shipping.
5. **Correct what the measurement refutes**, and file what it cannot fix here.

## Expected fallout

Recorded before building, so the design doc can say which predictions survived.

- The alternating ceiling will be **lower than a pure decorator nest**, because each layer costs a fold
  re-entry rather than one wrapper frame. Unclear by how much.
- The async ceiling will be roughly **a quarter** of the synchronous one, matching Spec 3E's 633
  against 12,786.
- The catalogue reference chain will reach the alternating ceiling, because `RuleBinder.Decorate` wraps
  every named node and `DependencyGraph` refuses only cycles. If so, the published claim is wrong and
  the docs change in this commit.
- `MaxCompositionDepth` will not count the chain, because `CompositionDepthOf` stops at a `spec` leaf.

## Scope, and where it is cut

**In:** the measurement; the regression cover; the corrections to `docs/limits/index.md` and to
`MotivLimits`' XML documentation; plan and design.

**Out, with tickets:**

- The binding-edge cap that would make `MaxCompositionDepth` count across references. It needs a
  composed depth on `SpecRegistryEntry`, four binders changed, and an answer to what a *compiled*
  spec's depth is — Motiv has no stack-safe spec-tree walk to compute one. Its own slice.
- Anything that folds decorators into the evaluation driver. Spec 3E's decision 3 gives the reason and
  #145 explicitly defers it.
- Residual 2's parallel fold.

## Definition of done

- The ceilings are bisected and recorded where a reader of the test finds them.
- The catalogue question is answered with a test, not an argument.
- Every claim this slice refutes is corrected in the same commit that refutes it.
- `Motiv.Tests` and `Motiv.Serialization.Tests` green, plus the example suites, since assertion text is
  not touched but the limits page is.
