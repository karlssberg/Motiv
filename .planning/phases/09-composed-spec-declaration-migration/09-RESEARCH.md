# Phase 9: Composed Spec Declaration Migration - Research

**Researched:** 2026-03-12
**Domain:** Roslyn SyntaxFactory API — block lambda construction, record parameter lists, syntax tree node replacement
**Confidence:** HIGH

## Summary

Phase 8 established the `SpecClassDeclaration` abstract base, `SpecFluentChainBuilder`, and the full class hierarchy. `ComposedSpecClassDeclaration` already extends `SpecClassDeclaration` and uses `SpecFluentChainBuilder`. However, it still contains four string-parsing call sites that must be replaced with SyntaxFactory construction to satisfy SFMC-01 through SFMC-05.

The remaining string-based code in `ComposedSpecClassDeclaration` is:

1. `ParseExpression(transformed)` in `GenerateClauseStatementSyntaxes` — the `transformed` string is `ExpressionSyntax.ToString()` output from `ExpressionDecomposer`, but the original `ExpressionSyntax Expression` is available directly on each clause tuple. Use it instead.
2. `ReturnStatement(ParseExpression(updatedComposition))` in `AttachLambdaBody` — `ResolveComposition` converts the composition string to a new string by doing `string.Replace`. This must become a syntax-tree `ReplaceNodes` operation, satisfying SFMC-04.
3. `ParseParameterList($"({nestedRecordParameters})")` in `AddClassBody` — `nestedRecordParameters` is a formatted string like `"int ValueA, bool ValueC"`. Must be replaced with explicit `ParameterList` SyntaxFactory construction using `recordParameters` data available at call site in `LogicalExpressionToSpecConverter`.
4. `ParseTypeName(innerLambdaModelType)` and `ParseTypeName(containingTypeName)` — these are acceptable per Phase 7/8 conventions (handling keyword types like `int`, `string`). They are not migration targets for this phase.

`ExpressionDecomposition.CompositionExpression` is currently a `string` field built by `ExpressionDecomposer` using string interpolation. SFMC-04 requires clause name substitution to use `ReplaceNodes` instead of `string.Replace`. This means either:
- `ExpressionDecomposition.CompositionExpression` stays as a string and `ClauseSet.ResolveComposition` is rewritten to operate on a parsed syntax tree, OR
- `ExpressionDecomposition` stores `ExpressionSyntax CompositionExpression` instead of `string`, eliminating the need to parse at all.

The second option is cleaner but requires changing `ExpressionDecomposer` to produce `ExpressionSyntax` directly — which is natural since `ExpressionDecomposer` already works with `ExpressionSyntax` nodes. The composition expression is built from clause names (`ClauseNameDeriver.DeriveName` returns a string, and these become `IdentifierNameSyntax` nodes in the expression tree) and logical operators (`.AndAlso`, `.OrElse`, `^`, `!`, `()`).

