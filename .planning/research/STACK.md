# Technology Stack: SyntaxFactory Migration

**Project:** Motiv CodeFix SyntaxFactory Refactor
**Researched:** 2026-02-08
**Scope:** Roslyn SyntaxFactory APIs needed to replace string-based code generation

## Executive Summary

The CodeFix currently generates C# source code as strings (StringBuilder, raw string interpolation with `$$"""..."""`), then parses them back into syntax trees via `SyntaxFactory.ParseCompilationUnit()` or `SyntaxFactory.ParseExpression()`. This roundtrip is wasteful and fragile. The migration replaces string construction with direct SyntaxFactory API calls that build syntax nodes bottom-up.

The project already references `Microsoft.CodeAnalysis.CSharp` v5.0.0 and `Microsoft.CodeAnalysis.CSharp.Workspaces` v5.0.0, which provide all necessary APIs including `PrimaryConstructorBaseTypeSyntax` (C# 12 primary constructors), `RecordDeclarationSyntax`, and the full SyntaxFactory surface area. No new package dependencies are required.

`PropositionModelSyntax.cs` already demonstrates the target pattern with `using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory` and direct node construction. This is the reference implementation.

## Decision: SyntaxFactory, Not SyntaxGenerator

**Use `SyntaxFactory` directly. Do NOT use `SyntaxGenerator`.**

| Criterion | SyntaxFactory | SyntaxGenerator |
|-----------|---------------|-----------------|
| Language specificity | C#-specific, precise control | Language-agnostic abstraction |
| API granularity | Full control over every token and trivia | Higher-level, hides details |
| Primary constructor support | Full support via `PrimaryConstructorBaseTypeSyntax` | Does not expose primary constructor base types |
| Record declaration | `RecordDeclaration()` with full parameter control | `ClassDeclaration()` only -- no record support |
| Existing codebase pattern | Already used in `PropositionModelSyntax.cs` | Not used anywhere in the project |
| Package | `Microsoft.CodeAnalysis.CSharp` (already referenced) | `Microsoft.CodeAnalysis.CSharp.Workspaces` (also referenced, but unnecessary) |
| Fluent chain construction | Full control over `InvocationExpression` nesting | Would require manual assembly anyway |

**Rationale:** This project generates exclusively C# code and needs fine-grained control over primary constructors, record types, lambda expressions with block bodies, and fluent method chains. `SyntaxGenerator` is designed for cross-language code fixes (C# and VB simultaneously) -- a capability this project does not need. `SyntaxGenerator` also lacks APIs for records and primary constructor base types, which are central to the generated code. Using `SyntaxFactory` maintains consistency with the existing `PropositionModelSyntax.cs` reference implementation.

**Confidence:** HIGH -- verified against official Microsoft documentation and existing codebase patterns.

## Required SyntaxFactory APIs by Code Construct

### 1. Class Declarations with Primary Constructors

**What the CodeFix generates:**
```csharp
public class IsValidProposition() : Spec<int>(() => ...)
// or with constructor parameter:
public class IsFeatureEnabledProposition(MyNamespace.Playground instance) : Spec<string>(() => ...)
```

**SyntaxFactory methods needed:**

| Method | Purpose |
|--------|---------|
| `ClassDeclaration(string)` | Create the class node |
| `.WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))` | Add `public` modifier |
| `.WithParameterList(ParameterList(...))` | Add primary constructor parameters `()` or `(Type param)` |
| `.WithBaseList(BaseList(...))` | Add `: Spec<T>(...)` base type |
| `.WithMembers(List<MemberDeclarationSyntax>(...))` | Add nested record, if any |
| `.WithSemicolonToken(Token(SyntaxKind.SemicolonToken))` | For classes without body braces |
| `.WithOpenBraceToken(...)` / `.WithCloseBraceToken(...)` | For classes with body (containing nested record) |

**Critical: PrimaryConstructorBaseTypeSyntax for base class with arguments**

The base type `Spec<T>(() => ...)` is not a simple `SimpleBaseType` -- it passes a lambda argument to the base constructor. This requires `PrimaryConstructorBaseTypeSyntax`:

```
SyntaxFactory.PrimaryConstructorBaseType(
    type: GenericName("Spec").WithTypeArgumentList(...),
    argumentList: ArgumentList(SingletonSeparatedList(
        Argument(lambdaExpression)
    ))
)
```

| Factory Method | Purpose |
|----------------|---------|
| `PrimaryConstructorBaseType(TypeSyntax, ArgumentListSyntax)` | Create `Spec<T>(lambda)` base type with constructor args |
| `PrimaryConstructorBaseType(TypeSyntax)` | Create base type without args (not needed here) |
| `SimpleBaseType(TypeSyntax)` | For simple inheritance without constructor args (not used in this project) |
| `BaseList(SeparatedSyntaxList<BaseTypeSyntax>)` | Container for base types |

**Confidence:** HIGH -- `PrimaryConstructorBaseTypeSyntax` confirmed available in Microsoft.CodeAnalysis.CSharp v5.0.0 via official Microsoft Learn documentation.

### 2. Record Type Declarations

**What the CodeFix generates:**
```csharp
public record Model(int ValueA, int ValueB, bool ValueC);
// or
public record IsFeatureEnabledModel(int ValueA, int ValueC, string Text);
```

**SyntaxFactory methods needed:**

| Method | Purpose |
|--------|---------|
| `RecordDeclaration(SyntaxKind.RecordDeclaration, Token(SyntaxKind.RecordKeyword), Identifier(name))` | Create record node |
| `.WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))` | Add `public` modifier |
| `.WithParameterList(ParameterList(...))` | Add positional parameters |
| `.WithSemicolonToken(Token(SyntaxKind.SemicolonToken))` | Semicolon terminator (no body braces) |

**Important:** `RecordDeclaration` requires `SyntaxKind.RecordDeclaration` as the first argument to distinguish from `RecordStructDeclaration`. The `classOrStructKeyword` parameter should be `default` (no `class` or `struct` keyword -- plain `record`).

**Confidence:** HIGH -- `RecordDeclaration` factory method confirmed in Roslyn 5.0.0 API docs with 8 overloads.

### 3. Lambda Expressions (Factory Lambdas)

**What the CodeFix generates:**
```csharp
// Expression-body lambda (simple case):
() => Spec.Build((int value) => value > 0)
         .WhenTrue("...")
         .WhenFalse("...")
         .Create()

// Block-body lambda (composed case):
() =>
{
    var clause1 = Spec.Build((Model m) => m.ValueA > m.ValueB)
        .WhenTrue("...")
        .WhenFalse("...")
        .Create();
    // ...
    return clause1.AndAlso(isValueC);
}
```

**SyntaxFactory methods needed:**

| Method | Purpose |
|--------|---------|
| `ParenthesizedLambdaExpression()` | Create `() => ...` lambda |
| `.WithBody(expression)` | Expression-body lambda |
| `.WithBlock(Block(...))` | Block-body lambda |
| `SimpleLambdaExpression(Parameter(Identifier("m")))` | NOT USED -- use ParenthesizedLambda for typed params |
| `ParenthesizedLambdaExpression(ParameterList(...), null)` | Lambda with typed parameters like `(Model m) => ...` |

**Key distinction:** The outer factory lambda `() => { ... }` uses `ParenthesizedLambdaExpression()` with no parameters. The inner Spec.Build lambda `(Type name) => expr` uses `ParenthesizedLambdaExpression` with a `ParameterList` containing one typed parameter.

**Confidence:** HIGH -- lambda expression factories are core Roslyn APIs.

### 4. Fluent Method Chains (Spec.Build().WhenTrue().WhenFalse().Create())

**What the CodeFix generates:**
```csharp
Spec.Build((int value) => value > 0)
    .WhenTrue("(value > 0) == true")
    .WhenFalse("(value > 0) == false")
    .Create()
```

**This is a chain of InvocationExpressions wrapping MemberAccessExpressions.** Built inside-out:

```
InvocationExpression(                              // .Create()
  MemberAccessExpression(
    InvocationExpression(                          // .WhenFalse("...")
      MemberAccessExpression(
        InvocationExpression(                      // .WhenTrue("...")
          MemberAccessExpression(
            InvocationExpression(                  // Spec.Build(lambda)
              MemberAccessExpression(
                IdentifierName("Spec"),
                IdentifierName("Build")),
              ArgumentList(Argument(lambda))),
            IdentifierName("WhenTrue")),
          ArgumentList(Argument(StringLiteral))),
        IdentifierName("WhenFalse")),
      ArgumentList(Argument(StringLiteral))),
    IdentifierName("Create")),
  ArgumentList())
```

**SyntaxFactory methods needed:**

| Method | Purpose |
|--------|---------|
| `InvocationExpression(ExpressionSyntax, ArgumentListSyntax)` | Method call |
| `MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, ExpressionSyntax, SimpleNameSyntax)` | `.Method` access |
| `ArgumentList(SeparatedSyntaxList<ArgumentSyntax>)` | Arguments `(...)` |
| `Argument(ExpressionSyntax)` | Single argument |
| `LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(string))` | String literal `"..."` |

**Recommendation:** Create a helper method `FluentCall(ExpressionSyntax receiver, string methodName, params ExpressionSyntax[] args)` to reduce nesting depth. The existing codebase already uses helper patterns in `PropositionModelSyntax.cs`.

**Confidence:** HIGH -- these are fundamental SyntaxFactory APIs.

### 5. Object Creation Expressions

**What the CodeFix generates:**
```csharp
new IsValidProposition().IsSatisfiedBy(value).Satisfied
// or
new IsFeatureEnabledProposition(this)
// or
new Proposition.Model(valueA, valueB, valueC)
```

**SyntaxFactory methods needed:**

| Method | Purpose |
|--------|---------|
| `ObjectCreationExpression(TypeSyntax)` | `new Type(...)` |
| `.WithArgumentList(ArgumentList(...))` | Constructor arguments |
| `IdentifierName(string)` | Simple type name |
| `QualifiedName(NameSyntax, SimpleNameSyntax)` | `Proposition.Model` |
| `GenericName(string).WithTypeArgumentList(...)` | `Spec<T>` |

**Confidence:** HIGH -- core APIs.

### 6. Property Declarations (Already Implemented)

Already handled in `PropositionModelSyntax.cs`. Pattern established:

```
PropertyDeclaration(ParseTypeName(typeName), propertyName)
    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
    .WithAccessorList(AccessorList(List([
        AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
    ])))
```

No changes needed.

### 7. Constructor Declarations (Already Implemented)

Already handled in `PropositionModelSyntax.cs`. Pattern established:

```
ConstructorDeclaration(className)
    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
    .WithParameterList(ParameterList([..parameters]))
    .WithBody(Block(List<StatementSyntax>([..assignments])))
```

No changes needed for model constructors. The class-level primary constructor is handled by `ClassDeclaration.WithParameterList()` (see section 1).

### 8. Field Declarations

**What the CodeFix generates:**
```csharp
private readonly IsFeatureEnabledProposition _isFeatureEnabledProposition;
// or
private readonly Proposition _proposition = new Proposition();
```

**SyntaxFactory methods needed:**

| Method | Purpose |
|--------|---------|
| `FieldDeclaration(VariableDeclaration(...))` | Field declaration |
| `VariableDeclaration(TypeSyntax)` | Type part |
| `.AddVariables(VariableDeclarator(Identifier(name)))` | Variable name |
| `.WithInitializer(EqualsValueClause(ObjectCreationExpression(...)))` | `= new Type()` |
| `.WithModifiers(TokenList(Token(Private), Token(Readonly)))` | Access modifiers |

**Confidence:** HIGH -- standard APIs.

### 9. Method Declarations

**What the CodeFix generates:**
```csharp
public bool IsValid(int value)
{
    // original expression
    var result = _proposition.IsSatisfiedBy(new Proposition.Model(valueA, valueB));
    return result.Satisfied;
}
```

**SyntaxFactory methods needed:**

| Method | Purpose |
|--------|---------|
| `MethodDeclaration(TypeSyntax, string)` | Method header |
| `.WithModifiers(...)` | Access modifiers |
| `.WithParameterList(...)` | Parameters |
| `.WithBody(Block(...))` | Method body |
| `LocalDeclarationStatement(...)` | `var result = ...` |
| `ReturnStatement(...)` | `return result.Satisfied;` |

**Confidence:** HIGH -- standard APIs.

### 10. Variable Declarations (Local)

**What the CodeFix generates (inside block lambdas):**
```csharp
var clause1 = Spec.Build((Model m) => m.ValueA > m.ValueB)
    .WhenTrue("...").WhenFalse("...").Create();
```

**SyntaxFactory methods needed:**

| Method | Purpose |
|--------|---------|
| `LocalDeclarationStatement(VariableDeclaration(IdentifierName("var")))` | `var` declaration |
| `.AddVariables(VariableDeclarator(Identifier(name)).WithInitializer(EqualsValueClause(expr)))` | `= expr` |

**Confidence:** HIGH.

### 11. Composition Expressions (AndAlso/OrElse/XOR/Not)

**What the CodeFix generates:**
```csharp
clause1.AndAlso(isValueC)
isValueANonNegative.AndAlso((isValueBNonNegative.OrElse(!(isValueCAtLeast1 ^ isValueCAtMost10))))
```

**SyntaxFactory methods needed:**

| Method | Purpose |
|--------|---------|
| `InvocationExpression(MemberAccessExpression(...), ArgumentList(...))` | `.AndAlso(...)`, `.OrElse(...)` |
| `PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, operand)` | `!expr` |
| `BinaryExpression(SyntaxKind.ExclusiveOrExpression, left, right)` | `a ^ b` |
| `ParenthesizedExpression(expr)` | `(expr)` grouping |

**Important:** The current implementation builds composition expressions as strings (`compositionExpression`). To migrate fully, the `ExpressionDecomposition` struct would need to carry syntax nodes instead of string representations for the composition expression. This is the most complex part of the migration because composition expression construction is currently interleaved with string manipulation in `DecomposeExpression`.

**Confidence:** HIGH for the SyntaxFactory APIs themselves. The integration challenge is MEDIUM -- requires restructuring how composition expressions are built.

### 12. Comment Trivia

**What the CodeFix generates:**
```csharp
// valueA > 0 && valueB > 0
var result = ...
```

**SyntaxFactory methods needed:**

| Method | Purpose |
|--------|---------|
| `Comment(string)` | Create `// text` trivia |
| `TriviaList(SyntaxTrivia...)` | Group trivia |
| `.WithLeadingTrivia(...)` | Attach to a statement |

**Confidence:** HIGH.

## Trivia and Whitespace Strategy

### Recommendation: NormalizeWhitespace() at the Top Level

**Strategy:** Build syntax trees without manual trivia, then call `.NormalizeWhitespace()` on the outermost node before returning.

**Rationale:**
1. The existing `PropositionModelSyntax.cs` reference implementation already uses this pattern (line 38: `.NormalizeWhitespace()`)
2. Manual trivia insertion is error-prone and verbose -- each space, newline, and indent must be explicit
3. `NormalizeWhitespace()` produces consistently formatted output that matches standard C# formatting
4. The CodeFix tests compare against expected source strings, and `NormalizeWhitespace()` produces deterministic output

**Caveats:**
- `NormalizeWhitespace()` will override any manually-set trivia. Do NOT add custom trivia before calling it.
- For the comment trivia (`// original expression`), apply the comment AFTER `NormalizeWhitespace()` by finding the target statement and adding leading trivia to it.
- The method accepts optional parameters: `indentation` (default: 4 spaces), `eol` (default: `\r\n`), `elasticTrivia` (default: false).

**Order of operations:**
1. Build complete syntax tree with SyntaxFactory
2. Call `.NormalizeWhitespace()` on the top-level node
3. If needed, locate specific nodes and add comment trivia

**Confidence:** HIGH -- validated by existing reference implementation and official docs.

### WithTriviaFrom() for Preserving Source Trivia

When replacing nodes in the original document (e.g., replacing the expression with a spec invocation), use `.WithTriviaFrom(originalNode)` to preserve the user's whitespace around the replacement.

Already used in `ConvertToSpecCodeFix.cs` line 259: `.WithTriviaFrom(original)`.

## What NOT to Use

### Do NOT use SyntaxFactory.ParseCompilationUnit() or ParseExpression()

These are the methods being replaced. They exist as an escape hatch for parsing C# strings into syntax trees, but they:
- Require a valid string representation first (defeating the purpose of programmatic construction)
- Add a parse-then-extract roundtrip (`compilationUnit.DescendantNodes().OfType<...>().First()`)
- Hide structural errors until runtime (a malformed string produces a node with diagnostics, not a compile error)
- Cannot participate in incremental syntax tree construction

**One exception:** `ParseTypeName(string)` remains valid for converting type symbol display strings (from `ITypeSymbol.ToDisplayString()`) into `TypeSyntax` nodes. There is no better way to convert arbitrary fully-qualified type names into syntax nodes.

### Do NOT use SyntaxGenerator

As discussed above. No record support, no primary constructor base type support, adds unnecessary abstraction for a C#-only code fix.

### Do NOT use StringBuilder or raw string interpolation for source code

This is the pattern being eliminated. After migration, the only remaining string interpolation should be for:
- `WhenTrue`/`WhenFalse` description strings (runtime literal values, not source code)
- Identifier names derived from expression analysis
- Type names from `ITypeSymbol.ToDisplayString()`

## Migration Scope by File

### Files to Migrate

| File | Current Pattern | Migration Complexity | Key Constructs |
|------|----------------|---------------------|----------------|
| `CustomSpecDeclarationSyntax.cs` | `StringBuilder` + `ParseCompilationUnit` | HIGH | Class with primary constructor, `PrimaryConstructorBaseTypeSyntax`, block lambda, fluent chains, nested record, `var` declarations |
| `ConvertToSpecCodeFix.cs` | `$$"""..."""` + `ParseCompilationUnit` + `ParseStatement` | HIGH | Field declarations, constructor declarations, method bodies, block statements |
| `SpecInvocationSyntax.cs` | `$$"""..."""` + `ParseExpression` | LOW | `ObjectCreationExpression`, `MemberAccessExpression` chain |

### Files Already Migrated (Reference)

| File | Pattern | Constructs Demonstrated |
|------|---------|------------------------|
| `PropositionModelSyntax.cs` | Pure SyntaxFactory | `ClassDeclaration`, `ConstructorDeclaration`, `PropertyDeclaration`, `ParameterList`, `Block`, `ExpressionStatement`, `AssignmentExpression`, `NormalizeWhitespace()` |

### Files Unchanged

| File | Why |
|------|-----|
| `ExpressionDecomposition.cs` | Data structure, no code generation |
| `ExpressionNameDeriver.cs` | Name derivation logic, no code generation |
| `ClauseNameDeriver.cs` | Name derivation logic, no code generation |
| `InstanceMethodDetector.cs` | Detection logic, no code generation |
| `VariableExtractorWalker.cs` | Analysis logic, no code generation |
| `StringExtensions.cs` | String utilities, still needed for identifier manipulation |
| `SymbolExtensions.cs` | Type name conversion, still needed |
| `MotivCodeFixProvider.cs` | Orchestration, delegates to converter |

## Recommended Helper Utilities

### 1. Fluent Chain Builder

Reduce the verbose nesting of `InvocationExpression(MemberAccessExpression(...))`:

```csharp
static InvocationExpressionSyntax FluentCall(
    ExpressionSyntax receiver,
    string methodName,
    params ExpressionSyntax[] arguments)
```

### 2. Spec.Build Chain Builder

The `Spec.Build(lambda).WhenTrue(str).WhenFalse(str).Create()` pattern repeats for every clause:

```csharp
static ExpressionSyntax CreateSpecBuildChain(
    TypeSyntax modelType,
    string parameterName,
    ExpressionSyntax bodyExpression,
    string whenTrueText,
    string whenFalseText)
```

### 3. Primary Constructor Class Builder

The `public class Name(params) : Spec<T>(lambda)` pattern appears in multiple places:

```csharp
static ClassDeclarationSyntax CreateSpecClass(
    string className,
    ParameterListSyntax? primaryConstructorParams,
    TypeSyntax specTypeArgument,
    ExpressionSyntax factoryLambda,
    MemberDeclarationSyntax[]? nestedMembers)
```

## Version Compatibility

| Component | Version | Confirmed |
|-----------|---------|-----------|
| `Microsoft.CodeAnalysis.CSharp` | 5.0.0 | YES - via `Directory.Packages.props` |
| `Microsoft.CodeAnalysis.CSharp.Workspaces` | 5.0.0 | YES - via `Directory.Packages.props` |
| Target framework (CodeFix) | netstandard2.0 | YES - via `Motiv.CodeFix.csproj` |
| `PrimaryConstructorBaseTypeSyntax` | Available in 5.0.0 | YES - verified via Microsoft Learn docs |
| `RecordDeclaration` factory method | Available in 5.0.0 | YES - verified via Microsoft Learn docs |
| `ClassDeclaration.WithParameterList()` | Available in 5.0.0 | YES - verified via Microsoft Learn docs |

**Note on netstandard2.0:** The CodeFix project targets netstandard2.0 (required for Roslyn analyzers to load in all IDE versions). This is the runtime target of the analyzer itself, NOT the language version of the code being generated. The SyntaxFactory APIs for C# 12 features (primary constructors, records) are available in the Roslyn 5.0.0 NuGet package regardless of the analyzer's target framework. The generated code targets whatever language version the user's project uses.

## Sources

- [SyntaxFactory.ClassDeclaration - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntaxfactory.classdeclaration?view=roslyn-dotnet-4.9.0)
- [SyntaxFactory.RecordDeclaration - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntaxfactory.recorddeclaration?view=roslyn-dotnet-4.3.0)
- [PrimaryConstructorBaseTypeSyntax - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntax.primaryconstructorbasetypesyntax?view=roslyn-dotnet-5.0.0)
- [SyntaxFactory.PrimaryConstructorBaseType - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntaxfactory.primaryconstructorbasetype?view=roslyn-dotnet-5.0.0)
- [ClassDeclarationSyntax - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntax.classdeclarationsyntax?view=roslyn-dotnet-4.9.0)
- [NormalizeWhitespace - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.syntaxnodeextensions.normalizewhitespace?view=roslyn-dotnet-4.9.0)
- [Roslyn Quoter - Online Tool](https://roslynquoter.azurewebsites.net/)
- [SyntaxGenerator vs SyntaxFactory - dotnet/roslyn-analyzers#23](https://github.com/dotnet/roslyn-analyzers/issues/23)
- [Jeremy Davis - Generating C# code with Roslyn APIs](https://blog.jermdavis.dev/posts/2024/csharp-code-with-roslyn)
- Existing codebase: `PropositionModelSyntax.cs` (reference implementation)
- Existing codebase: `CustomSpecDeclarationSyntax.cs` (migration target)
- Existing codebase: `ConvertToSpecCodeFix.cs` (migration target)
- Existing codebase: `SpecInvocationSyntax.cs` (migration target)

---
*Researched: 2026-02-08*
