# Spec 3E follow-up — The decorator ceiling, measured — Design

**Date:** 2026-09-03
**Status:** Approved (design)
**Source:** bundle spec
[3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md).
Tracked as [#145](https://github.com/karlssberg/Motiv/issues/145) (residual 1). Follows
[#144](https://github.com/karlssberg/Motiv/pull/144) (Spec 3E), whose design doc named this ceiling as
one of the two it was leaving standing.
**Plan:** [2026-09-03-spec-3e-decorator-ceiling-measurement](../plans/2026-09-03-spec-3e-decorator-ceiling-measurement.md)

## Summary

Spec 3E made the logical-operator family flat at any depth and stated the honest residual: *"a chain of
combinators is now flat at any depth; a chain of alternating decorators still costs a frame per
decorator layer."* It defended leaving that alone with an argument about where depth comes from —
operator depth is attacker-controlled through a rule document's operand array, decorator depth is how
many propositions an author wraps, "bounded by the catalogue rather than by a request body."

Measured, **the argument is right about the mechanism and wrong about the bound.** The catalogue is not
a bound; it is the vector. A proposition that references another proposition composes one alternating
layer, and nothing counts the chain.

## The numbers

Bisected out of process — a stack overflow aborts rather than throws, so a ceiling is a child process's
exit code, not an assertion. Release, 1 MB thread (the ASP.NET request-thread stack), the same method
and the same stack size Spec 3E used for its three.

| Shape | Entry point | Last depth that returns |
|---|---|---|
| left-deep `And` chain (folded, baseline) | `Evaluate` | ≥ 50,000 |
| minimal decorator nest | `Evaluate` | 9,327 |
| explanation decorator nest | `Evaluate` | 7,262 |
| minimal decorator nest | `Matches` | ≥ 20,000 |
| **alternating operator / decorator** | **`Evaluate`** | **1,047** |
| alternating operator / decorator | `Matches` | 1,235 |
| async minimal decorator nest | `EvaluateAsync` | 1,302 |
| **async alternating** | **`EvaluateAsync`** | **261** |
| `AndConcurrently` nest | `EvaluateAsync` | 669 |
| `OrConcurrently` nest | `EvaluateAsync` | 669 |
| `AndConcurrently` nest | `MatchesAsync` | 1,037 |

Two of the plan's three predictions held. The async ceiling is a quarter of the synchronous one, as
Spec 3E's 633-against-12,786 predicted. The alternating shape is worse than a pure nest — but by **nine
times**, not the modest margin the prediction implied, and the stack trace at the ceiling says why:

```
Motiv.BooleanResultBase`1.And
Motiv.And.AndSpec`2.IOperationFold.Combine
Motiv.Traversal.EvaluationFold+ResultDriver`2.Combine
Motiv.Traversal.EvaluationFold.Fold
Motiv.Traversal.EvaluationFold.Evaluate
Motiv.And.AndSpec`2.EvaluateSpec
Motiv.SpecBase`2.EvaluateInternal
```

A decorator layer does not cost *a* frame. It costs a **whole fold re-entry** — seven frames, including
the driver's own — because the fold reaches the decorator, calls `EvaluateInternal` on it as a leaf,
and the decorator's operand is another operation that starts a fresh fold. The design's phrase "a frame
per wrapping layer" undercounts by an order of magnitude, and that is the whole distance between a
ceiling nobody can reach and one a catalogue reaches.

## Decisions

### 1. The catalogue is the vector, and the published claim is withdrawn

`RuleBinder.Decorate` wraps **every** node carrying a `name` or a `whenTrue`, and `RuleBinder.Bind`
wraps a named document's root. An authored proposition may reference another authored proposition, and
`DependencyGraph` refuses only *cycles* — never long chains. So a catalogue of propositions each
referencing the one before it composes exactly the alternating shape, one publish at a time.

Verified rather than argued: `PropositionChainDepthTests` builds the chain through `PropositionSet`'s
public hosting path — the same path `PublicHostingTests` exists to pin — and evaluates it through a
`Rule`. Bisected the same way as the rest, the catalogue chain's ceiling is **1,046** links
synchronously and **259** asynchronously, against the 1,047 and 261 of the hand-built shape. It does
not approach the ceiling; it *is* the ceiling, less the one layer the rule itself contributes.

`docs/limits/index.md` published the claim that this depth is "bounded by the catalogue". That
sentence is now false in the only sense that matters, and it is corrected in this commit rather than in
the slice that fixes the cap. **A wrong published bound is worse than a missing one**, because a reader
budgets against it.

### 2. No cap counts the chain, and the one named for it counts least

| Cap | Default | What one link scores |
|---|---|---|
| `MaxDocumentDepth` | 64 | 2 — its JSON nests twice |
| `MaxNodeCount` | 10,000 | 3 |
| `MaxCompositionDepth` | 4,096 | **1** |
| `MotivLimits.MaxEvaluationSize` | 250,000 | never reached; the stack goes at ~2,000 nodes |

The third is the interesting one, because it is the cap whose XML documentation says it measures "the
depth of the spec tree a node binds to, which is what result-tree walks recurse over — not the
document's JSON nesting." `CompositionDepthOf` counts operator levels and stops at a `spec` leaf, so it
sees neither the decorator levels within a document nor anything at all beyond a reference.

`PropositionChainDepthTests` states this as behaviour at its sharpest: a 200-link chain is accepted with
`MaxCompositionDepth` set to **1**, the lowest value the option admits.

Filed as [#201](https://github.com/karlssberg/Motiv/issues/201) rather than fixed here. The reason is
not size but an unanswered question: a **compiled** spec registered through the public
`SpecRegistry.Register` has an unknown composed depth, and Motiv has no stack-safe spec-tree walk to
compute one — decorators expose no common seam onto what they wrap, which is the same fact that keeps
them out of the evaluation driver. Assuming 1 re-creates the under-count; requiring the caller to
declare it changes a public API. That is a decision, not a patch.

### 3. The measurement found a second defect, in the backstop Spec 3E shipped

`MotivLimits.MaxEvaluationSize` says it bounds "the maximum number of nodes **a single evaluation** may
compose", and names exactly one exclusion — work done *inside* a node, a higher-order proposition over
a collection. A decorator's operand is not inside a node; it is part of the same logical composition,
so the documentation claims it is counted.

It is not. `EvaluationFold.Fold` holds its running size in a local (`var size = 1;`), and
`AsyncEvaluationFold` does the same, so every decorator re-entry starts a fresh count and the bound
applies **per fold**. At a limit of 100, a flat chain of 200 operands is refused and 50 decorator layers
of 10 operands — over a thousand nodes — is not.

This was found by writing the assertion the documentation implies and watching it fail, with the flat
chain passing in the same run so the failure could not be "the limit was never set". Both are kept:
`DecoratorSeamTests` carries the control *and* the three recorded holes, on `Evaluate`, `Matches` and
`EvaluateAsync` — three code paths that share the defect and not the code, so a fix that lands on one
is not mistaken for a fix.

Filed as [#202](https://github.com/karlssberg/Motiv/issues/202). Not fixed here for a reason worth
recording, because it is the kind of repair that looks like one line: **an ambient budget would charge
work the same documentation promises it does not.** A higher-order proposition evaluates its inner spec
once per element through the same entry point, so a counter spanning the top-level evaluation would
start refusing a 250,000-element collection under the default. The budget has to separate *the
composition tree* from *work inside a node*, and the fold cannot tell a decorator's `EvaluateInternal`
from a higher-order proposition's. That is the whole ticket.

One thing the ticket does inherit settled: a `[ThreadStatic]` counter is right for `Evaluate` and
`Matches`, which never leave the thread, and wrong for the async fold, where a continuation may resume
on a thread holding another evaluation's leftover count. Spec 3E made exactly this call for the frame
buffer, in the same direction and for the same reason.

### 4. Residual 2's claim is verified, not merely repeated

Spec 3E and #145 both assert that the concurrent operators are unreachable from a rule document because
`AsyncRuleBinder` composes only sequential ones. Given how decision 1 went, the claim was checked rather
than carried forward: `RuleOperator` has thirteen members — `Spec`, `Expression`, `And`, `Or`, `XOr`,
`AndAlso`, `OrElse`, `Not` and the five higher-order quantifiers — and no concurrent one, so no document
can name a fan-out. The claim holds. Its ceiling (669 nesting layers) is therefore author-controlled in
the way decision 1 showed the decorator ceiling is not, and residual 2 stays deferred on a bound that is
now measured rather than assumed.

### 5. The cover sits at a quarter of the *Debug* ceiling

The table above is Release. The suite runs Debug, where frames are fatter and the same bisection gives
876, 232 and 574 for the three shapes covered. Pinning against the Release numbers would have left the
async case at 64 against a real ceiling of 232 while appearing to claim a 4× margin against 261.

Each case sits at roughly a quarter of its Debug ceiling — a 3.4× margin — which is wide enough that
Windows CI's different frame sizes do not decide the outcome and narrow enough that a change making a
frame fatter fails a test. The synchronous depth of 256 is also four times
`RuleSerializerOptions.MaxDocumentDepth`'s default of 64, which is as deep as a *single* rule document
can nest: the one depth guarantee this slice can state without qualification.

### 6. The thread the folder measures on became a type

The mandatory `code-simplifier` pass found `OnASmallStack` written out three times, byte-identical,
alongside an identical `StackBytes` constant — Spec 3A's two depth suites and this slice's third. It is
now `SmallStack`, next to the folder's existing shared helpers (`OracleHelpers`, `ChainSpine`,
`ResultTreeGenerator`), and every call site is unchanged.

Worth recording because CLAUDE.md warns against over-DRYing this codebase and the warning does not
reach here: it is about builder paths whose duplication carries nuanced differences, and a thread
runner with zero variation carries none. Two defects were then fixed once instead of three times.
`throw failure;` reset the stack trace to the rethrow point, so a Shouldly failure inside a
small-stack body pointed at the helper rather than at the assertion — which is the worst place for
that to happen, in suites whose subject *is* a stack. It captures and rethrows now. And the thread was
foreground with an untimed `Join()`, so a wedged body would have outlived the run.

One consequence of the pass worth stating: `DecoratorNestingTests` joins
`MotivLimitsTestCollection` although it sets no limit. Its concurrent case composes 160 layers, well
past the 100 that `DecoratorSeamTests` lowers the process-wide `MaxEvaluationSize` to, and the
collection's `DisableParallelization` was already keeping the two apart — from outside, through an
attribute on another class. Joining the collection makes the suite's isolation its own.

## What this does not do

- **Fix either defect.** [#201](https://github.com/karlssberg/Motiv/issues/201) and
  [#202](https://github.com/karlssberg/Motiv/issues/202).
- **Fold decorators into the driver.** Spec 3E's decision 3 stands: the driver's type parameters cannot
  admit `ChangeModelTypeSpec` (whose model type varies) or `MetadataToExplanationAdapterSpec` (whose
  metadata type does). The measurement raises the value of doing it; it does not change the design.
- **Move `MaxCompositionDepth`.** #145 ruled that out, and nothing here disturbs the ruling — 4,096 is
  a cost-per-evaluation budget, and the new ceilings are a different constraint that a *different* cap
  should enforce.
- **Bound residual 2.** Its parallel fold is a different algorithm and its depth is author-controlled.

## Verification obligations

- The alternating shape evaluates and matches at 256 layers, and asynchronously at 64, on a 1 MB
  thread — the depths held against the Debug ceilings, not the Release ones.
- A concurrent-operator nest evaluates at 160 on the same thread, so residual 2 has cover before it has
  a fix.
- A 200-link proposition chain is accepted with `MaxCompositionDepth` at 1, and evaluates.
- The three recorded `MaxEvaluationSize` holes pass **and** the flat-chain control fails at the same
  limit, so the holes cannot be read as the limit being unset.
- Every number quoted in a doc comment or a docs page is one this slice bisected, in the build
  configuration it names.

## Outcome (recorded after the build)

No production behaviour changed: this slice is a measurement, its cover, and the withdrawal of two
claims that measurement refuted. The two defects it found are filed with the reasoning that makes each
of them a decision rather than a patch.

The general lesson is the one #137 and #195 already carry in a different register. Spec 3E's residual
was not hidden, hand-waved or undocumented — it was named precisely, in a design doc, with an argument
attached. The argument was still wrong, and the whole suite stayed green through it, because
no test builds a two-hundred-link catalogue. A bound stated but never bisected is a guess with a
citation.