**Primary recommendation:** Change `ExpressionDecomposition.CompositionExpression` from `string` to `ExpressionSyntax`. Rewrite `ExpressionDecomposer.DecomposeBinary/DecomposeParenthesized/DecomposeNot` to build `ExpressionSyntax` nodes instead of strings. Change `ClauseSet.ResolveComposition` to use `ReplaceNodes` on the `ExpressionSyntax` tree. The record parameter list in `AddClassBody` should be constructed from typed `ParameterSyntax` nodes passed through `ComposedSpecClassDeclaration`.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| SFMC-01 | `CustomSpecDeclarationSyntax.CreateComposedInternal()` uses SyntaxFactory (no StringBuilder or raw string code generation) | The entry point is now `ComposedSpecClassDeclaration.Build()` via `SpecClassDeclaration`. Four remaining `ParseExpression`/`ParseParameterList` calls must be replaced. |
| SFMC-02 | Block lambda with local variable declarations constructed via SyntaxFactory | `GenerateClauseStatementSyntaxes` already uses `LocalDeclarationStatement`+`VariableDeclaration` for the var declarations. The only remaining issue is `ParseExpression(transformed)` — fix by using the `ExpressionSyntax` node directly. |
| SFMC-03 | Nested record declaration constructed via SyntaxFactory | `AddClassBody` uses `RecordDeclaration(...)` from SyntaxFactory but calls `ParseParameterList($"({nestedRecordParameters})")`. Replace with explicit `ParameterList` construction using typed parameters. |
| SFMC-04 | Composition expression uses `ReplaceNodes` instead of `string.Replace` for clause name substitution | `ClauseSet.ResolveComposition` currently does `result.Replace(originalClauseName, camelCaseName)` on a string. Must become `ReplaceNodes` on an `ExpressionSyntax` tree. |
| SFMC-05 | All existing composed spec tests pass unchanged | 94 CodeFix tests currently pass. Verification gate: `dotnet test src/Motiv.CodeFix.Tests` must remain 94/94 green. |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.CodeAnalysis.CSharp | 4.x+ | Roslyn SyntaxFactory API | Official C# code generation API; established in Phase 7/8 |
| using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory | N/A | Static imports to reduce verbosity | Project-wide convention (all Syntax/ files use this) |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Microsoft.CodeAnalysis.CSharp.Syntax | 4.x+ | Typed syntax node types | Already in use; no changes |

**Installation:** All dependencies already installed.

## Architecture Patterns

### Current State of ComposedSpecClassDeclaration (before Phase 9)

The file has five remaining string-parsing sites:

```
ComposedSpecClassDeclaration.cs
├── GetModelType()                  ← ParseTypeName(innerLambdaModelType)       OK — keep (keyword handling)
├── AttachLambdaBody()
│   ├── clauseSet.ResolveComposition(decomposition.CompositionExpression)        MIGRATE (SFMC-04)
│   └── ReturnStatement(ParseExpression(updatedComposition))                     MIGRATE (flows from above)
├── BuildParameterList()            ← ParseTypeName(containingTypeName)          OK — keep (keyword handling)
├── AddClassBody()                  ← ParseParameterList($"({nestedRecordParameters})")  MIGRATE (SFMC-03)
└── GenerateClauseStatementSyntaxes()
    └── ParseExpression(transformed)                                              MIGRATE (SFMC-02)

ClauseSet.cs
└── ResolveComposition(string)      ← string.Replace on string                  MIGRATE (SFMC-04)

ExpressionDecomposition.cs
└── CompositionExpression: string   ← currently a string                         MIGRATE (change to ExpressionSyntax)
```

### Pattern 1: Use ExpressionSyntax Directly Instead of ParseExpression(ToString())

**What:** The `transformed` string in `GenerateClauseStatementSyntaxes` comes from `transformed.ToString()` on an `ExpressionSyntax` node in `ExpressionDecomposer.CreateLeafClause`. Use the `Expression` node directly.

**Current `ExpressionDecomposition.Clauses` tuple:** `(string OriginalText, string TransformedText, ExpressionSyntax Expression)` — the `Expression` field is the **original** expression. The `TransformedText` is `transformed.ToString()` from the transformed expression. To pass the transformed `ExpressionSyntax` directly, `ExpressionDecomposer.CreateLeafClause` must store the `transformed` node (already available as `ExpressionSyntax`) rather than calling `.ToString()` on it.

**Change required in `ExpressionDecomposition`:**
```csharp
// Before: TransformedText is string (lossy)
public IReadOnlyList<(string OriginalText, string TransformedText, ExpressionSyntax Expression)> Clauses

// After: TransformedExpression is ExpressionSyntax (lossless)
public IReadOnlyList<(string OriginalText, ExpressionSyntax TransformedExpression, ExpressionSyntax OriginalExpression)> Clauses
```

