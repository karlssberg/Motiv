# Spec 3E follow-up — The contract that was free only at the top — Design

**Date:** 2026-09-06
**Ticket:** [#205](https://github.com/karlssberg/Motiv/issues/205)
**Plan:** [`2026-09-06-spec-3e-followup-frame-buffer-depth.md`](../plans/2026-09-06-spec-3e-followup-frame-buffer-depth.md)
**Source:** bundle spec
[3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
§2 Structural safety (19).
**Lineage:** Spec 3E ([#144](https://github.com/karlssberg/Motiv/pull/144)) →
[#145](https://github.com/karlssberg/Motiv/issues/145) →
[#202](https://github.com/karlssberg/Motiv/issues/202) / [#206](https://github.com/karlssberg/Motiv/pull/206)
→ this, found while building #206's allocation guard.

> **Renamed since.** `IOperationFold`, named below, is now `IFoldableOperation`
> ([#211](https://github.com/karlssberg/Motiv/issues/211)). The old name is kept here because this document is the record of what
> shipped on the date above.

## What the defect was

`EvaluationFold` kept one frame buffer per thread, in a single `[ThreadStatic]` slot, taken rather than
borrowed:

```csharp
[ThreadStatic] private static Frame<TModel, TMetadata, TValue>[]? _buffer;

internal static Frame<TModel, TMetadata, TValue>[] Take()
{
    var buffer = _buffer;
    if (buffer is null)
        return new Frame<TModel, TMetadata, TValue>[InitialCapacity];
    _buffer = null;
    return buffer;
}
```

Taken rather than borrowed is not a detail — it is the correctness of the whole type. The fold descends
into operands that are themselves operations and evaluates everything else through the operand's own
evaluation, which **re-enters the fold**. A nested fold handed the same array would overwrite the frames
its caller is still unwinding. Nulling the slot is what prevents that, and the cost of nulling it is
that the nested fold finds nothing.

So the buffer was reused by exactly one fold per evaluation: the outermost. Every other fold allocated.
The remarks knew and called it *"correct and rare"*.

**It was never rare, and the arithmetic says exactly how un-rare.** One frame array per nested fold, and
one nested fold per decorator layer:

| Layers (4 operands each) | Allocated, warm |
|---|---|
| flat chain of 16 | 0 bytes |
| 4 | 456 = 3 × 152 |
| 8 | 1,064 = 7 × 152 |

152 bytes is not a coincidence to be checked against a table — it is derivable, and deriving it is what
identifies the culprit beyond argument. `Frame<int, string, bool>` is one object reference and five
`bool`s, padded to 16 bytes; `InitialCapacity` is 8; 8 × 16 + 24 bytes of array header = 152. The
allocation is `FrameBuffer.Take()` and nothing else — not a driver, not a closure, not the budget.

`SpecBase<TModel>.Matches` therefore allocated **linearly in the decorator depth of the composition**,
while documenting that it allocates nothing. `Motiv.Serialization`'s `RuleBinder.Decorate` wraps every
node carrying a `name` or a `whenTrue`, so a `Matches` over any document-composed rule paid it.

## Why this is #202's defect and not a new one

Both are the same sentence with a different noun: *the contract is stated per evaluation and enforced
per fold.* #202's was `MaxEvaluationSize`, whose count lived in a fold-local so every re-entry started
a fresh one. This one is the allocation-free claim, whose buffer lived in a single slot so every
re-entry allocated a fresh one. The reachable shape is identical, the ratio is identical (one per
decorator layer), and both were invisible for the identical reason: **the only test held the flat
case.** `EvaluationBudgetTests.Should_carry_the_budget_without_allocating` measures a
`FlatChain(operands: 16)` — one fold, and therefore the one shape where the contract was true.

That is worth stating as a rule rather than as a fact about two tickets. *A property that is per-fold
and claimed per-evaluation cannot be distinguished by any test whose composition is one fold*, and a
flat chain of sixteen looks like a demanding case while being the least demanding one available. It is
the same lesson [#189](https://github.com/karlssberg/Motiv/issues/189) recorded from the other end —
a fix that passes because the contract it breaks is untested is not a fix.

## The fix, and the option that could not work

`FrameBuffer` keeps a per-thread array of buffers **indexed by nesting level**. `_depth` counts the
folds in flight, which is the level the next one enters at; slot `d` holds the buffer level `d` last
returned. `Take` and `Return` are the only writers and are paired by `Fold`'s `try`/`finally`, so
`_depth` is back to zero whenever no fold is running, however a fold leaves. `Fold`'s control flow is
untouched.

Two properties are structural rather than argued:

- **The reuse really is depth-indexed.** Level `d` is handed level `d`'s own array, at every depth. A
  deep level is never given a shallow level's buffer and made to resize it, and the converse — which
  matters more, and is the subject of the review section below — cannot happen either.
- **The aliasing invariant.** A buffer is taken rather than borrowed: `Take` nulls the slot, so the
  pool never names an array a fold is using. Two live folds are at two different levels, so they read
  two different slots; nulling makes that true of the same level across time as well.

#205 offers "a depth-indexed stack of buffers" as its first option, and the two nouns pull in different
directions — a stack indexes by *position in the pool*, which is not the same thing as the level. That
distinction is the whole of the review round below, and it took a red test to see.

### Why the region-partitioned option is not contained in `FrameBuffer`

#205's second option is one array partitioned by the caller's high-water mark, so a nested fold appends
above its caller's frames. The ticket flags `Array.Resize` as "a shared concern across folds that are
unwinding independently" and concludes that both options are contained inside `FrameBuffer`.

Checked, that conclusion does not hold, and the reason is worse than shared concern. `Fold` holds
`frames` in a **local**:

```csharp
var frames = FrameBuffer<TModel, TMetadata, TValue>.Take();
...
if (depth == frames.Length)
    Array.Resize(ref frames, depth * 2);
```

A nested fold that grew the shared array would allocate a new one and copy into it. The outer fold's
local still points at the array *before* the resize, so it would unwind against a stale copy — silently,
since the copy holds its frames intact. Keeping it correct means re-reading the buffer from the
thread-static after every leaf evaluation, on the hot path. That is a change to the fold, not to its
buffer.

This is recorded rather than left implicit because the ticket's own framing invited the wrong choice:
"cheaper, and closer to what an explicit stack machine would do" is true of the shape and false of the
version that would have to be written.

## The review round, which found the fix's own defect

**This is the part worth reading.** The first `code-simplifier` pass ran against a pool that was a
**stack**: `Take` popped, `Return` pushed. Everything above about depth-indexing was written of that
design, on the argument that stack discipline gives the pairing for free — folds return innermost-first,
so the next evaluation pops each fold back its own buffer.

That argument is true **up to the cap and false past it**, and the review found the false half.

`Return` refuses once the pool is full. Buffers arrive innermost-first, so a full pool refuses the ones
that arrive last — the **outermost** folds. And the outermost fold is precisely the wrong one to drop:
a fold's buffer is as wide as its operand run, and the outermost run is the one a caller writes by hand
and can make wide, where the levels a decorator chain adds are two frames each.

A top-level operator run of sixty operands over a body decorated more than sixteen layers deep would,
every evaluation: pop some inner level's eight-frame array, walk the entire 8 -> 16 -> 32 -> 64 resize
ladder, and then have the grown buffer refused because the pool had filled from below. Three
allocations per evaluation, permanently — and **worse than the single slot this change replaced**,
which at least handed the outermost fold its own buffer back, because the outermost was the last to
return.

So the fix carried a defect of the same family as the one it was fixing: a claim true of the common
case and false of the shape the ticket exists for. This design doc said *parity with the old behaviour
past the cap*; for that shape there was none.

The repair is to index by **nesting level** rather than by stack position. Level `d` is served level
`d`'s buffer at every depth, and what goes unserved past the cap is the deep tail — the narrow,
two-frame decorator levels — rather than the one level that can be wide. It also makes the
"depth-indexed" claim literally true rather than emergent, which is why the paragraph that argued for
it could be replaced by a one-line invariant on the field.

**The finding was taken as a hypothesis, not as a verdict.** It was written as a failing test first:

```csharp
var narrow = Allocated(WideOverNested(layers: 20, width: 1));
var wide   = Allocated(WideOverNested(layers: 20, width: 60));
wide.ShouldBe(narrow);
```

Same fold count, same number of levels past the cap; the only difference is the width of the outermost.
Red under the stack pool, green under the level-indexed one. That matters because a review finding
about a hot-path pool is exactly the kind of claim that is persuasive and wrong; this one was
persuasive and right, and the red is what established which.

Four other findings were taken in the first round: the memory arithmetic in the new constant's remarks
was wrong (below); `MaxCachedDepth` was declared away from its two sibling constants; `Return`'s push
did lazy initialisation, indexing and a post-increment in one expression, against the repo's stated
preference for explicit over compact; and the test file excluded net472 wholesale when only its four
*measuring* cases need to — the correctness guard at depth is exactly the case an indexing mistake
would break, and indexing is not framework-specific.


### The second round, which found only claims

A second pass over the redesigned pool found no defect in the code and four in what the code *says*
about itself. That asymmetry is the useful part: the first round changed the design, the second changed
only sentences, and both were worth running.

- **`MaxCachedDepth`'s summary was off by one.** It read "the deepest nesting level whose buffer is
  kept", but sixteen is an exclusive bound and the array's length, so the deepest level served is
  fifteen. It is a *count*, exactly as its sibling `MaxCachedCapacity` is a count of frames — and the
  test file already had it right ("sixteen nested folds are sixteen retained buffers"). Reworded rather
  than renamed, so the two constants read the same way.
- **The class remark credited the wrong mechanism.** It opened "a buffer is taken rather than borrowed"
  and then concluded "that is why there is one per level" — two true statements welded into one false
  implication, vestigial from the single-slot text. Per-level indexing answers the re-entrancy hazard;
  taking rather than borrowing answers the pinning one. `Take`'s inline comment had the second right all
  along, so the class remark now defers to it instead of restating it wrongly.
- **The sizing argument omitted an axis, and this change multiplied it by sixteen.** `MaxCachedCapacity`
  bounds width and `MaxCachedDepth` bounds depth, but the pool is a static of a **generic type** — so
  retention is 32 KB × the closed instantiations a thread has touched, two per `(TModel, TMetadata)`
  pair. An application with a few dozen model types reaches the 2 MB figure by a route neither bound
  watches. It is not a defect: the worst case still needs every level of every instantiation grown to
  the width bound, and the axis pre-dates this change at a sixteenth of the size. But presenting the
  sizing as complete while omitting it is the same over-claim the first round caught in the arithmetic,
  and it is now stated in the constant's own remarks.
- **`_depth` is per instantiation, not per thread**, and its summary said otherwise. A re-entry through
  `ChangeModelTo` lands in a different closed type with its own pool and its own count, and enters at
  level zero — correct, since separate pools cannot alias, but not what "folds in flight on this thread"
  describes.

One genuine code gap was raised and deliberately left: `Take` increments `_depth` before allocating its
fresh buffer, so an `OutOfMemoryException` from that allocation would leave the count high by one for
the life of the thread. Nothing aliases — a taken slot is nulled whatever the count says — and the
consequence is that the pool quietly stops serving that instantiation. Under OOM that is moot, and the
suggested reorder costs a second write to `_depth` on the hot path. The claim was qualified instead of
the code changed, which is the right trade and is recorded here so the next reader does not re-derive
it.

The review also chased a parallel-flake risk this suite looked to have — its fixtures run to 216 nodes
while sibling suites lower `MaxEvaluationSize` process-wide to as little as 5 — and **dismissed it with
evidence**: every class that lowers the limit sits in `MotivLimitsTestCollection`, whose
`DisableParallelization` withdraws it from parallel execution entirely. The dependency on another
class's attribute is real, and is now written down as knowingly accepted rather than left to be
rediscovered.

## The retention cap, and why it is a real bound rather than a formality

The single slot needed no depth bound because there was no depth. A per-level pool does, and the number
is not free-floating: **#201 supplies it.** The alternating ceiling is 1,047 layers synchronously, so an
unbounded pool would retain, in the worst case, a buffer per level of the deepest composition ever
evaluated on that thread. At `MaxCachedCapacity` (64 frames, ~32 bytes each for the result driver) that
is about 2 MB per thread per generic instantiation — which is, to the digit, the figure
`MaxCachedCapacity`'s own remarks already name as unacceptable:

> where holding a two-megabyte array per thread for the rest of the process would not be.

So the cap is the existing rule applied to the new axis, not a new policy. Past it a fold allocates its
own buffer, which is exactly what every nested fold did before this change, so the degradation is to
the old behaviour rather than to a new one.

**The honest way to size it is against what was retained before, and the first draft did not do that.**
It claimed *tens of kilobytes, three orders below the two megabytes* — flattering and wrong, because it
silently compared the new retention against the unbounded case rather than against the old one. The
review caught it. The real arithmetic: a thread used to retain **one** buffer, so at the width bound
about 2 KB per instantiation. `MaxCachedDepth = 16` takes that to 16 × 64 × 32 ≈ **32 KB** — a
sixteenfold rise, and about 64× below the 2 MB line, which is under two orders and not three.

The rise is the price of the fix and is worth stating as such rather than dressing down. Thirty-two
kilobytes per thread per instantiation, in the worst case where *every* level grew to the width bound,
buys an allocation-free `Matches` on every document-composed rule.

And *per instantiation* is load-bearing in that sentence — see the second review round below. The pool
is a static of a generic type, so nothing here bounds how many pools a thread accumulates.

**Sixteen was chosen from the memory, not from the tests.** The deepest allocation case in the suite is
eight layers; a cap of eight would have passed it, and would have been a cap fitted to its tests. The
cases at 16 / 20 / 24 exist to make the cap's own behaviour observable rather than incidental.

## The verification, and what each part refuses

Three of the seven new cases are the contract; four are about the fix's own failure modes.

| Case | Refuses |
|---|---|
| `Should_match_a_flat_composition_without_allocating` | the control — a build where `Matches` had stopped folding at all, on which every other case would pass |
| `Should_match_a_decorator_layered_composition_without_allocating` (2, 4, 8) | the defect, at three depths because it is linear in depth and one case cannot tell "no allocation" from "one allocation, deeper than this" |
| `Should_pay_one_buffer_per_layer_past_the_retention_cap_and_nothing_before_it` | a cap that is not where it says it is, in **both** directions |
| `Should_keep_the_buffer_of_a_wide_outermost_fold_past_the_retention_cap` | a cap that drops the **wrong** levels — the one finding of the review round, written red first |
| `Should_evaluate_a_composition_nested_deeper_than_the_retention_cap` | a cap that drops a buffer *and* corrupts something — it would surface as a wrong answer at depth, not as a throw. The only case that runs on all four targets, since an indexing mistake is not framework-specific |

The cap case is stated as three measurements against each other rather than against a constant:

```csharp
atTheCap.ShouldBe(0);
(eightPast - fourPast).ShouldBe(fourPast - atTheCap);
fourPast.ShouldBeGreaterThan(0);
```

It says *nothing up to the cap, one buffer per layer past it* without naming what a buffer costs, so a
change to `InitialCapacity` or to the frame layout cannot make it fail for a reason that is not this
one. The cap itself is the single number it encodes, and that is deliberate — it is the number under
review.

### The mutants

A guard is only worth what it refuses, so each was run against a mutant rather than trusted for
passing.

| Mutant | Result |
|---|---|
| every fold reads slot 0 **and** borrows rather than takes — true aliasing | **many failures**, across `ExplanationTests` and beyond |
| every fold reads slot 0 but still takes — a single slot again | 4 failures, all of them the new allocation cases |
| indexed by stack position rather than by level | red on `Should_keep_the_buffer_of_a_wide_outermost_fold_past_the_retention_cap` |
| `MaxCachedDepth = 8` | red on the cap case |
| `MaxCachedDepth = 32` | red on the cap case, on a *different* assertion |
| `Take` does not null the slot it serves | **green** |

The first is why **no bespoke aliasing test was added**. Aliasing is the one property this change could
break and the one no new case states directly, so the honest question was whether the existing suite is
a live guard on it or merely happens to be green. It is live and comprehensively so — a borrowed buffer
corrupts frames the caller is still unwinding, and a wide swathe of existing cases say so. Adding one
more case restating what they already refuse would be coverage, not evidence.

The two cap mutants matter for a smaller reason: they fail on *different* assertions, so the case is
genuinely two-sided and neither direction can be absorbed by the other.

**The last row is the uncomfortable one and is recorded because it is uncomfortable.** `Take` nulls the
slot it serves, and the comment says this prevents pinning a buffer that is never returned. Removing
the null leaves the entire suite green — so that comment is a claim **no test holds**, and the honest
reading is that it guards a *leak*, not correctness. Under level indexing nothing else reads slot `d`
while level `d` is running, so aliasing does not depend on it; what depends on it is that a buffer
refused by `Return` for having grown past the width bound does not stay named by the pool with its
frames uncleared. That is real and worth keeping, and it is invisible to a test that asserts on
behaviour. Saying so is better than leaving a reader to infer, from a mutation table, that every line
in the type is guarded.

### One methodological note, because it nearly produced a false result

The first attempt at the cap mutants reported nothing at all, and "nothing" reads like "no failures".
The mutation script was `open(p,'w').write(open(p).read().replace(...))` — which truncates the file
before the inner read runs, so both runs built against an **empty** `EvaluationFold.cs`. Every
compilation error, and no test result.

Worth recording because of the failure mode rather than the typo: **a mutation check that fails to
compile is indistinguishable from a mutation check that passed**, if the reader is grepping for a
`Failed!` line. Both produce silence. The grep was widened to include `error CS` and the runs redone.
This is the same shape as the ledger's standing note that a claim about a check is not checked by that
check passing.

## Blast radius, and what did not change

- **`Fold` is untouched.** The diff is one private nested class and one constant.
- **No public API, no behaviour, no output.** Every one of the 5,976 `Motiv.Tests` cases passes on
  net8, net9 and net10, as do the example projects and the Studio, serialization, EF Core, SQL,
  AspNetCore, Blazor, analyzer and CodeFix suites. The `net472` *test* targets could not be run here —
  they need mono, which is not installed on this machine — but that TFM is compiled clean by a bare
  `dotnet build`, which is the check CI's own net472 leg depends on.
- **No user-facing documentation was wrong.** `SpecBase.Matches` documents "without allocating result
  objects", which is narrower than the claim that failed and remained true throughout;
  `docs/builder/AsAllSatisfied.md`'s allocation-free claim is about the higher-order fast path and is
  unaffected; `docs/limits/index.md` says nothing about allocation. The false claim lived only in
  `EvaluationFold`'s own remarks, which is where it is corrected.
- **`IOperationFold.cs`'s "allocation-free boolean path"** was imprecise before this and is accurate
  after it. It needed no edit, which is a small argument for wording a claim by intent rather than by
  measurement.

## What was deliberately not done

**[#211](https://github.com/karlssberg/Motiv/issues/211)'s rename is not folded in**, though #211 asks
for exactly that — "fold this into whichever budget/traversal slice is next open … the marginal cost of
renaming while they are open is near zero."

The reasoning is sound and its premise does not hold here. The marginal cost is near zero for a slice
that already touches the 21 files `IOperationFold` spans; this slice touches **one**, and one private
nested class within it. Folding in a 30-file mechanical rename would make a subtle hot-path change to
thread-static lifetime review as a rounding error inside a diff that is mostly noise — which is the
specific cost #211 itself names as the reason not to do the rename standalone. The recommendation is
still right; it wants a slice with a wider footprint, and #208 (nineteen call sites across four
proposition families) is the obvious one.

> **Landed since.** The rename shipped standalone after all
> ([#211](https://github.com/karlssberg/Motiv/issues/211)) — every slice it could have ridden along
> with declined it for the reviewer-cost reason recorded here, and there were no candidates left.

**The three seams #204, #208 and #201 name are untouched.** Nothing here moves them, and this change is
neutral to all three: the async fold has no frame buffer, `EvaluationBudget` is a different
thread-static with a different lifetime, and the composition caps are a parse-time concern.

## What a later reader should know

- **`_depth` must stay balanced, and only `Fold`'s `try`/`finally` makes it so.** `Take` increments and
  `Return` decrements; `Take` is called *before* the try block and `Return` is in the finally, so every
  increment has its decrement however the fold leaves. `EvaluationBudget.Enter()` runs before `Take`,
  so a fold refused on entry never incremented. A future edit that moves either call across that
  boundary desynchronises the level index for the rest of the thread's life, and no test would say so
  immediately — it would surface later as a pool that serves the wrong widths.
- **`Return` allocates once per thread, and that is inside the warm-up.** `_buffers ??= new …` creates
  the slot array on the first return. Every allocation case warms with one call first, which is what
  makes 0 the right expectation rather than "one array's worth".
- **Past the cap it is the deep tail that allocates, and the steady state is flat.** At depth 24 with a
  cap of 16, levels 0–15 are served and levels 16–23 allocate — eight per evaluation, the same eight
  every time, not eight more. Measured, the cap case's three figures are **0, 608 and 1,216** bytes:
  four buffers and eight at the same 152 each, which is why the two differences it compares are equal.
