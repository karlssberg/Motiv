# Spec 3B follow-up — the two renderers of one tree, and the run only one of them collapsed — Plan

**Date:** 2026-09-03
**Status:** Shipped
**Source:** bundle spec [3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
build step 2 — the decision log. Tracked as
[#139](https://github.com/karlssberg/Motiv/issues/139).

Not a build-map slice: #139 is a bug ticket spawned by 3B, not a child of the build map
[#169](https://github.com/karlssberg/Motiv/issues/169), so it takes no row in that map's slice table —
the same call #136, #188, #189, #137, #195, #192 and #193 made. It is recorded on #169 under the
follow-ups the shipped slices spawned.

**The first follow-up in the series that is not 3A's.** Every ticket from #136 to #193 came out of
Spec 3A's traversal work. This one was measured while building 3B, and its subject is the *description*
tree rather than the result tree.

## The debt being paid

Bundle spec §2 makes `RuleEvaluationResult` the decision log's payload, and names its six fields:
`Satisfied` / `Reason` / `Assertions` / `Values` / `Justification` / `Explanation`. §2 also puts the
sink "off the hot path" behind a bounded channel. The *projection that fills the record* is not off the
hot path, and cannot be — 3B considered moving it to the background writer and rejected it, because the
result tree memoises as it is read and none of that memoisation is documented thread-safe.

So every field of that record is paid for on the evaluation path of every audited rule. #139 measured
the total at roughly 35x the evaluation itself on a single-spec rule, growing faster than the
composition does, and filed the measurement.

## What the ticket suspected, and why it is worth re-deriving

#139 nominated a cause and said so tentatively:

> Likely the same defect as #137: `RuleEvaluationResult` reads `Values`, and #137 records the metadata
> tier as quadratic-plus. If so this is not a separate bug, and this ticket should be closed against
> that one once confirmed.

It also named the alternative, which is the sentence this slice exists to act on:

> Worth checking before assuming, since `ToEvaluationResult` also materialises `Assertions`,
> `Justification` and the whole explanation tree, any of which could be the real term.

Three things have happened to the metadata tier since #139 was filed: #136 fixed the cyclic
source edge, #137 measured the cure, #195 made the tier's union lazy and #193 cached the root
projection. A ticket whose diagnosis names a defect that four later tickets have worked on is a
hypothesis about code its author did not re-read — the standing lesson of #192 and #193, both of which
had a diagnosis that measurement corrected.

## Plan

1. **Instrument each of the six reads separately**, on a *fresh* result each time, since the tree
   memoises as it is read and a shared result would hide whichever read paid first. Report allocated
   bytes rather than elapsed time: allocation is what the defect produces, is nearly deterministic, and
   supports a fitted exponent where a clock at these sizes does not.
2. **Fit an exponent per read** across 25 → 1600 operands and identify the dominant term. Expect this to
   either confirm `Values` (closing #139 against #137, as the ticket proposes) or refute it.
3. **Separate overhead from output.** Per #195's rule, a square that is the size of what the API was
   asked for is not a defect. State, for whichever term dominates, how big the answer is.
4. **Find the mechanism**, then look for a working sibling to compare it against rather than inventing
   a scheme — §5's own instruction for this bundle is "measure before pooling", and the same applies to
   restructuring a walk.
5. **Write the cost test before the fix**, in the form `MetadataTierCostTests` established for #137 —
   the cost stated without a clock — and prove it red.
6. **Write the equivalence guards before the fix too, and prove they bite** by implementing the naive
   version first and watching them fail. A guard that has never refused anything is not evidence.
7. Cut anything that does not fit one PR and file it, rather than widening.

## Expected fallout

- The corpus oracle `DescriptionBaselineTests` hashes the rendering of every node of a generated corpus
  and is the instrument that decides whether a rendering change is byte-identical. Any restructuring of
  the reason renderer will be judged there first.
- `net472` is built by CI and not run locally. Anything the cost test needs from the runtime has to
  compile there, and the per-thread allocation counter does not exist on it.
- If the dominant term turns out not to be `Values`, #139 cannot be closed against #137 and its own
  title stops being fully accurate — the projection would still be superlinear after the dominant term
  is fixed. In that case the honest outcome is a comment re-scoping #139 to the residue rather than a
  close.
