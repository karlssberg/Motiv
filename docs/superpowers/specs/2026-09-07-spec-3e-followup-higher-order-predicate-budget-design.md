# Spec 3E follow-up — The elements that were excluded, and the answer they were resolved for — Design

**Date:** 2026-09-07
**Ticket:** [#208](https://github.com/karlssberg/Motiv/issues/208)
**Plan:** [`2026-09-07-spec-3e-followup-higher-order-predicate-budget.md`](../plans/2026-09-07-spec-3e-followup-higher-order-predicate-budget.md)
**Source:** bundle spec
[3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
§2 Structural safety (19).
**Lineage:** Spec 3E ([#144](https://github.com/karlssberg/Motiv/pull/144)) →
[#145](https://github.com/karlssberg/Motiv/issues/145) →
[#202](https://github.com/karlssberg/Motiv/issues/202) / [#206](https://github.com/karlssberg/Motiv/pull/206)
→ [#209](https://github.com/karlssberg/Motiv/issues/209) / [#210](https://github.com/karlssberg/Motiv/pull/210)
→ this. **Spawns:** [#213](https://github.com/karlssberg/Motiv/issues/213).

## What the defect was

`HigherOrderResults.Materialize` opened an `EvaluationBudget.Exclude()` scope spanning the enumeration
and the projection, and closed it before returning. Every one of the nineteen higher-order proposition
classes then applied its predicate *outside* that scope:

```csharp
private (ModelResult<TModel>[] Results, bool IsSatisfied) EvaluateModels(IEnumerable<TModel> models)
{
    var results = HigherOrderResults.Materialize(models, predicate,
        static (model, p) => new ModelResult<TModel>(model, p(model)));   // excluded
    return (results, higherOrderPredicate(results));                      // charged
}
```

For the built-in quantifiers that is free — `AsAllSatisfied` and its siblings count booleans and
evaluate nothing. For a predicate supplied through `As(...)` it can cost a whole evaluation, because
the caller's lambda may evaluate a proposition of its own. The red run, on a three-node composition
against a limit of twenty, with a nineteen-node threshold read inside the predicate:

```
Motiv.SpecException : The evaluation exceeded the maximum size of 20 nodes.
   at Motiv.Traversal.EvaluationBudget.Charge()
   at Motiv.Traversal.EvaluationFold.Fold[...]
   at ...HigherOrderFromBooleanPredicateProposition`2.EvaluateModels(IEnumerable`1 models)
```

All four families, on both funnels: nine cases red, one message, one cause.

### The shape of the line, which is the part worth keeping

This is not "one more seam was missed", the way telemetry was in #209. The seam **existed and was
applied to this exact node**. What it got wrong was the *unit*: it declared the elements excluded and
stopped short of the answer they were resolved for.

Stated as a rule, it is the same one that already justifies every other entry on the list — *work a
node does to reach its own answer is inside the node* — applied consistently rather than to the part
that happened to be in a helper. A quorum whose threshold is itself a proposition is one evaluation
inside one node; it is not composition, and the composition it sits in did not ask for it.

## The fix

Option 2 from the ticket, plus one thing the ticket does not say.

```csharp
internal static (TResult[] Results, bool IsSatisfied) MaterializeAndDecide<TSource, TState, TResult>(
    IEnumerable<TSource> source,
    TState state,
    Func<TSource, TState, TResult> project,
    Func<IEnumerable<TResult>, bool> decide)
{
    if (source is null) throw new ArgumentNullException(nameof(source));

    using var exclusion = EvaluationBudget.Exclude();

    var results = Materialize(source, state, project);
    return (results, decide(results));
}
```

and all nineteen `EvaluateModels` bodies collapse to one expression:

```csharp
private (ModelResult<TModel>[] Results, bool IsSatisfied) EvaluateModels(IEnumerable<TModel> models) =>
    HigherOrderResults.MaterializeAndDecide(models, predicate,
        static (model, p) => new ModelResult<TModel>(model, p(model)),
        higherOrderPredicate);
```

**The addition: `Materialize` became private.** Option 1 in the ticket was rejected for "nineteen
chances to miss one, with nothing that would go red if one were missed" — but option 2 *as written*
does not remove that risk, it only removes today's instance of it. A public materializing half is
still there to be called, and a twentieth family that calls it and applies its own predicate is the
same bug back. Deleting the half-seam is what changes "you must remember" into "you cannot express
it".

The `else` branch of that argument is real and was weighed: `Materialize` had a direct unit test with
five cases pinning its array / `IReadOnlyList` / buffered branches, and privatising it means those
cases reach the projection through a predicate they do not care about. That is a small, honest cost —
the branches are still covered, through the only door that exists.

### Semantics, precisely

`Exclude()` parks the composition's count at zero and restores it on dispose, so the predicate's own
evaluation enters the fold as a *root*: it gets a full budget of its own, and `Ownership` releases it
on the way out. So the predicate is not *unbounded* — a runaway predicate is still refused, on its own
account — it simply costs the enclosing composition nothing. That is the same guarantee the elements
already had, and the reason the fix is not a hole in the backstop.

## The gate, and why it reads IL

The ticket's third ask is the one with the longest reach:

> Nothing checks the list is complete — #206 shipped with a claim that there were two seams when there
> were three, and that was found by reading rather than by CI.

`HigherOrderSeamGateTests` enumerates, by reflection, every non-abstract `*Proposition` class in the
four higher-order namespaces — nineteen, today — and asserts each one has a method whose **IL**
contains a call to `MaterializeAndDecide`.

Three deliberate choices:

- **IL, not source text.** A source-level check can be satisfied by a comment and defeated by
  reformatting. IL is what runs.
- **The population is discovered, not listed.** A twentieth family is in scope the moment it is
  written, without anyone remembering this file exists. The complement — a hard-coded list of nineteen
  type names — would pass green on the exact change it is meant to catch.
- **A guard against passing vacuously.** The population is asserted to be exactly nineteen in a
  separate case, *before* anything is asserted about its members. A reflection query that silently
  stops matching is a gate reporting a property it is not checking, which 4I's retrofit named as worse
  than no check at all.

The scan is naive: it looks for opcode `0x28` and resolves the following four bytes as a metadata
token, without decoding operand lengths, so those four bytes could in principle be another
instruction's operand. **The asymmetry is what makes that acceptable, and it is stated in the test
rather than left to be discovered.** The assertion is that the call is *present*. A false negative —
the failure mode that would make the gate lie — cannot be produced by an over-eager scan; a false
positive would need the seam's own token to appear by accident. If the gate is ever wrong it is wrong
in the direction that shows up as a red build.

**Red-proved rather than assumed.** One site was rewritten to reach its decision with
`models.Select(...).ToArray()` and its own predicate — what a hand-written twentieth family plausibly
looks like — and the gate named it exactly:

```
should be empty but had
["HigherOrderFromBooleanPredicateProposition`2"]
```

## The review round, and the two findings that were about the gate rather than the code

The mandatory `code-simplifier` pass found nothing wrong with the fix and two real defects **in the
gate the fix shipped** — which is the same shape 4I's retrofit generalised: *a check reporting a
property it is not actually checking is worse than no check, because everything downstream reads its
green as evidence.*

**1. The population was defined by a method name.** The first gate selected types by
`GetMethod("EvaluateModels")`. The claim in its own doc was about *propositions* — "a twentieth family
added tomorrow is red here without anyone remembering this file exists" — and that holds only if the
twentieth family also names its method `EvaluateModels`. One that inlined the decision into
`EvaluatePolicy`, or called it `Resolve`, contributes nothing to the population and the gate stays
green at nineteen-of-nineteen. **The gate's population is now derived from types** — non-abstract
`*Proposition` classes in the four family namespaces — with the count itself pinned at 19 in a separate
case, so a family added outside those namespaces surfaces as a miscount rather than as silence.

**2. Nothing distinguished "found the call in all nineteen" from "returns true for anything".** The
scanner is hand-rolled; a version of `Calls` that answered `true` unconditionally leaves the gate
green. There is now a negative control asserting `Calls` reports **false** for
`HigherOrderShortCircuit.Evaluate` — deliberately the *sibling seam*, the allocation-free funnel to the
same elements, so the control is a method that plausibly might have called this one rather than an
arbitrary unrelated victim. Both directions were then red-proved by mutation: bypassing one site turns
the gate red, and a `Calls` that always returns `true` turns the control red.

### The measurement that changed a test's documentation rather than its code

Three failure shapes were added to `EvaluationBudgetTests`' leak-canary theory to cover the new
exclusion's exceptional paths — a throwing `As(...)` predicate on both funnels, and an oversized one.
Before shipping them they were measured against the mutation they appear to guard: `MaterializeAndDecide`
changed to restore the parked count only on the normal path.

**Every case stayed green, the pre-existing `throwing-element` ones included.** The reason is
structural rather than incidental: a root `Ownership` zeroes the count on *every* exit path, so whether
an exclusion handed its parked count back or dropped it is invisible to the next caller on the thread.
The theory enumerates exceptional paths; it does not prove the restore on any of them. What actually
holds the park-and-restore is `Should_resume_the_compositions_count_after_a_higher_order_operand`, on
the **normal** path, where the composition carries on counting afterwards.

The cases were kept — they are the net that catches a change to `Ownership` itself, and an enumeration
a later edit cannot quietly shrink — and the theory's own remarks now say what it can and cannot see,
with the measurement. Deleting them would have removed free coverage; shipping them silently would have
left a green that a reader takes as proof of a restore nobody checked. This is #195's lesson pointed at
a test rather than at a guard: *a green suite is not evidence about the specific thing you assume it is
evidence about* — and the way to settle it is to run the mutation, not to reason about it.

### The count-pin earned its keep on the next review round

A bot review of the PR suggested tightening the population matcher from `Name.Contains("Proposition")`
to `Name.EndsWith("Proposition")`, on the correct ground that a substring match would also admit a
`PropositionBuilder` or `PropositionExtensions` added to one of these namespaces later.

**Applied literally it empties the population.** All nineteen are generic, and a generic type reflects
as `HigherOrderFromBooleanPredicateProposition`2` — the arity suffix means a plain `EndsWith` matches
none of them. The gate's own assertion, `undeclared.ShouldBeEmpty()`, then passes **vacuously**: zero
propositions, zero undeclared.

The separate count case caught it immediately, which is exactly the reason it is a separate assertion
rather than a guard clause inside the gate. The shipped matcher strips the arity and then tests the
suffix, so the reviewer's concern is addressed without the failure mode their fix would have
introduced. Two things generalise:

- **A tightening is as capable of blinding a gate as a loosening**, and the direction that blinds it is
  the one that leaves everything green.
- **The vacuity guard has to be a peer of the gate, not a precondition inside it.** Had it been an
  early `if (population.Length == 0) return;`, or even an assertion in the same method, the fix would
  have been applied, the suite would have been green, and the gate would have been dead.

### Findings taken and not taken

Taken, beyond the two above: the class-level doc on `HigherOrderResults` moved onto
`MaterializeAndDecide` (with one internal method, a class doc that describes a method has to demote its
`paramref`s to prose — which is the symptom, not the style); the fast-path paragraph moved down to the
private `Materialize`, which is now the only thing it describes; a half-sentence added that `decide`
takes an `IEnumerable` and so allocates one enumerator per decision, which reads oddly next to a
paragraph about avoiding enumerator allocations unless said; the gate split into its own class, since
it neither mutates `MotivLimits` nor needs the serialized collection the budget cases run in; the
assertions restated as `Should.NotThrow`, because the failure guarded is a refusal and stating it as
`Satisfied.ShouldBeTrue(because)` throws before the carefully-written reason can print.

Not taken: reformatting all nineteen call sites one-argument-per-line — real, but nineteen files of
pure churn against a diff whose value is that each site is one line and each site is identical to the
others already; and de-duplicating the two theories, which the repo's own "avoid over-DRYing" note
covers and which would cost the two distinct doc comments that say why both funnels are stated.

## Scope: what was cut, and why it is a decision rather than an omission

#208 names three delegate families. Only the eager one is here.

| Family | When | Shipped |
|---|---|---|
| `higherOrderPredicate` in `EvaluateModels` | eagerly, during the evaluation | yes |
| `causeSelector` from a result's `field ??=` | lazily, at first property read | [#213](https://github.com/karlssberg/Motiv/issues/213) |
| `whenTrue` / `whenFalse` from a result | lazily, at first property read | #213 |

The eager family had a seam to hide behind and nineteen identical bodies. The lazy families have
neither: ~19 result classes, each deferring a different mix of the two delegates in a different
generic shape, with no shared call site.

More importantly they need an answer the ticket does not contain — **is reading a result property part
of the evaluation that produced it?** The result may be read long after its evaluation ended, in which
case there is no budget in force and nothing to fix; or during an unrelated one, in which case the
charge lands at a node with no connection to the delegate. That is #209's shape, but for code the
caller *did* write, which is the argument that keeps a per-item leaf predicate counted. Editing
nineteen classes to match an answer nobody has written down is how a convention becomes folklore.

**The asymmetry this leaves is real and should be named rather than glossed.** `As(...)`'s default
`causeSelector` is `Causes.Get(..., higherOrderPredicate)` — so the *same* user predicate is now
excluded in `EvaluateModels` and still charged when a result's `Causes` are read. Shipping half a fix
is defensible; shipping it silently is not, which is why #213 exists and why `EvaluationBudget`'s
remarks cite it from the seam list itself.

## The documented line moved

`MaxEvaluationSize`'s remarks and `docs/limits/index.md` both listed *"a higher-order predicate
supplied through `As(...)`"* among the things that **are** counted. That sentence is now false, and it
was the only statement of the line a caller could read, so both moved in this commit.

Two details preserved rather than swept along with it:

- `Spec.Build((Order o) => o.Lines.All(line.Matches))` — a plain predicate evaluating propositions per
  item — is **still counted**, and still the example. It is not a higher-order proposition; it is a
  leaf whose lambda happens to loop, and the engine cannot tell it from composition.
- The `WhenTrue`/`WhenFalse`-and-cause-selector entry stays on the counted side, now naming the
  cause-selecting delegate explicitly, because that is what is true until #213.

This is a **permissive** change to a backstop: a composition that used to be refused is now admitted.
That is the safe direction — #206 went the other way, which is why it needed the "breaking change
wearing a bug fix's clothes" argument and this does not — but it is still a published promise being
withdrawn, and a promise withdrawn without editing the page that made it is indistinguishable from a
bug.

## Verification

- `HigherOrderPredicateBudgetTests` — eight cases: four families × two funnels. All red before, green
  after.
- `HigherOrderSeamGateTests` — three: the population count, the gate, and the scanner's negative
  control. Red before (no seam to name), green after, and both directions red-proved by mutation.
- `EvaluationBudgetTests` — three exceptional shapes added for the new scope, and the theory's remarks
  corrected to state what it can and cannot see.
- Full solution suite: 17 test assemblies, **5,990** in `Motiv.Tests` on each of net8.0 / net9.0 /
  net10.0, plus `Motiv.Serialization.Tests` (983 × 3), `Motiv.Studio.Tests`, the three example
  suites, `Motiv.CodeFix.Tests`, `Motiv.Analyzer.Tests` and the EF/SQL/AspNetCore suites. No failures.
- **Not run: net472.** There is no `mono` on this machine, so the two net472 test runs abort before
  executing. They *build* clean (`dotnet build`, 0 errors, 0 warnings), which is what CI's Windows leg
  needs; the gate's reflection is exercised locally on net8/9/10 only.
