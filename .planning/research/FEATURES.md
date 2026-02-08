# Feature Landscape: SyntaxFactory Code Generation Patterns

**Domain:** Roslyn CodeFix migration from string-based to SyntaxFactory-based code generation
**Researched:** 2026-02-08
**Overall Confidence:** HIGH (patterns verified against official Roslyn API docs and existing reference implementation in PropositionModelSyntax.cs)

## Table Stakes

Features that must be implemented for the migration to succeed. Without these, the refactored code cannot produce the same output as the current string-based approach.

### TS-1: Class Declaration with Primary Constructor and Base Type

**What:** Generate `public class PropositionName() : Spec<ModelType>(() => ...)` using SyntaxFactory
**Why Expected:** This is the core output structure. Every generated spec class uses this pattern.
**Complexity:** HIGH
**Current Implementation:** String interpolation in `CustomSpecDeclarationSyntax.CreateInternal()` and `CreateComposedInternal()` builds the entire class as a raw string, then calls `SyntaxFactory.ParseCompilationUnit()` to parse it back into a syntax tree.

**SyntaxFactory Pattern:**
```csharp
// Use the 8-parameter overload that includes parameterList for primary constructors
SyntaxFactory.ClassDeclaration(
    attributeLists: default,
    modifiers: SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)),
    identifier: SyntaxFactory.Identifier(propositionName),
    typeParameterList: null,
    parameterList: SyntaxFactory.ParameterList(/* primary constructor params */),
    baseList: SyntaxFactory.BaseList(
        SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(
            SyntaxFactory.SimpleBaseType(
                SyntaxFactory.GenericName("Spec")
                    .WithTypeArgumentList(
                        SyntaxFactory.TypeArgumentList(
                            SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                SyntaxFactory.ParseTypeName(modelType))))))),
    constraintClauses: default,
    members: /* class body members */)
```

**Key Details:**
- The `ClassDeclaration` overload with `ParameterListSyntax?` parameter supports C# 12 primary constructors (confirmed via official docs)
- For the simple case (no body), use `.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))` instead of open/close braces
- For composed specs with record body, use `.WithOpenBraceToken()` and `.WithCloseBraceToken()` plus `.WithMembers()` for the nested record
- The base type `Spec<T>` requires `GenericName` with `TypeArgumentList`, not just `ParseTypeName`
- When the base type constructor takes a lambda argument (the `() => ...` part), this is part of the primary constructor's `ArgumentList` on the `SimpleBaseType`, OR more precisely, the base list entry needs a `PrimaryConstructorBaseTypeSyntax` pattern

**Dependencies:** TS-6 (generic type references), TS-3 (lambda for the factory body)

**Confidence:** HIGH -- ClassDeclaration overload with parameterList confirmed in [official Roslyn docs](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntaxfactory.classdeclaration?view=roslyn-dotnet-4.7.0)

---

### TS-2: Record Declaration with Parameter List

**What:** Generate `public record ModelName(int ValueA, string ValueB);` using SyntaxFactory
**Why Expected:** Multi-variable specs generate a nested record type to aggregate model properties.
**Complexity:** MEDIUM
**Current Implementation:** String interpolation `$"public record {param.ModelName}({param.RecordParameters});"` inside StringBuilder, then parsed.

**SyntaxFactory Pattern:**
```csharp
SyntaxFactory.RecordDeclaration(
    SyntaxKind.RecordDeclaration,
    attributeLists: default,
    modifiers: SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)),
    keyword: SyntaxFactory.Token(SyntaxKind.RecordKeyword),
    identifier: SyntaxFactory.Identifier(modelName),
    typeParameterList: null,
    parameterList: SyntaxFactory.ParameterList(
        SyntaxFactory.SeparatedList(
            parameters.Select(p =>
                SyntaxFactory.Parameter(SyntaxFactory.Identifier(p.Name))
                    .WithType(SyntaxFactory.ParseTypeName(p.TypeName))))),
    baseList: null,
    constraintClauses: default,
    members: default)
.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
```

**Key Details:**
- `RecordDeclaration` requires `SyntaxKind.RecordDeclaration` as the first parameter (not `RecordClassDeclaration` or `RecordStructDeclaration` -- those are for `record class` and `record struct`)
- The `keyword` parameter takes `SyntaxFactory.Token(SyntaxKind.RecordKeyword)`
- The `classOrStructKeyword` parameter (in some overloads) should be `default` for plain `record` declarations
- Parameters use the same `ParameterList` / `Parameter` APIs as method parameters
- Must end with `.WithSemicolonToken()` for positional records without a body
- The existing `PropositionModelSyntax.cs` already demonstrates parameter creation patterns -- reuse that knowledge