**Change in `ExpressionDecomposer.CreateLeafClause`:**
```csharp
// Before:
[(expr.ToString().Trim(), transformed.ToString(), expr)]

// After:
[(expr.ToString().Trim(), transformed, expr)]
```

**Change in `ComposedSpecClassDeclaration.GenerateClauseStatementSyntaxes`:**
```csharp
// Before:
var specChain = SpecFluentChainBuilder.Build(
    innerLambdaModelType,
    innerLambdaParameterName,
    ParseExpression(transformed),   // <-- string round-trip
    original);

// After:
var specChain = SpecFluentChainBuilder.Build(
    innerLambdaModelType,
    innerLambdaParameterName,
    transformedExpression,          // <-- use ExpressionSyntax directly
    original);
```

### Pattern 2: ExpressionSyntax Composition via SyntaxFactory Instead of String

**What:** `ExpressionDecomposition.CompositionExpression` changes from `string` to `ExpressionSyntax`. `ExpressionDecomposer` builds the composition as a syntax tree.

**Change in `ExpressionDecomposition`:**
```csharp
// Before:
public string CompositionExpression { get; }

// After:
public ExpressionSyntax CompositionExpression { get; }
```

**Change in `ExpressionDecomposer`:**

Each composition method returns a new `ExpressionDecomposition` with an `ExpressionSyntax` node:

```csharp
// DecomposeParenthesized: wrap in ParenthesizedExpression
ExpressionDecomposition DecomposeParenthesized(ParenthesizedExpressionSyntax paren)
{
    var inner = DecomposeCore(paren.Expression);
    return new ExpressionDecomposition(
        inner.Clauses,
        ParenthesizedExpression(inner.CompositionExpression));  // SyntaxFactory
}

// DecomposeNot: wrap in PrefixUnaryExpression with !
ExpressionDecomposition DecomposeNot(PrefixUnaryExpressionSyntax unary)
{
    var inner = DecomposeCore(unary.Operand);
    return new ExpressionDecomposition(
        inner.Clauses,
        PrefixUnaryExpression(
            SyntaxKind.LogicalNotExpression,
            inner.CompositionExpression));
}

// DecomposeBinary: use InvocationExpression for .AndAlso/.OrElse or BinaryExpression for ^
ExpressionDecomposition DecomposeBinary(BinaryExpressionSyntax binary, LogicalOperator op)
{
    var left = DecomposeCore(binary.Left);
    var right = DecomposeCore(binary.Right);
    var allClauses = left.Clauses.Concat(right.Clauses).ToList();

    ExpressionSyntax composition = op switch
    {
        // .AndAlso(right): left.AndAlso(right)
        { Method: "AndAlso" } =>
            InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    left.CompositionExpression,
                    IdentifierName("AndAlso")),
                ArgumentList(SingletonSeparatedList(Argument(right.CompositionExpression)))),

        // .OrElse(right): left.OrElse(right)
        { Method: "OrElse" } =>
            InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    left.CompositionExpression,
                    IdentifierName("OrElse")),
                ArgumentList(SingletonSeparatedList(Argument(right.CompositionExpression)))),

        // ^ (XOR infix): left ^ right
        _ =>
            BinaryExpression(
                SyntaxKind.ExclusiveOrExpression,
                left.CompositionExpression,
                right.CompositionExpression)
    };

    return new ExpressionDecomposition(allClauses, composition);
}

// CreateLeafClause: IdentifierName(clauseName) as the composition node
ExpressionDecomposition CreateLeafClause(ExpressionSyntax expr)
{
    counter++;
    var transformed = transformClause(expr);
    var clauseName = ClauseNameDeriver.DeriveName(expr, counter);
    return new ExpressionDecomposition(
        [(expr.ToString().Trim(), transformed, expr)],
        IdentifierName(clauseName));   // SyntaxFactory — leaf is just its variable name
}
```

Note: The current `GetLogicalOperator` returns `(string Op, bool IsInfix)`. Refactor to an enum or typed discriminated union to make the SyntaxFactory branching clean.

