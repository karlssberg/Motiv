# Phase 1: Analyzer Expansion - Research

**Researched:** 2026-02-06
**Domain:** Roslyn Analyzers for C# Pattern Matching
**Confidence:** HIGH

## Summary

C# pattern matching using the `is` keyword is represented in Roslyn's syntax tree as `IsPatternExpressionSyntax` with `SyntaxKind.IsPatternExpression`. The analyzer needs to register this additional SyntaxKind and handle the nesting interaction with existing binary expression handlers.

Pattern expressions have **relational precedence** (same as `>`, `<`, `>=`, `<=`) which is **higher than logical operators** (`&&`, `||`). This means `x > 5 && obj is string` parses as `(x > 5) && (obj is string)`, where the `&&` is the root binary expression and both children are potential diagnostic sites. The existing binary handler reports the root; the new pattern handler must suppress when nested.

The critical implementation challenge is **bidirectional nesting suppression**: pattern expressions can be nested inside binary expressions (`x > 5 && obj is string`), and binary expressions can be nested inside pattern expressions via constant patterns and property patterns (`obj is { Value: > 5 }`).

**Primary recommendation:** Register `SyntaxKind.IsPatternExpression` with a new handler that shares the existing nesting suppression logic (`IsNestedInBinaryExpression`) and adds reciprocal check for `IsNestedInPatternExpression` to suppress both pattern-in-binary and binary-in-pattern scenarios.

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.CodeAnalysis.CSharp | netstandard2.0 | Roslyn compiler API | Official C# compiler platform, required for analyzers |
| Microsoft.CodeAnalysis.Analyzers | Latest | Analyzer development guidelines | Required for building Roslyn analyzers |
| Microsoft.CodeAnalysis.CSharp.Testing | Latest | Analyzer testing framework | Official testing framework for analyzers |
| Microsoft.CodeAnalysis.Testing | Latest | Testing infrastructure | Core testing abstractions |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| xUnit | Latest | Test framework | Already used in Motiv.Analyzer.Tests |
| Microsoft.NET.Test.Sdk | Latest | Test host | Required to run xUnit tests |

**Installation:**
Already installed in the existing Motiv.Analyzer and Motiv.Analyzer.Tests projects.

## Architecture Patterns

### Current Analyzer Structure (Existing Pattern)
```
src/Motiv.Analyzer/
├── MotivAnalyzer.cs              # DiagnosticAnalyzer implementation
└── Motiv.Analyzer.csproj

src/Motiv.Analyzer.Tests/
├── FindBooleanExpressionsTests.cs    # Test suite
├── CSharpDiagnosticAnalyzerVerifier.cs  # Test infrastructure
└── Motiv.Analyzer.Tests.csproj
```

### Pattern 1: Multiple SyntaxKind Registration with Shared Handler
**What:** Register multiple related SyntaxKind values that should produce the same diagnostic.
**When to use:** When different syntax constructs represent the same semantic problem.
**Current implementation:**
```csharp
// Source: Existing MotivAnalyzer.cs
public override void Initialize(AnalysisContext context)
{
    context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
    context.EnableConcurrentExecution();

    // Multiple binary expression kinds share one handler
    context.RegisterSyntaxNodeAction(AnalyzeBinaryExpression, SyntaxKind.GreaterThanExpression);
    context.RegisterSyntaxNodeAction(AnalyzeBinaryExpression, SyntaxKind.LessThanExpression);
    context.RegisterSyntaxNodeAction(AnalyzeBinaryExpression, SyntaxKind.GreaterThanOrEqualExpression);
    context.RegisterSyntaxNodeAction(AnalyzeBinaryExpression, SyntaxKind.LessThanOrEqualExpression);
    context.RegisterSyntaxNodeAction(AnalyzeBinaryExpression, SyntaxKind.EqualsExpression);
    context.RegisterSyntaxNodeAction(AnalyzeBinaryExpression, SyntaxKind.NotEqualsExpression);
    context.RegisterSyntaxNodeAction(AnalyzeBinaryExpression, SyntaxKind.LogicalAndExpression);
    context.RegisterSyntaxNodeAction(AnalyzeBinaryExpression, SyntaxKind.LogicalOrExpression);
}
```

