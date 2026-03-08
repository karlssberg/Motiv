# Phase 7: SpecInvocation Migration - Research

**Researched:** 2026-02-09
**Domain:** Roslyn SyntaxFactory API for invocation expression construction
**Confidence:** HIGH

## Summary

This phase migrates `SpecInvocationExpressionSyntax.Create()` from string interpolation + `ParseExpression()` to pure SyntaxFactory API construction. The target output is a chained member access and invocation expression: `new SpecName().Evaluate(arg).Satisfied`.

The migration is the simplest of the six-phase SyntaxFactory refactor. It serves as the foundation for establishing trivia handling patterns (specifically, the CRLF constant) that will be reused in Phases 8-12. The existing test suite (10 tests in `MotivConvertToSpecTests.cs`) acts as the verification gate—all tests must pass with identical output.

**Primary recommendation:** Use SyntaxFactory to build the expression from leaf to root (IdentifierName → ObjectCreationExpression → MemberAccessExpression → InvocationExpression → MemberAccessExpression), call `NormalizeWhitespace()` at the end, and establish a `CarriageReturnLineFeed` constant in `SyntaxFactory` static usings for consistent trivia handling across all phases.

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.CodeAnalysis.CSharp | 4.x+ | Roslyn SyntaxFactory API | Official C# code generation API from Roslyn team |
| using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory | N/A | Static imports for SyntaxFactory methods | Community standard pattern to reduce verbosity |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Microsoft.CodeAnalysis.CSharp.Syntax | 4.x+ | Syntax node types (ExpressionSyntax, etc.) | Already in use; no changes |
| Roslyn Quoter | Online tool | Generate SyntaxFactory code from C# snippets | During development to verify complex patterns |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| ParseExpression | SyntaxFactory | ParseExpression loses contextual information, less type-safe |
| SyntaxGenerator | SyntaxFactory | SyntaxGenerator doesn't support C#-specific constructs (records, primary constructors) |

**Installation:**
Already installed (existing dependency). No additional packages needed.

## Architecture Patterns

### Current Implementation (Phase 7 Target)
```
SpecInvocationExpressionSyntax.cs
├── Create(IdentifierNameSyntax, ObjectCreationExpressionSyntax)
├── Create(GenericNameSyntax, IdentifierNameSyntax)
├── Create(IdentifierNameSyntax, IdentifierNameSyntax)
└── CreateInternal(string specName, string modelObjectCreation)
    └── Uses: ParseExpression($$"""new {{specName}}().Evaluate({{modelObjectCreation}}).Satisfied""")
```

**Goal:** Replace `CreateInternal` with SyntaxFactory construction, remove `ParseExpression`.

### Recommended Pattern: Leaf-to-Root Construction

Build the expression tree bottom-up, as demonstrated by the existing `PropositionModelSyntax.cs` (reference implementation):

```csharp
// Target expression: new SpecName().Evaluate(arg).Satisfied

// Step 1: Create object creation (leaf)
var newSpec = ObjectCreationExpression(
    IdentifierName(specName))
    .WithArgumentList(ArgumentList());

// Step 2: Create .Evaluate(arg) member access + invocation
var isSatisfiedByAccess = MemberAccessExpression(
    SyntaxKind.SimpleMemberAccessExpression,
    newSpec,
    IdentifierName("Evaluate"));

var isSatisfiedByInvocation = InvocationExpression(
    isSatisfiedByAccess,
    ArgumentList(
        SingletonSeparatedList(
            Argument(modelArgument))));  // modelArgument is passed in

// Step 3: Create .Satisfied property access
var satisfiedAccess = MemberAccessExpression(
    SyntaxKind.SimpleMemberAccessExpression,
    isSatisfiedByInvocation,
    IdentifierName("Satisfied"));

// Step 4: Normalize whitespace
return satisfiedAccess.NormalizeWhitespace();
```

### Pattern 1: Static Using for SyntaxFactory
**What:** Import SyntaxFactory methods as static to reduce verbosity
**When to use:** All files that construct syntax nodes
**Example:**
```csharp
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

// Instead of:
var node = SyntaxFactory.IdentifierName("Test");

// Use:
var node = IdentifierName("Test");
```

**Current status:** `ConvertToSpecCodeFix.cs` and `PropositionModelSyntax.cs` already use this pattern.

### Pattern 2: ArgumentList Construction
**What:** Create argument lists using `ArgumentList()` + `SeparatedList` or `SingletonSeparatedList`
**When to use:** Any invocation expression with arguments
**Example:**
```csharp
// Source: https://johnkoerner.com/csharp/creating-code-using-the-syntax-factory/
// Single argument
var argumentList = ArgumentList(
    SingletonSeparatedList(
        Argument(IdentifierName("value"))));

// Multiple arguments
var argumentList = ArgumentList(
    SeparatedList<ArgumentSyntax>(
        new[] {
            Argument(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(1))),
            Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal("abc")))
        }));
```

