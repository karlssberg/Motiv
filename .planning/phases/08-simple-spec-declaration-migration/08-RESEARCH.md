# Phase 8: Simple Spec Declaration Migration - Research

**Researched:** 2026-02-09
**Domain:** Roslyn SyntaxFactory API for primary constructor and lambda expression construction
**Confidence:** HIGH

## Summary

This phase migrates `CustomSpecDeclarationSyntax.CreateInternal()` from string interpolation + `ParseCompilationUnit()` to pure SyntaxFactory API construction. The target output is a class with a primary constructor and a lambda expression in the base type arguments: `public class Proposition() : Spec<int>(() => Spec.Build(...).WhenTrue(...).WhenFalse(...).Create())`.

This is the first migration involving primary constructors and `PrimaryConstructorBaseTypeSyntax`, which is significantly more complex than the SpecInvocation migration completed in Phase 7. The migration establishes patterns for constructing lambda expressions, fluent method chains, and class declarations with primary constructor base type arguments that will be reused in Phases 9-10 for more complex scenarios (composed specs with block lambdas and constructor-based specs with instance method injection).

The existing test suite (first test in `MotivConvertToSpecTests.cs`: `Should_convert_single_variable_boolean_expressions_that_can_be_converted_to_spec`) acts as the verification gate—all tests must pass with identical output.

**Primary recommendation:** Use SyntaxFactory to build the class declaration from components (class keyword + identifier + parameter list → base list with PrimaryConstructorBaseTypeSyntax → lambda expression with fluent chain body), leveraging Phase 7's established patterns for nested InvocationExpression/MemberAccessExpression construction, and call `NormalizeWhitespace()` on the final declaration.

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.CodeAnalysis.CSharp | 4.x+ | Roslyn SyntaxFactory API | Official C# code generation API from Roslyn team |
| using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory | N/A | Static imports for SyntaxFactory methods | Community standard pattern to reduce verbosity (already used in Phase 7) |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Microsoft.CodeAnalysis.CSharp.Syntax | 4.x+ | Syntax node types (ClassDeclarationSyntax, PrimaryConstructorBaseTypeSyntax, etc.) | Already in use; no changes |
| Roslyn Quoter | Online tool | Generate SyntaxFactory code from C# snippets | During development to verify complex patterns (primary constructor syntax) |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| ParseCompilationUnit | SyntaxFactory | ParseCompilationUnit loses contextual information, less type-safe |
| SyntaxGenerator | SyntaxFactory | SyntaxGenerator doesn't support C#-specific constructs (records, primary constructors) |
| Hybrid parse+construct | Pure SyntaxFactory | Hybrid reduces risk but doesn't fully eliminate string-based generation |

**Installation:**
Already installed (existing dependency). No additional packages needed.

## Architecture Patterns

### Current Implementation (Phase 8 Target)

```
CustomSpecDeclarationSyntax.cs
├── Create(IdentifierNameSyntax, IdentifierNameSyntax, ExpressionSyntax, string)
└── CreateInternal(string propositionName, string modelParameterName, ExpressionSyntax expression, string modelTypeName)
    └── Uses: ParseCompilationUnit($$"""public class {{name}}() : Spec<{{type}}>(() => ...)""")
```

**Current code (lines 379-400):**
```csharp
private static TypeDeclarationSyntax CreateInternal(
    string propositionName,
    string modelParameterName,
    ExpressionSyntax transformedExpression,
    ExpressionSyntax originalExpression,
    string modelTypeName)
{
    var camelCasedModelParameterName = modelParameterName.ToCamelCase();
    var escapedOriginalExpression = originalExpression.ToString().EscapeDoubleQuotes();
    var propositionSource =
        $$"""
          public class {{propositionName}}() : Spec<{{modelTypeName}}>(() =>
              Spec.Build(({{modelTypeName}} {{camelCasedModelParameterName}}) => {{transformedExpression}})
                  .WhenTrue("({{escapedOriginalExpression}}) == true")
                  .WhenFalse("({{escapedOriginalExpression}}) == false")
                  .Create());
          """;

    var compilationUnit = SyntaxFactory.ParseCompilationUnit(propositionSource);

    return compilationUnit.DescendantNodes().OfType<TypeDeclarationSyntax>().First();
}
```

**Goal:** Replace the entire method with SyntaxFactory construction, remove `ParseCompilationUnit`, remove `ToString()` calls.

### Recommended Pattern: Component-Based Class Construction

Build the class declaration from components, leveraging the existing `transformedExpression` syntax node:

```csharp
private static TypeDeclarationSyntax CreateInternal(
    string propositionName,
    string modelParameterName,
    ExpressionSyntax transformedExpression,
    ExpressionSyntax originalExpression,
    string modelTypeName)
{
    var camelCasedModelParameterName = modelParameterName.ToCamelCase();
    var escapedOriginalExpression = originalExpression.ToString().EscapeDoubleQuotes();

    // Step 1: Build the fluent chain (innermost)
    // Spec.Build((Type param) => expr).WhenTrue("...").WhenFalse("...").Create()
    var fluentChain = BuildFluentChain(
        modelTypeName,
        camelCasedModelParameterName,
        transformedExpression,
        escapedOriginalExpression);

    // Step 2: Build the lambda expression
    // () => [fluentChain]
    var lambdaExpression = ParenthesizedLambdaExpression(
        ParameterList(),  // Empty parameter list: ()
        fluentChain);     // Expression body

    // Step 3: Build the base type with arguments
    // Spec<Type>(() => ...)
    var baseType = PrimaryConstructorBaseType(
        GenericName(
            Identifier("Spec"),
            TypeArgumentList(
                SingletonSeparatedList<TypeSyntax>(
                    IdentifierName(modelTypeName)))),
        ArgumentList(
            SingletonSeparatedList(
                Argument(lambdaExpression))));

    // Step 4: Build the class declaration
    // public class PropositionName() : [baseType];
    var classDeclaration = ClassDeclaration(propositionName)
        .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
        .WithParameterList(ParameterList())  // Primary constructor: ()
        .WithBaseList(BaseList(SingletonSeparatedList<BaseTypeSyntax>(baseType)))
        .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

    return classDeclaration.NormalizeWhitespace();
}

private static ExpressionSyntax BuildFluentChain(
    string modelTypeName,
    string parameterName,
    ExpressionSyntax bodyExpression,
    string whenTrueMessage)
{
    // Build: Spec.Build((Type param) => body)
    var specBuildInvocation = InvocationExpression(
        MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            IdentifierName("Spec"),
            IdentifierName("Build")),
        ArgumentList(
            SingletonSeparatedList(
                Argument(
                    ParenthesizedLambdaExpression(
                        ParameterList(
                            SingletonSeparatedList(
                                Parameter(Identifier(parameterName))
                                    .WithType(IdentifierName(modelTypeName)))),
                        bodyExpression)))));

    // Chain .WhenTrue("...")
    var whenTrueInvocation = InvocationExpression(
        MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            specBuildInvocation,
            IdentifierName("WhenTrue")),
        ArgumentList(
            SingletonSeparatedList(
                Argument(
                    LiteralExpression(
                        SyntaxKind.StringLiteralExpression,
                        Literal($"({whenTrueMessage}) == true"))))));

    // Chain .WhenFalse("...")
    var whenFalseInvocation = InvocationExpression(
        MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            whenTrueInvocation,
            IdentifierName("WhenFalse")),
        ArgumentList(
            SingletonSeparatedList(
                Argument(
                    LiteralExpression(
                        SyntaxKind.StringLiteralExpression,
                        Literal($"({whenTrueMessage}) == false"))))));

    // Chain .Create()
    var createInvocation = InvocationExpression(
        MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            whenFalseInvocation,
            IdentifierName("Create")),
        ArgumentList());

    return createInvocation;
}
```

### Pattern 1: Primary Constructor Class Declaration

**What:** Create a class with primary constructor syntax using `ClassDeclaration` with `ParameterList`
**When to use:** Any class with primary constructor (Phase 8, 9, 10)
**Example:**
```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntax.classdeclarationsyntax
// Pattern: public class Name() : BaseType { }

var classDecl = ClassDeclaration("ClassName")
    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
    .WithParameterList(ParameterList())  // Primary constructor parameters
    .WithBaseList(BaseList(...))
    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));  // For expression body
```

**Current status:** Not yet used in codebase. This phase establishes the pattern.

### Pattern 2: PrimaryConstructorBaseTypeSyntax for Base Type Arguments

**What:** Create base type syntax with arguments passed to primary constructor using `PrimaryConstructorBaseType`
**When to use:** Base class/interface that requires constructor arguments (e.g., `Spec<T>(() => ...)`)
**Example:**
```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntax.primaryconstructorbasetypesyntax
// Pattern: BaseType<T>(arg1, arg2)

var baseType = PrimaryConstructorBaseType(
    GenericName(
        Identifier("Spec"),
        TypeArgumentList(
            SingletonSeparatedList<TypeSyntax>(
                IdentifierName("int")))),
    ArgumentList(
        SingletonSeparatedList(
            Argument(lambdaExpression))));
```

