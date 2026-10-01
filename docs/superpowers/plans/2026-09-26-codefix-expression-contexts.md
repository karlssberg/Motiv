# Code fix expression contexts — Plan

**Design:** `docs/superpowers/specs/2026-09-26-codefix-expression-contexts-design.md`

Four stacked PRs, bottom to top. Each layer's failing tests are rows the layer below left skipped.

1. **`codefix/context-matrix`: the matrix and its harness.**
   - Add `test/Motiv.CodeFix.Tests/CodeFixHarness.cs` (ad hoc workspace, `[|…|]` markup, compile
     check before and after, `// must survive` lines).
   - Add `MotivConvertToSpecContextTests`: one template per slot, a theory for converting contexts,
     a theory for contexts that must not be flagged, and a completeness fact.
   - Run once with every `Skip` removed and label each failure by its cause. Six slots pass today.
2. **`codefix/analyzer-constant-contexts`: never flag what cannot be converted.**
   - Analyzer tests first. Skip an expression whose `GetConstantValue` has a value, and one inside a
     lambda converted to `System.Linq.Expressions.Expression<T>`.
   - Un-skip the six "must not be flagged" rows.
3. **`codefix/static-specs`: hoist specs that do not capture `this`.**
   - Update the exact-output tests to `private static readonly XProposition XProposition = new();`
     and `XProposition.Evaluate(…)`. Watch them fail, then make `SpecInvocationReplacer` choose
     static from `hasInstanceMethods` instead of from the method's modifiers.
4. **`codefix/replace-expression-in-place`: rewrite the expression, not the method.**
   - Un-skip the `MethodRewrite` and `RequiresMethod` rows and watch them fail.
   - Split `SpecInvocationReplacer` into choosing the replacement: the statement form (return,
     local declaration or assignment in a block, and bool expression-bodied methods) or the inline
     check from `ISpecFieldCustomizer`.
   - Place the field on the nearest `TypeDeclarationSyntax`. Offer no fix without one.
   - Take line endings from `SyntaxContext` rather than a hard-coded `\n`.
   - Update this design's status.

Each layer runs the full solution suite before it is pushed, since the example projects assert
formatted output too. Each is then reviewed by `code-simplifier`.
