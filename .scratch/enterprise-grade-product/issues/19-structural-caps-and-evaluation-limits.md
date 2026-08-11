# Structural caps, evaluation limits, and the recursive-traversal class

Type: grilling
Status: resolved
Blocked by: —

## Question

Graduated from the fog patch "rate limiting and request-size limits" once ticket 05's threat model
landed and made it sizeable.

**⚠️ The critical finding in ticket 05 should almost certainly not wait for this map.** An
unauthenticated `POST /api/rules/evaluate` carrying a ~49 KB flat operand array terminates the host
with an uncatchable `StackOverflowException`. That is a live defect in a published MIT package, not a
planning question. This ticket is the *considered* hardening design; the immediate fix is a separate,
faster path. See "Disclosure and sequencing" below.

The session must resolve:

1. **What are the caps, and what are their defaults?** `RuleSerializerOptions` already carries
   `MaxDocumentDepth` (64) and `MaxNodeCount` (10,000). The gap is **operand-array width** — the
   crash arrives at ~1,640 siblings while node count permits 10,000. Candidates: a
   `MaxOperandCount`, or lowering `MaxNodeCount` beneath the crash point, or deriving the effective
   limit from measured stack headroom. Note that any default is a **behaviour change for existing
   adopters** whose legitimate documents might exceed it — what is the migration story?
2. **Is a cap the right fix, or a workaround?** The finding's root cause is that
   `BooleanResultBase.UnderlyingAssertionSources` (`BooleanResultBase.cs:104-111`) is non-tail
   recursive over an arbitrarily deep result tree. A cap makes the crash unreachable *through the
   document path* while leaving the recursion intact for anyone composing deeply in C#. Rewriting
   the traversal iteratively removes the whole class — at the cost of touching Motiv core, which is
   the most-depended-upon and most-tested code in the repo. Do both, in what order?
3. **Which traversals?** `UnderlyingAssertionSources` is the one proven to crash.
   `UnderlyingAllAssertionSources`, `Causes`, `Underlying`, `Justification` rendering, and
   `ResultSerializer` are all candidates for the same shape. An audit of recursive result-tree
   walks belongs in this ticket, not a later surprise.
4. **The amplification finding needs a different answer.** k quantifiers over n elements is k·n
   evaluations with no timeout or cancellation — `/evaluate` calls the synchronous, non-cancellable
   `spec.Evaluate` (`MotivRulesOptions.cs:82`). Options: a collection-cardinality cap at bind time, a
   response-size cap, an evaluation timeout (which requires a cancellable evaluation path that does
   not exist today), or accepting it as an authenticated-only concern once ticket 03 lands.
   Note the interaction: **ticket 03's authentication does not fix the crash, it only reduces who can
   reach it** — and `/evaluate` may be deliberately anonymous for machine callers.
5. **Where do the caps belong?** `RuleSerializerOptions` is SDK-level and applies to every adopter.
   Rate limiting is host-level and belongs in the app. Keep the distinction crisp: structural caps
   protect the *library*; rate limits protect the *deployment*.

### Disclosure and sequencing

The repo has a `SECURITY.md` and publishes to NuGet. Whether this is handled as a normal bug fix or
through a coordinated disclosure is the maintainer's call and is **not** a decision for this map —
recorded here only so the sequencing is deliberate rather than accidental.

## Status update — the critical finding is closed

Fixed on branch `fix/operand-width-stack-overflow`, commit `49899970`, outside this map (the
maintainer authorised stepping past the plan-only destination for a live defect). **Not pushed.**

`RuleSerializerOptions.MaxCompositionDepth` (default 256) bounds the depth of the *composed* spec,
computed over the parsed tree and refused during parsing. Measured threshold is ~1 KB of stack per
level: crashes at ~250 on a 256 KB stack, ~550 on 512 KB, ~1075 on 1 MB, ~8000 on 8 MB. Requests are
served on pool threads, so the small end governs. `POST /api/rules/evaluate` now returns 400
`DocumentTooLarge` instead of dying; regression tests at both the parser and endpoint layers.

**A per-operand width cap was tried and rejected as unsound** — nesting compounds the left-deep fold
multiplicatively, so eleven operands nested sixty deep crash just as reliably as 1,200 flat ones.
Sub-question 1 is therefore answered: the bound must be on the composed shape, not on width.

