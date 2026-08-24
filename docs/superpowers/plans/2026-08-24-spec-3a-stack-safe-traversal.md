# Spec 3A — Stack-Safe Result Traversal — Implementation Plan

**Design:** [2026-08-24-spec-3a-stack-safe-traversal-design.md](../specs/2026-08-24-spec-3a-stack-safe-traversal-design.md)
**Ticket:** [19](https://github.com/karlssberg/Motiv/issues/119), sub-questions 2 and 3

## Global constraints

- **TDD throughout.** Every task writes its test first and watches it fail for the right reason.
- **No public signature changes.** Not one. The whole slice is invisible to a compiler and visible only
  to a stack-depth probe.
- **The oracle lands before the rewrite.** Task 2 captures today's recursion in the test project and
  proves the captured copy agrees with the live code. Only then does any production body change. A
  green oracle over unchanged code is what makes it an oracle rather than a restatement.
- **One member at a time.** Each of tasks 4–9 swaps one family, runs the oracle suite, and runs
  `Motiv.Tests` in full before the next starts.
- **Run the whole solution at the end.** Per CLAUDE.md, justification output is asserted in
  `Motiv.Poker.Tests`, `Motiv.ECommerce.Tests` and `Motiv.SmartHome.Tests`, not only in `Motiv.Tests`.

## File structure

```
src/Motiv/Traversal/PostOrderFold.cs                        (new)
src/Motiv/BooleanResultBase.cs                              (6 bodies)
src/Motiv/AssertionExtensions.cs                            (4 helpers)
src/Motiv/Shared/Explanation.cs                             (2 resolvers)
src/Motiv/ResultDescriptionBase.cs                          (child-selector + combine seam)
src/Motiv/**/[12 description implementations]               (render-from-children)
src/Motiv.Tests/Traversal/RecursiveTraversalOracle.cs       (new — captured recursion)
src/Motiv.Tests/Traversal/ResultTreeGenerator.cs            (new — seeded tree generator)
src/Motiv.Tests/Traversal/StackSafeTraversalOracleTests.cs  (new — the gate)
src/Motiv.Tests/Traversal/DeepCompositionTests.cs           (new — depth regressions)
src/Motiv.Tests/Traversal/PostOrderFoldTests.cs             (new — primitive unit tests)
src/examples/Motiv.Benchmark/                               (allocation case)
```

---

### Task 1: The `PostOrderFold` primitive

1. `PostOrderFoldTests` — a hand-rolled toy tree (`sealed class Node(string Name, Node[] Children)`) with
   a dictionary memo. Assert: post-order visit order; `combine` receives children's values in
   `descend` order; a node whose `read` returns non-null is never passed to `combine`; a shared node
   appears in `combine` exactly once; a 100,000-deep left spine returns.
2. Watch them fail (no `PostOrderFold`).
3. Write `src/Motiv/Traversal/PostOrderFold.cs`. Explicit frame stack, shared value list, `valueBase`
   window per frame. No `yield`, no closures inside the loop, no LINQ.
4. Green.

**Care:** the value window handed to `combine` must be a view over the shared list that is valid only
for the duration of the call — document that, and have every caller materialise what it needs.

### Task 2: Capture the oracle, prove it agrees

1. `RecursiveTraversalOracle` — a static class in `Motiv.Tests` holding a verbatim copy of today's
   recursive body for every member the plan will rewrite. It reaches the same public virtuals
   (`Causes`, `Underlying`, `CausesWithValues`, `UnderlyingWithValues`, `Explanation`, `Description`)
   through `InternalsVisibleTo`, so it is a faithful copy rather than a paraphrase.
2. `ResultTreeGenerator` — seeded `Random`, produces trees over the full vocabulary: `And`, `Or`,
   `XOr`, `AndAlso` / `OrElse` in both two-operand and short-circuited one-operand forms, `Not` nests
   of random parity, minimal / explanation / metadata propositions, higher-order (`AsAllSatisfied`,
   `AsAnySatisfied`, `AsAtLeastNSatisfied`) and expression-tree (`Spec.From`) propositions. Depth
   1–40, breadth 1–5, both satisfied and unsatisfied models.
3. `StackSafeTraversalOracleTests` — theory over ~200 seeds × every member, asserting the oracle
   agrees with the live property **right now**, before anything changes.
4. Run. All green. This is the step that makes the oracle trustworthy: a discrepancy here is a bug in
   the *copy*, and must be fixed before proceeding.

**Care:** the generator must produce short-circuited results with a null `Right`. `a.OrElse(b)` only
short-circuits when `a` is satisfied — generate models that reach both arms, and assert the corpus
contains at least one node with `Right is null`, so a generator regression cannot silently drop the
riskiest shape.

### Task 3: The depth regressions

1. `DeepCompositionTests` — one test per member, each running on `new Thread(body, 1024 * 1024)` and
   joining, over a chain of 2,000 minimal propositions. They abort the test process today.
2. Mark them skipped with the ticket reference, and unskip each one as its member lands. (A test that
   *aborts the runner* cannot be left red the way an ordinary failing test can — skipping is the only
   way to keep the suite runnable between tasks.)

**Care:** a 1 MB thread is the point. On the 8 MB main stack most of these pass today and the test
proves nothing.

### Task 4: `UnderlyingAssertionSources` and `UnderlyingAllAssertionSources`

1. Unskip their depth regressions; watch them abort.
2. Convert both `field ??=` stores to explicit private fields.
3. Rewrite both bodies as `PostOrderFold.Fold` calls: `descend` = the operation-typed children of
   `Causes` / `Underlying`; `combine` re-walks the full child list and interleaves each descended
   child's value with each non-descended child itself, then `ElseIfEmpty([node])`.
4. Static readonly delegate fields for all five arguments.
5. Oracle suite green, depth regressions green, `Motiv.Tests` green.

**Care:** `combine`'s interleave must consume descended values *in `descend` order*, and `descend`'s
filter and `combine`'s filter must be the same predicate. Extract the predicate to one private static
method used by both — two copies of `is IBooleanOperationResult` is exactly how these three bodies
drifted in the first place.

### Task 5: `UnderlyingMetadataSources`

1. Unskip its depth regression.
2. Same fold, plus the memoization it never had.
3. **Preserve the divergence verbatim**: the non-operation arm yields `node` (the parent), not the
   child, and there is no `ElseIfEmpty`. Add an XML remark saying so and pointing at the follow-up
   ticket, so the next reader does not "fix" it.
4. Oracle green — the oracle copy must contain the divergence too, or this task passes for the wrong
   reason.

### Task 6: `UnderlyingExpressionResults` and `AllAssertions`

1. Unskip; fold both. `UnderlyingExpressionResults` branches on the `(node, child)` pair and in one
   arm yields the child *and* its value — the combine step reproduces that; `descend` is all of
   `Causes`.
2. `UnderlyingReasons` needs no change; verify its regression passes once `UnderlyingExpressionResults`
   has landed.

### Task 7: The `AssertionExtensions` root walks

1. Unskip `RootAssertions`, `AllRootAssertions`, `SubAssertions`, `AllSubAssertions`.
2. `GetAssertions`, `GetAllAssertions`, `GetRootAssertions`, `GetAllRootAssertions` fold with a
   walk-local `Dictionary<node, value>` memo — they take an arbitrary sequence and have no node field
   to cache in.
3. These four are the *lowest* ceilings in the design's table and the ones that re-allocate their
   iterator chain per enumeration; expect the benchmark to move here.

**Care:** these are `public static` and currently return lazily. Folding makes them eager. That is a
timing change, not a contract change, but assert the existing `AssertionExtensionsTests` still pass
before moving on.

### Task 8: `Explanation.Underlying` / `AllUnderlying`

1. Unskip. Fold over `Explanation` nodes; the "children's assertions equal the parent's → collapse a
   level" comparison becomes the combine step.
2. `Explanation` is `public sealed` with `internal` constructors, so the memo can be a private field.

### Task 9: The description tree

1. Unskip `Reason` and `Justification`.
2. **`Reason` first.** `BinaryBooleanResultDescription.Reason` and `XOrBooleanResultDescription`'s
   `ContainsBinaryOperation` fold. `NotBooleanResultDescription.NegateNotOperator` already loops —
   leave it.
3. **Then `GetJustificationAsLines`.** Add to `ResultDescriptionBase` an internal child-selector
   (`IReadOnlyList<(ResultDescriptionBase, JustificationMode)>`) and an internal combine
   (`IEnumerable<string> Render(IReadOnlyList<IReadOnlyList<string>> childBlocks, JustificationMode mode)`).
   Move each of the twelve formatters' bodies into `Render` unchanged except that child lines come from
   the parameter. `Justification` drives one fold keyed on `(description, mode)`.
4. Run the oracle suite, `Motiv.Tests`, **and the three example test projects** — this is the task most
   likely to move a justification string.

**Care:** `GetBinaryJustificationAsLines`' `FlattenCollapsible` is itself recursive over `Causes` and
collapses same-operation children into one heading. It must fold too, and it changes which
*descriptions* are children of a binary description — so the child-selector, not just the renderer,
has to account for it. This is the single subtlest step in the plan; do it on its own commit.

### Task 10: Documentation, benchmark, and verification

1. Benchmark case; record before/after allocation in the PR body.
2. Restate `MaxCompositionDepth`'s XML docs: it bounds result-tree size and work, not stack. Do not
   change its value (design doc, "Explicitly out of scope").
3. File the three follow-up tickets: evaluation recursion + the result-size bound that travels with it;
   `UnderlyingMetadataSources`' parent-vs-child divergence; `MaxCompositionDepth`'s re-derivation.
4. Full solution test run on net10.0, then the mandatory code-simplifier pass, then re-run.

---

## Verification obligations (from bundle spec 3, §7)

- [ ] The fold returns identical output to the recursive oracle on generated short-circuited trees.
- [ ] A composition far past today's 1,038 ceiling no longer aborts the process.
- [ ] Every member in the design's table returns at the same depth as every other — uniformity, not
      merely a higher number.
- [ ] Transient allocation does not rise; `UnderlyingMetadataSources` falls.