**Dependencies:** None -- standalone construct

**Confidence:** HIGH -- RecordDeclaration API confirmed in [official docs](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntaxfactory.recorddeclaration?view=roslyn-dotnet-4.3.0)

---

### TS-3: Lambda Expressions with Block Body

**What:** Generate `() => { var clause1 = ...; return composition; }` and `(ModelType m) => expression` using SyntaxFactory
**Why Expected:** The factory lambda is the core of every Spec declaration. Simple specs use expression lambdas; composed specs use block-body lambdas.
**Complexity:** MEDIUM
**Current Implementation:** String building constructs the lambda body line-by-line, then the whole class is parsed.

**SyntaxFactory Pattern (expression lambda):**
```csharp
SyntaxFactory.ParenthesizedLambdaExpression(
    SyntaxFactory.ParameterList(
        SyntaxFactory.SingletonSeparatedList(
            SyntaxFactory.Parameter(SyntaxFactory.Identifier(paramName))
                .WithType(SyntaxFactory.ParseTypeName(typeName)))),
    bodyExpression)
```

**SyntaxFactory Pattern (block lambda):**
```csharp
SyntaxFactory.ParenthesizedLambdaExpression(
    SyntaxFactory.ParameterList(),  // empty () for factory lambda
    SyntaxFactory.Block(
        /* local variable declarations */
        clauseStatements,
        /* return statement */
        SyntaxFactory.ReturnStatement(compositionExpression)))
```

**Key Details:**
- `ParenthesizedLambdaExpression` accepts either a `BlockSyntax` (for `() => { ... }`) or an `ExpressionSyntax` (for `() => expr`)
- When the lambda has no parameters, use `SyntaxFactory.ParameterList()` for `()`
- When the lambda has typed parameters like `(int value) => ...`, create the parameter with `.WithType()`
- The block body contains `LocalDeclarationStatement` nodes for `var clause1 = ...` and a `ReturnStatement` for the composition

**Dependencies:** TS-4 (for the fluent chain inside the block), TS-5 (for member access in lambda body)