### Pattern 3: ReplaceNodes for Clause Name Substitution (SFMC-04)

**What:** `ClauseSet.ResolveComposition` rewrites clause placeholder names in the composition expression. With `CompositionExpression` as `ExpressionSyntax`, this becomes `ReplaceNodes` targeting `IdentifierNameSyntax` nodes whose text matches `Clause{N}` or `PascalCaseName`, replacing them with `IdentifierName(camelCaseName)`.

**Current (string-based):**
```csharp
public string ResolveComposition(string compositionExpression)
{
    var result = compositionExpression;
    for (var i = 0; i < _clauseNameMapping.Count; i++)
    {
        var originalClauseName = $"Clause{i + 1}";
        var pascalCaseName = _clauseNameMapping[i];
        var camelCaseName = pascalCaseName.ToCamelCase();
        result = result.Replace(originalClauseName, camelCaseName);
        result = result.Replace(pascalCaseName, camelCaseName);
    }
    return result;
}
```

**After (SyntaxFactory ReplaceNodes):**
```csharp
// Source: Roslyn SyntaxNode.ReplaceNodes API
public ExpressionSyntax ResolveComposition(ExpressionSyntax compositionExpression)
{
    // Build a mapping from clause placeholder identifiers -> camelCase names
    var replacements = new Dictionary<string, string>();
    for (var i = 0; i < _clauseNameMapping.Count; i++)
    {
        var originalClauseName = $"Clause{i + 1}";
        var pascalCaseName = _clauseNameMapping[i];
        var camelCaseName = pascalCaseName.ToCamelCase();
        replacements[originalClauseName] = camelCaseName;
        replacements[pascalCaseName] = camelCaseName;
    }

    return compositionExpression.ReplaceNodes(
        compositionExpression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>(),
        (original, _) =>
            replacements.TryGetValue(original.Identifier.Text, out var camelName)
                ? IdentifierName(camelName)
                : original);
}
```

`SyntaxNode.ReplaceNodes` is the standard Roslyn API for this operation. It takes a collection of nodes and a replacement function. The function receives `(originalNode, rewrittenNode)` — use `originalNode` for the identifier lookup.

**Signature change:** `ResolveComposition` changes from `string` → `ExpressionSyntax` parameter and return type. `AttachLambdaBody` callers must be updated.

### Pattern 4: Nested Record ParameterList via SyntaxFactory (SFMC-03)

**What:** Replace `ParseParameterList($"({nestedRecordParameters})")` with explicit SyntaxFactory construction.

**Current approach:** `nestedRecordParameters` is built as `"int ValueA, bool ValueC"` in `LogicalExpressionToSpecConverter.BuildMultiVarComposedSpec`. It is passed as a `string` to `ComposedSpecClassDeclaration`, which wraps it with `ParseParameterList`.

**Options:**

Option A — Build the `ParameterListSyntax` in `LogicalExpressionToSpecConverter` and pass it down:
```csharp
// In LogicalExpressionToSpecConverter.BuildMultiVarComposedSpec:
var recordParameterList = ParameterList(
    SeparatedList(
        variableSymbols.Select(s =>
            Parameter(Identifier(s.Name.Capitalize()))
                .WithType(ParseTypeName(GetSymbolTypeName(s))))));
```

Option B — Change the `nestedRecordParameters` parameter of `ComposedSpecClassDeclaration` from `string?` to `ParameterListSyntax?` and let the converter construct it.

Option A is simpler (no constructor signature change beyond removing the old string param). Option B is cleaner since it removes the string-to-syntax conversion from the class signature entirely.

**Recommendation:** Use Option B — change `nestedRecordParameters` constructor parameter type from `string?` to `ParameterListSyntax?`. This makes the intent explicit and removes the last string-based construction from `AddClassBody`.

