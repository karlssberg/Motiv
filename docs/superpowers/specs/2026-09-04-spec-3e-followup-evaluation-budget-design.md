# Spec 3E follow-up — The budget that bounded one fold — Design

**Date:** 2026-09-04
**Ticket:** [#202](https://github.com/karlssberg/Motiv/issues/202)
**Plan:** [`2026-09-04-spec-3e-followup-evaluation-budget.md`](../plans/2026-09-04-spec-3e-followup-evaluation-budget.md)
**Source:** bundle spec
[3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
§2 Structural safety (19) and §7.
**Lineage:** Spec 3E ([#144](https://github.com/karlssberg/Motiv/pull/144)) →
[#145](https://github.com/karlssberg/Motiv/issues/145) (the measurement) → this.

## What the defect was

`MotivLimits.MaxEvaluationSize` is what Spec 3E put in place of the stack overflow: the spec asks for
*"a result-size bound counted in the traversal loop"* to replace *"the crash that used to cap the
amplification finding"*. It was counted in the loop and nowhere else — `EvaluationFold.Fold` held it in
`var size = 1`, and `AsyncEvaluationFold` in its own.

A decorator is not folded. `EvaluationFold` descends through operands that are `IOperationFold` and
evaluates everything else through `EvaluateInternal`, so a decorator between two operator layers
**re-enters the fold**, and the re-entry started a fresh count. The bound was per fold.

That is not a shape one has to construct. `RuleBinder.Decorate` wraps every node carrying a `name` or a
`whenTrue`, so a rule document composes alternating operator and decorator layers by construction —
which is how #145 found this while bisecting the decorator ceiling for something else.

## The decision the ticket could not make, and why it was makeable after all

#202 filed the defect rather than fixing it, for a reason it stated plainly: the obvious repair — an
ambient budget spanning the whole evaluation — would also charge a higher-order proposition's
per-element evaluations, which the same XML remarks promise it does not. *"A 250,000-element collection
would start tripping the default bound. That is a breaking change, not a bug fix."* And:

> So the budget has to distinguish *the composition tree* from *work inside a node*, and the fold
> cannot tell a decorator's `EvaluateInternal` from a higher-order proposition's.

Both halves are true. The conclusion does not follow, because **the fold does not have to be the one
that tells them apart.** Two facts about the tree, both checked before designing against them:

1. **Every higher-order element resolution in the library already passes through two methods.** All
   twenty higher-order propositions — the boolean-predicate, boolean-result, policy-result and
   expression-tree families — resolve their elements through `HigherOrderResults.Materialize` or
   `HigherOrderShortCircuit.Evaluate`. Those two were centralised in an earlier slice for an unrelated
   reason (avoiding a closure allocation per evaluation on the hot path), and they hand the projection
   its state explicitly so call sites can pass a `static` lambda. That is the seam #202 wanted: the
   fold stays ignorant, and the two funnels declare that what happens beneath them is work inside a
   node.
2. **There are no asynchronous higher-order propositions.** `HigherOrderProposition/` contains no async
   type at all. On the asynchronous fold the exclusion has nothing to protect — which matters for
   [#204](https://github.com/karlssberg/Motiv/issues/204), not for this PR.

So: **inheritance by default, suppression by declaration.** A nested fold spends its caller's budget,
which is the fix; the two higher-order funnels set the budget aside for the duration of one element,
which is the documented exclusion. The rule is stated once, in `EvaluationBudget`'s remarks, because the
two ends of it live in files that never refer to each other.

### Suppression is per element, not per collection

The remarks say per-element work *"is not counted and is not bounded by this"*. Suppressing once around
the whole loop would satisfy the first clause and not the second: the elements would accumulate against
a budget that merely started at zero, and a large enough collection would still trip. Suppressing per
element means each element is budgeted **afresh** — uncounted against the composition, and still bounded
in itself, so the backstop that replaced the crash keeps applying to whatever one element composes.

## The carrier, and why the slice is cut where it is

A parameter would have been better than ambient state, and is not available. The fold re-enters through
`EvaluateInternal`, which lands in an override of the `protected abstract` `EvaluateSpec` /
`EvaluatePolicy` — the signature every user-defined `Spec` subclass implements. Threading a budget to
where a decorator re-enters the fold means changing that signature. #202 says the same thing about its
option 2, and it is a public break for a bug fix.

Given ambient, the carrier is the whole remaining question, and it does not have one answer:

| Fold | Carrier | Why |
|---|---|---|
| `Evaluate`, `Matches` | `[ThreadStatic] int` | The synchronous folds never leave the thread that started them. Correct, and free. |
| `EvaluateAsync`, `MatchesAsync` | **not this PR** | A continuation may resume on a thread whose slot holds a *suspended* evaluation's count. |

The asynchronous case is filed as [#204](https://github.com/karlssberg/Motiv/issues/204) with the three
decisions it actually needs — the `AsyncLocal` box, what a concurrent operator's `Task.WhenAll` fan-out
does to one counter, and where an `AsyncLocal` budget meets the thread-static one at
`SyncSpecAsyncAdapter`. #202 anticipated this split in as many words, and it is the same call Spec 3E
made for the frame buffer, in the same direction: per-thread reuse on the synchronous fold, fresh
allocation on the asynchronous one.

Cutting here leaves a real asymmetry — `EvaluateAsync` admits what `Evaluate` refuses — so it is stated
in three places a reader actually reaches: `MotivLimits`' remarks, `docs/limits/index.md` as a note, and
the characterization test that still records the async hole.

## What the tests hold

The two synchronous assertions in `DecoratorSeamTests` were written by #145 as a hand-over —
*"Flip this to a ShouldThrow when the bound becomes per-evaluation"* — and flipping them is where this
started. They are the fix's proof and nothing more; the interesting cases are the ones that stop the fix
from going too far.

- **The exclusion, both funnels.** A higher-order proposition whose *element* spec is a composition,
  over 50 elements, is 150 nodes against a limit of 100 — and must still evaluate. It is deliberately
  composed into an `And` first: evaluated alone it is the outermost fold's caller, so every element
  would start a fresh budget whether the exclusion existed or not, and the case would pass without
  proving anything. Two cases, because `Evaluate` reaches its elements through `Materialize` and
  `Matches` through `HigherOrderShortCircuit` — one funnel fixed would pass the other's test.
- **The arithmetic, exactly.** Two decorator layers of one operand each is **6** nodes: per layer, the
  operation at the fold's root plus its two operands. Admitted at 6, refused at 5. A comfortable margin
  would have passed whether a nested fold were charged once, twice, or by its whole subtree — and the
  choice is real: the nested fold charges its own root because the outer fold charged the *decorator*,
  not the operation the decorator wraps.
- **Release, including on the way out through an exception.** A budget left behind by a refused
  evaluation would make the *next* caller's ordinary composition fail — the failure landing somewhere
  other than the fault, which is the worst shape this bug could take. `MotivLimits` is process-wide and
  its tests are collection-serialised precisely because that class of leak lands in a neighbour.
- **Per-thread independence**, which is the property the thread-static rests on, stated rather than
  assumed.
- **Zero allocation**, which is discussed below.
- **The shape a document actually composes**, in `Motiv.Serialization.Tests`. Everything above is a
  hand-built model of the defect; `PropositionChainDepthTests` builds a 200-link authored reference
  chain through `PropositionSet` the way a catalogue accumulates one, and asserts it is now refused at
  a lowered bound. That class gains a serialised collection of its own for the same reason `Motiv.Tests`
  has one — collections do not span assemblies, and a process-wide limit lowered in a parallel class
  aborts a neighbour's ordinary composition.

  Each of the three was checked red before the fix and green after, including this one: with `Enter`
  temporarily restored to pre-#202 semantics (every fold a fresh count), the chain evaluates, because
  each link's fold sees only three nodes and no single fold ever approaches 100. That is the defect
  stated from the other end.

## Predictions, scored

The plan recorded five before building. Scoring them is the point of recording them.

- **"Something in the existing suite will trip the tightened bound" — wrong.** 5,949 tests passed
  first time, and the whole solution — 15 test projects across net8/9/10, including the three example
  suites — passed unchanged. This was flagged as the prediction most likely to cost time and cost
  none. The reason is worth keeping: the suites that lower `MaxEvaluationSize` set it to 5–100 against
  compositions built for the purpose, and nothing else in the repo composes anywhere near 250,000
  nodes. A tightened bound only bites where the bound was already the subject.
- **"Suppression will be needed on both funnels, not one" — right**, and cheap insurance: the two
  cases were written before either funnel was touched.
- **"`Matches` stays allocation-free" — right, and it turned out not to have been true in the way the
  library claims.** See below.
- The `net472` guard was needed, as `ReasonCostTests` predicted for its own measurement: there is no
  per-thread allocation counter on .NET Framework.

## What the allocation guard found

`Matches` allocating nothing is a contract Spec 3E paid for with a per-thread frame buffer, asserted in
prose in three places and checked nowhere. An ambient budget is exactly the kind of change that could
quietly end it, so the claim got a gate: `GC.GetAllocatedBytesForCurrentThread` around a warm second
call, asserting **0**.

It failed on the first shape tried — 456 bytes — and the cause was not the budget. Measured across two
depths:

| Shape | Allocated by a warm `Matches` |
|---|---|
| flat chain of 16 | **0 bytes** |
| 4 decorator layers | 456 bytes |
| 8 decorator layers | 1,064 bytes |

Both are exactly `(layers − 1) × 152`: **one frame array per nested fold**, the outermost reusing the
cached one. `FrameBuffer.Take()` hands a nested fold nothing on purpose — a borrowed buffer would let a
nested fold overwrite frames its caller is still unwinding — and its remarks dismiss the cost as
*"correct and rare"*.

So the sibling property has **the same defect shape as #202 itself**: "allocates nothing" is a per-fold
claim, not a per-evaluation one, and reachable the same way. And "rare" is a claim that predates #145:
a rule document produces one nested fold per decorator layer, so a `Matches` over a document-composed
rule allocates linearly in its decorator depth. Filed as
[#205](https://github.com/karlssberg/Motiv/issues/205) rather than widened into this PR — the repair is
a change to how the frame buffer is cached, not to how the budget is counted.

The gate that ships states the flat case, which is the contract as the library can currently keep it.
A gate that asserted the nested case would have to assert the defect, and #205 is where that belongs.

## The simplifier pass

CLAUDE.md's mandatory `code-simplifier` round changed the shape of the exclusion, and the reasoning is
worth keeping because it cuts against the repo's own "avoid over-DRYing" rule.

The first draft gave each higher-order funnel a private `Resolve` helper wrapping the projection in a
`using var` suppression scope. The two were byte-identical modulo `TResult = bool`, and the tell that
they should not have been two was **in their own comments**: each existed partly to point at the other
("one of only two places… the other being `HigherOrderShortCircuit`" / "the allocation-free half of the
exclusion `HigherOrderResults` documents"). "Avoid over-DRYing" guards *builder paths whose branches
differ semantically*; this was a single invariant with two copies, and an invariant that has to name its
own duplicates is one a third call site can silently omit.

They collapsed into a single element-resolution helper on the budget itself — an invocation rather
than a scope, which deleted the second `ref struct` outright. Code review later moved it back to a
scope for reasons the section below records. Both cautions the pass was asked to weigh turned
out not to bite: neither helper closed over anything (both were already `static`, threading `state`
explicitly so call sites keep handing over non-capturing `static` lambdas), and `using var` compiles to
try/finally, so nothing was inlineable before either. The 0-byte allocation case is what confirms it.

`Enter`/`Ownership` were deliberately left as they were. The obvious tidy — have `Ownership` restore the
entry count rather than carry an `isRoot` flag — is *wrong*: a nested fold would refund its spending on the way
out, so sibling decorator subtrees would each get a fresh allowance and the flat-versus-layered
equivalence this slice establishes would break in a way none of the tests above are shaped to catch.
The `bool` is the honest encoding.

One smaller change with a reason: `Charge()` runs once per node in the library's hottest loop and
replaced an increment that was inline in the fold. Its refusal message moved to a private
`ThrowExceeded()` so the charge is a compare-and-call, keeping the method small enough for the JIT to
inline for a branch never taken. (No `[DoesNotReturn]` — `netstandard2.0` is a target and the attribute
is not available there.)

That message is now duplicated between `EvaluationBudget` and `AsyncEvaluationFold`. It was duplicated
between the two folds before this PR, so it is not a regression, and collapsing it means giving the
async fold a budget — which is [#204](https://github.com/karlssberg/Motiv/issues/204), where it will
fall out for free.

## The failure paths, and the mutant that got away

Ambient mutable state fails in a way that is hard to reason about and easy to get wrong on a later
edit: a leaked count surfaces on the *next* caller of the thread, so the error lands nowhere near the
fault. That deserves mechanical cover rather than an argument, and it reduces to one invariant —
**however an evaluation terminates, the thread's count is back to zero** — checked black-box, with no
hook into the budget. A canary composition costing *exactly* the limit is satisfied only from zero and
refused by a leak of a single node.

Eight failure shapes, run as a theory: the bound exceeded at the top and inside a nested fold; a
throwing predicate mid-fold; a throwing element (inside the exclusion); an element whose own
composition exceeds the bound (a refusal raised *beneath* a suppression); and a sequence that throws
while being enumerated (inside the exclusion, but not inside a projection) — each on `Evaluate`
and, where it differs, `Matches`.

Then the same treatment applied to the implementation, because a suite that has never been seen to
fail is not evidence:

| Mutation | Caught by | |
|---|---|---|
| `Ownership` never releases | all 8 failure shapes | ✅ |
| every fold releases (the "always reset" encoding) | the layered-arithmetic and abandoned-budget cases | ✅ |
| `Ownership` restores its entry count | the layered-arithmetic cases | ✅ |
| **the exclusion never restores** | **nothing** | ❌ |

**The fourth one survived**, and the reason is worth keeping. Failing to restore leaks in the
*permissive* direction: the composition forgets what it spent *before* the higher-order operand, so the
bound quietly weakens. No exclusion test sees it — they all assert an evaluation succeeds — and the
leak canary cannot either, because a discarded count leaves nothing behind to find. Both halves of the
suite were looking the other way.

`Should_resume_the_compositions_count_after_a_higher_order_operand` closes it, and states the property
the other cases don't: the exclusion **parks** the count and hands it back; it does not discard it.
Seven nodes — a higher-order operand followed by three ordinary ones — admitted at 7 and refused at 6,
where a discarded count would make it cost three.

The general lesson, and it is the ledger's own restated: **a bound has two failure directions, and a
suite written while fixing a too-permissive bug tends to test only the strict one.** Every case here
was written against over-charging; none was written against under-charging, and that is exactly the gap
the mutation found.

### Naming, after the fact

The first names were `Scope` / `owned` / `Suppressed`, and they read as one mechanism spelled twice —
both look like "set aside and restore". They are opposites, and the names now say which:

| | Question it answers |
|---|---|
| `Ownership` (`isRoot`) | *When this fold ends, does the **evaluation** end?* Only the outermost says yes. |
| `Exclude` | *This span of work was never **part of** the evaluation.* True at that seam however deeply nested. |

Swap either for the other and you get one of the two broken encodings above. `Scope` also collided by
eye with the unrelated `Diagnostics/EvaluationScope`, which is a telemetry activity and nothing to do
with counting.

## The code review, and what it moved

A high-effort review over the merged diff returned ten findings. Three confirmed correctness findings
shared one root cause, and fixing them changed the shape of the exclusion.

### The exclusion was attached to two helpers, not to a concept

`EvaluationBudget`'s remarks claimed `HigherOrderResults` and `HigherOrderShortCircuit` were *"the only
two places in the library where an element is resolved"*. That was checked before it was written and
was still wrong: **`EnumerableExtensions.Where(spec)`** resolves one element at a time through a
deferred `Select`, and was charging every element to whatever composition happened to be in flight. A
50-element `Where` inside a rule refused at a limit of 100 that the rule itself never approached.

Two more re-entries were charged for the same reason — a `Tap` callback (a side effect hung off a node,
by definition not part of the decision) and any predicate of the author's own that evaluates a
proposition per item. The first is now excluded; the second **cannot be**, and the honest fix was to
stop promising otherwise. `MotivLimits`' remarks and `docs/limits/index.md` now say the exclusion is
*declared, not detected*, list the three seams that declare it, and name what stays counted —
`Spec.Build((Order o) => o.Lines.All(line.Matches))` among them, with the `AsAllSatisfied` +
`ChangeModelTo` form that is excluded shown beside it. (The example is compile-checked, not written
from memory.)

### The bound depended on whether the caller wrote `.ToArray()`

Wrapping the projection alone left a lazy source's `MoveNext` outside the exclusion. A sequence whose
enumerator evaluated a proposition charged the composition once per element; the same models passed as
an array did not, because an array is fully produced before the funnel is entered. Producing an element
is part of resolving it, so the scope now spans the enumeration too.

### The fix is smaller than the first attempt, and mutation testing is why

The first attempt hoisted the scope around each loop *and* reset the count per element, on the
reasoning that elements would otherwise accumulate within one span. Mutating that reset away left the
suite green — and the reason is not a coverage gap:

> An element's own evaluation enters the fold with the count at zero, so it is a **root**, and
> `Ownership` releases a root's count on the way out. Elements cannot accumulate against each other.

The per-element reset was restating a guarantee that already existed one layer down. Deleting it
collapsed the change to **one `using` line per funnel**, let `HigherOrderShortCircuit`'s switch revert
to its original one-line cases, and removed the hand-stepped enumerator the first attempt needed. The
review's separate complaint about that switch's added braces and locals dissolved with it.

Worth keeping as a rule: *a redundancy that no mutation can distinguish is not defence in depth, it is
a second statement of an invariant that can drift from the first.*

### What each seam now refuses

Every exclusion is mutation-checked — removing it from any of the four seams turns the suite red:

| Seam | Cases that go red |
|---|---|
| `HigherOrderResults.Materialize` | 4 |
| `HigherOrderShortCircuit.Evaluate` | 4 |
| `EnumerableExtensions.Where` | 1 |
| `Tap` callbacks | 1 |

And the source-shape theory (`T[]`, `List<T>`, lazy) closes the coverage gap the review found: every
earlier case fed `Enumerable.Repeat`, which is an `IReadOnlyList<T>` and never an array, so unwrapping
either funnel's array fast path had left the whole suite green. The theory subsumed the two
fixed-shape cases that preceded it, and its failure message names the shape that broke.

The simplifier pass that followed found two mistakes in the fix itself, both of the kind a green suite
cannot see: a `using` inserted between a `// ReSharper disable once CheckNamespace` comment and the
`namespace` it suppressed for — silently re-targeting the suppression at the using — and a "the same
applies to" clause in the limits page whose antecedent had moved when the paragraph was edited. It also
collapsed the three `Tap*Spec` classes, which were byte-identical but for one boolean and had just
acquired a third copy of the same four-line rationale, onto a `TapSpecBase` with a single
`ShouldInvokeCallback` — so the exclusion is stated once. That is not the "avoid over-DRYing" carve-out,
which CLAUDE.md scopes to builder paths whose branches differ semantically; these had no semantic
difference at all.

### Findings not taken

- **The asynchronous carrier** stays [#204](https://github.com/karlssberg/Motiv/issues/204). The review
  added that mixing sync and async can make the bound *scheduling-dependent* rather than merely looser,
  which #204 now records.
- **A higher-order predicate supplied through `As(...)`** is still charged —
  [#208](https://github.com/karlssberg/Motiv/issues/208). One evaluation per node rather than one per
  element, so the magnitude is bounded where the per-element cases were not; excluding it means
  touching nineteen proposition classes, and the version worth having (a combined
  materialize-and-decide helper, so the exclusion is structural rather than repeated) is a reshaping of
  every higher-order proposition.
- **`Charge()` increments before it throws** — [#209](https://github.com/karlssberg/Motiv/issues/209).
  A caught refusal leaves the count over the limit for the rest of the evaluation, and the library
  swallows `SpecException` in three places around lazy explanation resolution. Its weaker
  exception-free half is arguably the more interesting one: rendering `Reason` under telemetry charges
  the composition, and explanation rendering is not composition.
- **The per-node thread-static cost** was measured at 2–3% on net9 with the sign flipping between runs
  — inside noise. Only the unmeasured net472 argument survives, and the fold-local alternative adds
  three synchronisation points for no measured gain.

## What was left alone, and why

- **The depth.** The alternating shape still costs a stack frame per decorator layer, ceiling 1,046
  links. That is [#201](https://github.com/karlssberg/Motiv/issues/201), and it needs a stack-safe
  spec-tree walk Motiv does not have. This PR bounds *size*, not depth — a distinction worth keeping
  clear, because both were found by the same measurement and they have different repairs.
- **`MaxCompositionDepth`.** #145 ruled it does not move, and nothing here moves it.
- **A user's higher-order predicate.** `higherOrderPredicate(results)` runs with the budget in force, so
  a predicate that evaluated a spec of its own would charge the composition. It is inside the node by
  the same argument the elements are, but it is not on the element seam, and inventing a third
  suppression point for a case nobody has hit would be guessing.

## Files

| File | What changed |
|---|---|
| `src/Motiv/Traversal/EvaluationBudget.cs` | New. `Enter` / `Charge` / `Exclude`, a `[ThreadStatic]` count, two `ref struct`s (`Ownership`, `Exclusion`). Carries the rule and its reasons. |
| `src/Motiv/Traversal/EvaluationFold.cs` | Claims the budget before taking the frame buffer, charges through it; the class remark that stated Spec 3E's refuted argument now says what #145 found. |
| `src/Motiv/HigherOrderProposition/HigherOrderResults.cs` | Elements resolved under suppression. |
| `src/Motiv/HigherOrderProposition/HigherOrderShortCircuit.cs` | The same, on the allocation-free path. |
| `src/Motiv/MotivLimits.cs` | The remarks that published the hole now describe the behaviour, and name the async asymmetry. |
| `docs/limits/index.md` | "Not a bound across decorator layers" was a documented non-guarantee; it is now a guarantee, with the async exception as a note. |
| `src/Motiv.Tests/Traversal/EvaluationBudgetTests.cs` | New. Exclusion, arithmetic, release, threads, allocation. |
| `src/Motiv.Tests/Traversal/DecoratorSeamTests.cs` | Two characterization assertions flipped; the async one repointed at #204. |
| `src/Motiv.Serialization.Tests/Propositions/PropositionChainDepthTests.cs` | The reachability proof: a 200-link authored chain is refused at a lowered bound. |
| `src/Motiv.Serialization.Tests/Propositions/MotivLimitsTestCollection.cs` | New. This assembly's twin of the serialised collection, since collections do not span assemblies. |

## Verification

Whole solution, `dotnet test Motiv.slnx`: 15 test projects green across net8.0, net9.0 and net10.0 —
`Motiv.Tests` at 5,950 on each, plus `Motiv.Serialization`, `Motiv.Serialization.Sql`,
`Motiv.Serialization.AspNetCore`, `Motiv.Serialization.EntityFrameworkCore`, `Motiv.Studio`,
`Motiv.Analyzer`, `Motiv.CodeFix`, `Motiv.RuleAuthoring.Blazor` and the three example suites
(`Motiv.Poker`, `Motiv.ECommerce`, `Motiv.SmartHome`), which CLAUDE.md requires because they assert on
justification output. `net472` builds but does not run tests locally; the one allocation case is
`#if !NETFRAMEWORK`-guarded, as `ReasonCostTests` guards its own.