**Key distinction:** Use `PrimaryConstructorBaseType` when base requires arguments. Use `SimpleBaseType` for interfaces or base without arguments.

### Pattern 3: ParenthesizedLambdaExpression for Lambda Syntax

**What:** Create lambda expressions with explicit parameter lists and expression/block bodies
**When to use:** Factory lambdas in Spec base constructor arguments
**Example:**
```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntax.parenthesizedlambdaexpressionsyntax
// Pattern: () => expression

// Expression body (Phase 8)
var expressionLambda = ParenthesizedLambdaExpression(
    ParameterList(),  // Empty: ()
    expressionBody);

// Block body (Phase 9, 10)
var blockLambda = ParenthesizedLambdaExpression(
    ParameterList(),  // Empty: ()
    Block(statements));

// With parameters
var parameterizedLambda = ParenthesizedLambdaExpression(
    ParameterList(
        SingletonSeparatedList(
            Parameter(Identifier("x"))
                .WithType(IdentifierName("int")))),
    expressionBody);
```

### Pattern 4: Fluent Method Chain Construction (Reuse from Phase 7)

**What:** Build fluent chains like `obj.Method1().Method2().Method3()` via nested `InvocationExpression`/`MemberAccessExpression`
**When to use:** Spec.Build().WhenTrue().WhenFalse().Create() chains (Phase 8, 9, 10)
**Example:**
```csharp
// Source: Phase 7 patterns (SpecInvocationSyntax.cs)
// Pattern: Spec.Build(lambda).WhenTrue("...").WhenFalse("...").Create()

// Start with Spec.Build(arg)
var buildCall = InvocationExpression(
    MemberAccessExpression(
        SyntaxKind.SimpleMemberAccessExpression,
        IdentifierName("Spec"),
        IdentifierName("Build")),
    ArgumentList(SingletonSeparatedList(Argument(lambdaArg))));

// Chain .WhenTrue(arg)
var whenTrue = InvocationExpression(
    MemberAccessExpression(
        SyntaxKind.SimpleMemberAccessExpression,
        buildCall,
        IdentifierName("WhenTrue")),
    ArgumentList(SingletonSeparatedList(Argument(stringArg))));

// Continue chaining...
```

**Current status:** Established in Phase 7, reused here for Spec.Build() chain.

### Pattern 5: String Literal Escaping via SyntaxFactory.Literal

**What:** Use `SyntaxFactory.Literal(string)` to properly escape string content for code generation
**When to use:** WhenTrue/WhenFalse message strings containing special characters
**Example:**
```csharp
// Source: Roslyn SyntaxFactory documentation
// Pattern: String literal with escaping

var stringLiteral = LiteralExpression(
    SyntaxKind.StringLiteralExpression,
    Literal("text == \"green\""));  // SyntaxFactory.Literal handles escaping
```

**Key insight:** `SyntaxFactory.Literal()` automatically handles double quote escaping, potentially eliminating the need for `EscapeDoubleQuotes()` extension method.

### Anti-Patterns to Avoid

- **ParseCompilationUnit for entire class:** Loses semantic information and type safety. Use explicit ClassDeclaration construction.
- **ToString() on ExpressionSyntax nodes mid-construction:** Breaks the syntax tree. Keep nodes as nodes until final output.
- **SimpleBaseType for base with arguments:** Wrong syntax node. Use `PrimaryConstructorBaseType` when base requires constructor arguments.
- **String concatenation for lambda bodies:** Use `ParenthesizedLambdaExpression` with expression/block body.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Primary constructor syntax | String templates with () in class name | `ClassDeclaration.WithParameterList()` | Proper syntax tree structure, IDE-friendly |
| Base type with constructor args | String interpolation with base list | `PrimaryConstructorBaseType` | Handles generic types, argument lists correctly |
| Lambda expressions | String concatenation with => | `ParenthesizedLambdaExpression` | Type-safe, preserves semantic information |
| String literal escaping | Manual backslash insertion | `SyntaxFactory.Literal(string)` | Handles all escape sequences correctly |
| Fluent method chains | String concatenation with dots | Nested `InvocationExpression` (Phase 7 pattern) | Refactoring support, semantic preservation |

**Key insight:** Primary constructors have a unique syntax tree structure (parameters on class declaration + arguments in base list via `PrimaryConstructorBaseTypeSyntax`). Hand-rolling this with strings produces incorrect AST structure that breaks IDE features.

## Common Pitfalls