### Pattern 2: Nesting Suppression to Avoid Duplicate Diagnostics
**What:** Walk up the syntax tree to check if the current node is nested inside a related parent node.
**When to use:** When multiple SyntaxKind registrations could trigger on the same logical expression.
**Current implementation:**
```csharp
// Source: Existing MotivAnalyzer.cs
private static bool IsNestedInBinaryExpression(SyntaxNode node)
{
    // Walk up through parenthesized expressions to find if we're inside a binary expression
    var parent = node.Parent;
    while (parent is ParenthesizedExpressionSyntax or PrefixUnaryExpressionSyntax)
    {
        parent = parent.Parent;
    }

    return parent is BinaryExpressionSyntax;
}
```

### Pattern 3: Lambda Context Detection for False Positive Suppression
**What:** Use semantic model to detect if expression is inside specific method call (Spec.Build).
**When to use:** When analyzer should ignore expressions in specific API usage contexts.
**Current implementation:**
```csharp
// Source: Existing MotivAnalyzer.cs
private static bool IsInsideSpecBuildLambda(SyntaxNode node, SemanticModel semanticModel)
{
    var lambda = node.FirstAncestorOrSelf<LambdaExpressionSyntax>();
    var invocation = lambda?.FirstAncestorOrSelf<InvocationExpressionSyntax>();

    if (invocation?.Expression is
        not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Build" } memberAccess)
        return false;

    var symbolInfo = semanticModel.GetSymbolInfo(memberAccess.Expression);
    if (symbolInfo.Symbol is INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.ContainingNamespace.Name == "Motiv" &&
               typeSymbol.Name == "Spec";
    }

    return false;
}
```

### Pattern 4: Analyzer Testing with Diagnostic Location Verification
**What:** Use Microsoft.CodeAnalysis.Testing framework with expected diagnostic spans.
**When to use:** For all analyzer tests to verify diagnostic location accuracy.
**Current implementation:**
```csharp
// Source: Existing FindBooleanExpressionsTests.cs
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
```

### New Pattern Required: IsPatternExpression Handler
**What:** New handler for `SyntaxKind.IsPatternExpression` that casts to `IsPatternExpressionSyntax`.
**When to use:** To detect `is` type-check and pattern-matching expressions.
**Recommended implementation:**
```csharp
// New handler to add
context.RegisterSyntaxNodeAction(AnalyzeIsPatternExpression, SyntaxKind.IsPatternExpression);

private static void AnalyzeIsPatternExpression(SyntaxNodeAnalysisContext context)
{
    var isPatternExpression = (IsPatternExpressionSyntax)context.Node;

    // Suppress if nested inside a binary expression that will report the root
    if (IsNestedInBinaryExpression(isPatternExpression)) return;

    // Suppress if inside Spec.Build() lambda
    if (IsInsideSpecBuildLambda(isPatternExpression, context.SemanticModel)) return;

    // Report diagnostic for the is pattern expression
    var diagnostic = Diagnostic.Create(Motiv0001, isPatternExpression.GetLocation());
    context.ReportDiagnostic(diagnostic);
}
```

### Anti-Patterns to Avoid
- **Creating separate handlers with duplicate logic:** Pattern and binary handlers should share nesting suppression logic.
- **Not checking both directions:** Must suppress pattern-in-binary AND binary-in-pattern.
- **Casting without type check:** RegisterSyntaxNodeAction guarantees the cast is safe; direct cast is idiomatic.
- **Over-relying on semantic model:** Use syntax-only checks when possible for performance (semantic model is expensive).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Detecting all pattern types | Custom PatternSyntax walker | IsPatternExpressionSyntax (base expression type) | All pattern forms use IsPatternExpression syntax kind; pattern details are in Pattern property |
| Testing analyzer diagnostics | Custom test framework | Microsoft.CodeAnalysis.Testing | Official framework with diagnostic location verification, reference assembly handling |
| Syntax tree traversal | Manual parent walking | SyntaxNode.FirstAncestorOrSelf<T>() | Roslyn provides optimized traversal methods |
| Pattern type detection | Inspecting pattern string representation | Pattern property typed as PatternSyntax with derived types | Syntax tree provides strongly-typed pattern nodes |