### What remains for this ticket

- **Sub-question 2** — the cap makes the crash unreachable *through the document path*, but
  `BooleanResultBase.UnderlyingAssertionSources` is still non-tail recursive, so anyone composing
  deeply in C# can still reach it. The iterative rewrite is untouched.
- **Sub-question 3** — the audit of other recursive result-tree walks
  (`UnderlyingAllAssertionSources`, `Causes`, `Underlying`, `Justification` rendering,
  `ResultSerializer`) has not been done. Only `UnderlyingAssertionSources` was proven to crash;
  the others were never checked.
- **Sub-question 4** — the HIGH amplification finding (146 KB → 200 MB, no timeout) is **not**
  addressed by this fix at all.
- **Sub-question 5** — placement of host-level rate limits.
- The default of 256 is a **behaviour change for existing adopters** whose legitimate documents
  compose deeper. No migration note has been written.

## Corrected by ticket 03

Sub-question 4 above records that authentication does not mitigate this class because `/evaluate` is
*"the surface machine callers use — one of the endpoints most likely to stay anonymous by design."*
**That premise was wrong.** There is no machine execution path: rules run in-process via DI, and
`/evaluate` only tests drafts. Ticket 03 makes the entire surface authenticated and secure-by-default.

So authentication *does* shrink this attack surface — to authenticated users. That lowers the
severity of the remaining amplification finding without closing it: an authenticated analyst can still
turn a 146 KB request into a 200 MB response with no timeout, and the deeper recursive-traversal class
is untouched.

## Corrected by ticket 06 — who is actually exposed, and what the fix did not fix

The publication facts change this ticket's priorities in both directions.

**The remote vector never shipped.** The unauthenticated `POST /api/rules/evaluate` crash requires
`Motiv.Serialization.AspNetCore`, which has **never been published to NuGet**. Neither has
`Motiv.Serialization`, which holds `RuleDocumentParser` and `RuleBinder`. **No consumer has ever been
remotely exposed.** Earlier notes on this map describing an urgent public disclosure with adopters at
risk overstated it.

**But the fix landed entirely in unpublished code, and the root cause is in the published package.**
`MaxCompositionDepth` guards `RuleDocumentParser` — the JSON path. `Motiv` v8.0.0 contains neither the
parser nor the endpoints. What it *does* contain is `BooleanResultBase.UnderlyingAssertionSources`,
recursing non-tail at roughly a kilobyte of stack per level, reachable with no JSON and no HTTP:

```csharp
var combined = specs.Aggregate((a, b) => a.And(b));   // ~2,000 specs
var reasons  = combined.Evaluate(model).Assertions;    // StackOverflowException
```

Aggregating a large collection of specs is an ordinary thing to write. On an ASP.NET request thread
(~1 MB stack) this dies around a thousand specs. So the **published** package has a live crash bug
reachable by normal use, and **cutting a release would not address it** — the cap protects a code path
`Motiv` v8 consumers never take.

### What this does to priorities

- **Sub-question 2 (iterative traversal) is promoted.** It is no longer the principled follow-up to a
  closed issue; it is the *only* fix for the one genuinely published defect.
- **Sub-question 3 (audit the other traversals) rises with it** — `UnderlyingAllAssertionSources`,
  `Causes`, `Underlying`, `Justification` rendering, `ResultSerializer` are all unchecked, all in
  `Motiv`, all published.
- **Sub-question 4 (amplification) falls.** It needs the unpublished endpoints, and ticket 03
  authenticates the whole surface.
- Release urgency for the composition-depth fix is **low** — it protects nobody who exists yet.

## Sub-question 3 — the audit, done

Recursion over the result tree, complete inventory of `src/Motiv`. **~19 sites, in three shapes.**

### Family A — flatten-with-self-fallback (3 members, literally the same algorithm)

```csharp
field ??= «children»
    .SelectMany(r => r is IBooleanOperationResult ? r.«self» : r.ToEnumerable())
    .ElseIfEmpty(this.ToEnumerable())
    .ToArray();
```

| member | children |
|---|---|
| `UnderlyingAssertionSources` (the proven crash) | `Causes` |
| `UnderlyingAllAssertionSources` | `Underlying` |
| `UnderlyingMetadataSources` | `CausesWithValues` — **and it has no `field ??=`** |