```csharp
// In AddClassBody, after change:
var nestedRecord = RecordDeclaration(
        SyntaxKind.RecordDeclaration,
        Token(SyntaxKind.RecordKeyword),
        Identifier(nestedRecordName))
    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
    .WithParameterList(nestedRecordParameterList)   // ParameterListSyntax directly
    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
```

### Pattern 5: SyntaxNode.ReplaceNodes API

**What:** `SyntaxNode.ReplaceNodes<TNode>` — replace a set of nodes in a syntax tree.

**Signature (from Roslyn official docs):**
```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.syntaxnode.replacenodes
public SyntaxNode ReplaceNodes<TNode>(
    IEnumerable<TNode> nodes,
    Func<TNode, TNode, SyntaxNode> computeReplacementNode)
    where TNode : SyntaxNode
```

**Key detail:** The replacement function receives `(originalNode, currentNode)`. Use `originalNode` when looking up in a dictionary (its identity matches the nodes returned by `DescendantNodesAndSelf`). Use `currentNode` if you need a node that has already been partially rewritten by prior replacements in the same call (rare, not needed here).

**Key constraint:** `ReplaceNodes` processes all replacements in a single pass through the tree. No need to call it multiple times. The collection of nodes to replace must all belong to the same tree snapshot passed as the receiver.

### Recommended Project Structure (no changes to file layout)

```
src/Motiv.CodeFix/
├── ExpressionDecomposer.cs          # change: return ExpressionSyntax composition
├── ExpressionDecomposition.cs       # change: CompositionExpression: ExpressionSyntax, TransformedExpression: ExpressionSyntax
├── LogicalExpressionToSpecConverter.cs  # change: build ParameterListSyntax for record
└── Syntax/
    ├── ClauseSet.cs                 # change: ResolveComposition takes/returns ExpressionSyntax
    └── ComposedSpecClassDeclaration.cs  # change: use ExpressionSyntax directly, ParameterListSyntax param
```

### Anti-Patterns to Avoid

- **`ParseExpression(node.ToString())`:** Round-tripping an `ExpressionSyntax` through string loses trivia, semantic annotations, and exact source representation. Use the original node directly.
- **String.Replace on composition expressions:** Cannot distinguish identifiers from substrings (e.g., replacing `Clause1` in `Clause10` would corrupt). `ReplaceNodes` on `IdentifierNameSyntax` nodes is exact and structural.
- **Calling `ReplaceNodes` with nodes from a different snapshot:** The `nodes` parameter must be `DescendantNodesAndSelf()` of the exact receiver instance, not from a previously stored reference.
- **Changing `ExpressionDecomposition` to hold syntax nodes without understanding immutability:** Syntax nodes are immutable and have parent references. Nodes stored in `ExpressionDecomposition.Clauses` from the original expression tree will be re-rooted when inserted into the new class tree — this is expected and correct in Roslyn.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Replace identifier nodes in syntax tree | `string.Replace` on `.ToString()` output | `SyntaxNode.ReplaceNodes` with `IdentifierNameSyntax` | Exact match on node identity, no substring collisions |
| Record parameter list | `ParseParameterList(formatted_string)` | `ParameterList(SeparatedList(Parameter(...).WithType(...)))` | Type-safe, no round-trip through text parser |
| Wrap expression in parens | Manual string `$"({x})"` | `ParenthesizedExpression(inner)` | Correct precedence, proper trivia handling |
| Negate expression | `$"!{x}"` | `PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, inner)` | Proper precedence, no whitespace ambiguity |
| Method call composition (.AndAlso, .OrElse) | `$"{left}.AndAlso({right})"` | `InvocationExpression(MemberAccessExpression(...), ArgumentList(...))` | Tree structure preserved for further rewriting |

## Common Pitfalls