**Key insight:** Roslyn's syntax tree is strongly typed. Use pattern matching on syntax node types rather than string inspection or custom traversal.

## Common Pitfalls

### Pitfall 1: Missing Bidirectional Nesting Suppression
**What goes wrong:** Binary expressions inside pattern property patterns (`obj is { Value: > 5 }`) trigger duplicate diagnostics.
**Why it happens:** Pattern expressions can contain binary expressions via property patterns, positional patterns, and list patterns. The relational expression `> 5` in a property pattern is a `BinaryExpressionSyntax` (kind: `GreaterThanExpression`).
**How to avoid:**
- Existing `IsNestedInBinaryExpression` already handles pattern-in-binary suppression (pattern is nested in binary root)
- Need NEW `IsNestedInPatternExpression` to handle binary-in-pattern suppression (binary is nested in pattern)
- BOTH handlers (binary and pattern) should check BOTH directions
**Warning signs:**
- Test for `obj is { Value: > 5 }` reports TWO diagnostics (one for `is`, one for `> 5`)
- Test for `x > 5 && obj is string` reports THREE diagnostics (root `&&`, left `> 5`, right `is string`)

### Pitfall 2: Forgetting "is not" Patterns
**What goes wrong:** Assuming "is not" is a different syntax kind or requires special handling.
**Why it happens:** The `not` keyword appears in the source, leading to assumption it's a different expression type.
**How to avoid:** `is not` is represented as `IsPatternExpressionSyntax` where the `Pattern` property is a `UnaryPatternSyntax` (with operator kind: `NotKeyword`). The expression-level SyntaxKind is still `IsPatternExpression`. No special handling needed.
**Warning signs:** Tests for `obj is not string` fail to trigger the diagnostic.

### Pitfall 3: Missing Logical Pattern Combinators
**What goes wrong:** Patterns like `obj is > 5 and < 10` or `value is 1 or 2 or 3` don't trigger.
**Why it happens:** Assuming pattern `and`/`or`/`not` are different from expression-level `is`.
**How to avoid:** These are still `IsPatternExpressionSyntax` where the `Pattern` property is a `BinaryPatternSyntax` (for `and`/`or`) or `UnaryPatternSyntax` (for `not`). The expression-level SyntaxKind is `IsPatternExpression`. All pattern variations use the same root syntax kind.
**Warning signs:** Tests for relational patterns (`is > 5`) work but logical combinators (`is > 5 and < 10`) don't.

### Pitfall 4: Not Suppressing in Spec.Build() Lambda
**What goes wrong:** Pattern expressions inside `Spec.Build()` lambdas trigger diagnostics.
**Why it happens:** Forgetting to apply the existing `IsInsideSpecBuildLambda` check to the new pattern handler.
**How to avoid:** New `AnalyzeIsPatternExpression` handler must call `IsInsideSpecBuildLambda` exactly like the binary handler does.
**Warning signs:** Test for `Spec.Build(x => x is string)` reports a diagnostic when it should be suppressed.

### Pitfall 5: Operator Precedence Confusion
**What goes wrong:** Misunderstanding which node is the parent in `x > 5 && obj is string`.
**Why it happens:** Assuming `is` has lower precedence than `&&`.
**How to avoid:** Remember precedence order: Relational (`is`, `>`, `<`) BEFORE Equality (`==`, `!=`) BEFORE Logical (`&&`, `||`). In `x > 5 && obj is string`, the `&&` is the root, both comparisons are children.
**Warning signs:** Nesting suppression logic fails for mixed pattern/binary expressions.

## Code Examples

Verified patterns from Roslyn documentation and API:

### Accessing IsPatternExpression Components
```csharp
// Source: Microsoft Learn - IsPatternExpressionSyntax API documentation
// https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntax.ispatternexpressionsyntax

IsPatternExpressionSyntax isPatternExpr = (IsPatternExpressionSyntax)context.Node;

// Get the expression being tested (left side of 'is')
ExpressionSyntax testedExpression = isPatternExpr.Expression;

// Get the pattern being matched (right side of 'is')
PatternSyntax pattern = isPatternExpr.Pattern;

// Get the "is" keyword token
SyntaxToken isKeyword = isPatternExpr.IsKeyword;
```

### Pattern Type Detection (if needed for future expansion)
```csharp
// Source: Microsoft Learn - PatternSyntax derived types
// https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntax.patternsyntax

PatternSyntax pattern = isPatternExpr.Pattern;

// Pattern types (for reference, not all needed now):
switch (pattern)
{
    case TypePatternSyntax typePattern:
        // obj is string
        break;
    case UnaryPatternSyntax { OperatorToken.Kind: SyntaxKind.NotKeyword } notPattern:
        // obj is not string
        break;
    case BinaryPatternSyntax { OperatorToken.Kind: SyntaxKind.AndKeyword } andPattern:
        // value is > 5 and < 10
        break;
    case BinaryPatternSyntax { OperatorToken.Kind: SyntaxKind.OrKeyword } orPattern:
        // value is 1 or 2 or 3
        break;
    case RecursivePatternSyntax recursivePattern:
        // obj is { Length: > 0 }  (property pattern)
        // point is (0, 0)  (positional pattern)
        break;
    case RelationalPatternSyntax relationalPattern:
        // value is > 5
        break;
    case ConstantPatternSyntax constantPattern:
        // value is 42
        break;
    case ListPatternSyntax listPattern:
        // array is [1, 2, 3]
        break;
    case VarPatternSyntax varPattern:
        // obj is var x
        break;
    case DiscardPatternSyntax discardPattern:
        // obj is _
        break;
}
```

### Checking for Nested Pattern Context (needed for binary handler)
```csharp
// New helper to add for bidirectional suppression
private static bool IsNestedInPatternExpression(SyntaxNode node)
{
    // Walk up through parenthesized/unary expressions
    var parent = node.Parent;
    while (parent is ParenthesizedExpressionSyntax or PrefixUnaryExpressionSyntax)
    {
        parent = parent.Parent;
    }

    // Check if we're inside a pattern expression
    // This covers cases like: obj is { Value: > 5 }
    // where "> 5" is a BinaryExpressionSyntax nested in RecursivePatternSyntax
    if (parent is IsPatternExpressionSyntax)
        return true;

    // Also check if we're inside a pattern itself (for recursive patterns)
    var patternAncestor = node.FirstAncestorOrSelf<PatternSyntax>();
    return patternAncestor != null;
}
```