### Pitfall 1: Primary Constructor Complexity
**What goes wrong:** Assuming `ClassDeclaration` + base list is sufficient, forgetting the `ParameterList` property
**Why it happens:** Primary constructors are a C# 12 feature with non-obvious syntax tree structure
**How to avoid:**
- Always use `ClassDeclaration.WithParameterList(ParameterList())` for primary constructors
- Use `PrimaryConstructorBaseType` (not `SimpleBaseType`) for base types with arguments
- Use Roslyn Quoter to verify the expected tree structure
**Warning signs:** Generated code missing `()` after class name, or compiler errors about base constructor arguments

### Pitfall 2: Lambda Body Type Mismatch
**What goes wrong:** Passing an expression node where a statement/block is expected, or vice versa
**Why it happens:** `ParenthesizedLambdaExpression` overloads accept either `ExpressionSyntax` (expression body) or `BlockSyntax` (block body)
**How to avoid:**
- Phase 8 (simple spec): Use expression body overload with the fluent chain expression
- Phase 9/10 (composed/constructor): Use block body overload with `Block(statements)`
**Warning signs:** Compile error "cannot convert expression to statement" or vice versa

### Pitfall 3: Reusing ExpressionSyntax Nodes Directly
**What goes wrong:** The `transformedExpression` parameter is already a syntax node, but may need adjustment (trivia, parent references)
**Why it happens:** Syntax nodes are immutable with parent/trivia metadata
**How to avoid:**
- Reuse the expression node directly as the lambda body in Spec.Build()
- Don't call `ToString()` and re-parse
- `NormalizeWhitespace()` on final class declaration handles trivia
**Warning signs:** Generated code has weird indentation or spacing around the expression

### Pitfall 4: Generic Type Syntax
**What goes wrong:** Using `IdentifierName("Spec<int>")` instead of proper generic syntax
**Why it happens:** Treating generic types as simple identifiers
**How to avoid:** Use `GenericName` with `TypeArgumentList`:
```csharp
GenericName(
    Identifier("Spec"),
    TypeArgumentList(
        SingletonSeparatedList<TypeSyntax>(
            IdentifierName("int"))))
```
**Warning signs:** Generated code has literal angle brackets in identifier, compiler error

### Pitfall 5: Semicolon Token for Expression-Bodied Class
**What goes wrong:** Forgetting the trailing semicolon for expression-bodied class members
**Why it happens:** Traditional class declarations don't require semicolons
**How to avoid:** Use `WithSemicolonToken(Token(SyntaxKind.SemicolonToken))` for classes with primary constructor and expression body base
**Warning signs:** Generated code missing semicolon, compiler error

## Code Examples

Verified patterns from official sources:

### Primary Constructor Base Type with Lambda Argument
```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntax.primaryconstructorbasetypesyntax
// Pattern: public class MyClass() : Spec<int>(() => expr);

var lambdaExpression = ParenthesizedLambdaExpression(
    ParameterList(),
    SomeExpression());

var baseType = PrimaryConstructorBaseType(
    GenericName(
        Identifier("Spec"),
        TypeArgumentList(
            SingletonSeparatedList<TypeSyntax>(
                IdentifierName("int")))),
    ArgumentList(
        SingletonSeparatedList(
            Argument(lambdaExpression))));

var classDecl = ClassDeclaration("MyClass")
    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
    .WithParameterList(ParameterList())
    .WithBaseList(BaseList(SingletonSeparatedList<BaseTypeSyntax>(baseType)))
    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
```

### Parameterized Lambda with Type Annotation
```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntax.parenthesizedlambdaexpressionsyntax
// Pattern: (int value) => value > 0

var lambda = ParenthesizedLambdaExpression(
    ParameterList(
        SingletonSeparatedList(
            Parameter(Identifier("value"))
                .WithType(IdentifierName("int")))),
    BinaryExpression(
        SyntaxKind.GreaterThanExpression,
        IdentifierName("value"),
        LiteralExpression(
            SyntaxKind.NumericLiteralExpression,
            Literal(0))));
```

### Generic Name with Type Arguments
```csharp
// Source: Roslyn SyntaxFactory documentation
// Pattern: Dictionary<string, int>

var genericType = GenericName(
    Identifier("Dictionary"),
    TypeArgumentList(
        SeparatedList<TypeSyntax>(
            new[] {
                IdentifierName("string"),
                IdentifierName("int")
            })));
```

