---
phase: 01-analyzer-expansion
plan: 01
type: tdd
wave: 1
depends_on: []
files_modified:
  - src/Motiv.Analyzer.Tests/FindBooleanExpressionsTests.cs
  - src/Motiv.Analyzer/MotivAnalyzer.cs
autonomous: true

must_haves:
  truths:
    - "Analyzer detects simple `obj is string` type-check and reports MOTIV0001"
    - "Analyzer detects `obj is string s` declaration pattern and reports MOTIV0001"
    - "Analyzer detects property pattern `obj is { Length: > 0 }` and reports MOTIV0001"
    - "Analyzer detects relational pattern `value is > 5` and reports MOTIV0001"
    - "Analyzer detects logical pattern `value is > 5 and < 10` and reports MOTIV0001"
    - "Analyzer detects negated pattern `obj is not string` and reports MOTIV0001"
  artifacts:
    - path: "src/Motiv.Analyzer/MotivAnalyzer.cs"
      provides: "IsPatternExpression handler registration and analysis method"
      contains: "SyntaxKind.IsPatternExpression"
    - path: "src/Motiv.Analyzer.Tests/FindBooleanExpressionsTests.cs"
      provides: "Test coverage for is type-check and pattern-matching detection"
      min_lines: 250
  key_links:
    - from: "src/Motiv.Analyzer/MotivAnalyzer.cs"
      to: "AnalyzeIsPatternExpression"
      via: "RegisterSyntaxNodeAction"
      pattern: "RegisterSyntaxNodeAction.*AnalyzeIsPatternExpression.*IsPatternExpression"
---

<objective>
Add `is` type-check and pattern-matching detection to the Motiv analyzer using TDD.

Purpose: The analyzer currently only detects binary expressions (comparisons and logical operators). This plan extends it to detect `is` patterns — the other major category of boolean expressions in C#. This covers requirements ANLZ-01, ANLZ-02, TEST-01, TEST-02.

Output: Failing tests written first, then a new `AnalyzeIsPatternExpression` handler that makes them pass. All existing tests must continue to pass.
</objective>

<execution_context>
@/home/dan/.claude/get-shit-done/workflows/execute-plan.md
@/home/dan/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/PROJECT.md
@.planning/ROADMAP.md
@.planning/STATE.md
@.planning/phases/01-analyzer-expansion/01-RESEARCH.md
@src/Motiv.Analyzer/MotivAnalyzer.cs
@src/Motiv.Analyzer.Tests/FindBooleanExpressionsTests.cs
@src/Motiv.Analyzer.Tests/CSharpDiagnosticAnalyzerVerifier.cs
</context>

<tasks>

<task type="auto">
  <name>Task 1: RED — Write failing tests for is type-check and pattern-matching detection</name>
  <files>src/Motiv.Analyzer.Tests/FindBooleanExpressionsTests.cs</files>
  <action>
Add the following test methods to `FindBooleanExpressionsTests`. Each test follows the existing pattern: define the boolean expression string, embed it in a class method, and assert MOTIV0001 diagnostic at the correct span.

**ANLZ-01 — is type-check tests (TEST-01):**

1. `Should_detect_is_type_check_expression` — Source: `obj is string` in a method returning bool. Expect MOTIV0001 on the `obj is string` span.

2. `Should_detect_is_type_check_with_declaration_pattern` — Source: `obj is string s` in an if-condition or local assignment. Expect MOTIV0001 on the `obj is string s` span.

3. `Should_detect_negated_is_type_check` — Source: `obj is not string` in a return statement. Expect MOTIV0001 on the `obj is not string` span. (Note: `is not` still produces `IsPatternExpressionSyntax` at the expression level.)

**ANLZ-02 — pattern-matching tests (TEST-02):**

4. `Should_detect_property_pattern_expression` — Source: `obj is { Length: > 0 }` where obj is a string parameter. Expect MOTIV0001 on the full `obj is { Length: > 0 }` span.

5. `Should_detect_relational_pattern_expression` — Source: `value is > 5` where value is an int parameter. Expect MOTIV0001 on the `value is > 5` span.

6. `Should_detect_logical_and_pattern_expression` — Source: `value is > 5 and < 10` where value is an int parameter. Expect MOTIV0001 on the `value is > 5 and < 10` span.

7. `Should_detect_logical_or_pattern_expression` — Source: `value is 1 or 2 or 3` where value is an int parameter. Expect MOTIV0001 on the `value is 1 or 2 or 3` span.