### Test Pattern for IsPatternExpression
```csharp
// Based on existing test patterns in FindBooleanExpressionsTests.cs
[Fact]
public async Task Should_analyze_and_identify_is_type_check_expressions()
{
    const string booleanExpression = "obj is string";
    const string code =
        $$"""
          namespace MyNamespace;

          public class MyClass
          {
              public bool IsString(object obj)
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

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Pattern matching via `as` + null check | `is` with pattern matching | C# 7.0 (2017) | `is` patterns are the modern way; must support in analyzer |
| Simple type patterns only | Full pattern ecosystem (property, positional, relational, logical, list) | C# 7.0-11.0 (2017-2022) | Analyzer must handle all pattern forms, not just simple type checks |
| Separate `is` (type) and switch (matching) | Unified pattern syntax | C# 8.0 (2019) | IsPatternExpressionSyntax covers all `is` pattern forms |
| No `not` pattern | `is not` pattern | C# 9.0 (2020) | `is not` is UnaryPatternSyntax, not a different expression kind |

**Deprecated/outdated:**
- **`as` casting pattern:** `var s = obj as string; if (s != null)` → Use `if (obj is string s)` instead. Analyzer should detect the modern `is` pattern form.

## Pattern Matching Types Reference

All pattern types from C# 7.0-11.0 (for completeness):

| Pattern Type | Syntax Example | SyntaxNode Type | Notes |
|-------------|----------------|-----------------|-------|
| Type pattern | `obj is string` | TypePatternSyntax | Simple type check |
| Declaration pattern | `obj is string s` | DeclarationPatternSyntax | Type check + variable binding |
| Constant pattern | `value is 42` | ConstantPatternSyntax | Value equality |
| Relational pattern | `value is > 5` | RelationalPatternSyntax | Comparison operators in pattern |
| Logical `not` | `obj is not null` | UnaryPatternSyntax | Negation |
| Logical `and` | `value is > 5 and < 10` | BinaryPatternSyntax | Conjunction |
| Logical `or` | `value is 1 or 2 or 3` | BinaryPatternSyntax | Disjunction |
| Property pattern | `obj is { Length: > 0 }` | RecursivePatternSyntax | Nested property matching |
| Positional pattern | `point is (0, 0)` | RecursivePatternSyntax | Tuple/deconstruct matching |
| List pattern | `array is [1, 2, 3]` | ListPatternSyntax | Array/list matching |
| Var pattern | `obj is var x` | VarPatternSyntax | Always matches, assigns variable |
| Discard pattern | `obj is _` | DiscardPatternSyntax | Always matches, no variable |

**All of these use `SyntaxKind.IsPatternExpression` at the expression level.** The pattern details are in the `Pattern` property.

## Operator Precedence (Critical for Nesting Logic)

From C# language specification:

| Precedence | Operators | Category |
|-----------|-----------|----------|
| 9 | `>`, `<`, `>=`, `<=`, `is`, `as` | **Relational** |
| 8 | `==`, `!=` | **Equality** |
| 7 | `&` | Logical/bitwise AND |
| 6 | `^` | Logical/bitwise XOR |
| 5 | `\|` | Logical/bitwise OR |
| 4 | `&&` | **Conditional AND** |
| 3 | `\|\|` | **Conditional OR** |

**Key insight:** `is` has **higher precedence** than `&&` and `||`. In `x > 5 && obj is string`:
- Parse tree: `(x > 5) && (obj is string)`
- Root node: `BinaryExpressionSyntax` (kind: LogicalAndExpression)
- Left child: `BinaryExpressionSyntax` (kind: GreaterThanExpression)
- Right child: `IsPatternExpressionSyntax` (kind: IsPatternExpression)

Both children are nested; root reports diagnostic.

## Test Scenarios to Cover

Based on requirements and edge case analysis:

### ANLZ-01: Type Check Detection
1. ✅ Simple type check: `obj is string`
2. ✅ Type check with declaration: `obj is string s`
3. ✅ Type check in return: `return obj is string;`
4. ✅ Type check in assignment: `var result = obj is string;`
5. ✅ Type check in local declaration: `bool isString = obj is string;`
6. ✅ Negated type check: `obj is not string`

### ANLZ-02: Pattern Matching Detection
1. ✅ Property pattern: `obj is { Length: > 0 }`
2. ✅ Nested property pattern: `obj is { Value.Length: > 0 }`
3. ✅ Positional pattern: `point is (0, 0)`
4. ✅ Relational pattern: `value is > 5`
5. ✅ Logical and pattern: `value is > 5 and < 10`
6. ✅ Logical or pattern: `value is 1 or 2 or 3`
7. ✅ List pattern: `array is [1, 2, 3]`
8. ✅ Complex pattern: `obj is { Value: > 5 and < 10 }`

### ANLZ-03: Nesting Suppression (Pattern in Binary)
1. ✅ Pattern right of AND: `x > 5 && obj is string` → diagnostic on root `&&` only
2. ✅ Pattern left of AND: `obj is string && x > 5` → diagnostic on root `&&` only
3. ✅ Pattern right of OR: `x > 5 || obj is string` → diagnostic on root `||` only
4. ✅ Pattern in complex binary: `x > 5 && y < 10 && obj is string` → diagnostic on root only
5. ✅ Parenthesized pattern in binary: `x > 5 && (obj is string)` → diagnostic on root only

### ANLZ-04: Spec.Build Lambda Suppression
1. ✅ Pattern in Spec.Build: `Spec.Build(x => x is string)` → no diagnostic
2. ✅ Pattern in nested lambda: `Spec.Build(x => { Func<bool> f = () => x is string; return f(); })` → no diagnostic
3. ✅ Pattern in Spec.Build property pattern: `Spec.Build(x => x is { Length: > 0 })` → no diagnostic

### TEST-01/02: Edge Cases
1. ✅ Standalone pattern (not nested): `return obj is string;` → diagnostic
2. ✅ Binary nested in pattern property: `obj is { Value: > 5 }` → diagnostic on root `is` only
3. ✅ Multiple levels of nesting: `x > 5 && (y < 10 || obj is string)` → diagnostic on root `&&` only
4. ✅ Pattern in ternary: `obj is string ? true : false` → diagnostic on `is`
5. ✅ Pattern in if condition: `if (obj is string) { }` → diagnostic on `is`
6. ✅ Pattern in while condition: `while (obj is string) { }` → diagnostic on `is`

## Open Questions

No significant open questions. The implementation path is clear:

1. ✅ **Roslyn API:** `SyntaxKind.IsPatternExpression` and `IsPatternExpressionSyntax` are well-documented
2. ✅ **Pattern types:** All C# pattern forms use the same expression-level syntax kind
3. ✅ **Nesting:** Precedence rules are clear; bidirectional suppression needed
4. ✅ **Testing:** Existing test infrastructure supports pattern expression tests

**Minor clarification needed during implementation:** Exact syntax tree shape for property patterns with nested binary expressions (e.g., `obj is { Value: > 5 }`). This can be validated with a quick Roslyn syntax tree dump during implementation.

**Recommendation:** Write a test for `obj is { Value: > 5 }` FIRST to confirm the nesting structure before implementing bidirectional suppression.

## Sources

### Primary (HIGH confidence)
- [Microsoft Learn - IsPatternExpressionSyntax API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntax.ispatternexpressionsyntax) - Class structure and properties
- [Microsoft Learn - C# Patterns Reference](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/patterns) - All pattern types with examples
- [Microsoft Learn - C# Operators Precedence](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/) - Operator precedence table
- [Microsoft Learn - PatternSyntax Derived Types](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntax.patternsyntax) - All pattern syntax node types
- [GitHub - Roslyn SyntaxKind.cs](https://github.com/dotnet/roslyn/blob/main/src/Compilers/CSharp/Portable/Syntax/SyntaxKind.cs) - Official SyntaxKind definitions

### Secondary (MEDIUM confidence)
- [GitHub - Roslyn Pattern Matching Documentation](https://github.com/dotnet/roslyn/blob/main/docs/features/patterns.md) - Pattern matching feature specification
- [C# Pattern Matching Explained (2026) - NDepend Blog](https://blog.ndepend.com/c-pattern-matching-explained/) - Comprehensive pattern matching overview
- [Productive Rage - Creating a Roslyn Analyzer](https://www.productiverage.com/creating-a-c-sharp-roslyn-analyser-for-beginners-by-a-beginner) - Analyzer best practices
- [Syncfusion - Writing Code Analyzers](https://www.syncfusion.com/succinctly-free-ebooks/roslyn/writing-code-analyzers) - Analyzer development patterns

### Tertiary (LOW confidence - community insights)
- [StackOverflow/blogs - Various analyzer examples](https://github.com/DotNetAnalyzers/StyleCopAnalyzers/issues/285) - Real-world RegisterSyntaxNodeAction patterns

## Metadata

**Confidence breakdown:**
- Roslyn API (IsPatternExpressionSyntax): **HIGH** - Official Microsoft documentation
- Pattern types and examples: **HIGH** - Official C# language reference
- Operator precedence: **HIGH** - Official C# specification
- Nesting interaction: **HIGH** - Verified through precedence rules and syntax tree structure
- Analyzer patterns: **HIGH** - Existing codebase + official Roslyn guidance
- Test scenarios: **HIGH** - Derived from requirements + known edge cases

**Research date:** 2026-02-06
**Valid until:** Stable - Roslyn APIs and C# pattern matching are mature features (C# 7.0-11.0, 2017-2022). Syntax tree structure won't change.
