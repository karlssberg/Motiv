---
phase: 01-analyzer-expansion
plan: 02
type: tdd
wave: 2
depends_on: ["01-01"]
files_modified:
  - src/Motiv.Analyzer.Tests/FindBooleanExpressionsTests.cs
  - src/Motiv.Analyzer/MotivAnalyzer.cs
autonomous: true

must_haves:
  truths:
    - "When `is` pattern is nested inside a binary expression (`x > 5 && obj is string`), only ONE diagnostic is reported on the root binary expression"
    - "When binary expression is nested inside a pattern (`obj is { Value: > 5 }`), only ONE diagnostic is reported on the root `is` expression"
    - "When parenthesized pattern is inside a binary expression (`x > 5 && (obj is string)`), only the root binary gets a diagnostic"
    - "Pattern expressions inside Spec.Build() lambdas produce NO diagnostics"
  artifacts:
    - path: "src/Motiv.Analyzer/MotivAnalyzer.cs"
      provides: "Bidirectional nesting suppression (IsNestedInPatternExpression helper)"
      contains: "IsNestedInPatternExpression"
    - path: "src/Motiv.Analyzer.Tests/FindBooleanExpressionsTests.cs"
      provides: "Tests for nesting suppression and Spec.Build suppression of patterns"
      min_lines: 350
  key_links:
    - from: "AnalyzeBinaryExpression"
      to: "IsNestedInPatternExpression"
      via: "early return guard"
      pattern: "IsNestedInPatternExpression.*return"
---

<objective>
Add bidirectional nesting suppression and Spec.Build suppression for pattern expressions using TDD.

Purpose: Without this, the analyzer reports duplicate diagnostics when `is` patterns and binary expressions are mixed (e.g., `x > 5 && obj is string` would fire on both the `&&` root AND the `obj is string` child). This plan ensures exactly one diagnostic per root expression. Covers requirements ANLZ-03 and ANLZ-04.

Output: Failing tests written first, then bidirectional suppression logic that makes them pass. All tests (existing + Plan 01) must continue to pass.
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
@.planning/phases/01-analyzer-expansion/01-01-SUMMARY.md
@src/Motiv.Analyzer/MotivAnalyzer.cs
@src/Motiv.Analyzer.Tests/FindBooleanExpressionsTests.cs
</context>

<tasks>

<task type="auto">
  <name>Task 1: RED — Write failing tests for nesting suppression and Spec.Build pattern suppression</name>
  <files>src/Motiv.Analyzer.Tests/FindBooleanExpressionsTests.cs</files>
  <action>
Add the following test methods to `FindBooleanExpressionsTests`. These test the suppression behavior — ensuring only one diagnostic fires on the root expression.

**ANLZ-03 — Nesting suppression tests:**

1. `Should_report_single_diagnostic_when_is_pattern_is_right_operand_of_logical_and` — Source contains method with `value > 0 && obj is string` where value is int and obj is object. Expect exactly ONE MOTIV0001 on the full `value > 0 && obj is string` span (the root `&&` binary expression). No diagnostic on `obj is string` alone.

2. `Should_report_single_diagnostic_when_is_pattern_is_left_operand_of_logical_and` — Source: `obj is string && value > 0`. Expect ONE diagnostic on the full expression span.

3. `Should_report_single_diagnostic_when_is_pattern_is_right_operand_of_logical_or` — Source: `value > 0 || obj is string`. Expect ONE diagnostic on the full expression span.

4. `Should_report_single_diagnostic_when_parenthesized_is_pattern_is_in_binary_expression` — Source: `value > 0 && (obj is string)`. Expect ONE diagnostic on the full expression span.

5. `Should_report_single_diagnostic_when_binary_expression_is_nested_in_property_pattern` — Source: `obj is { Length: > 0 }` where obj is a string parameter. Expect ONE MOTIV0001 on the `obj is { Length: > 0 }` span. The `> 0` inside the property pattern must NOT produce its own diagnostic.

   IMPORTANT: This test is expected to FAIL before implementation because the existing `AnalyzeBinaryExpression` handler does not yet check `IsNestedInPatternExpression` — it will report a diagnostic on `> 0` inside the property pattern.