**Test code pattern** (follow existing conventions exactly):
```csharp
[Fact]
public async Task Should_detect_is_type_check_expression()
{
    const string booleanExpression = "obj is string";
    const string code =
        $$"""
          namespace MyNamespace;

          public class MyClass
          {
              public bool IsValid(object obj)
              {
                  return {{booleanExpression}};
              }
          }
          """;

    await new VerifyCS.Test
    {
        TestState =
        {
            Sources = { (Source, code) },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 7, 16, 7, 16 + booleanExpression.Length)
            }
        }
    }.RunAsync();
}
```

For tests where the expression is not in a return statement (e.g., local variable assignment), adjust the line/column span accordingly. The column of the expression start depends on the assignment syntax — for `var result = EXPR;` on an indented line, calculate the column carefully.

After writing all 7 tests, run the test suite to confirm these new tests FAIL (the analyzer does not yet handle IsPatternExpression). Existing tests must still PASS.
  </action>
  <verify>
Run: `dotnet test src/Motiv.Analyzer.Tests/ --filter "Should_detect_is_type_check|Should_detect_property_pattern|Should_detect_relational_pattern|Should_detect_logical_and_pattern|Should_detect_logical_or_pattern|Should_detect_negated_is_type_check" --no-build 2>&1 || dotnet test src/Motiv.Analyzer.Tests/ --filter "Should_detect_is_type_check|Should_detect_property_pattern|Should_detect_relational_pattern|Should_detect_logical_and_pattern|Should_detect_logical_or_pattern|Should_detect_negated_is_type_check"`

All 7 new tests must FAIL (expected diagnostic not found — analyzer does not yet detect IsPatternExpression).

Then run: `dotnet test src/Motiv.Analyzer.Tests/ --filter "Should_analyze_and_identify|Should_ignore|Should_create_a_single"`

All 4 existing tests must PASS.
  </verify>
  <done>7 new test methods exist and fail for the right reason (missing MOTIV0001 diagnostic). 4 existing tests pass.</done>
</task>

<task type="auto">
  <name>Task 2: GREEN — Implement AnalyzeIsPatternExpression handler</name>
  <files>src/Motiv.Analyzer/MotivAnalyzer.cs</files>
  <action>
Add `IsPatternExpression` detection to the analyzer. Three changes to `MotivAnalyzer.cs`:

**1. Register the new handler** in the `Initialize` method — add after the existing `RegisterSyntaxNodeAction` lines:
```csharp
context.RegisterSyntaxNodeAction(AnalyzeIsPatternExpression, SyntaxKind.IsPatternExpression);
```

**2. Add the handler method** (follows the same pattern as `AnalyzeBinaryExpression`):
```csharp
private static void AnalyzeIsPatternExpression(SyntaxNodeAnalysisContext context)
{
    var isPatternExpression = (IsPatternExpressionSyntax)context.Node;

    if (IsNestedInBinaryExpression(isPatternExpression)) return;

    if (IsInsideSpecBuildLambda(isPatternExpression, context.SemanticModel)) return;

    var diagnostic = Diagnostic.Create(Motiv0001, isPatternExpression.GetLocation());
    context.ReportDiagnostic(diagnostic);
}
```

Note: The `IsNestedInBinaryExpression` check is important here — it suppresses pattern diagnostics when the pattern is already part of a larger binary expression (e.g., `x > 5 && obj is string`). This provides ANLZ-03 suppression for the pattern-nested-in-binary direction. The bidirectional case (binary-nested-in-pattern) is handled in Plan 02.

The `IsInsideSpecBuildLambda` check reuses the existing helper to provide ANLZ-04 suppression for pattern expressions.

**Do not modify** `AnalyzeBinaryExpression` or `IsNestedInBinaryExpression` in this task. Bidirectional suppression (binary-in-pattern) is Plan 02.

Run all tests to confirm the 7 new tests now PASS and all 4 existing tests still PASS.
  </action>
  <verify>
Run: `dotnet test src/Motiv.Analyzer.Tests/`

All 11 tests (4 existing + 7 new) must PASS.
  </verify>
  <done>Analyzer detects `is` type-check expressions and pattern-matching expressions. All tests green. ANLZ-01 and ANLZ-02 requirements satisfied.</done>
</task>

</tasks>

<verification>
1. `dotnet test src/Motiv.Analyzer.Tests/` — all 11 tests pass
2. `dotnet build src/Motiv.Analyzer/` — builds without errors or warnings
3. Grep for `IsPatternExpression` in MotivAnalyzer.cs confirms registration
</verification>

<success_criteria>
- Analyzer reports MOTIV0001 for: `obj is string`, `obj is string s`, `obj is not string`, `obj is { Length: > 0 }`, `value is > 5`, `value is > 5 and < 10`, `value is 1 or 2 or 3`
- All existing tests continue to pass (no regressions)
- 7 new tests all pass
</success_criteria>

<output>
After completion, create `.planning/phases/01-analyzer-expansion/01-01-SUMMARY.md`
</output>