### Pattern 3: Trivia Handling Order
**What:** Call `NormalizeWhitespace()` BEFORE adding custom leading/trailing trivia
**When to use:** Any syntax node that needs both normalized whitespace and custom trivia
**Example:**
```csharp
// Source: https://criticalhittech.com/2019/03/19/getting-whitespace-right-with-roslyn-csharpsyntaxrewriter/
// CORRECT:
var node = SomeExpression()
    .NormalizeWhitespace()
    .WithLeadingTrivia(CarriageReturnLineFeed);

// INCORRECT (trivia will be overridden):
var node = SomeExpression()
    .WithLeadingTrivia(CarriageReturnLineFeed)
    .NormalizeWhitespace();
```

### Pattern 4: CRLF Trivia Constant
**What:** Establish a constant for `CarriageReturnLineFeed` trivia for consistent line ending handling
**When to use:** Phase 7 and all subsequent phases (8-12)
**Example:**
```csharp
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

// Already used in ConvertToSpecCodeFix.cs:
member.WithLeadingTrivia(CarriageReturnLineFeed, CarriageReturnLineFeed)
```

**Current status:** `CarriageReturnLineFeed` is already imported via `using static SyntaxFactory` in `ConvertToSpecCodeFix.cs` (lines 423, 460, 472). This pattern should be maintained in `SpecInvocationSyntax.cs`.

### Anti-Patterns to Avoid
- **ParseExpression for simple expressions:** Loses contextual information, less type-safe. Use explicit SyntaxFactory construction.
- **String interpolation for source code:** Current anti-pattern being eliminated. Breaks IDE integration and loses semantic information.
- **Calling NormalizeWhitespace() after WithTrivia:** Overwrites custom trivia. Always normalize first, then add trivia.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Chained member access expressions | String concatenation with dots | Nested `MemberAccessExpression` calls | Type-safe, preserves semantic information, IDE-friendly |
| Method invocation with arguments | String interpolation with parentheses | `InvocationExpression` + `ArgumentList` | Handles escaping, type information, refactoring support |
| Generic name construction | String with angle brackets | `GenericName` + `TypeArgumentList` | Proper syntax tree structure for tooling |
| Whitespace formatting | Manual `\r\n` insertion | `NormalizeWhitespace()` method | Consistent with Roslyn formatting rules |

**Key insight:** Roslyn's SyntaxFactory handles all syntactic edge cases (escaping, trivia, generic arity, etc.). Hand-rolling string concatenation duplicates this logic incorrectly.

## Common Pitfalls

### Pitfall 1: Incorrect Trivia Ordering
**What goes wrong:** Calling `NormalizeWhitespace()` after adding custom trivia erases the custom trivia
**Why it happens:** `NormalizeWhitespace()` replaces ALL trivia with standardized formatting
**How to avoid:** Always call `NormalizeWhitespace()` first, THEN add custom trivia via `WithLeadingTrivia` or `WithTrailingTrivia`
**Warning signs:** Custom line breaks or spacing disappearing from generated code

### Pitfall 2: ToString() Before Construction Complete
**What goes wrong:** Converting syntax nodes to strings mid-construction and parsing them back
**Why it happens:** Mixing SyntaxFactory patterns with string-based patterns during migration
**How to avoid:** Keep ExpressionSyntax nodes as nodes throughout construction; only call `ToString()` for final output or debugging
**Warning signs:** Multiple `ParseExpression` calls chained together

### Pitfall 3: Empty ArgumentList Missing Parentheses
**What goes wrong:** Forgetting to call `ArgumentList()` for parameterless constructors/methods
**Why it happens:** Assuming empty arguments mean no ArgumentList node needed
**How to avoid:** Always include `ArgumentList()` even if empty: `ObjectCreationExpression(type).WithArgumentList(ArgumentList())`
**Warning signs:** Generated code missing parentheses like `new SpecName.Evaluate(arg)` instead of `new SpecName().Evaluate(arg)`

### Pitfall 4: Generic Name Handling
**What goes wrong:** Generic type arguments lost when using `IdentifierName` instead of `GenericName`
**Why it happens:** Not checking if the specName contains generic type parameters
**How to avoid:** Accept `GenericNameSyntax` overloads (already present in current implementation); preserve generic information
**Warning signs:** Test failures where generic specs lose their type arguments

## Code Examples

Verified patterns from official sources:

### Chained Member Access and Invocation
```csharp
// Source: https://johnkoerner.com/csharp/creating-code-using-the-syntax-factory/
// Pattern: object.Method(arg).Property

var objectExpr = IdentifierName("Console");
var methodName = IdentifierName("WriteLine");

var methodAccess = MemberAccessExpression(
    SyntaxKind.SimpleMemberAccessExpression,
    objectExpr,
    methodName);

var argument = Argument(
    LiteralExpression(
        SyntaxKind.StringLiteralExpression,
        Literal("A")));

var invocation = InvocationExpression(
    methodAccess,
    ArgumentList(SingletonSeparatedList(argument)));

var propertyAccess = MemberAccessExpression(
    SyntaxKind.SimpleMemberAccessExpression,
    invocation,
    IdentifierName("Property"));
```