### Pitfall 1: String.Replace Collides on Substrings
**What goes wrong:** `result.Replace("Clause1", "x")` also replaces `Clause10`, `Clause11`, etc.
**Why it happens:** `string.Replace` is substring-based, not word-boundary-based.
**How to avoid:** Use `ReplaceNodes` on `IdentifierNameSyntax` — the `Identifier.Text` comparison is exact.
**Warning signs:** Generated code referencing `0` (from `Clause10` becoming `x0` after partial replacement)

### Pitfall 2: Rewriting ExpressionDecomposer Changes ExpressionDecomposition Public API
**What goes wrong:** Both `ExpressionDecomposition.Clauses` tuple shape and `CompositionExpression` type change. All consumers must be updated.
**Why it happens:** `ExpressionDecomposition` is a public struct used in `ComposedSpecClassDeclaration`, `ClauseSet`, and `LogicalExpressionToSpecConverter`.
**How to avoid:** Use `git grep ExpressionDecomposition` to enumerate all use sites before changing the struct. Update all callers in one commit.
**Warning signs:** Compile errors in `ComposedSpecClassDeclaration.AttachLambdaBody`, `ClauseSet` constructor, `GenerateClauseStatementSyntaxes`.

### Pitfall 3: ReplaceNodes Modifying Nodes Already Rewritten
**What goes wrong:** Passing a `nodes` collection built from a previous `compositionExpression` snapshot to a later `.ReplaceNodes` call.
**Why it happens:** After calling `.ReplaceNodes`, the resulting tree is a new object. The original nodes from the old tree don't exist in the new tree.
**How to avoid:** Collect all nodes to replace in one `DescendantNodesAndSelf()` call on the receiver, pass them to a single `ReplaceNodes` call.
**Warning signs:** `ReplaceNodes` silently makes no replacements (nodes not found in tree).

### Pitfall 4: ExpressionDecomposer Composition Operator Representation
**What goes wrong:** `GetLogicalOperator` currently returns `(string Op, bool IsInfix)` — the `IsInfix` flag distinguishes `^` (binary expression) from `.AndAlso`/`.OrElse` (method calls). The switch expression in `DecomposeBinary` must use this same distinction.
**Why it happens:** `.AndAlso(right)` is a method invocation, `^ right` is a binary expression — they differ in SyntaxFactory construction.
**How to avoid:** Keep the `(string Op, bool IsInfix)` pattern but interpret it during SyntaxFactory construction:
  - `IsInfix = true` → `BinaryExpression(SyntaxKind.ExclusiveOrExpression, left, right)`
  - `IsInfix = false, Op = ".AndAlso"` → `InvocationExpression(MemberAccessExpression(..., "AndAlso"), ArgumentList(Argument(right)))`
  - `IsInfix = false, Op = ".OrElse"` → same pattern with `"OrElse"`
**Warning signs:** XOR expressions generating method calls or vice versa.

### Pitfall 5: Trivia on Composition ExpressionSyntax Nodes
**What goes wrong:** The `IdentifierName("ClauseName")` leaf nodes created by `ExpressionDecomposer.CreateLeafClause` have no trivia. When inserted into method call chains and formatted with `NormalizeWhitespace`, extra spaces may appear unexpectedly.
**Why it happens:** `NormalizeWhitespace()` is called in `SpecClassDeclaration.Build()` and then `FormatOutput` (rewriter) runs for spacing corrections.
**How to avoid:** Do not add trivia during composition tree construction. The `BlankLineRewriter` in `ComposedSpecClassDeclaration.FormatOutput` handles final formatting. `NormalizeWhitespace` handles spacing within the method chain.
**Warning signs:** Test output differences in indentation around the `return` statement.

## Code Examples

### ReplaceNodes for Identifier Substitution
```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.syntaxnode.replacenodes
// Pattern: Replace named identifiers in a syntax tree

public ExpressionSyntax ResolveComposition(ExpressionSyntax compositionExpression)
{
    var replacements = BuildReplacementMap();  // Dictionary<string, string>

    return compositionExpression.ReplaceNodes(
        compositionExpression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>(),
        (original, _) =>
            replacements.TryGetValue(original.Identifier.Text, out var camelName)
                ? IdentifierName(camelName)
                : original);
}
```