### Using Roslyn Quoter for Verification
```
// Input to https://roslynquoter.azurewebsites.net/:
public class IsValidProposition() : Spec<int>(() =>
    Spec.Build((int value) => value > 0)
        .WhenTrue("(value > 0) == true")
        .WhenFalse("(value > 0) == false")
        .Create());

// Expected output: SyntaxFactory code showing full tree structure
// Use this to verify the component-based approach produces identical structure
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| String templates + parsing | Direct SyntaxFactory construction | Roslyn 1.0 (2015) | Type safety, IDE integration, refactoring support |
| Manual string escaping | `SyntaxFactory.Literal()` | Roslyn 1.0 (2015) | Automatic escaping for all special characters |
| SimpleBaseType for all base types | `PrimaryConstructorBaseType` for argument lists | Roslyn 4.0 (C# 12 support, 2023) | Proper primary constructor support |
| Single factory method pattern | Component-based construction | Modern practice (2020+) | Reusability, testability, maintainability |

**Deprecated/outdated:**
- `ParseCompilationUnit` for class declarations: Use `ClassDeclaration` with explicit components for type safety
- `ToString()` on syntax nodes during construction: Keep nodes as nodes until final output
- Manual escaping with `EscapeDoubleQuotes()`: `SyntaxFactory.Literal()` handles this automatically

## Open Questions

1. **EscapeDoubleQuotes() redundancy**
   - What we know: `SyntaxFactory.Literal(string)` handles escaping automatically
   - What's unclear: Whether the existing `EscapeDoubleQuotes()` extension is still needed
   - Recommendation: Test with string containing quotes (test on line 774: `text == "green"`). If `Literal()` produces correct output, `EscapeDoubleQuotes()` can be removed in Phase 12

2. **NormalizeWhitespace() placement**
   - What we know: Phase 7 calls `NormalizeWhitespace()` on final expression
   - What's unclear: Whether to call on `ClassDeclaration` or just return nodes without normalization
   - Recommendation: Call `NormalizeWhitespace()` on final `ClassDeclaration` to match Phase 7 pattern and ensure consistent formatting

3. **SimpleBaseType vs PrimaryConstructorBaseType usage**
   - What we know: `PrimaryConstructorBaseType` is for base types with constructor arguments
   - What's unclear: When to use each type in the broader codebase
   - Recommendation: Use `PrimaryConstructorBaseType` for `Spec<T>(() => ...)` pattern (Phase 8-10). Use `SimpleBaseType` if we ever generate classes with base types but no arguments

## Sources

### Primary (HIGH confidence)
- [Microsoft Learn: PrimaryConstructorBaseTypeSyntax](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntax.primaryconstructorbasetypesyntax?view=roslyn-dotnet-4.13.0) - Official API documentation for primary constructor base types
- [Microsoft Learn: ClassDeclarationSyntax](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntax.classdeclarationsyntax) - ParameterList property for primary constructors
- [Microsoft Learn: ParenthesizedLambdaExpressionSyntax](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntax.parenthesizedlambdaexpressionsyntax?view=roslyn-dotnet-4.9.0) - Lambda expression construction
- [Microsoft Learn: BaseTypeSyntax](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntax.basetypesyntax?view=roslyn-dotnet-4.9.0) - Inheritance hierarchy showing PrimaryConstructorBaseTypeSyntax and SimpleBaseTypeSyntax
- Phase 7 Research (`.planning/phases/07-specinvocation-migration/07-RESEARCH.md`) - Established patterns for fluent chains and NormalizeWhitespace

### Secondary (MEDIUM confidence)
- [Roslyn Quoter Tool](https://roslynquoter.azurewebsites.net/) - Code-to-SyntaxFactory converter (verified tool exists and supports primary constructors)
- [John Koerner: Creating Code Using the Syntax Factory](https://johnkoerner.com/csharp/creating-code-using-the-syntax-factory/) - Practical examples with fluent chains
- [GitHub: dotnet/roslyn - CSharpSemanticModel.cs](https://github.com/dotnet/roslyn/blob/main/src/Compilers/CSharp/Portable/Compilation/CSharpSemanticModel.cs) - Reference implementation showing PrimaryConstructorBaseTypeSyntax usage

### Tertiary (LOW confidence)
- [C# via RoslynAPI - The Big Picture](https://sapehin.com/blog/csharp-via-roslynapi-the-big-picture/) - General Roslyn overview
- [Generating C# code with Roslyn APIs by Jeremy Davis](https://blog.jermdavis.dev/posts/2024/csharp-code-with-roslyn) - 2024 practical guide

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Official Roslyn APIs, Phase 7 already established patterns
- Architecture: HIGH - PrimaryConstructorBaseTypeSyntax is officially documented; patterns verified via Microsoft Learn
- Pitfalls: MEDIUM - Derived from API structure and Phase 7 experience; primary constructor complexity noted in `.planning/research/PITFALLS.md`

**Research date:** 2026-02-09
**Valid until:** 90 days (Roslyn API is stable; primary constructor support is in C# 12+, no breaking changes expected)