6. `Should_report_single_diagnostic_for_complex_pattern_with_relational_subpatterns` — Source: `obj is { Value: > 5 and < 10 }` where obj has an int Value property. Expect ONE MOTIV0001 on the root `is` expression. No diagnostics on the relational sub-patterns.

   Use a helper class to provide the Value property:
   ```csharp
   const string code = """
       namespace MyNamespace;

       public class Container { public int Value { get; set; } }

       public class MyClass
       {
           public bool IsValid(Container obj)
           {
               return obj is { Value: > 5 and < 10 };
           }
       }
       """;
   ```

**ANLZ-04 — Spec.Build suppression for patterns:**

7. `Should_ignore_is_type_check_inside_spec_fluent_builder` — Source: `Spec.Build((object x) => x is string).WhenTrue("is string").WhenFalse("not string").Create()`. Expect NO diagnostics.

8. `Should_ignore_property_pattern_inside_spec_fluent_builder` — Source: `Spec.Build((string x) => x is { Length: > 0 }).WhenTrue("has length").WhenFalse("empty").Create()`. Expect NO diagnostics (neither from the `is` pattern nor from the `> 0` inside).

After writing all 8 tests, run the test suite. The nesting suppression tests (especially #5 and #6 — binary-in-pattern) should FAIL because `AnalyzeBinaryExpression` does not yet suppress when nested inside a pattern expression. Tests #1-4 may already pass due to the `IsNestedInBinaryExpression` check added in Plan 01 — that is acceptable; they serve as regression tests. Tests #7-8 may pass if `IsInsideSpecBuildLambda` already covers these — that is also acceptable.

At minimum, test #5 and #6 MUST fail to confirm the RED phase for bidirectional suppression.
  </action>
  <verify>
Run: `dotnet test src/Motiv.Analyzer.Tests/ --filter "Should_report_single_diagnostic_when_binary_expression_is_nested_in_property_pattern"`

This test must FAIL (the `> 0` inside `{ Length: > 0 }` incorrectly gets its own diagnostic).

Run: `dotnet test src/Motiv.Analyzer.Tests/ --filter "Should_report_single_diagnostic|Should_ignore_is_type_check_inside_spec|Should_ignore_property_pattern_inside_spec"`

Confirm which tests fail and which pass. At minimum the binary-in-pattern tests must fail.

Run: `dotnet test src/Motiv.Analyzer.Tests/ --filter "Should_detect_|Should_analyze_and_identify|Should_ignore_boolean_expressions|Should_create_a_single"`

All Plan 01 tests (11 total) must still PASS.
  </verify>
  <done>8 new test methods exist. Binary-in-pattern suppression tests fail (missing IsNestedInPatternExpression logic). All Plan 01 tests still pass.</done>
</task>

<task type="auto">
  <name>Task 2: GREEN — Implement bidirectional nesting suppression</name>
  <files>src/Motiv.Analyzer/MotivAnalyzer.cs</files>
  <action>
Add the reciprocal nesting check so that binary expressions nested inside pattern expressions are suppressed. Two changes to `MotivAnalyzer.cs`:

**1. Add `IsNestedInPatternExpression` helper method:**

```csharp
private static bool IsNestedInPatternExpression(SyntaxNode node)
{
    // Walk up through parenthesized and prefix unary expressions
    var parent = node.Parent;
    while (parent is ParenthesizedExpressionSyntax or PrefixUnaryExpressionSyntax)
    {
        parent = parent.Parent;
    }

    // Direct child of an is-pattern expression
    if (parent is IsPatternExpressionSyntax)
        return true;

    // Nested inside a pattern syntax node (e.g., property pattern, relational pattern)
    // This covers cases like `obj is { Value: > 5 }` where `> 5` is a GreaterThanExpression
    // inside a SubpatternSyntax inside a PropertyPatternClauseSyntax
    return node.FirstAncestorOrSelf<PatternSyntax>() != null;
}
```

**IMPORTANT:** The `FirstAncestorOrSelf<PatternSyntax>()` check handles the case where relational operators (`> 5`, `< 10`) appear inside property patterns, relational patterns, and logical patterns. These are represented as `ConstantPatternSyntax`, `RelationalPatternSyntax`, etc. — all derive from `PatternSyntax`. A binary expression node like `GreaterThanExpression` inside `{ Value: > 5 }` will have a `RelationalPatternSyntax` ancestor.

**CRITICAL NUANCE:** The binary expression `> 5` inside `obj is { Value: > 5 }` is NOT actually a `BinaryExpressionSyntax` — Roslyn parses the `> 5` inside patterns as a `RelationalPatternSyntax`, not a `GreaterThanExpression`. So the existing `AnalyzeBinaryExpression` handler may not fire on it at all.

Before implementing, verify this by examining what Roslyn actually produces. If the `> 5` inside a property pattern is NOT a `BinaryExpressionSyntax` (likely), then `IsNestedInPatternExpression` may not be needed at all for the binary handler. In that case:

- If tests #5 and #6 from Task 1 already PASS without changes (because Roslyn does not produce BinaryExpressionSyntax inside patterns), then this helper is not strictly needed.
- If they fail, add the helper.

**Either way**, add the `IsNestedInPatternExpression` guard to `AnalyzeBinaryExpression` as a defensive measure for future-proofing:

```csharp
private static void AnalyzeBinaryExpression(SyntaxNodeAnalysisContext context)
{
    var binaryExpression = (BinaryExpressionSyntax)context.Node;

    if (IsNestedInBinaryExpression(binaryExpression)) return;
    if (IsNestedInPatternExpression(binaryExpression)) return;  // NEW: bidirectional suppression
    if (IsInsideSpecBuildLambda(binaryExpression, context.SemanticModel)) return;

    var diagnostic = Diagnostic.Create(Motiv0001, binaryExpression.GetLocation());
    context.ReportDiagnostic(diagnostic);
}
```

Also add `IsNestedInPatternExpression` guard to `AnalyzeIsPatternExpression` for completeness (nested is-in-is patterns, though rare):

```csharp
private static void AnalyzeIsPatternExpression(SyntaxNodeAnalysisContext context)
{
    var isPatternExpression = (IsPatternExpressionSyntax)context.Node;

    if (IsNestedInBinaryExpression(isPatternExpression)) return;
    if (IsNestedInPatternExpression(isPatternExpression)) return;  // Defensive
    if (IsInsideSpecBuildLambda(isPatternExpression, context.SemanticModel)) return;

    var diagnostic = Diagnostic.Create(Motiv0001, isPatternExpression.GetLocation());
    context.ReportDiagnostic(diagnostic);
}
```

Run the full test suite. All tests must pass.

**If any tests fail**, analyze the Roslyn syntax tree for the failing case. Adjust `IsNestedInPatternExpression` logic or test expectations based on what Roslyn actually produces. The goal is: exactly one diagnostic per root boolean expression, no duplicates, no false positives inside Spec.Build.
  </action>
  <verify>
Run: `dotnet test src/Motiv.Analyzer.Tests/`

ALL tests must PASS — the full suite including:
- 4 original tests
- 7 Plan 01 tests (is detection)
- 8 Plan 02 tests (nesting suppression + Spec.Build pattern suppression)

Total: 19 tests, 0 failures.
  </verify>
  <done>Bidirectional nesting suppression works. Binary expressions inside patterns do not produce duplicate diagnostics. Pattern expressions inside binary expressions do not produce duplicate diagnostics. Pattern expressions inside Spec.Build produce no diagnostics. All 19 tests pass. ANLZ-03 and ANLZ-04 requirements satisfied.</done>
</task>

</tasks>

<verification>
1. `dotnet test src/Motiv.Analyzer.Tests/` — all 19 tests pass (0 failures)
2. `dotnet build src/Motiv.Analyzer/` — builds without errors or warnings
3. Manual review: `MotivAnalyzer.cs` has both `AnalyzeBinaryExpression` and `AnalyzeIsPatternExpression` handlers, both with `IsNestedInBinaryExpression`, `IsNestedInPatternExpression`, and `IsInsideSpecBuildLambda` guards
</verification>

<success_criteria>
- `x > 5 && obj is string` produces exactly 1 diagnostic (on the root `&&`)
- `obj is { Length: > 0 }` produces exactly 1 diagnostic (on the root `is`)
- `obj is { Value: > 5 and < 10 }` produces exactly 1 diagnostic (on the root `is`)
- `Spec.Build(x => x is string)` produces 0 diagnostics
- `Spec.Build(x => x is { Length: > 0 })` produces 0 diagnostics
- All 19 tests pass with 0 failures
- No regressions in existing behavior
</success_criteria>

<output>
After completion, create `.planning/phases/01-analyzer-expansion/01-02-SUMMARY.md`
</output>