### Building Method Call Composition Expression
```csharp
// Source: Phase 7/8 established patterns (SpecInvocationSyntax.cs, SpecFluentChainBuilder.cs)
// Pattern: left.AndAlso(right)

InvocationExpression(
    MemberAccessExpression(
        SyntaxKind.SimpleMemberAccessExpression,
        leftCompositionExpr,
        IdentifierName("AndAlso")),
    ArgumentList(
        SingletonSeparatedList(
            Argument(rightCompositionExpr))))
```

### Building XOR Binary Composition Expression
```csharp
// Source: Roslyn SyntaxFactory documentation
// Pattern: left ^ right

BinaryExpression(
    SyntaxKind.ExclusiveOrExpression,
    leftCompositionExpr,
    rightCompositionExpr)
```

### Building Negation Expression
```csharp
// Source: Roslyn SyntaxFactory documentation
// Pattern: !inner

PrefixUnaryExpression(
    SyntaxKind.LogicalNotExpression,
    innerCompositionExpr)
```

### Building Parenthesized Expression
```csharp
// Source: Roslyn SyntaxFactory documentation
// Pattern: (inner)

ParenthesizedExpression(innerCompositionExpr)
```

### Building Record ParameterList from Typed Parameters
```csharp
// Source: Roslyn SyntaxFactory documentation
// Pattern: (int ValueA, bool ValueC, ...)

ParameterList(
    SeparatedList(
        variableSymbols.Select(s =>
            Parameter(Identifier(s.Name.Capitalize()))
                .WithType(ParseTypeName(GetSymbolTypeName(s))))))
```

Note: `ParseTypeName` is retained for record parameters since variable types may include keyword types (`int`, `string`, `bool`). This is per the established project convention from Phases 7/8.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| String.Replace for clause name substitution | `ReplaceNodes` on `ExpressionSyntax` | Phase 9 | Exact identifier replacement, no substring collision |
| `ParseExpression(node.ToString())` round-trip | Use `ExpressionSyntax` node directly | Phase 9 | No lossy string conversion, preserves tree structure |
| `CompositionExpression: string` | `CompositionExpression: ExpressionSyntax` | Phase 9 | Structural representation throughout pipeline |
| `ParseParameterList(formatted_string)` | `ParameterList(SeparatedList(...))` | Phase 9 | Explicit typed construction, no round-trip parsing |

**Deprecated/outdated after this phase:**
- `ExpressionDecomposition.CompositionExpression` as `string`
- `ClauseSet.ResolveComposition(string)` returning `string`
- `(string OriginalText, string TransformedText, ExpressionSyntax Expression)` clause tuple shape

## Open Questions

1. **Whether to rename `TransformedText` to `TransformedExpression` in clauses tuple**
   - What we know: Changing from `string TransformedText` to `ExpressionSyntax TransformedExpression` requires updating `ClauseSet` constructor and `GenerateClauseStatementSyntaxes`.
   - What's unclear: `OriginalText` (the original expression as string) is still used for the `.Create("clause text")` argument in `SpecFluentChainBuilder`. It stays as `string`.
   - Recommendation: Keep `OriginalText: string` (used for the Create name), change `TransformedText: string` to `TransformedExpression: ExpressionSyntax`. The `ClauseSet` key (used for deduplication) can switch from `transformed` (string) to `transformed.ToString()` — or keep using the string representation for deduplication since `ExpressionSyntax` equality is reference-based.

2. **ClauseSet deduplication key with ExpressionSyntax**
   - What we know: `ClauseSet` deduplicates by `transformed` string. With `ExpressionSyntax`, calling `.ToString()` on the expression gives an equivalent string for dedup purposes.
   - Recommendation: Keep `uniqueClauses` keyed on `transformed.ToString()` (the text of the transformed expression) to maintain existing deduplication semantics.

