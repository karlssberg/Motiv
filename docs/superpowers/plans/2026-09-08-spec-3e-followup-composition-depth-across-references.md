# Plan — the caps that stopped at a `spec` leaf

Source: bundle spec [3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
§2 *Structural safety (19)* and §4. Ticket [#201](https://github.com/karlssberg/Motiv/issues/201),
follow-up to [#145](https://github.com/karlssberg/Motiv/issues/145)'s measurement.

## The defect

`RuleDocumentParser.CompositionDepthOf` scores a `spec` leaf as 0 however deep the proposition it
names composes, and counts no decorator level at all. A catalogue of propositions each referencing
the one before it therefore composes an unbounded alternating operator/decorator shape while every
cap reads small — and #145 measured that shape aborting the process at 1,047 links synchronously and
261 asynchronously on a 1 MB thread.

## The shape of the fix

1. A single measure — `(Composed, Decorator)` — computed from a `RuleNode` tree **against an
   `ISpecSource`**, so a `spec` leaf contributes the depth of the entry it resolves to.
2. `SpecRegistryEntry` carries that measure, so an authored proposition's depth is available to the
   next document that references it. A compiled registration carries 0 (see the design doc for why
   that settles #201's open question without a public API change).
3. Two bounds, because two resources are bounded:
   - `MaxCompositionDepth` (existing, 4,096) — total levels; the cost-per-evaluation budget.
   - `MaxDecoratorDepth` (new, 128) — the decorator subset; the stack budget, derived from #145's
     measured 261-layer async ceiling.
4. The check moves to **bind time**, where a source exists. The parser's source-blind check stays as
   the cheap pre-filter it already is.

## Steps

- [x] Failing tests: a reference chain deeper than either cap is refused; a single document nesting
      more decorators than the cap is refused; the two `PropositionChainDepthTests` cases that state
      the defect as behaviour are inverted.
- [x] `CompositionMeasure` + `CompositionDepth.Of` / `.ReportIfTooDeep`.
- [x] `SpecRegistryEntry.Depth`; stamped by `PropositionSet.AddModel`'s bind delegate.
- [x] `RuleSerializerOptions.MaxDecoratorDepth`.
- [x] The four binders check at their `Bind` entry point.
- [x] `docs/limits/index.md` corrected — it published the claim #145 refuted.
- [x] `code-simplifier` pass; full solution suite.

## Not in scope

Folding decorators into the evaluation driver (spec 3E design decision 3; #145 asked for the
measurement, not the rewrite). Re-deriving `MaxCompositionDepth` itself — 4,096 is still the right
cost budget now that the stack budget is a separate number.