That missing memoization is exactly the divergence copy-three-times produces: it re-walks the whole
tree on every access while its two siblings cache. A performance bug independent of the crash.

### Family B — variants (2 members)

- `UnderlyingExpressionResults` — branches on the `(this, child)` *pair*, and in one arm yields the
  child **and** recurses. Different shape.
- `AllAssertions` — recurses on `Underlying` for `IBinaryBooleanOperationResult`, returns strings.

### Family C — justification rendering (12 implementations, 17 recursive call sites)

`GetJustificationAsLines` — produces indented text, needs depth, bespoke formatting per result type.
Would share only a traversal primitive, not an implementation.

Also recursive and calling back into family A: `Explanation.Underlying` and `Explanation.AllUnderlying`.

### Two facts that shape the fix

**`RuntimeHelpers.EnsureSufficientExecutionStack()` is used nowhere in the codebase.** It probes for
remaining stack and throws the *catchable* `InsufficientExecutionStackException` before the runtime
reaches an uncatchable `StackOverflowException` — the mechanism Roslyn and System.Text.Json use for
unbounded-depth tree walks. Available on every target including netstandard2.0. It does not make deep
composition succeed; it makes failure catchable, which is the actual defect.

**The memoization is load-bearing and is the trap in any iterative rewrite.** These are memoized
recursions, not plain ones:

1. *Correctness* — `.ElseIfEmpty(this.ToEnumerable())` is a per-node **post-order fold** ("if my
   children contributed nothing, I am the source"), and a cached child already has that fallback
   baked in. An iterative visitor must reproduce the fold at every level, not merely flatten.
2. *Cost* — recursion stops at an already-computed child. A visitor walking raw `Causes` bypasses
   every cache, so it must write results back into each node's backing field as the explicit stack
   unwinds. Otherwise an uncatchable crash is traded for repeated full re-traversals. **Confirm first
   whether result nodes can be shared between positions** — if they can, naive iteration degrades
   badly rather than linearly.

### On CLAUDE.md's anti-over-DRY guidance

It warns against consolidating the nuanced per-builder-path duplication. Family A is not that: it is
one algorithm written three times, and one copy has already drifted by losing its memoization. A
shared implementation makes that drift unrepresentable.

### Still undecided

Which approach — reusable iterative visitor, `EnsureSufficientExecutionStack` guards, or a split
across families. Options were put and not answered; nothing chosen.

## Decision — iterative everywhere, all three families

A reusable iterative traversal, applied across all ~19 sites, rather than a visitor for some families
and `EnsureSufficientExecutionStack` guards for the rest.

**The reason is not consistency for its own sake.** A split leaves a depth band in which a result is
*partially inspectable*: at 5,000 levels `Assertions` (iterative) returns while `Justification`
(guarded) throws. Same object, same evaluation — a consumer can learn *what* fired but not *why*, and
nothing in the API signals which properties carry which limit. For a library whose promise is
explainability, a partially-explicable result is a worse outcome than a uniformly failing one.

Shape: one traversal primitive yielding `(node, depth)` with memo write-back on unwind. Families A and
B rebuild on it directly. Family C keeps its twelve bespoke line formatters but takes traversal and
depth from the primitive rather than recursing.

Carry forward from the audit above: the memo write-back is mandatory, not an optimisation — the
per-node `.ElseIfEmpty(this.ToEnumerable())` post-order fold must be reproduced at every level, and
skipping the cache would trade an uncatchable crash for repeated full re-traversals.

## Decision — sub-question 2: one stack-safe traversal, oracle-tested

Not 19 hand-rolled rewrites, not recursion-plus-guard, but a **single explicit-stack traversal** the
public properties delegate to.

**Why not recursion + `EnsureSufficientExecutionStack` (the minimal-risk option, seriously weighed):**
recursion — lazy or eager, spread across nodes or centralised in a root record — uses O(depth) native
stack regardless, so it must be guarded to be catchable, and even then it does not make deep
composition *succeed*. Worse, it does not deliver **uniform** behaviour: `Justification` builds
indented strings with fatter frames than `UnderlyingAssertionSources`, so its guard trips at a
shallower depth, and on a tree between the two trip points `Assertions` returns while `Justification`
throws — the exact "what fired but not why" asymmetry sub-question 2 exists to prevent, merely made
catchable. Only a stack-safe traversal makes every property behave identically at every depth.

**Why the correctness risk is acceptable here:** the short-circuit irregularity is real
(`BinaryBooleanResult.GetCausalResults` prunes to 0/1/2 causal children by outcome; `OrElse`/`AndAlso`
may have a null right operand; the `ElseIfEmpty` fallback is a post-order fold). Re-deriving that fold
in an explicit stack is where bugs would live. But **the current recursive code is a perfect oracle**:
differential-test the new traversal against it on randomly generated result trees — including
short-circuited ones — at depths shallow enough that recursion does not overflow, asserting identical
output. That converts "bugs live in the rewrite" from a standing risk into a checked invariant.

Shape: one traversal primitive with memo write-back on unwind (the write-back is mandatory, not an
optimisation — skipping the per-node cache trades an uncatchable crash for repeated full
re-traversals). Families A and B rebuild on it; family C's twelve formatters take traversal and depth
from it rather than recursing. Fixes `UnderlyingMetadataSources`'s missing memoization as a
by-product, since there is then one implementation to omit it from.

## Decision — the traversal is single-dispatch, not a visitor (code-grounded)

A visitor "simulating multiple dispatch" was proposed as the way to get iteration without
compromising short-circuit behaviour, weighed, and rejected. Reading the tree
(`BooleanResultBase.cs`, `BinaryBooleanResult.cs`, `AssertionExtensions.cs`) settles it — the
architecture is already most of the way to the right shape, by a plainer means.

**1. Child-selection is already single-dispatch, and already memoized.** The per-node variation the
walk needs is not "an operation that varies by concrete type" — the case a visitor's double dispatch
exists for — it is *which children are causal*, and that is four abstract properties overridden per
node: `Causes` / `CausesWithValues` (de-noised, short-circuit-pruned) and `Underlying` /
`UnderlyingWithValues` (all children). In `BinaryBooleanResult` they funnel through
`CausalResults => field ??= GetCausalResults()` (:26) and `AllResults => field ??= Right is null ? [Left] : [Left, Right]`
(:29) — both memoized. The short-circuit de-noising the Fable session feared re-implementing lives in
exactly one place per node, and the walk reads the same virtual the recursion reads; it never
re-derives it. Multiple dispatch is a misdiagnosis: behaviour depends on the runtime type of *one*
node, which C# virtuals give for free.

**2. The recursion is centralised, not scattered.** Families A and B are *not* per-node overrides —
they are base-class property bodies on `BooleanResultBase` (`UnderlyingAssertionSources` :104,
`UnderlyingAllAssertionSources` :114, `UnderlyingExpressionResults` :52, `AllAssertions` :71), the
metadata-typed `UnderlyingMetadataSources` (:320), and the `AssertionExtensions` root-walk helpers
(`GetAssertions`, `GetAllAssertions`, `GetRootAssertions`, `GetAllRootAssertions`). Each is generic
code calling the abstract child-selectors. So the change is *swap ~9 recursive bodies for delegation
to one iterative driver that consumes the existing `Causes`/`Underlying` virtuals* — the node types
do not change at all, no `Accept`/`Visit` pair is added, and CLAUDE.md's "explicit over abstraction
with branching logic" is honoured rather than fought.

**3. A visitor would not have helped the hard part anyway.** The post-order fold
(`.ElseIfEmpty(this)`) needs a result stack whatever the dispatch mechanism; a void-returning `Visit`
just threads that accumulator by hand — the exact place bugs live. Dispatch was never the difficulty;
the fold state machine is, and the visitor dresses up the easy half while leaving the hard half naked.

## Decision — MaxCompositionDepth kept, re-derived, raised

Iterative traversal removes the stack rationale the cap was built on ("~1 KB per level"). Rather than
delete it, **restate its purpose** — bounding result-tree size and work, not stack — and **raise the
default well above 256** so legitimately wide documents pass. A now-unjustified 256 left in place would
be a cargo-cult limit nobody downstream could reason about; removed entirely, the parse-time rejection
that yields a clean 400 instead of a slow success is lost. Requires a defensible new number, derived
against result-tree size once the traversal is measurable.

## Implementation note — allocation (ArrayPool)

The iterative traversal introduces heap allocation the native call stack gave for free. Guidance:

- **Do not pool the result arrays.** `field ??=` retains them and hands them out as `IEnumerable`;
  returning one to a pool while a consumer holds the enumerable is silent corruption. The memoization
  that makes these properties cheap is what makes them un-poolable.
- **Write the working stack as a plain right-sized buffer first**, sized from `MaxCompositionDepth`
  (a bounded depth means one array, not a doubling `Stack<T>`). Match the house style: the five `perf:`
  commits since v8.0.0 *eliminate* allocation (closures, boxed enumerators, `Lazy`) rather than pool
  it, and there is no `ArrayPool` precedent anywhere in the codebase.
- **Fix `UnderlyingMetadataSources`' missing memoization** — that removes far more allocation than
  pooling the working stack ever would, by converting an O(tree)-per-access walk into one.
- **Only then measure.** If `ResultSerializer.ToEvaluationResult` (which ticket 15 puts on a
  per-evaluation path) still shows the working stack as significant, pool it — with
  `clearArray: true`, because these are reference-type arrays and an uncleared buffer pins whole
  evaluation trees in the pool.

### Does the iterative rewrite allocate more heap than today? Grounded answer

Reading the tree confirmed the guidance above and corrected one guess. Split by heap category, because
they move independently:

- **Retained heap is unchanged.** Every Family-A/B property is `field ??=` a `ToArray()` — one array
  per node, held for the result's lifetime. A driver producing the same arrays keeps the same
  footprint. The *only* lever on retained heap is memoisation granularity (per-node today vs a
  root-only lazy record), which is **orthogonal** to iterative-vs-recursive and is not changed here:
  per-node memoisation is what makes repeated enumeration of these public `IEnumerable` properties
  cheap, and dropping it to save heap would silently regress that. Keep it. Do not conflate "iterative"
  with "lazy root" — they are two knobs and only the second touches retained heap.

- **Transient churn goes *down*.** The base bodies build `SelectMany` + `ToEnumerable` + `ElseIfEmpty`
  iterator chains per node; the `AssertionExtensions` root-walk helpers are lazy, un-memoised
  `SelectMany` recursions that re-allocate their iterator chain on every enumeration. A driver looping
  over the already-materialised `CausalResults`/`AllResults` arrays replaces all of that with plain
  index walks. Net GC pressure is expected to fall — **conditional on** a buffer-reusing, closure- and
  iterator-free inner loop. A careless driver (`new Stack<>()` per access, captured closures, boxed
  frames, a `yield` chain that merely relocates the LINQ garbage) forfeits the win and can allocate
  *more* than the tidy memoised recursion it replaces. This is a hard requirement, not a nicety.

- **The one genuinely new allocation is the working stack** — O(depth), the frames deliberately moved
  off the native call stack, poolable per the note above (a bounded `MaxCompositionDepth` makes it one
  right-sized buffer, not a doubling `Stack<T>`). It is the trade that turns an uncatchable abort into
  a graceful bounded cost, and it is dwarfed by the tree it walks.

- **Correction to an earlier hypothesis.** A dedup win was expected from lifting causal-selection into
  one memoised virtual — all consumers sharing one child array instead of re-deriving it. **That win
  does not exist:** `CausalResults` (:26) and `AllResults` (:29) are *already* memoised in
  `BinaryBooleanResult`, so the causal child arrays are already computed once and shared across every
  consumer. Nothing is recoverable there. The recoverable allocation is exactly what the original note
  named — `UnderlyingMetadataSources`' missing `field ??=` (:320), which re-walks the whole subtree on
  every access and is the single largest allocation the rewrite removes.

## Remaining open — sub-question 4 (amplification), de-escalated

Ticket 03 reduced this to an authenticated-analyst concern; ticket 06 confirmed the remote vector
never shipped. **But the stack-safe traversal *raises* it:** the overflow was an accidental ceiling,
so a 5,000-deep composition that used to crash will now succeed and produce a correspondingly enormous
result tree. Recommended (not yet confirmed): a **result-size bound counted inside the traversal
loop** — the explicit stack is exactly where node-count is free to check — failing with a clear error
past a threshold, since a collection-cardinality cap cannot work (`n` is runtime, only `k` is static)
and a timeout needs a cancellable evaluation path that does not exist. Rate limiting stays a host
concern (sub-question 5), per the library-vs-deployment split. This is the one sub-question left for
the implementer to confirm rather than settled here.