### Object Creation with Empty Constructor
```csharp
// Source: Roslyn SyntaxFactory documentation
// Pattern: new TypeName()

var newObject = ObjectCreationExpression(
    IdentifierName("TypeName"))
    .WithArgumentList(ArgumentList());
```

### Using Roslyn Quoter for Verification
```csharp
// Source: https://roslynquoter.azurewebsites.net/
// Input: new SpecName().Evaluate(value).Satisfied
// Output (expected SyntaxFactory code):

MemberAccessExpression(
    SyntaxKind.SimpleMemberAccessExpression,
    InvocationExpression(
        MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            ObjectCreationExpression(
                IdentifierName("SpecName"))
            .WithArgumentList(ArgumentList()),
            IdentifierName("Evaluate")))
    .WithArgumentList(
        ArgumentList(
            SingletonSeparatedList<ArgumentSyntax>(
                Argument(
                    IdentifierName("value"))))),
    IdentifierName("Satisfied"))
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| String concatenation + parsing | Direct SyntaxFactory construction | Roslyn 1.0 (2015) | Type safety, IDE integration, refactoring support |
| Manual whitespace handling | `NormalizeWhitespace()` | Roslyn 1.0 (2015) | Consistent formatting without manual trivia |
| Global trivia constants | Per-node trivia methods | Roslyn 2.0 (2017) | More flexible, but constants still useful for consistency |

**Deprecated/outdated:**
- `ParseExpression` for simple expressions: Use SyntaxFactory factory methods instead for better type safety and contextual information
- `SyntaxFactory.Identifier` without `contextualKind`: Use overloads with `SyntaxKind` parameter for proper semantic binding

## Open Questions

1. **Generic name preservation across overloads**
   - What we know: Current code has three `Create` overloads, one accepts `GenericNameSyntax`
   - What's unclear: Whether all three overloads preserve generic type arguments correctly through `ToString()`
   - Recommendation: Test with generic spec names (e.g., `Spec<Model>`); may need to pass syntax nodes directly instead of calling `ToString()` first

2. **Trivia preservation from input nodes**
   - What we know: Input parameters are `IdentifierNameSyntax`, `ObjectCreationExpressionSyntax`, etc. (already syntax nodes)
   - What's unclear: Whether to preserve original trivia from these input nodes or normalize completely
   - Recommendation: Call `NormalizeWhitespace()` on final result (current behavior with `ParseExpression(...).NormalizeWhitespace()`), discarding input trivia

3. **CRLF constant location**
   - What we know: `CarriageReturnLineFeed` is used in `ConvertToSpecCodeFix.cs` via static using
   - What's unclear: Whether to define a constant or rely on static import in all files
   - Recommendation: Use static import (`using static SyntaxFactory`) in `SpecInvocationSyntax.cs`; no need for explicit constant

## Sources

### Primary (HIGH confidence)
- [Microsoft Learn: SyntaxFactory.InvocationExpression](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntaxfactory.invocationexpression) - Official API documentation
- [Microsoft Learn: SyntaxExtensions.NormalizeWhitespace](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntaxextensions.normalizewhitespace) - Whitespace normalization
- [Microsoft Learn: SyntaxFactory.ArgumentList](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntaxfactory.argumentlist) - Argument list construction
- [Roslyn GitHub: SyntaxFactory.cs](https://github.com/dotnet/roslyn/blob/main/src/Compilers/CSharp/Portable/Syntax/SyntaxFactory.cs) - Source implementation

### Secondary (MEDIUM confidence)
- [John Koerner: Creating Code Using the Syntax Factory](https://johnkoerner.com/csharp/creating-code-using-the-syntax-factory/) - Practical examples with Console.WriteLine pattern
- [Roslyn Quoter Tool](https://roslynquoter.azurewebsites.net/) - Code-to-SyntaxFactory converter
- [Critical Hit: Whitespace with CSharpSyntaxRewriter](https://criticalhittech.com/2019/03/19/getting-whitespace-right-with-roslyn-csharpsyntaxrewriter/) - Trivia ordering best practices
- [Jeremy Davis: Generating C# with Roslyn APIs](https://blog.jermdavis.dev/posts/2024/csharp-code-with-roslyn) - 2024 practical guide

### Tertiary (LOW confidence)
- WebSearch results on SyntaxFactory patterns (verified against official docs above)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Official Roslyn APIs, well-documented
- Architecture: HIGH - Existing `PropositionModelSyntax.cs` demonstrates SyntaxFactory patterns in this codebase
- Pitfalls: MEDIUM - Trivia ordering verified via Critical Hit article; other pitfalls inferred from API design

**Research date:** 2026-02-09
**Valid until:** 90 days (Roslyn API is stable; no breaking changes expected in minor versions)
