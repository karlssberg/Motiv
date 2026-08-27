# Spec 3E — Stack-Safe Evaluation — Implementation Plan

**Design:** [2026-08-27-spec-3e-stack-safe-evaluation-design.md](../specs/2026-08-27-spec-3e-stack-safe-evaluation-design.md)
**Ticket:** [#135](https://github.com/karlssberg/Motiv/issues/135), the remainder of
[19](https://github.com/karlssberg/Motiv/issues/119)

## Global constraints

- **TDD throughout.** Failing test → confirm it fails for the right reason → minimum code → green.
- **Behaviour-preserving by construction.** This is an evaluation-order rewrite. Nothing about which
  operands are evaluated, what results they produce, or how those results compose may change. The
  existing suite is the oracle: a green run is the claim.
- **A stack overflow aborts the process, not the test.** The depth cases run on an explicitly-sized
  1 MB thread, as `DeepCompositionTests` does — on the 8 MB main stack they would pass without the fix.
- **Run the whole solution at the end.** Per CLAUDE.md the example projects assert justification
  strings.

## File structure

```
src/Motiv/Traversal/IOperationFold.cs                (new — the seam)
src/Motiv/Traversal/EvaluationFold.cs                (new — the driver)
src/Motiv/Traversal/IAsyncOperationFold.cs           (new — the async seam)
src/Motiv/Traversal/AsyncEvaluationFold.cs           (new — the async driver)
src/Motiv/Traversal/ISelectedValueResult.cs          (new — a policy result's Value selection)
src/Motiv/Traversal/SelectedValue.cs                 (new — and its iterative resolution)
src/Motiv/MotivLimits.cs                             (new — the size bound's setting)
src/Motiv/{And,Or,XOr,AndAlso,OrElse,Not}/*.cs       (27 operators: implement the seam,
                                                      EvaluateSpec/EvaluatePolicy/Matches call the fold)
src/Motiv.Serialization/RuleSerializerOptions.cs     (MaxCompositionDepth re-derived + XML docs)
src/Motiv.Tests/Traversal/DeepEvaluationTests.cs     (new — the depth regression)
src/Motiv.Tests/Traversal/EvaluationFoldTests.cs     (new — order, short-circuit, the bound)
docs/…                                               (the size bound and the new cap)
```

## Sequence

1. **The failing test.** A 50,000-operand `And` chain, evaluated on a 1 MB thread. Confirm it aborts
   with the recursion the ticket names, then leave it red.
2. **The seam and the driver.** `IOperationFold` plus the generic frame machine, with the `And`
   family only, so the driver is exercised before eighteen classes depend on it.
3. **The rest of the family**, operator by operator: `Or`, `XOr`, `Not`, then the two short-circuiting
   ones last, since they are the cases the descent seam exists for.
4. **`Matches`.** The second fold over the same seam; its own depth case at a depth past its own
   (higher) measured ceiling.
5. **Short-circuit assertions.** Direct evidence that `AndAlso`'s right operand is not evaluated when
   the left is unsatisfied, and `OrElse`'s when the left is satisfied — the property the fold is most
   able to break silently.
6. **The size bound.** A limit, a message that names it, a default derived by measuring what a bound
   evaluation costs, and a test that the default admits everything the suite builds.
7. **Async.** The parallel seam and driver, the nine async operators, and the depth cases at a depth
   past async's own (much lower) measured ceiling. Deliberately after the synchronous half is green,
   so the driver shape is settled before it is copied.
8. **`MaxCompositionDepth`.** Re-derived against the bound from step 6 and the cost measurements from
   step 7; XML docs restated to say which constraint the number comes from.
9. **Docs**, then the full solution run, then the simplification pass.