3. **Whether `ExpressionDecomposition` should store `ExpressionSyntax` leaf composition nodes before `ClauseSet` renaming**
   - What we know: `ExpressionDecomposer` creates `IdentifierName(clauseName)` leaf nodes using the PascalCase name. `ClauseSet.ResolveComposition` maps PascalCase → camelCase. This two-step rename still works with `ReplaceNodes`.
   - Recommendation: No change needed to the two-step design. `ReplaceNodes` handles the rename transparently.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit (via Roslyn code fix testing framework) |
| Config file | none (auto-discovered) |
| Quick run command | `dotnet test src/Motiv.CodeFix.Tests/Motiv.CodeFix.Tests.csproj -q` |
| Full suite command | `dotnet test /c/Dev/Motiv/Motiv.sln -q` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SFMC-01 | CreateComposedInternal uses SyntaxFactory | unit (code inspection) | `dotnet test src/Motiv.CodeFix.Tests -q` | Yes (compile-time) |
| SFMC-02 | Block lambda local var declarations via SyntaxFactory | integration | `dotnet test src/Motiv.CodeFix.Tests -q` | Yes — `Should_convert_double_variable_boolean_return_expressions` etc. |
| SFMC-03 | Nested record via SyntaxFactory | integration | `dotnet test src/Motiv.CodeFix.Tests -q` | Yes — all multi-var tests generate `public record Model(...)` |
| SFMC-04 | ReplaceNodes instead of string.Replace | integration | `dotnet test src/Motiv.CodeFix.Tests -q` | Yes — deduplication tests verify correct var names in return expression |
| SFMC-05 | All existing tests pass unchanged | regression | `dotnet test src/Motiv.CodeFix.Tests -q` | Yes — 94 tests currently green |

### Sampling Rate
- **Per task commit:** `dotnet test src/Motiv.CodeFix.Tests/Motiv.CodeFix.Tests.csproj -q`
- **Per wave merge:** `dotnet test /c/Dev/Motiv/Motiv.sln -q`
- **Phase gate:** Full solution suite green before `/gsd:verify-work`

### Wave 0 Gaps
None — existing test infrastructure covers all phase requirements. The 94 existing tests in `Motiv.CodeFix.Tests` are the verification gate.

## Sources

### Primary (HIGH confidence)
- `src/Motiv.CodeFix/Syntax/ComposedSpecClassDeclaration.cs` — direct inspection of all string-parsing call sites
- `src/Motiv.CodeFix/Syntax/ClauseSet.cs` — direct inspection of `ResolveComposition` string replacement logic
- `src/Motiv.CodeFix/ExpressionDecomposer.cs` — direct inspection of composition string building
- `src/Motiv.CodeFix/ExpressionDecomposition.cs` — direct inspection of struct shape
- `src/Motiv.CodeFix/LogicalExpressionToSpecConverter.cs` — direct inspection of `nestedRecordParameters` string construction
- [Microsoft Learn: SyntaxNode.ReplaceNodes](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.syntaxnode.replacenodes) — official API for in-tree node replacement
- `.planning/phases/08-simple-spec-declaration-migration/08-01-SUMMARY.md` — established patterns from Phase 8

### Secondary (MEDIUM confidence)
- Phase 7/8 patterns in `SpecInvocationSyntax.cs`, `SpecFluentChainBuilder.cs` — `InvocationExpression`/`MemberAccessExpression` construction is established project pattern
- `src/Motiv.CodeFix.Tests/MotivConvertToSpecTests.cs` — exact expected output for all composed spec tests (lines 64-719)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — established in Phases 7/8; no new dependencies
- Architecture: HIGH — all code directly inspected; migration path is clear and mechanical
- Pitfalls: HIGH — derived from direct code inspection and known Roslyn API behaviors

**Research date:** 2026-03-12
**Valid until:** 90 days (Roslyn SyntaxFactory API is stable)