**Confidence:** HIGH -- ParenthesizedLambdaExpression confirmed in [Roslyn source](https://github.com/dotnet/roslyn/blob/main/src/Workspaces/CSharp/Portable/CodeGeneration/CSharpSyntaxGenerator.cs)

---

### TS-4: Fluent Method Chain (InvocationExpression with MemberAccessExpression)

**What:** Generate `Spec.Build(lambda).WhenTrue("...").WhenFalse("...").Create()` and `clause1.AndAlso(clause2)` using SyntaxFactory
**Why Expected:** Every clause in the spec body uses the Spec.Build().WhenTrue().WhenFalse().Create() fluent chain. Composition uses .AndAlso(), .OrElse() chains.
**Complexity:** HIGH
**Current Implementation:** StringBuilder appends each fluent call as a separate line.

**SyntaxFactory Pattern (chained calls -- bottom-up construction):**
```csharp
// Build from inside out: Spec.Build(lambda).WhenTrue("...").WhenFalse("...").Create()

// Step 1: Spec.Build(lambda)
var specBuild = SyntaxFactory.InvocationExpression(
    SyntaxFactory.MemberAccessExpression(
        SyntaxKind.SimpleMemberAccessExpression,
        SyntaxFactory.IdentifierName("Spec"),
        SyntaxFactory.IdentifierName("Build")),
    SyntaxFactory.ArgumentList(
        SyntaxFactory.SingletonSeparatedList(
            SyntaxFactory.Argument(lambdaExpression))));

// Step 2: .WhenTrue("...")
var whenTrue = SyntaxFactory.InvocationExpression(
    SyntaxFactory.MemberAccessExpression(
        SyntaxKind.SimpleMemberAccessExpression,
        specBuild,
        SyntaxFactory.IdentifierName("WhenTrue")),
    SyntaxFactory.ArgumentList(
        SyntaxFactory.SingletonSeparatedList(
            SyntaxFactory.Argument(
                SyntaxFactory.LiteralExpression(
                    SyntaxKind.StringLiteralExpression,
                    SyntaxFactory.Literal(whenTrueText))))));

// Step 3: .WhenFalse("...")
var whenFalse = SyntaxFactory.InvocationExpression(
    SyntaxFactory.MemberAccessExpression(
        SyntaxKind.SimpleMemberAccessExpression,
        whenTrue,
        SyntaxFactory.IdentifierName("WhenFalse")),
    SyntaxFactory.ArgumentList(
        SyntaxFactory.SingletonSeparatedList(
            SyntaxFactory.Argument(
                SyntaxFactory.LiteralExpression(
                    SyntaxKind.StringLiteralExpression,
                    SyntaxFactory.Literal(whenFalseText))))));

// Step 4: .Create()
var create = SyntaxFactory.InvocationExpression(
    SyntaxFactory.MemberAccessExpression(
        SyntaxKind.SimpleMemberAccessExpression,
        whenFalse,
        SyntaxFactory.IdentifierName("Create")));
```

**Key Details:**
- Fluent chains are constructed bottom-up: the innermost call is the receiver of the next
- Each `.Method(args)` is an `InvocationExpression` wrapping a `MemberAccessExpression` on the previous result
- `SyntaxFactory.Literal(string)` handles escaping automatically -- no need for manual `EscapeDoubleQuotes()` when using `SyntaxFactory.Literal()`
- Composition operators (`clause1.AndAlso(clause2)`) follow the same InvocationExpression pattern but with the clause variable as receiver
- The `!` operator (for negation) uses `SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, operand)`
- The `^` operator uses `SyntaxFactory.BinaryExpression(SyntaxKind.ExclusiveOrExpression, left, right)`

**Dependencies:** TS-3 (lambda as argument to Build), string literal creation

**Confidence:** HIGH -- InvocationExpression/MemberAccessExpression pattern well-documented in [Roslyn examples](https://johnkoerner.com/csharp/creating-code-using-the-syntax-factory/) and confirmed in Roslyn source

---

### TS-5: Member Access Chains (a.B.C)

**What:** Generate `m.ValueA`, `m.Order.Total`, `instance.IsGreen(text)` using SyntaxFactory
**Why Expected:** Model property access in lambda bodies, instance method qualification.
**Complexity:** LOW
**Current Implementation:** Already partially done via `ConvertLogicVariablesToModelMemberAccess()` which uses `SyntaxFactory.MemberAccessExpression()` and `ReplaceNodes()`. This is the ONE area that already uses SyntaxFactory correctly.

**SyntaxFactory Pattern:**
```csharp
// m.ValueA
SyntaxFactory.MemberAccessExpression(
    SyntaxKind.SimpleMemberAccessExpression,
    SyntaxFactory.IdentifierName("m"),
    SyntaxFactory.IdentifierName("ValueA"))

// m.Order.Total (nested)
SyntaxFactory.MemberAccessExpression(
    SyntaxKind.SimpleMemberAccessExpression,
    SyntaxFactory.MemberAccessExpression(
        SyntaxKind.SimpleMemberAccessExpression,
        SyntaxFactory.IdentifierName("m"),
        SyntaxFactory.IdentifierName("Order")),
    SyntaxFactory.IdentifierName("Total"))
```

**Key Details:**
- `ConvertLogicVariablesToModelMemberAccess()` in ConvertToSpecCodeFix.cs already uses this pattern correctly
- `RebuildMemberAccessChain()` already handles arbitrary depth member access chains
- The existing implementation is the reference for how to do this right
- No migration needed for the expression transformation logic itself -- only the surrounding code that consumes the transformed expressions

**Dependencies:** None -- already implemented

**Confidence:** HIGH -- verified in existing codebase (ConvertToSpecCodeFix.cs lines 195-261)

---

### TS-6: Generic Type References (Spec\<T\>)

**What:** Generate `Spec<ModelType>`, `Spec<Proposition.Model>` using SyntaxFactory
**Why Expected:** Every base type in the generated class uses a generic Spec type reference.
**Complexity:** LOW
**Current Implementation:** Embedded in string interpolation `$"Spec<{modelType}>"`.

**SyntaxFactory Pattern:**
```csharp
// Spec<int>
SyntaxFactory.GenericName("Spec")
    .WithTypeArgumentList(
        SyntaxFactory.TypeArgumentList(
            SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword)))))

// Spec<Proposition.Model> (qualified name)
SyntaxFactory.GenericName("Spec")
    .WithTypeArgumentList(
        SyntaxFactory.TypeArgumentList(
            SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                SyntaxFactory.QualifiedName(
                    SyntaxFactory.IdentifierName("Proposition"),
                    SyntaxFactory.IdentifierName("Model")))))

// Spec<MyNamespace.Order> (fully qualified)
SyntaxFactory.GenericName("Spec")
    .WithTypeArgumentList(
        SyntaxFactory.TypeArgumentList(
            SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                SyntaxFactory.ParseTypeName("MyNamespace.Order"))))
```

**Key Details:**
- `GenericName` is for the unbound generic like `Spec<>`, with `TypeArgumentList` providing the type arguments
- `ParseTypeName()` is acceptable for model type names that come from semantic model (already strings) -- this is NOT string-based code gen, it is parsing a type name string into a TypeSyntax node
- For simple types like `int`, `string`, `bool`, use `SyntaxFactory.PredefinedType()` with the corresponding keyword token
- For qualified names like `Proposition.Model`, either `ParseTypeName()` or `QualifiedName()` works; `ParseTypeName()` is simpler and acceptable here since the input is already a validated type name from the semantic model

**Dependencies:** None

**Confidence:** HIGH -- GenericName pattern confirmed in [Roslyn source](https://github.com/dotnet/roslyn/blob/main/src/Workspaces/CSharp/Portable/CodeGeneration/CSharpSyntaxGenerator.cs)

---

### TS-7: Object Creation Expressions (new SpecName())

**What:** Generate `new PropositionName()`, `new PropositionName(this)`, `new PropositionName.Model(valueA, valueB)` using SyntaxFactory
**Why Expected:** Spec invocation sites create instances of generated spec classes and model records.
**Complexity:** LOW
**Current Implementation:** String interpolation in `SpecInvocationExpressionSyntax` and `ConvertToSpecCodeFix.ReplaceMultiVariableExpression()`.

**SyntaxFactory Pattern:**
```csharp
// new PropositionName()
SyntaxFactory.ObjectCreationExpression(
    SyntaxFactory.IdentifierName("PropositionName"))
    .WithArgumentList(SyntaxFactory.ArgumentList())

// new PropositionName(this)
SyntaxFactory.ObjectCreationExpression(
    SyntaxFactory.IdentifierName("PropositionName"))
    .WithArgumentList(
        SyntaxFactory.ArgumentList(
            SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.Argument(SyntaxFactory.ThisExpression()))))

// new PropositionName.Model(valueA, valueB)
SyntaxFactory.ObjectCreationExpression(
    SyntaxFactory.QualifiedName(
        SyntaxFactory.IdentifierName("PropositionName"),
        SyntaxFactory.IdentifierName("Model")))
    .WithArgumentList(
        SyntaxFactory.ArgumentList(
            SyntaxFactory.SeparatedList(
                new[] { "valueA", "valueB" }.Select(name =>
                    SyntaxFactory.Argument(SyntaxFactory.IdentifierName(name))))))
```

**Key Details:**
- `ObjectCreationExpression` takes a `TypeSyntax` (the type being created) and optional `ArgumentList`
- For nested types like `Proposition.Model`, use `QualifiedName` as the type
- For parameterless construction, still provide `ArgumentList()` to get the `()` in output
- `this` is represented by `SyntaxFactory.ThisExpression()`

**Dependencies:** None

**Confidence:** HIGH -- ObjectCreationExpression pattern confirmed in [Roslyn examples](https://blog.jermdavis.dev/posts/2024/csharp-code-with-roslyn)

---

### TS-8: Spec Invocation Chain (new Spec().IsSatisfiedBy(model).Satisfied)

**What:** Generate `new SpecName().IsSatisfiedBy(value).Satisfied` using SyntaxFactory
**Why Expected:** The invocation site that replaces the original boolean expression.
**Complexity:** MEDIUM
**Current Implementation:** String interpolation in `SpecInvocationExpressionSyntax.CreateInternal()`.

**SyntaxFactory Pattern:**
```csharp
// new SpecName().IsSatisfiedBy(value).Satisfied
SyntaxFactory.MemberAccessExpression(
    SyntaxKind.SimpleMemberAccessExpression,
    SyntaxFactory.InvocationExpression(
        SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.ObjectCreationExpression(
                SyntaxFactory.IdentifierName(specName))
                .WithArgumentList(SyntaxFactory.ArgumentList()),
            SyntaxFactory.IdentifierName("IsSatisfiedBy")),
        SyntaxFactory.ArgumentList(
            SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.Argument(modelExpression)))),
    SyntaxFactory.IdentifierName("Satisfied"))
```

**Key Details:**
- This is a compound expression: ObjectCreation -> MemberAccess -> Invocation -> MemberAccess
- Build inside-out: `new Spec()` first, then `.IsSatisfiedBy(model)`, then `.Satisfied`
- The `model` argument can be an `IdentifierNameSyntax` (single variable) or `ObjectCreationExpression` (new Model(...))
- `.Satisfied` is a property access (MemberAccess without Invocation), not a method call

**Dependencies:** TS-5 (member access), TS-7 (object creation)

**Confidence:** HIGH -- composition of well-established patterns

---

### TS-9: Local Variable Declarations in Block Lambda

**What:** Generate `var clause1 = Spec.Build(...).WhenTrue("...").WhenFalse("...").Create();` as a statement
**Why Expected:** Composed specs declare local variables for each clause inside the factory lambda block.
**Complexity:** MEDIUM
**Current Implementation:** StringBuilder appends `$"var {camelCaseName} = Spec.Build(...)..."` lines.

**SyntaxFactory Pattern:**
```csharp
SyntaxFactory.LocalDeclarationStatement(
    SyntaxFactory.VariableDeclaration(
        SyntaxFactory.IdentifierName("var"))
    .WithVariables(
        SyntaxFactory.SingletonSeparatedList(
            SyntaxFactory.VariableDeclarator(
                SyntaxFactory.Identifier(camelCaseName))
            .WithInitializer(
                SyntaxFactory.EqualsValueClause(
                    fluentChainExpression))))) // from TS-4
```

**Key Details:**
- `var` is represented as `SyntaxFactory.IdentifierName("var")` in the `VariableDeclaration`
- Each variable declaration wraps the entire fluent chain as its initializer
- The statement needs `.WithSemicolonToken()` or the semicolon is auto-generated

**Dependencies:** TS-4 (the fluent chain is the initializer value)

**Confidence:** HIGH -- well-established pattern, used extensively in Roslyn code fixes

---

### TS-10: Field Declarations and Constructor Injection

**What:** Generate `private readonly Proposition _proposition = new Proposition();` and constructor with field initialization
**Why Expected:** Multi-variable specs inject a field and (for instance methods) a constructor into the containing class.
**Complexity:** MEDIUM
**Current Implementation:** String interpolation builds field and constructor as raw strings in `ReplaceMultiVariableExpression()`.

**SyntaxFactory Pattern:**
```csharp
// Field declaration: private readonly Proposition _proposition = new Proposition();
SyntaxFactory.FieldDeclaration(
    SyntaxFactory.VariableDeclaration(
        SyntaxFactory.IdentifierName(propositionName))
    .WithVariables(
        SyntaxFactory.SingletonSeparatedList(
            SyntaxFactory.VariableDeclarator(
                SyntaxFactory.Identifier(fieldName))
            .WithInitializer(
                SyntaxFactory.EqualsValueClause(
                    SyntaxFactory.ObjectCreationExpression(
                        SyntaxFactory.IdentifierName(propositionName))
                    .WithArgumentList(SyntaxFactory.ArgumentList()))))))
.WithModifiers(SyntaxFactory.TokenList(
    SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
    SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)))
```

**Key Details:**
- For instance method case, the field has no initializer (initialized in constructor)
- Constructor body assigns `_field = new Proposition(this);`
- The existing `PropositionModelSyntax.cs` already demonstrates constructor creation pattern (reference implementation)

**Dependencies:** TS-7 (object creation for initializer)

**Confidence:** HIGH -- PropositionModelSyntax.cs is the reference implementation

---

### TS-11: Method Body Replacement (Comment + Result + Assignment)

**What:** Generate method body with comment, result variable, and assignment statement
**Why Expected:** Multi-variable specs replace the method body with a comment of the original expression, a result variable, and an assignment.
**Complexity:** MEDIUM
**Current Implementation:** String interpolation in `ReplaceMultiVariableExpression()` builds entire method body as a raw string.

**SyntaxFactory Pattern:**
```csharp
// // originalExpression
// var result = _proposition.IsSatisfiedBy(new Proposition.Model(a, b));
// return result.Satisfied;

var comment = SyntaxFactory.Comment($"// {originalExprText}");
var resultDecl = SyntaxFactory.LocalDeclarationStatement(
    SyntaxFactory.VariableDeclaration(
        SyntaxFactory.IdentifierName("var"))
    .WithVariables(
        SyntaxFactory.SingletonSeparatedList(
            SyntaxFactory.VariableDeclarator("result")
            .WithInitializer(
                SyntaxFactory.EqualsValueClause(invocationExpression)))))
    .WithLeadingTrivia(comment, SyntaxFactory.CarriageReturnLineFeed);

var returnStmt = SyntaxFactory.ReturnStatement(
    SyntaxFactory.MemberAccessExpression(
        SyntaxKind.SimpleMemberAccessExpression,
        SyntaxFactory.IdentifierName("result"),
        SyntaxFactory.IdentifierName("Satisfied")));
```

**Key Details:**
- Comments are trivia attached to the next statement's leading trivia
- The assignment line varies by context (return, local decl, assignment) -- switch expression maps statement kind to output
- `SyntaxFactory.Comment()` creates single-line comment trivia
- Method replacement uses `.WithExpressionBody(null).WithBody(Block(...))` for expression-bodied methods

**Dependencies:** TS-7 (model object creation), TS-5 (member access for result.Satisfied)

**Confidence:** HIGH -- trivia handling is well-documented in [Roslyn SDK docs](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/get-started/syntax-transformation)

---

### TS-12: String Literals with Proper Escaping

**What:** Generate `"(expression) == true"` and `"(expression) == false"` string literals with properly escaped content
**Why Expected:** WhenTrue/WhenFalse descriptions include the original expression text, which may contain double quotes.
**Complexity:** LOW
**Current Implementation:** Manual `EscapeDoubleQuotes()` extension method in StringExtensions.cs.

**SyntaxFactory Pattern:**
```csharp
// SyntaxFactory.Literal handles escaping automatically
SyntaxFactory.LiteralExpression(
    SyntaxKind.StringLiteralExpression,
    SyntaxFactory.Literal($"({originalExpression}) == true"))
```

**Key Details:**
- `SyntaxFactory.Literal(string)` automatically handles double-quote escaping in the generated code -- the manual `EscapeDoubleQuotes()` method becomes unnecessary
- Pass the raw string value (with actual double quotes, not escaped), and Roslyn will produce the correct escaped string literal in output
- This is a genuine improvement over the current approach
- The `StringExtensions.EscapeDoubleQuotes()` method can be removed after migration

**Dependencies:** None

**Confidence:** HIGH -- confirmed in [SyntaxFactory.Literal docs](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntaxfactory.literal?view=roslyn-dotnet-4.9.0)

---

### TS-13: Using Directive Generation

**What:** Generate `using Motiv;` directive
**Why Expected:** Generated code requires the Motiv namespace import.
**Complexity:** LOW
**Current Implementation:** Already uses SyntaxFactory via `SyntaxFactory.UsingDirective(SyntaxFactory.IdentifierName("Motiv"))` in `AddUsingStatementsIfNeeded()`.

**No migration needed.** This code already uses SyntaxFactory correctly.

**Dependencies:** None

**Confidence:** HIGH -- already implemented

---

### TS-14: Whitespace and Formatting

**What:** Ensure generated code has proper indentation and line breaks
**Why Expected:** Generated code must match the expected test output exactly.
**Complexity:** MEDIUM
**Current Implementation:** `NormalizeWhitespace()` is called on the final syntax tree after parsing from string. The string approach inherently manages line breaks through `AppendLine()`.

**SyntaxFactory Pattern:**
- Call `.NormalizeWhitespace()` on the completed syntax tree before returning
- For selective formatting, annotate nodes with `Formatter.Annotation` (but this requires workspace context, which is available in the code fix)
- Do NOT call `NormalizeWhitespace()` after attaching trivia -- it will overwrite trivia

**Key Details:**
- `NormalizeWhitespace()` normalizes all whitespace to standard indentation (4 spaces per level)
- It is the simplest approach and is already used in `PropositionModelSyntax.cs`
- The current tests expect specific formatting; `.NormalizeWhitespace()` should produce equivalent output
- If indentation issues arise, consider `Formatter.Format()` with workspace options as fallback

**Dependencies:** All other features (this is applied last)

**Confidence:** MEDIUM -- `NormalizeWhitespace()` is straightforward but may produce slightly different whitespace than the current string-based approach. Tests will catch discrepancies.

---

## Differentiators

Patterns that improve on the current string approach. Not required for migration parity, but provide genuine value.

### DF-1: Automatic String Escaping via SyntaxFactory.Literal

**Value Proposition:** Eliminates the manual `EscapeDoubleQuotes()` utility entirely. `SyntaxFactory.Literal()` handles all escaping correctly by design.
**Complexity:** Already addressed in TS-12
**Notes:** This means the `EscapeDoubleQuotes()` extension method in StringExtensions.cs becomes dead code and can be removed. This reduces the surface area for bugs in string literal generation.

---

### DF-2: Structured Error Prevention

**Value Proposition:** SyntaxFactory construction is type-checked at compile time. String interpolation errors (missing quotes, unbalanced braces, typos in keywords) become impossible.
**Complexity:** N/A -- inherent benefit of SyntaxFactory approach
**Notes:** The current string-based approach has produced subtle bugs in the past (e.g., line ending normalization with `.Replace("\r\n", "\n").Replace("\n", "\r\n")` in `ReplaceMultiVariableExpression()`). SyntaxFactory eliminates this class of issues entirely.

---

### DF-3: Composable Syntax Node Builders

**Value Proposition:** Extract reusable builder methods for common patterns (e.g., `BuildFluentChain()`, `BuildClauseDeclaration()`) that compose cleanly. String interpolation forces duplication across `CreateInternal`, `CreateComposedInternal`, `CreateWithConstructorInternal`.
**Complexity:** LOW (refactoring existing patterns into shared methods)
**Notes:** The three current creation methods share 80%+ of their logic (clause generation, fluent chains, composition expressions). SyntaxFactory nodes compose naturally via method parameters, enabling a single `BuildClauseDeclaration()` method used by all three paths.

---

### DF-4: Trivia Preservation from Source Context

**Value Proposition:** SyntaxFactory allows fine-grained control over trivia (comments, whitespace). The code can attach source context comments precisely where needed without string concatenation gymnastics.
**Complexity:** LOW
**Notes:** Currently, comments like `// originalExpression` are injected via string building. With SyntaxFactory, they become proper trivia attached to the correct syntax node, ensuring they survive formatting transformations.

---

### DF-5: Helper Method: BuildSpecBuildFluentChain

**Value Proposition:** A single reusable method that constructs the `Spec.Build(lambda).WhenTrue("...").WhenFalse("...").Create()` chain given a lambda expression, original expression text, and model type info. Eliminates the repeated StringBuilder logic in three different creation methods.
**Complexity:** MEDIUM
**Notes:** This helper encapsulates TS-4, TS-3 (clause lambda), and TS-12 into one composable unit. The method signature would be something like:
```csharp
static ExpressionSyntax BuildSpecBuildChain(
    ExpressionSyntax lambdaBody,
    string modelType,
    string parameterName,
    string originalExpressionText)
```

---

## Anti-Features

Things to deliberately NOT do during this migration.

### AF-1: Do NOT Introduce Formatter.Format() Dependency

**Why Avoid:** `Formatter.Format()` requires a `Workspace` instance and is significantly heavier than `NormalizeWhitespace()`. The current code does not use it, and `NormalizeWhitespace()` produces adequate results for generated code.
**What to Do Instead:** Use `NormalizeWhitespace()` on completed syntax trees, matching the pattern already established in `PropositionModelSyntax.cs`.

---

### AF-2: Do NOT Parse Type Names with SyntaxFactory Constructors When Strings Suffice

**Why Avoid:** For type names that originate from `ITypeSymbol.ToDisplayString()` or similar semantic model queries, the value is already a valid C# type name string. Constructing the type from individual `IdentifierName` and `QualifiedName` nodes is unnecessary complexity.
**What to Do Instead:** Use `SyntaxFactory.ParseTypeName(typeString)` for type names from semantic model. This is not "string-based code generation" -- it is parsing a validated type name into its syntax node representation. The distinction matters: `ParseTypeName("MyNamespace.Order")` is correct usage; building `$$"""Spec<{{type}}>"""` and parsing the whole expression is the anti-pattern being eliminated.

---

### AF-3: Do NOT Eliminate All String Interpolation

**Why Avoid:** Per PROJECT.md decision: "String interpolation OK for literal values." WhenTrue/WhenFalse description strings are runtime string values (e.g., `$"({expression}) == true"`), not source code constructs. Using string interpolation to build these description values is correct and expected.
**What to Do Instead:** Use string interpolation for building the *content* of string literals (the description text), then wrap the result in `SyntaxFactory.Literal()` to create the syntax node. The boundary is clear: strings that become C# string literal *values* use interpolation; strings that represent C# *source code structure* use SyntaxFactory.

---

### AF-4: Do NOT Change Test Expected Outputs

**Why Avoid:** The existing tests are the verification gate (per PROJECT.md). If all tests pass after refactor, the output is correct. Changing expected outputs defeats the purpose.
**What to Do Instead:** If `NormalizeWhitespace()` produces slightly different whitespace than the current approach, investigate and match the current output. The goal is identical output, not "better" formatting.

---

### AF-5: Do NOT Create a Generic "Syntax Builder" Abstraction Layer

**Why Avoid:** Over-abstraction adds indirection without value. This is a specific code fix with specific output patterns, not a general-purpose code generation framework.
**What to Do Instead:** Create focused helper methods for repeated patterns (DF-5), but keep them as private methods in the Syntax classes, not as a separate abstraction layer.

---

### AF-6: Do NOT Mix SyntaxFactory.Parse* with SyntaxFactory Construction

**Why Avoid:** The current code uses `SyntaxFactory.ParseCompilationUnit(stringBuilder.ToString())` to parse entire class declarations from strings. Partially migrating (e.g., building some parts with SyntaxFactory but still parsing the overall structure from a string) creates a confusing hybrid that is harder to maintain than either approach alone.
**What to Do Instead:** Fully migrate each creation method. A method either uses SyntaxFactory throughout or uses string parsing throughout. No hybrids within a single method.

---

## Feature Dependencies

```
TS-6 (Generic Types) ──┐
                        ├──> TS-1 (Class Declaration)
TS-3 (Lambdas) ─────────┤
                        │
TS-5 (Member Access) ──┤
                        ├──> TS-4 (Fluent Chains)
TS-12 (String Literals) ┤
                        │
TS-4 ───────────────────┤
                        ├──> TS-9 (Local Variable Decl)
                        │
TS-7 (Object Creation) ─┤
                        ├──> TS-8 (Spec Invocation Chain)
TS-5 ───────────────────┤
                        │
TS-2 (Record Decl) ────── standalone
TS-10 (Field Decl) ────── depends on TS-7
TS-11 (Method Body) ───── depends on TS-5, TS-7
TS-13 (Using Directive) ── already done
TS-14 (Formatting) ────── applied last to everything
```

## Migration Order Recommendation

Based on dependencies and the file structure of the current code, the recommended migration order:

1. **SpecInvocationSyntax.cs** (TS-7, TS-8, TS-5) -- smallest file, fewest patterns, highest confidence. Good proof-of-concept.
2. **CustomSpecDeclarationSyntax.CreateInternal** (TS-1, TS-3 expression lambda, TS-4, TS-6, TS-12) -- single-variable simple case. Moderate complexity.
3. **CustomSpecDeclarationSyntax.CreateComposedInternal** (TS-1, TS-2, TS-3 block lambda, TS-4, TS-9) -- multi-variable composed case. Uses all patterns.
4. **CustomSpecDeclarationSyntax.CreateWithConstructorInternal** (all of above + TS-10, primary constructor with `instance` param) -- most complex case.
5. **ConvertToSpecCodeFix.ReplaceMultiVariableExpression** (TS-10, TS-11) -- method body replacement, field/constructor injection.

This order follows increasing complexity and builds confidence incrementally. Each step reuses patterns proven in the previous step.

## MVP Recommendation

For the migration, ALL table stakes features are required -- this is a refactoring with zero tolerance for output differences. The MVP is the complete migration.

However, the migration can be done file-by-file with tests validating each step:

1. **First:** Migrate `SpecInvocationSyntax.cs` (3 methods, ~30 lines of string code) -- run tests
2. **Second:** Migrate `CustomSpecDeclarationSyntax.cs` (3 creation methods, ~100 lines of string code) -- run tests
3. **Third:** Migrate `ConvertToSpecCodeFix.ReplaceMultiVariableExpression()` (~80 lines of string code) -- run tests

Defer to post-migration:
- DF-3 (composable builders) -- extract after migration is stable
- DF-5 (helper method) -- extract after migration is stable
- Removal of dead code (`EscapeDoubleQuotes`, private `ToCamelCase` duplicate) -- cleanup pass after tests green

## Sources

- [SyntaxFactory.ClassDeclaration -- Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntaxfactory.classdeclaration?view=roslyn-dotnet-4.7.0) -- confirmed overloads with parameterList for primary constructors
- [SyntaxFactory.RecordDeclaration -- Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntaxfactory.recorddeclaration?view=roslyn-dotnet-4.3.0) -- confirmed overloads with parameterList
- [RecordDeclarationSyntax.ParameterList -- Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntax.recorddeclarationsyntax.parameterlist?view=roslyn-dotnet-4.6.0)
- [SyntaxFactory.Literal -- Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntaxfactory.literal?view=roslyn-dotnet-4.9.0) -- automatic string escaping
- [CSharpSyntaxGenerator.cs -- Roslyn source](https://github.com/dotnet/roslyn/blob/main/src/Workspaces/CSharp/Portable/CodeGeneration/CSharpSyntaxGenerator.cs) -- reference patterns for BaseList, GenericName, ParameterList
- [Creating Code Using the Syntax Factory -- John Koerner](https://johnkoerner.com/csharp/creating-code-using-the-syntax-factory/) -- InvocationExpression, MemberAccessExpression examples
- [Generating C# code with Roslyn APIs -- Jeremy Davis](https://blog.jermdavis.dev/posts/2024/csharp-code-with-roslyn) -- ObjectCreationExpression, MemberAccess patterns
- [Roslyn Quoter](https://roslynquoter.azurewebsites.net/) -- tool for converting C# code to SyntaxFactory calls (useful during implementation)
- [Get started with syntax transformation -- Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/get-started/syntax-transformation) -- trivia handling, NormalizeWhitespace
- [NormalizeWhitespace best practices -- Roslyn issue #639](https://github.com/dotnet/format/issues/639) -- formatting considerations
- PropositionModelSyntax.cs (existing codebase) -- reference implementation already using SyntaxFactory correctly
