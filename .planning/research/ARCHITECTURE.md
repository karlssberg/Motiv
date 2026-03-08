# Architecture Patterns: SyntaxFactory Migration

**Domain:** Roslyn CodeFix -- String-based to SyntaxFactory migration
**Researched:** 2026-02-08
**Confidence:** HIGH

## Executive Summary

The migration from string-based code generation to SyntaxFactory construction is a refactoring-only exercise. The external behavior (generated C# code) must remain identical -- the 10 existing tests in `MotivConvertToSpecTests.cs` are the verification gate. The architecture does not change structurally; what changes is the **internal implementation** of three files that currently build code as strings and then parse them.

The recommended strategy is **construct-by-construct within each file**, migrating from the simplest code path to the most complex, using the existing `PropositionModelSyntax.cs` as the reference implementation for SyntaxFactory patterns. No new files or components are needed. Several helper methods on existing files become unnecessary and can be removed.

## Current Architecture

```
MotivCodeFixProvider.cs          [ENTRY POINT -- no string generation, NO CHANGES]
    |
    v
LogicalExpressionToSpecConverter  [ORCHESTRATOR -- has string gen in ReplaceMultiVariableExpression]
(ConvertToSpecCodeFix.cs)              |
    |                                   |
    +--- SpecInvocationExpressionSyntax.Create()  [LEAF -- string-based, MIGRATE]
    |    (Syntax/SpecInvocationSyntax.cs)
    |
    +--- CustomSpecDeclarationSyntax.Create()     [HEAVIEST -- string-based, MIGRATE]
    |    CustomSpecDeclarationSyntax.CreateComposed()
    |    CustomSpecDeclarationSyntax.CreateWithConstructor()
    |    (Syntax/CustomSpecDeclarationSyntax.cs)
    |
    +--- PropositionModelSyntax.Create()          [REFERENCE -- already SyntaxFactory, NO CHANGES]
    |    (Syntax/PropositionModelSyntax.cs)
    |
    +--- ClauseNameDeriver.DeriveName()           [HELPER -- pure logic, NO CHANGES]
    |    (ClauseNameDeriver.cs)
    |
    +--- ExpressionNameDeriver.DeriveClassNames() [HELPER -- pure logic, NO CHANGES]
    |    (ExpressionNameDeriver.cs)
    |
    +--- InstanceMethodDetector                   [WALKER -- pure analysis, NO CHANGES]
    |    (InstanceMethodDetector.cs)
    |
    +--- VariableExtractorWalker                  [WALKER -- pure analysis, NO CHANGES]
    |    (VariableExtractorWalker.cs)
    |
    +--- StringExtensions                         [UTILITIES -- partially obsoleted]
    |    (StringExtensions.cs)
    |
    +--- SymbolExtensions                         [UTILITIES -- NO CHANGES]
    |    (SymbolExtensions.cs)
    |
    +--- ExpressionDecomposition                  [DATA TYPE -- may need signature changes]
         (ExpressionDecomposition.cs)
```

## Component Classification

### Components That CHANGE (Migration Targets)

| Component | File | Current Approach | Migration Complexity |
|-----------|------|-----------------|---------------------|
| `SpecInvocationExpressionSyntax` | `Syntax/SpecInvocationSyntax.cs` | Raw string interpolation, `ParseExpression()` | LOW -- single expression |
| `CustomSpecDeclarationSyntax` | `Syntax/CustomSpecDeclarationSyntax.cs` | `StringBuilder` + `ParseCompilationUnit()` | HIGH -- multiple code paths, nested structures |
| `LogicalExpressionToSpecConverter.ReplaceMultiVariableExpression` | `ConvertToSpecCodeFix.cs` | Raw string interpolation, `ParseCompilationUnit()` for temp class | HIGH -- builds whole class + method + constructor |

### Components That DO NOT CHANGE

| Component | File | Reason |
|-----------|------|--------|
| `MotivCodeFixProvider` | `MotivCodeFixProvider.cs` | Entry point only; delegates to converter |
| `PropositionModelSyntax` | `Syntax/PropositionModelSyntax.cs` | Already uses SyntaxFactory (reference impl) |
| `ClauseNameDeriver` | `ClauseNameDeriver.cs` | Pure string derivation from expressions; returns `string` |
| `ExpressionNameDeriver` | `ExpressionNameDeriver.cs` | Pure string derivation from context; returns `(string, string)` |
| `InstanceMethodDetector` | `InstanceMethodDetector.cs` | Pure analysis walker; returns data |
| `VariableExtractorWalker` | `VariableExtractorWalker.cs` | Pure analysis walker; returns data |
| `SymbolExtensions` | `SymbolExtensions.cs` | Type name resolution utility |

### Components That Become PARTIALLY OBSOLETE

| Component | Method | Why Obsolete | Replacement |
|-----------|--------|-------------|-------------|
| `StringExtensions` | `EscapeDoubleQuotes()` | Still needed for WhenTrue/WhenFalse string literals | KEEP |
| `StringExtensions` | `Capitalize()` | Still used by ClauseNameDeriver, ExpressionNameDeriver, ConvertLogicVariablesToModelMemberAccess | KEEP |
| `StringExtensions` | `ToCamelCase()` | Still used by ConvertToSpecCodeFix for field name derivation; also used in CustomSpecDeclarationSyntax | KEEP |
| `CustomSpecDeclarationSyntax` | `ToCamelCase()` (private duplicate) | Duplicates `StringExtensions.ToCamelCase()` | REMOVE -- use extension method |
| `CustomSpecDeclarationSyntax` | `ReplaceInstanceMethodCalls()` | Crude string manipulation for "MethodName(" pattern | REPLACE with SyntaxFactory node replacement |
| `CustomSpecDeclarationSyntax` | `GetParameterNameFromExpression()` | Extracts parameter name via string heuristics | MAY KEEP if still needed for SyntaxFactory parameter naming |
| `ExpressionDecomposition` | `.CompositionExpression` (string) | Currently stores composition as raw string like `"isAgePositive.AndAlso(isName)"` | REPLACE with `ExpressionSyntax` |

## Recommended Architecture (Post-Migration)

The architecture remains structurally identical. The internal signatures and implementations change.

### Key Signature Changes

**ExpressionDecomposition** (currently stores strings, should store syntax nodes):

```
BEFORE:
  Clauses: IReadOnlyList<(string OriginalText, string TransformedText, ExpressionSyntax Expression)>
  CompositionExpression: string   // e.g. "isAgePositive.AndAlso(isName)"

AFTER:
  Clauses: IReadOnlyList<(string OriginalText, ExpressionSyntax TransformedExpression, ExpressionSyntax OriginalExpression)>
  CompositionExpression: ExpressionSyntax   // SyntaxFactory-built invocation chain
```

This is the most significant architectural decision. The `DecomposeExpression` method in `ConvertToSpecCodeFix.cs` currently builds composition strings like `"Clause1.AndAlso(Clause2)"`. After migration, it should build `ExpressionSyntax` nodes using SyntaxFactory.

**However**, there is a critical subtlety: the composition expression uses **variable names** that are only finalized during clause deduplication in `CustomSpecDeclarationSyntax`. The current flow is:

1. `DecomposeExpression` produces raw clause names like `"Clause1"`, `"IsAgePositive"`
2. `DeduplicateClauses` maps original clause indices to deduplicated names
3. `UpdateCompositionWithCamelCaseNames` does string replacement to update names

Converting this to SyntaxFactory means the composition expression must be built with placeholder identifiers that can be swapped via `ReplaceNodes` after deduplication. This is cleaner than the string replacement approach but requires careful ordering.

### CustomSpecDeclarationSyntax Internal Changes

**`Create` (simple spec) -- currently:**
```csharp
var source = $$"""
    public class {{propositionName}}() : Spec<{{modelTypeName}}>(() =>
        Spec.Build(({{modelTypeName}} {{paramName}}) => {{expression}})
            .WhenTrue("({{escaped}}) == true")
            .WhenFalse("({{escaped}}) == false")
            .Create());
    """;
var unit = SyntaxFactory.ParseCompilationUnit(source);
```

**After migration, builds:**
- `ClassDeclaration` with `ParameterList` (primary constructor `()`)
- `BaseList` with `SimpleBaseType` for `Spec<T>`
- Base constructor argument: lambda expression containing `Spec.Build(...).WhenTrue(...).WhenFalse(...).Create()`
- The lambda body reuses the original `ExpressionSyntax` node (already a syntax node)

**`CreateComposed` (multi-clause) -- currently:**
```csharp
var sb = new StringBuilder();
sb.AppendLine($"public class {name}() : Spec<{name}.{model}>(() =>");
sb.AppendLine("{");
foreach (clause in clauses) { sb.AppendLine(clauseDeclaration); }
sb.AppendLine($"    return {composition};");
sb.AppendLine("})");
sb.AppendLine("{");
sb.AppendLine($"    public record {model}({params});");
sb.AppendLine("}");
```

**After migration, builds:**
- `ClassDeclaration` with primary constructor
- `BaseList` with `SimpleBaseType` for `Spec<ClassName.ModelName>`
- Base constructor argument: lambda with block body containing:
  - Local variable declarations for each clause (`var clauseName = Spec.Build(...)...`)
  - Return statement with composition expression
- Class members include nested `RecordDeclaration`

**`CreateWithConstructor` (instance methods) -- same structure as CreateComposed but with:**
- Constructor parameter in class ParameterList: `(ContainingType instance)`
- Lambda body may reference `instance.MethodName(...)` via SyntaxFactory member access

### ConvertToSpecCodeFix Internal Changes

**`ReplaceMultiVariableExpression` -- currently builds a temp class as a string, parses it, extracts members:**
```csharp
var newMethodSource = $$"""
    class Temp
    {
        {{fieldDeclaration}}
        public {{returnType}} {{methodName}}{{params}}
        {
            // {{originalExpr}}
            var result = {{invocation}};
            {{assignment}}
        }
    }
    """;
var tempUnit = ParseCompilationUnit(newMethodSource);
var tempClass = tempUnit.DescendantNodes().OfType<ClassDeclarationSyntax>().First();
```

**After migration, builds directly:**
- `FieldDeclaration` for the spec field
- `MethodDeclaration` with block body containing comment trivia, result variable, and assignment
- Optional `ConstructorDeclaration` for instance method case
- Assembles these into the containing class's member list

This eliminates the "parse a temporary class to extract syntax nodes" anti-pattern.

### SpecInvocationSyntax Internal Changes

**Currently:**
```csharp
var source = $$"""new {{specName}}().Evaluate({{model}}).Satisfied""";
return SyntaxFactory.ParseExpression(source);
```

**After migration, builds:**
```
MemberAccessExpression(                          // .Satisfied
    InvocationExpression(                        // .Evaluate(model)
        MemberAccessExpression(                  // new Spec().Evaluate
            ObjectCreationExpression(specName),  // new Spec()
            "Evaluate"
        ),
        ArgumentList(model)
    ),
    "Satisfied"
)
```

## Integration Points

### Existing Helpers Interact Unchanged

The name derivers (`ClauseNameDeriver`, `ExpressionNameDeriver`) and walkers (`InstanceMethodDetector`, `VariableExtractorWalker`) operate on input expressions and return data (strings, symbols, method lists). They do not participate in code generation. Their interfaces remain stable.

**ClauseNameDeriver.DeriveName**: Returns `string`. Used to name clause variables. This string becomes an `IdentifierName` via `SyntaxFactory.IdentifierName(derivedName.ToCamelCase())`. No interface change needed.

**ExpressionNameDeriver.DeriveClassNames**: Returns `(string, string)`. Used in `MotivCodeFixProvider` before code generation begins. No interface change needed.

**ConvertLogicVariablesToModelMemberAccess**: Already returns `ExpressionSyntax`. This is the cleanest integration point -- it already produces syntax nodes that can be directly embedded in the SyntaxFactory output.

### The ExpressionDecomposition Data Flow

This is the most impactful integration point. Currently:

```
DecomposeExpression (ConvertToSpecCodeFix.cs)
    -> ExpressionDecomposition { Clauses: [(string, string, ExpressionSyntax)], CompositionExpression: string }
        -> CustomSpecDeclarationSyntax.CreateComposed / CreateWithConstructor
            -> DeduplicateClauses (string-based dedup)
            -> UpdateCompositionWithCamelCaseNames (string.Replace)
            -> StringBuilder assembly
            -> ParseCompilationUnit
```

After migration, two strategies are viable:

**Strategy A: Keep strings in ExpressionDecomposition, convert at boundary**
- `DecomposeExpression` continues to produce string-based composition expressions
- `CustomSpecDeclarationSyntax` methods convert strings to syntax nodes at the last step
- Lower risk, lower benefit -- still doing string manipulation internally

**Strategy B: ExpressionDecomposition carries ExpressionSyntax composition (RECOMMENDED)**
- `DecomposeExpression` builds `ExpressionSyntax` composition using SyntaxFactory
- Deduplication uses `ReplaceNodes` on syntax tree instead of `string.Replace`
- Higher upfront effort, but eliminates all string-based composition
- Consistent with the goal of "pure SyntaxFactory"

**Recommendation: Strategy B** because the string replacement in `UpdateCompositionWithCamelCaseNames` is fragile (it can match substrings incorrectly) and because the entire purpose of this milestone is to move away from string-based generation.

### WhenTrue/WhenFalse String Literals

Per PROJECT.md: "Maintain string interpolation only for runtime string literal values (WhenTrue/WhenFalse descriptions, identifiers)."

These are literal string arguments to the Motiv API, not source code generation. They continue to use string interpolation:

```csharp
// This is a C# string literal IN the generated code, not source code generation
var whenTrueArg = $"{originalText.EscapeDoubleQuotes()} == true";
// Then passed to SyntaxFactory as:
SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression,
    SyntaxFactory.Literal(whenTrueArg))
```

`EscapeDoubleQuotes()` and `ToCamelCase()` and `Capitalize()` remain necessary.

## Migration Order (Build Sequence)

The recommended order is driven by dependencies, complexity, and testability.

### Phase 1: SpecInvocationSyntax (Lowest Risk, Foundation)

**File:** `Syntax/SpecInvocationSyntax.cs`
**Complexity:** LOW (3 overloads, 1 private implementation, single expression)
**Risk:** LOW

**What changes:**
- Replace `ParseExpression($$"""...""")` with chained SyntaxFactory calls
- Three public overloads remain, but `CreateInternal` changes from string to SyntaxFactory
- `NormalizeWhitespace()` applied to final result

**Why first:**
- Simplest file with fewest code paths
- Self-contained -- no dependencies on other string-based generators
- Establishes the SyntaxFactory pattern for expressions before tackling declarations
- Tests immediately validate: single-variable spec tests exercise this path

**Test coverage:** `Should_convert_single_variable_boolean_expressions_that_can_be_converted_to_spec`, `Should_derive_name_from_member_access_root`, `Should_derive_common_root_from_multiple_member_accesses`, `Should_derive_name_from_is_expression_variable`

### Phase 2: CustomSpecDeclarationSyntax.Create (Simple Spec)

**File:** `Syntax/CustomSpecDeclarationSyntax.cs` -- only the `Create` / `CreateInternal` methods
**Complexity:** MEDIUM (class declaration with primary constructor, base type, lambda argument)
**Risk:** MEDIUM

**What changes:**
- `CreateInternal` method replaces raw string interpolation + `ParseCompilationUnit` with:
  - `ClassDeclaration` with `ParameterList` (empty primary constructor)
  - `BaseList` with `SimpleBaseType` wrapping a generic `Spec<T>` base invocation
  - The base argument is a `ParenthesizedLambdaExpression` containing the `Spec.Build(...)` chain
- `WhenTrue` / `WhenFalse` string arguments still use string interpolation (per project constraint)

**Why second:**
- Builds on Phase 1 pattern (expressions) and adds class declarations
- Single code path -- no clause decomposition or deduplication
- Tests provide direct validation: single-variable tests that produce `class X() : Spec<T>(() => ...)`

**Key SyntaxFactory construct -- primary constructor with base invocation:**
```
ClassDeclaration(propositionName)
    .WithParameterList(ParameterList())           // primary ctor ()
    .WithBaseList(BaseList(                        // : Spec<T>(...)
        SingletonSeparatedList<BaseTypeSyntax>(
            PrimaryConstructorBaseType(...)        // Spec<T>(() => lambdaBody)
        )))
    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
```

Note: `PrimaryConstructorBaseType` is available in recent Roslyn versions and accepts an `ArgumentList`. If targeting an older Roslyn version via netstandard2.0, this may need `SimpleBaseType` + manual argument list construction. **Verify with the actual Roslyn version in the project.**

**Test coverage:** Same as Phase 1 single-variable tests.

### Phase 3: CustomSpecDeclarationSyntax.CreateComposed (Composed Spec)

**File:** `Syntax/CustomSpecDeclarationSyntax.cs` -- `CreateComposed` / `CreateComposedInternal`
**Complexity:** HIGH
**Risk:** HIGH (clause deduplication, composition expression, nested record)

**What changes:**
- Replace `StringBuilder` assembly with SyntaxFactory construction
- `DeduplicateClauses` may change return type or internal representation
- `UpdateCompositionWithCamelCaseNames` either replaced with `ReplaceNodes` on syntax tree or kept as string-to-syntax bridge
- Nested `RecordDeclaration` built via SyntaxFactory
- Block lambda body with local variable declarations and return statement

**Why third:**
- Most complex code path in the Syntax layer
- Depends on patterns established in Phases 1-2
- After this phase, composed spec tests pass

**Critical construct -- block lambda as base constructor argument:**
```
ParenthesizedLambdaExpression()
    .WithBlock(Block(
        clauseDeclarations...,           // var isX = Spec.Build(...)...
        ReturnStatement(composition)     // return isX.AndAlso(isY);
    ))
```

**Critical construct -- nested record declaration:**
```
RecordDeclaration(Token(SyntaxKind.RecordKeyword), modelName)
    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
    .WithParameterList(ParameterList(recordParams))
    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
```

**Test coverage:** `Should_convert_double_variable_boolean_return_expressions_that_can_be_converted_to_spec`, `Should_convert_double_variable_boolean_expression_statement_that_can_be_converted_to_spec`, `Should_convert_many_nested_clauses_into_a_spec`, `Should_use_fallback_name_for_unrelated_variables`, `Should_deduplicate_identical_clauses_to_avoid_compile_errors`, `Should_deduplicate_clauses_in_or_expressions_with_shared_subclauses`, `Should_deduplicate_clauses_with_less_than_comparison`

### Phase 4: CustomSpecDeclarationSyntax.CreateWithConstructor (Instance Methods)

**File:** `Syntax/CustomSpecDeclarationSyntax.cs` -- `CreateWithConstructor` / `CreateWithConstructorInternal`
**Complexity:** HIGH
**Risk:** MEDIUM (shares patterns with Phase 3, adds constructor parameter)

**What changes:**
- Same as Phase 3 but with constructor parameter `(ContainingType instance)` in class parameter list
- `ReplaceInstanceMethodCalls` string manipulation replaced with SyntaxFactory node transformation
- Instance method references in lambda bodies use `MemberAccessExpression(IdentifierName("instance"), methodName)`

**Why fourth:**
- Shares almost all patterns with Phase 3 (composed clauses, block lambda, nested record)
- Adds only: primary constructor parameter and instance method reference injection
- Lower marginal risk because patterns are established

**Test coverage:** `Should_inject_instance_via_constructor_when_expression_calls_instance_methods`, `Should_convert_multiple_variables_with_instance_methods_and_generate_model`

### Phase 5: ConvertToSpecCodeFix.ReplaceMultiVariableExpression (Orchestrator Cleanup)

**File:** `ConvertToSpecCodeFix.cs` -- `ReplaceMultiVariableExpression` method
**Complexity:** MEDIUM-HIGH
**Risk:** MEDIUM

**What changes:**
- Replace the "build temp class as string, parse it, extract members" pattern
- Build `FieldDeclarationSyntax`, `MethodDeclarationSyntax`, and optional `ConstructorDeclarationSyntax` directly
- Comment trivia for original expression added via `SyntaxFactory.Comment($"// {originalExprText}")`
- Assignment statement built via SyntaxFactory

**Why last:**
- Depends on patterns from all prior phases
- This is the orchestrator -- it calls the Syntax layer methods that were migrated in Phases 1-4
- After this phase, the "parse temporary class" anti-pattern is fully eliminated

**The "temp class" anti-pattern being eliminated:**
Currently, `ReplaceMultiVariableExpression` builds an entire fake class as a string, parses it, then extracts the field, method, and constructor from the parsed tree. This is the most egregious string-based pattern because it uses Roslyn's parser as a string-to-syntax converter rather than building syntax directly.

**Test coverage:** All multi-variable tests exercise this path.

### Phase 6: Cleanup (Post-Migration)

**What changes:**
- Remove `CustomSpecDeclarationSyntax.ToCamelCase()` private method (duplicate of `StringExtensions.ToCamelCase()`)
- Remove `CustomSpecDeclarationSyntax.ReplaceInstanceMethodCalls()` if replaced by node transformation
- Remove `CustomSpecDeclarationSyntax.GetParameterNameFromExpression()` if no longer needed
- Remove `UpdateCompositionWithCamelCaseNames` and `UpdateCompositionWithDeduplicatedNames` string-replacement methods
- Consider whether `ExpressionDecomposition.CompositionExpression` should change type from `string` to `ExpressionSyntax`
- Verify all tests still pass
- Run full solution build

## Risk Assessment

| Phase | Risk | Primary Risk Factor | Mitigation |
|-------|------|-------------------|------------|
| 1: SpecInvocationSyntax | LOW | Simple expression chain | Direct SyntaxFactory mapping; tests verify |
| 2: Create (simple) | MEDIUM | Primary constructor + base type in SyntaxFactory | Use Roslyn Quoter to verify tree structure; `PropositionModelSyntax` as reference |
| 3: CreateComposed | HIGH | Clause dedup + composition expression + nested record | Migrate iteratively within the method; keep string-based bridge if needed |
| 4: CreateWithConstructor | MEDIUM | Instance method reference injection | Shares patterns with Phase 3; incremental addition |
| 5: ReplaceMultiVariable | MEDIUM | Building field + method + constructor directly | Well-understood SyntaxFactory patterns by this point |
| 6: Cleanup | LOW | Removing dead code | Tests catch regressions |

### Cross-Cutting Risks

**Whitespace / Trivia Fidelity:** SyntaxFactory-generated nodes have no trivia by default. The current string-based approach inherits formatting from the parser. After migration, `NormalizeWhitespace()` must be applied to ensure readable output. The tests compare exact string output, so whitespace differences will cause test failures. **Mitigation:** Apply `NormalizeWhitespace()` at the final output boundary (the returned `TypeDeclarationSyntax` or `ExpressionSyntax`), not on intermediate nodes.

**Primary Constructor Syntax in netstandard2.0 Roslyn:** The CodeFix targets netstandard2.0. The Roslyn version available may not have `PrimaryConstructorBaseType`. If so, the primary constructor pattern needs to be constructed using `ClassDeclaration` overloads that accept `ParameterList` directly. **Mitigation:** Check actual Roslyn API surface available in the project's referenced `Microsoft.CodeAnalysis.CSharp` version. The `ClassDeclaration` method has an overload accepting `parameterList` directly, which has been available since C# 12 support was added.

**Record Declaration Syntax:** `RecordDeclaration` requires specific token kinds (`SyntaxKind.RecordKeyword`). If the Roslyn version predates record support, this is a blocker. **Mitigation:** The existing `PropositionModelSyntax.cs` does NOT use `RecordDeclaration` -- it builds a class with constructor + properties. The current string-based code generates `public record Model(...)` which is only available in C# 9+. Verify that the test infrastructure supports record syntax and that the Roslyn version supports `RecordDeclaration`.

## Anti-Patterns to Avoid

### Anti-Pattern 1: Hybrid String-SyntaxFactory Approach
**What:** Migrating half of a method to SyntaxFactory and leaving the other half as strings, then joining them.
**Why bad:** Creates two maintenance paradigms in one method; string portions drift from SyntaxFactory portions.
**Instead:** Migrate each method completely. If a method is too large, extract sub-methods first (refactor to decompose, then migrate each sub-method).

### Anti-Pattern 2: Building Strings to Pass to ParseExpression/ParseStatement
**What:** Using SyntaxFactory for the outer structure but building inner expressions as strings and parsing them.
**Why bad:** Defeats the purpose of the migration. The generated expression won't participate in Roslyn's syntax tree properly.
**Instead:** Build expressions bottom-up with SyntaxFactory. The `ConvertLogicVariablesToModelMemberAccess` method already returns `ExpressionSyntax` -- use those nodes directly.

### Anti-Pattern 3: NormalizeWhitespace on Every Node
**What:** Calling `.NormalizeWhitespace()` on each individual syntax node during construction.
**Why bad:** Normalizing intermediate nodes can produce incorrect formatting when they are composed into larger structures. The final NormalizeWhitespace call reformats everything.
**Instead:** Call `NormalizeWhitespace()` once at the final output boundary -- the returned `TypeDeclarationSyntax`.

### Anti-Pattern 4: Replacing All String Usage
**What:** Converting WhenTrue/WhenFalse literal strings, identifier names, and other runtime values to non-string representations.
**Why bad:** These are C# string literals in the generated code, not source code templates. String interpolation is the correct tool.
**Instead:** Use string interpolation for values that become string literals in the output. Use SyntaxFactory for structural code (declarations, statements, expressions).

## Patterns to Follow

### Pattern 1: Bottom-Up Construction (from PropositionModelSyntax.cs)
Build the innermost nodes first, then compose outward:
```
Parameter -> ParameterList -> ConstructorDeclaration
TypeName -> PropertyDeclaration
Constructor + Properties -> ClassDeclaration
```
**Already demonstrated in the codebase at:** `Syntax/PropositionModelSyntax.cs`

### Pattern 2: Expression Reuse
The original `ExpressionSyntax` from the user's code (e.g., `value > 0`) can be embedded directly into the generated Spec.Build lambda. No need to serialize it to string and re-parse. `ConvertLogicVariablesToModelMemberAccess` already returns an `ExpressionSyntax` that can be placed directly as the lambda body.

### Pattern 3: Trivia-Aware Comment Injection
For preserving the original expression as a comment:
```csharp
var comment = SyntaxFactory.Comment($"// {originalExprText}");
var commentTrivia = SyntaxFactory.TriviaList(comment, SyntaxFactory.CarriageReturnLineFeed);
firstStatement.WithLeadingTrivia(commentTrivia);
```

### Pattern 4: Identifier Name to SyntaxFactory Bridging
String-based helper results (from ClauseNameDeriver, ExpressionNameDeriver) bridge to SyntaxFactory cleanly:
```csharp
var derivedName = ClauseNameDeriver.DeriveName(expr, counter);
var identifierSyntax = SyntaxFactory.IdentifierName(derivedName.ToCamelCase());
```

## String Utility Disposition

| Utility | Current Users | Post-Migration Status |
|---------|--------------|----------------------|
| `ToCamelCase()` | ConvertToSpecCodeFix (field naming), CustomSpecDeclarationSyntax (clause var naming) | KEEP -- still needed for identifier name construction |
| `Capitalize()` | ClauseNameDeriver, ExpressionNameDeriver, ConvertLogicVariablesToModelMemberAccess | KEEP -- still needed for identifier derivation |
| `EscapeDoubleQuotes()` | CustomSpecDeclarationSyntax (WhenTrue/WhenFalse strings) | KEEP -- still needed for string literal content |
| `ToCamelCase()` (private in CustomSpecDeclarationSyntax) | Internal to CustomSpecDeclarationSyntax | REMOVE -- use `StringExtensions.ToCamelCase()` instead |

## Scalability Considerations

Not applicable to this migration. The CodeFix executes once per user invocation in the IDE. Performance is not a concern -- the current string-based approach with `ParseCompilationUnit` is actually slower than direct SyntaxFactory construction because it involves a full parse pass. SyntaxFactory is O(n) construction vs O(n log n) parsing.

## Sources

### Primary (HIGH confidence)
- **Existing codebase** -- `PropositionModelSyntax.cs` demonstrates the target SyntaxFactory pattern in this exact project
- **Existing codebase** -- All 10 tests in `MotivConvertToSpecTests.cs` define exact expected output for every code path
- **Existing codebase** -- `ConvertToSpecCodeFix.cs`, `CustomSpecDeclarationSyntax.cs`, `SpecInvocationSyntax.cs` -- actual source of string-based generation being replaced
- [Microsoft Learn -- Syntax Transformation](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/get-started/syntax-transformation) -- Official guide for SyntaxFactory patterns
- [SyntaxFactory.ClassDeclaration API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntaxfactory.classdeclaration?view=roslyn-dotnet-4.7.0) -- Official API reference

### Secondary (MEDIUM confidence)
- [Generating C# code with Roslyn APIs (Jeremy Davis, 2024)](https://blog.jermdavis.dev/posts/2024/csharp-code-with-roslyn) -- Practical SyntaxFactory patterns and tradeoffs
- [Roslyn NormalizeWhitespace performance issue](https://github.com/dotnet/roslyn/issues/54144) -- Documents NormalizeWhitespace behavior
- [Roslyn CSharpSyntaxGenerator source](https://github.com/dotnet/roslyn/blob/main/src/Workspaces/CSharp/Portable/CodeGeneration/CSharpSyntaxGenerator.cs) -- Roslyn's own code generator uses SyntaxFactory patterns
- [Code Generation with Roslyn (Strumenta)](https://tomassetti.me/code-generation-with-roslyn-a-skeleton-class-from-uml/) -- SyntaxFactory vs ParseText tradeoffs
- [SyntaxFactory source on GitHub](https://github.com/dotnet/roslyn/blob/main/src/Compilers/CSharp/Portable/Syntax/SyntaxFactory.cs) -- Reference for available factory methods

### Tooling (Recommended)
- **Roslyn Quoter** (roslynquoter.azurewebsites.net) -- Paste target C# code, get SyntaxFactory calls. Essential for verifying the correct tree structure for complex constructs like primary constructors with base types.
