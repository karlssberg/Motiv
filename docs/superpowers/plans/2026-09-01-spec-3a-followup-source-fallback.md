# Spec 3A follow-up — a leaf is not its own source — Plan

**Date:** 2026-09-01
**Status:** Shipped
**Source:** bundle spec [3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
build step 1 — the question deferred by the
[first 3A follow-up](2026-09-01-spec-3a-followup-metadata-sources.md) (PR
[#190](https://github.com/karlssberg/Motiv/pull/190), ticket
[#136](https://github.com/karlssberg/Motiv/issues/136)), which was itself the deferred half of
[Spec 3A](2026-08-24-spec-3a-stack-safe-traversal.md). Tracked as
[#188](https://github.com/karlssberg/Motiv/issues/188).

Not a build-map slice: #188 is a bug ticket spawned by 3A, not a child of the build map
[#169](https://github.com/karlssberg/Motiv/issues/169), so it takes no row in that map's slice table —
the same call [#136](2026-09-01-spec-3a-followup-metadata-sources.md) made. It is recorded on #169
under the follow-ups the shipped slices spawned, so the map is not silent about it.

## The debt being paid

All three source walks answered "what are my sources?" with **myself** when nothing contributed:

```csharp
return sources.Count == 0
    ? [result]      // <- self
    : sources.ToArray();
```

#136 closed the drift between the three walks by folding them onto one helper,
`BooleanResultBase.SourcesOf<TResult>`, and gave `UnderlyingMetadataSources` the fallback its two
siblings carried. That settled *consistency*. It did not settle whether the fallback is right, and
said so, raising the question here.

The question is narrow. Measured over the Spec 3A oracle corpus, the fallback fires on nodes with no
causes and never on an operation node, so it reduces to one thing:

> **What should an atomic result report as the source of its own values?**

## The decision

**Empty.** The property is named `Underlying…`, and a leaf has nothing underlying it. More concretely,
`[this]` makes the family non-terminating under the obvious fixpoint descent — a consumer writing
"keep taking the first underlying source until there are none" hangs at every leaf, and the hang is
invisible until it happens in production.

The counter-argument in the ticket — that `Values` is non-empty on a leaf, so empty asserts "nothing
produced these values" — is answered by keeping the two families apart rather than by keeping the
fallback. See the design doc: `Underlying*Sources` names *other nodes*; `Root*` projects *values*.
Only the first is a category error, and only the first can hang.

## Why this could not have shipped inside #136

Structural, not caution. Two of the three walks have in-library consumers and the third had none;
changing the fallback for one alone would have recreated exactly the inconsistency #136 existed to
remove. So the change is wholesale or not at all — and wholesale touches two public properties with
real callers.

#136's unification is what makes it cheap: the fallback now lives in one place instead of three, and
"wholesale" is no longer something a later change can quietly opt out of.

## Landed here, though not part of the slice

**The `ResultTreeGenerator` corpus-walk duplication** the `code-simplifier` pass surfaced — the same
nested loop seven times across four files in `src/Motiv.Tests/Traversal/`, plus two `SelectMany`
variants. It was initially declined for scope, on the grounds that two of the four files were not in
this diff and half-converting the folder would be worse than leaving it. Converting *all* of it turned
out to be nine mechanical call sites and no assertion changes, so it rides along as its own commit
rather than waiting on a merge it structurally could not be based on: the new
`UnderlyingSourcesFallbackTests.cs` exists only here, so a standalone branch would have had to stack
on this one.

`ResultTreeGenerator.CorpusNodes(seed)` is exactly `Corpus(seed).SelectMany(Nodes)`, and the ordering
is load-bearing rather than incidental — `DescriptionBaselineTests` hashes its rendering in traversal
order, so a helper that reordered or de-duplicated across roots would leave every other suite green and
fail only that baseline, with a diff pointing at the formatters. That baseline passing unchanged is the
proof the conversion preserved order.

## Explicitly out of scope

**`RootValues`' own `ElseIfEmpty(Values)`,** which the ticket asked to consider for the same sweep. It
stays, on a stated principle, and a test now pins the distinction so a later tidy-up cannot collapse
the two families by accident. The reasoning is in the design doc.

**The residual `RootValues` defect in higher-order subtrees** ([#189](https://github.com/karlssberg/Motiv/issues/189)),
untouched here.

## Steps

1. **Failing tests first**, in a new `UnderlyingSourcesFallbackTests`: the atomic case across all three
   walks; the higher-order-with-no-children case, which reaches the same branch by a different route;
   the fixpoint descent, bounded so the trap fails rather than hangs; and the premise the change rests
   on — no operation node has an empty causal set.
2. **Watch them fail for the right reason.** They did, and the premise test failed too — on four seeds
   whose corpora contain no operation node at all. That is a granularity bug in the test, not a
   refutation, and it moved the "actually exercised" guard to corpus level.
3. **Implement**: delete the fallback from `SourcesOf`, which leaves its `result` parameter unused;
   drop it and update the three call sites.
4. **Correct the oracle** — the same fallback, in the same three walks — and say in its class remark
   that its claim is now weaker for them.
5. **Widen what the change lets a test claim.** `Should_never_report_an_operation_result_as_a_source`
   had to exclude nodes with no causal values, because the fallback made such a node its own source.
   That exclusion is now unnecessary.
6. **Full solution suite**, plus a bare `dotnet build` for net472, which CI builds and local test runs
   skip.
7. **`code-simplifier` pass**, per `CLAUDE.md`.

## Verification

- All thirteen test projects green on net10.0; `Motiv.Tests` also green on net8.0 and net9.0.
- `StackSafeTraversalOracleTests` green — the differential gate agrees with the changed walks at every
  node of every generated tree.
- `dotnet build Motiv.slnx` succeeds across all target frameworks, net472 included.
- The new tests fail before the change and pass after it.
- **In the behaviour commit, no test outside the three source-walk suites changed** — that is the
  evidence the change is invisible to every in-library consumer. The corpus-walk conversion above is a
  separate commit and does touch other suites, so read the two apart: the claim is about `9feb7b93`,
  not about the branch.
