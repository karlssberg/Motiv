# Spec 3A follow-up — `UnderlyingMetadataSources` yields the parent — Plan

**Date:** 2026-09-01
**Status:** Shipped
**Source:** bundle spec [3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
build step 1 — the deferred half of [Spec 3A](2026-08-24-spec-3a-stack-safe-traversal.md)
(PR [#134](https://github.com/karlssberg/Motiv/pull/134), ticket
[#119](https://github.com/karlssberg/Motiv/issues/119)). Tracked as
[#136](https://github.com/karlssberg/Motiv/issues/136).

Not a build-map slice: #136 is a bug ticket spawned by 3A, not a child of the build map
[#169](https://github.com/karlssberg/Motiv/issues/169), so it takes no ledger row there.

## The debt being paid

Ticket 19's audit named `UnderlyingAssertionSources`, `UnderlyingAllAssertionSources` and
`UnderlyingMetadataSources` as "literally the same algorithm". Spec 3A folded all three onto
`PostOrderFold` and, in doing so, found the third had drifted further than the missing `field ??=`
the audit had spotted.

The two assertion walks yield **the child** the walk stopped at:

```csharp
booleanResult is IBooleanOperationResult
    ? booleanResult.UnderlyingAssertionSources
    : booleanResult.ToEnumerable()      // <- the child
```

`UnderlyingMetadataSources` yielded **the result itself**, once per such child, and carried no
`ElseIfEmpty` fallback:

```csharp
booleanResult is IBooleanOperationResult
    ? booleanResult.UnderlyingMetadataSources
    : this.ToEnumerable()               // <- the parent
```

3A preserved both verbatim, on the correct principle that a stack-safety rewrite must not quietly
change published semantics. It left an XML remark pointing at #136, and #136 was opened to answer one
question: **deliberate, or a copy-paste slip that has been shipping?**

## What the question turned on

#136's own framing — *"the fix is a one-line change plus an oracle update — but it changes what a
published property returns, so it wants a decision rather than a patch"* — is why this slice began
with measurement rather than a patch.

**Evidence gathered before deciding:**

1. **The property structurally cannot return a source.** On a bare atomic result it returns empty; on
   `a.Or(b).Or(c)` it returns the inner `OrBooleanResult` twice. Across every shape probed, it never
   once returned a leaf.
2. **The intended semantics are written next to the call site.** `MetadataNode.Resolve` — the only
   in-library consumer — hand-writes the correct outer step (`_ => cause.ToEnumerable()`) and
   delegates only the recursion to the property.
3. **Nothing asserted the shipped behaviour.** Applying the correction and running all eight test
   projects failed **150 tests, every one of them a seed of `StackSafeTraversalOracleTests`** — the
   differential gate 3A wrote to freeze the divergence. Not one behavioural test, in the library, the
   examples, Studio, the serialization stack or Blazor, depended on it.

   **That is not the same as "nothing changed", and the review pass caught the difference.** A
   corpus-wide before/after characterisation of every value-bearing surface found that `Values` and
   `MetadataTier.Metadata` are byte-identical across all 13,680 nodes, but **`RootValues` — a
   documented public property — changes on 566 of them, and `MetadataTier.Underlying` on 1,000.** In
   all 566 cases the new value is a *strict superset*: the fix recovers metadata the old walk was
   dropping and never removes any. So the defect was not inert after all, and the fix is a wider
   repair than the ticket describes. See the design doc.

Evidence for "deliberate": none found. No document describes it, no test asserts it beyond the
oracle, and the shape of the difference — `this` where a sibling has `booleanResult`, inside a
`SelectMany` lambda — is exactly what a copy-paste that missed a rename looks like.

**Decision: slip. Fix it.** Taken by the maintainer on the evidence above.

## Scope

One shippable PR.

1. Yield the child the walk stopped at, not the result.
2. Restore the `ElseIfEmpty(this)` fallback the siblings carry.
3. **Unify** — generalise `AssertionSourcesOf` to `SourcesOf<TResult>` and route all three walks
   through it, so the drift has nowhere left to recur.
4. Update `RecursiveTraversalOracle` to the corrected walk.
5. Remove the XML remark, which now points at a resolved ticket.

## Explicitly out of scope

**Whether the `ElseIfEmpty(this)` fallback is itself right** — whether a leaf should report *itself*
or *nothing* as the source of its own values. Raised as
[#188](https://github.com/karlssberg/Motiv/issues/188). See the design doc for why that has to be a
wholesale change across all three walks and cannot ride along here. **Settled since:** *nothing* —
see [the follow-up plan](2026-09-01-spec-3a-followup-source-fallback.md).

**The residual `RootValues` defect in higher-order subtrees.** The repair above does not reach it:
after the fix, `RootValues` still drops contributing operands when a higher-order result is in the
subtree. The boundary is exact — over the corpus the "reaches every causal leaf" invariant holds at
every node without a higher-order result and fails only at those with one. Raised as
[#189](https://github.com/karlssberg/Motiv/issues/189), and the regression test carries the exclusion
with that ticket named as the reason.

## Steps

1. **Probe, don't assume.** Characterise the current behaviour on concrete shapes and measure the
   blast radius of the correction before writing a line of it. *(Done — see above.)*
2. **Failing tests first.** In `UnderlyingMetadataSourcesTests`: the child-not-parent case, the
   never-an-operation-result invariant over the 150-seed corpus, the empty-causes fallback, and the
   agrees-with-its-sibling invariant. A fifth — `RootValues` reaches every causal leaf — was added
   after the review pass surfaced the wider repair; it fails on 33 of 150 seeds without the fix.
3. **Watch them fail for the right reason.** All four, before any production edit.
4. **Implement**, taking the unification rather than the one-line patch.
5. **Correct the oracle**, with a remark saying why this member alone is not a verbatim copy.
6. **Full solution suite**, plus a bare `dotnet build` for net472, which CI builds and local test runs
   skip.
7. **`code-simplifier` pass**, per `CLAUDE.md`.

## Verification

- All eight test projects green on net10.0.
- `StackSafeTraversalOracleTests` green — the differential gate agrees with the corrected walk at
  every node of every generated tree.
- `dotnet build Motiv.slnx` succeeds across all target frameworks, net472 included.
- The four new tests fail before the change and pass after it.
