# Phase 2: CodeFix Foundation - Research

**Researched:** 2026-02-07
**Domain:** Roslyn CodeFix Providers - Name Derivation and Code Cleanup
**Confidence:** HIGH

## Summary

This phase focuses on making the Motiv CodeFix generate clean, context-aware code with meaningful names derived from expression content. The key challenges are: (1) extracting a common root identifier from boolean expressions to derive class names, (2) detecting and avoiding name collisions in the compilation, and (3) removing Debug.WriteLine statements and cleaning up unused imports.

Roslyn provides robust APIs for all three areas. The existing codebase already extracts identifiers using `DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()` with semantic model filtering (ConvertToSpecCodeFix.cs line 372-383). Name collision detection uses `SemanticModel.LookupSymbols(position, name)` to check if a type name exists at a given location. Statement removal uses `SyntaxNode.RemoveNode()` with trivia options. Import cleanup can leverage the CS8019 diagnostic for unused using directives or simply check if `System.Diagnostics` is referenced elsewhere in the file.

The name derivation algorithm is the custom logic piece: find all identifiers in the expression, determine the "common root" (most frequently occurring base identifier, ignoring member access chains), convert to PascalCase, append "Proposition"/"Model" suffixes, and check for collisions using LookupSymbols to generate unique names if needed.

**Primary recommendation:** Implement name derivation as a multi-step pipeline: (1) Extract all IdentifierNameSyntax nodes from expression, (2) Filter to root identifiers (leftmost in member access chains), (3) Find most common root via frequency count or use fallback "Proposition" if no clear winner, (4) Convert to PascalCase using existing `Capitalize()` extension, (5) Append suffixes, (6) Use LookupSymbols to detect collisions and append incrementing numbers if needed. For Debug.WriteLine removal, locate and remove ExpressionStatementSyntax nodes containing Debug.WriteLine invocations, then check if System.Diagnostics can be removed.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Name derivation strategy:**
- Names derive from **expression content**, not method name or variable context
- Find the **common root** identifier across the expression:
  - `age > 18` → root is `age`
  - `order.Total > 100 && order.IsActive` → common root is `order`
- Use **root only** for member access, not full path: `order.Total > 100` → `Order`, not `OrderTotal`
- Always **PascalCase** the derived name: `age` → `Age`, `isValid` → `IsValid`
- For `is` type-check expressions, derive from the **variable**, not the type: `obj is string` → `Obj`

**Proposition and Model naming:**
- Proposition and Model names differ only by suffix: `AgeProposition` and `AgeModel`
- Suffixes are **always** `Proposition` and `Model` — no context-aware suffix adaptation
- Fallback when no common root can be found (unrelated variables like `x > 5 && y < 10`): use generic `Proposition` and `Model`

**Name clash resolution:**
- If a generated name already exists in the compilation, append incrementing integers: `OrderProposition` → `OrderProposition1` → `OrderProposition2`
- Same logic applies to the Model name

**Debug.WriteLine removal:**
- Remove all Debug.WriteLine calls from generated code
- Remove `using System.Diagnostics` if no longer needed by other code in the file

### Claude's Discretion

- Exact algorithm for identifying the "common root" across complex expressions
- How to handle edge cases in PascalCase conversion (acronyms, numbers, etc.)
- Whether to preserve the original expression as a comment in generated code
- Import cleanup strategy details

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope

</user_constraints>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.CodeAnalysis.CSharp.Workspaces | netstandard2.0 | Roslyn CodeFix API | Required for CodeFix providers, includes semantic model and syntax manipulation |
| Microsoft.CodeAnalysis.Analyzers | Latest | Analyzer development guidelines | Required for building Roslyn analyzers and code fixes |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| xUnit | Latest | Test framework | Already used in Motiv.CodeFix.Tests |
| Microsoft.CodeAnalysis.CSharp.Testing | Latest | CodeFix testing framework | Official testing framework with code fix verification |

### Existing Codebase Utilities
| Utility | Location | Purpose |
|---------|----------|---------|
| `StringExtensions.Capitalize()` | Motiv.CodeFix/StringExtensions.cs | Convert first char to uppercase (camelCase → PascalCase) |
| `StringExtensions.ToCamelCase()` | Motiv.CodeFix/StringExtensions.cs | Convert first char to lowercase (PascalCase → camelCase) |
| `GetVariablesInExpression()` | ConvertToSpecCodeFix.cs line 372 | Extract ISymbol for all identifiers in expression using semantic model |

**Installation:**
Already installed in the existing Motiv.CodeFix and Motiv.CodeFix.Tests projects.

## Architecture Patterns

### Current CodeFix Structure (Existing Pattern)
```
src/Motiv.CodeFix/
├── MotivCodeFixProvider.cs           # CodeFix entry point
├── ConvertToSpecCodeFix.cs           # Main conversion logic (LogicalExpressionToSpecConverter)
├── StringExtensions.cs               # String manipulation utilities
├── Syntax/
│   ├── CustomSpecDeclarationSyntax.cs   # Proposition class generation
│   ├── SpecInvocationSyntax.cs          # Evaluate invocation generation
│   └── PropositionModelSyntax.cs        # Model record generation
└── Motiv.CodeFix.csproj

src/Motiv.CodeFix.Tests/
├── MotivConvertToSpecTests.cs        # CodeFix verification tests
├── CSharpCodeFixVerifier.cs          # Test infrastructure
└── Motiv.CodeFix.Tests.csproj
```

### Pattern 1: Name Derivation Pipeline
**What:** Multi-step process to derive meaningful class names from expression identifiers.
**When to use:** When generating Proposition and Model class names.
**Recommended implementation:**
```csharp
// Step 1: Extract all identifiers from expression
var identifiers = expression
    .DescendantNodesAndSelf()
    .OfType<IdentifierNameSyntax>()
    .Where(id => !IsPartOfMemberAccess(id) || IsRootOfMemberAccess(id))
    .Select(id => id.Identifier.ValueText)
    .ToList();

// Step 2: Find common root (most frequent identifier)
string rootName = identifiers
    .GroupBy(name => name)
    .OrderByDescending(g => g.Count())
    .ThenBy(g => g.Key)  // Deterministic tie-break
    .Select(g => g.Key)
    .FirstOrDefault() ?? "Proposition";  // Fallback

// Step 3: Convert to PascalCase
string pascalCaseName = rootName.Capitalize();

// Step 4: Append suffix
string propositionName = $"{pascalCaseName}Proposition";
string modelName = $"{pascalCaseName}Model";

// Step 5: Resolve name collisions
propositionName = EnsureUniqueName(propositionName, semanticModel, position);
modelName = EnsureUniqueName(modelName, semanticModel, position);
```

**Helper for member access root detection:**
```csharp
private static bool IsRootOfMemberAccess(IdentifierNameSyntax identifier)
{
    // If parent is MemberAccessExpression and identifier is on the left, it's the root
    // Example: order.Total -> "order" is root, "Total" is not
    return identifier.Parent is not MemberAccessExpressionSyntax memberAccess
           || memberAccess.Expression == identifier;
}
```

### Pattern 2: Name Collision Detection with LookupSymbols
**What:** Use SemanticModel.LookupSymbols to check if a type name exists at a given location.
**When to use:** Before finalizing generated class names.
**Implementation:**
```csharp
// Source: Microsoft Learn - SemanticModel.LookupSymbols documentation
// https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.semanticmodel.lookupsymbols

private string EnsureUniqueName(string baseName, SemanticModel semanticModel, int position)
{
    string candidateName = baseName;
    int counter = 0;

    while (NameExists(candidateName, semanticModel, position))
    {
        counter++;
        candidateName = $"{baseName}{counter}";
    }

    return candidateName;
}

private bool NameExists(string name, SemanticModel semanticModel, int position)
{
    var symbols = semanticModel.LookupSymbols(position, name: name);

    // Check if any symbol is a type (class, struct, record, etc.)
    return symbols.Any(s => s.Kind == SymbolKind.NamedType);
}
```

**Key parameters:**
- `position`: Character position in the document (use expression.SpanStart or insertion point)
- `name`: The exact name to search for
- Returns `ImmutableArray<ISymbol>` containing all matching symbols visible at that position

### Pattern 3: Statement Removal with RemoveNode
**What:** Remove specific statements (like Debug.WriteLine) from generated syntax tree.
**When to use:** Cleaning up generated code to remove debug statements.
**Implementation:**
```csharp
// Source: Microsoft Learn - SyntaxNodeExtensions.RemoveNode
// https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.syntaxnodeextensions.removenode

// Find Debug.WriteLine statements in method body
var debugStatements = methodDeclaration.Body.Statements
    .OfType<ExpressionStatementSyntax>()
    .Where(stmt => IsDebugWriteLineCall(stmt))
    .ToList();

// Remove each statement
var newRoot = root;
foreach (var stmt in debugStatements)
{
    newRoot = newRoot.RemoveNode(stmt, SyntaxRemoveOptions.KeepLeadingTrivia);
}

private bool IsDebugWriteLineCall(ExpressionStatementSyntax statement)
{
    return statement.Expression is InvocationExpressionSyntax invocation
           && invocation.Expression is MemberAccessExpressionSyntax memberAccess
           && memberAccess.Expression is IdentifierNameSyntax { Identifier.ValueText: "Debug" }
           && memberAccess.Name.Identifier.ValueText == "WriteLine";
}
```

**SyntaxRemoveOptions:**
- `KeepNoTrivia`: Remove all trivia (whitespace, comments)
- `KeepLeadingTrivia`: Preserve trivia before the node
- `KeepTrailingTrivia`: Preserve trivia after the node
- `KeepExteriorTrivia`: Preserve both leading and trailing trivia

### Pattern 4: Using Directive Cleanup
**What:** Remove `using System.Diagnostics` if it's no longer needed after removing Debug.WriteLine.
**When to use:** After removing Debug statements, as final cleanup step.
**Recommended implementation:**
```csharp
// Check if System.Diagnostics is still referenced
private bool IsSystemDiagnosticsStillNeeded(SyntaxNode root, SemanticModel semanticModel)
{
    // Find all identifier references in the file
    var identifiers = root.DescendantNodes()
        .OfType<IdentifierNameSyntax>()
        .Where(id => id.Identifier.ValueText == "Debug"
                     || id.Identifier.ValueText == "Trace"
                     || id.Identifier.ValueText == "Debugger")
        .ToList();

    if (identifiers.Any())
    {
        // Check if any reference System.Diagnostics namespace
        foreach (var identifier in identifiers)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(identifier);
            if (symbolInfo.Symbol?.ContainingNamespace?.ToString() == "System.Diagnostics")
                return true;
        }
    }

    return false;
}

// Remove using directive
private CompilationUnitSyntax RemoveUsingDirective(CompilationUnitSyntax compilationUnit, string namespaceToRemove)
{
    var usingToRemove = compilationUnit.Usings
        .FirstOrDefault(u => u.Name?.ToString() == namespaceToRemove);

    if (usingToRemove != null)
    {
        return compilationUnit.RemoveNode(usingToRemove, SyntaxRemoveOptions.KeepNoTrivia);
    }

    return compilationUnit;
}
```

**Alternative approach:** Use CS8019 diagnostic (unused using directive) detection:
```csharp
// Check if using is unused via diagnostic
var diagnostics = semanticModel.GetDiagnostics(usingDirective.Span);
bool isUnused = diagnostics.Any(d => d.Id == "CS8019");
```

### Pattern 5: Existing Comment Preservation Pattern
**What:** The codebase already preserves original expressions as comments in generated code.
**Current implementation:**
```csharp
// Source: ConvertToSpecCodeFix.cs line 236-262
var originalExprText = logicalExpressionSyntax.ToString();
var newMethodSource = $$"""
    // {{originalExprText}}
    var result = {{specInvocation}};
    Debug.WriteLine(result.Reason);
    {{assignmentLine}}
""";
```

**This pattern continues:** Original expression comments are well-formatted and provide context. Keep this practice.

### Anti-Patterns to Avoid
- **Using method name for class naming:** User constraint explicitly requires deriving from expression content, not method context
- **Generating suffix variations:** Always use "Proposition" and "Model" suffixes, never adapt based on context
- **Ignoring name collisions:** Always check LookupSymbols before finalizing names
- **Hard-coding "Proposition"/"Model" without fallback logic:** Handle cases where no common root exists
- **Leaving Debug.WriteLine in generated code:** User explicitly wants clean output
- **Removing System.Diagnostics if still needed:** Check for other references before removing using directive

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| PascalCase conversion with acronym handling | Custom string manipulation with acronym detection | Existing `Capitalize()` extension (simple, already used) | User decision specifies simple PascalCase (capitalize first letter), not complex acronym handling |
| Name collision detection | Custom symbol table lookup | `SemanticModel.LookupSymbols(position, name)` | Roslyn's semantic model handles all scoping, visibility, and namespace resolution |
| Syntax tree node removal | Manual node replacement logic | `SyntaxNode.RemoveNode(node, options)` | Roslyn's immutable tree API handles trivia preservation and parent updates |
| Identifier extraction from expressions | Custom expression traversal | `DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()` | Roslyn's strongly-typed syntax tree with LINQ is idiomatic and already used in codebase |
| Unused using detection | Manual reference counting | Semantic model diagnostics or reference checking via GetSymbolInfo | Roslyn's semantic model tracks all symbol references |

**Key insight:** Roslyn provides comprehensive APIs for all syntax and semantic operations. Use them rather than building custom tree walking or name resolution. The codebase already follows this pattern well.

## Common Pitfalls

### Pitfall 1: Including Member Access Suffixes in Root Name
**What goes wrong:** `order.Total > 100` generates "OrderTotalProposition" instead of "OrderProposition".
**Why it happens:** Naively using all identifiers in the expression without filtering member access chains.
**How to avoid:**
- Check if an identifier is part of a member access expression: `identifier.Parent is MemberAccessExpressionSyntax`
- Only use the root (leftmost) identifier in member access chains
- For `order.Total`, extract `order`, not `Total`
**Warning signs:** Generated class names are excessively long or include property/method names.

### Pitfall 2: No Fallback When Variables Are Unrelated
**What goes wrong:** Expression like `x > 5 && y < 10` (two unrelated variables) fails to generate a name.
**Why it happens:** No common root identifier exists; frequency count results in tie or ambiguity.
**How to avoid:**
- Implement fallback to generic "Proposition" and "Model" when no clear common root
- Use deterministic tie-breaking (e.g., alphabetical order) if multiple identifiers have same frequency
**Warning signs:** Null reference exceptions when trying to use derived name; empty string names.

### Pitfall 3: Incorrect Position for LookupSymbols
**What goes wrong:** LookupSymbols reports no collision even though name exists, or reports false collision.
**Why it happens:** Position parameter determines scope and visibility; wrong position = wrong results.
**How to avoid:**
- Use the position where the new class will be inserted (typically namespace scope)
- For namespace-level classes, use the end of namespace declaration span
- Consider using the diagnostic location span as starting point
**Warning signs:** Generated code produces compilation errors due to duplicate type names.

### Pitfall 4: Removing System.Diagnostics When Other Code Needs It
**What goes wrong:** Removing `using System.Diagnostics` breaks other code that references Debug, Trace, etc.
**Why it happens:** Only checking for Debug.WriteLine, not considering other System.Diagnostics usage.
**How to avoid:**
- Scan entire file for any System.Diagnostics references after removing Debug.WriteLine
- Use semantic model to check if identifiers resolve to System.Diagnostics namespace
- Only remove using if no references remain
**Warning signs:** Tests produce compilation errors after import cleanup.

### Pitfall 5: Not Handling `is` Type-Check Expression Variable Names
**What goes wrong:** Expression `obj is string` generates "StringProposition" (from type) instead of "ObjProposition" (from variable).
**Why it happens:** Not distinguishing between the tested expression and the pattern type.
**How to avoid:**
- For IsPatternExpressionSyntax, only extract identifiers from the `Expression` property (left side of `is`)
- Ignore the `Pattern` property which contains the type name
- User constraint explicitly states: derive from the variable, not the type
**Warning signs:** Class names are C# type names (String, Int32, Object) instead of variable names.

### Pitfall 6: Case Sensitivity in PascalCase Conversion
**What goes wrong:** Identifiers starting with uppercase (e.g., `OrderId`) get converted to `OrderidProposition` (lowercase 'i').
**Why it happens:** Simple `Capitalize()` only uppercases the first character; doesn't detect already-PascalCase identifiers.
**How to avoid:**
- Check if identifier is already PascalCase (first char is uppercase)
- If already PascalCase, use as-is; if camelCase, capitalize
- Current `Capitalize()` extension handles this correctly: preserves existing uppercase
**Warning signs:** Generated names have inconsistent casing (OrderidProposition instead of OrderIdProposition).

**NOTE:** Reviewing existing `Capitalize()` implementation (StringExtensions.cs line 25-30), it simply uppercases first char and preserves rest. This means `orderId.Capitalize()` → `OrderId` (correct), and `OrderId.Capitalize()` → `OrderId` (also correct, already uppercase). The implementation is safe for both cases.

## Code Examples

Verified patterns from Roslyn documentation and existing codebase:

### Extracting Root Identifiers from Expression
```csharp
// Source: Existing ConvertToSpecCodeFix.cs pattern (line 372-383) + member access filtering
private static IEnumerable<string> GetRootIdentifierNames(
    ExpressionSyntax expression,
    SemanticModel semanticModel)
{
    return expression
        .DescendantNodesAndSelf()
        .OfType<IdentifierNameSyntax>()
        .Where(IsRootIdentifier)
        .Select(id => id.Identifier.ValueText)
        .Distinct();
}

private static bool IsRootIdentifier(IdentifierNameSyntax identifier)
{
    // Exclude if it's the right side of member access (e.g., "Total" in "order.Total")
    if (identifier.Parent is MemberAccessExpressionSyntax memberAccess
        && memberAccess.Name == identifier)
    {
        return false;
    }

    return true;
}
```

### Finding Common Root with Fallback
```csharp
private static string DeriveBaseName(ExpressionSyntax expression, SemanticModel semanticModel)
{
    var identifierNames = GetRootIdentifierNames(expression, semanticModel).ToList();

    if (identifierNames.Count == 0)
    {
        return "Proposition";  // Fallback if no identifiers found
    }

    // Find most common identifier
    var commonRoot = identifierNames
        .GroupBy(name => name)
        .OrderByDescending(g => g.Count())
        .ThenBy(g => g.Key)  // Deterministic tie-break (alphabetical)
        .Select(g => g.Key)
        .FirstOrDefault();

    // If all identifiers appear once and are different, use fallback
    if (identifierNames.Distinct().Count() == identifierNames.Count && identifierNames.Count > 1)
    {
        return "Proposition";
    }

    return commonRoot ?? "Proposition";
}
```

### Complete Name Derivation Pipeline
```csharp
private (string PropositionName, string ModelName) DeriveClassNames(
    ExpressionSyntax expression,
    SemanticModel semanticModel,
    int insertionPosition)
{
    // Step 1: Derive base name from expression
    string baseName = DeriveBaseName(expression, semanticModel);

    // Step 2: Convert to PascalCase (using existing extension)
    string pascalName = baseName.Capitalize();

    // Step 3: Append suffixes
    string propositionName = $"{pascalName}Proposition";
    string modelName = $"{pascalName}Model";

    // Step 4: Ensure uniqueness
    propositionName = EnsureUniqueName(propositionName, semanticModel, insertionPosition);
    modelName = EnsureUniqueName(modelName, semanticModel, insertionPosition);

    return (propositionName, modelName);
}
```

### Name Collision Detection and Resolution
```csharp
// Source: Microsoft Learn - SemanticModel.LookupSymbols
// https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.semanticmodel.lookupsymbols
private string EnsureUniqueName(string baseName, SemanticModel semanticModel, int position)
{
    string candidateName = baseName;
    int counter = 0;

    while (TypeNameExists(candidateName, semanticModel, position))
    {
        counter++;
        candidateName = $"{baseName}{counter}";
    }

    return candidateName;
}

private bool TypeNameExists(string name, SemanticModel semanticModel, int position)
{
    var symbols = semanticModel.LookupSymbols(position, name: name);
    return symbols.Any(s => s.Kind == SymbolKind.NamedType);
}
```

### Removing Debug.WriteLine Statements
```csharp
// Source: Microsoft Learn - RemoveNode documentation
// https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.syntaxnodeextensions.removenode
private SyntaxNode RemoveDebugWriteLineStatements(SyntaxNode root)
{
    var debugStatements = root.DescendantNodes()
        .OfType<ExpressionStatementSyntax>()
        .Where(IsDebugWriteLineCall)
        .ToList();

    var newRoot = root;
    foreach (var statement in debugStatements)
    {
        newRoot = newRoot.RemoveNode(statement, SyntaxRemoveOptions.KeepLeadingTrivia);
    }

    return newRoot;
}

private bool IsDebugWriteLineCall(ExpressionStatementSyntax statement)
{
    if (statement.Expression is not InvocationExpressionSyntax invocation)
        return false;

    if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        return false;

    return memberAccess.Expression is IdentifierNameSyntax { Identifier.ValueText: "Debug" }
           && memberAccess.Name.Identifier.ValueText == "WriteLine";
}
```

### Checking if System.Diagnostics is Still Needed
```csharp
private bool IsSystemDiagnosticsStillReferenced(SyntaxNode root, SemanticModel semanticModel)
{
    // Find all identifier references that might be System.Diagnostics
    var potentialDiagnosticsIdentifiers = root.DescendantNodes()
        .OfType<IdentifierNameSyntax>()
        .Where(id => id.Identifier.ValueText == "Debug"
                     || id.Identifier.ValueText == "Trace"
                     || id.Identifier.ValueText == "Debugger"
                     || id.Identifier.ValueText == "Process"
                     || id.Identifier.ValueText == "Stopwatch")
        .ToList();

    foreach (var identifier in potentialDiagnosticsIdentifiers)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(identifier);
        var namespaceString = symbolInfo.Symbol?.ContainingNamespace?.ToDisplayString();

        if (namespaceString == "System.Diagnostics")
            return true;
    }

    return false;
}
```

### Removing Unused Using Directive
```csharp
private Document RemoveSystemDiagnosticsUsing(Document document, SyntaxNode root)
{
    var compilationUnit = (CompilationUnitSyntax)root;

    var diagnosticsUsing = compilationUnit.Usings
        .FirstOrDefault(u => u.Name?.ToString() == "System.Diagnostics");

    if (diagnosticsUsing != null)
    {
        var newRoot = root.RemoveNode(diagnosticsUsing, SyntaxRemoveOptions.KeepNoTrivia);
        return document.WithSyntaxRoot(newRoot);
    }

    return document;
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Hard-coded "Proposition"/"Model" names | Context-derived names from expression identifiers | This phase | Generated code is more readable and self-documenting |
| Debug.WriteLine in generated code | Clean generated output without debug statements | This phase | Production-ready generated code |
| Adding System.Diagnostics unconditionally | Conditional import based on actual usage | This phase | Cleaner import sections |
| Manual string manipulation for casing | Existing Capitalize()/ToCamelCase() extensions | Already in codebase | Consistent naming patterns |
| Custom name resolution | SemanticModel.LookupSymbols API | Roslyn standard | Accurate collision detection with proper scoping |

**Deprecated/outdated:**
- **Hard-coded class names in CodeFix:** Previous implementation always used "Proposition" and "Model". Now must derive from expression content per user requirements.

## Open Questions

### 1. Exact Common Root Algorithm for Ambiguous Cases
**What we know:**
- User wants the most frequently occurring identifier
- For member access, use only the root (leftmost identifier)
- Fallback to "Proposition" when no clear common root

**What's unclear:**
- Should we prefer single-occurrence identifiers over multiple-occurrence if the multiple are split across different roots?
- Example: `person.Age > 18 && company.Revenue > 1000` - both `person` and `company` appear once. Alphabetical tie-break?

**Recommendation:** Use frequency-first approach with deterministic alphabetical tie-breaking. If all identifiers are equally frequent, use fallback "Proposition". This ensures consistent, predictable behavior.

**Confidence:** MEDIUM - Implementation freedom area per CONTEXT.md "Claude's Discretion"

### 2. Preserving Original Expression Comment Format
**What we know:**
- Current code adds comments like: `// order.Total > 100 && order.IsActive`
- Comments provide context about the original expression

**What's unclear:**
- Should we preserve whitespace exactly, or normalize?
- Should we add any metadata (e.g., "Original expression:" prefix)?

**Recommendation:** Keep existing format (simple `// {expression}` comment). It's clean, standard, and already working well. No need to add metadata prefixes.

**Confidence:** HIGH - Current implementation is good, user didn't request changes

### 3. System.Diagnostics Import Cleanup Aggressiveness
**What we know:**
- Remove `using System.Diagnostics` if not needed after removing Debug.WriteLine
- Must check if other code still references the namespace

**What's unclear:**
- Should we scan the entire file or just the modified method?
- Should we check for other System.Diagnostics types beyond Debug (Trace, Stopwatch, etc.)?

**Recommendation:** Scan entire compilation unit (file), check for common System.Diagnostics types (Debug, Trace, Debugger, Process, Stopwatch). This ensures we don't break other code in the same file.

**Confidence:** HIGH - Conservative approach prevents breaking other code

## Sources

### Primary (HIGH confidence)
- [Microsoft Learn - SemanticModel.LookupSymbols](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.semanticmodel.lookupsymbols?view=roslyn-dotnet-4.7.0) - Name collision detection API
- [Microsoft Learn - SyntaxNodeExtensions.RemoveNode](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.syntaxnodeextensions.removenode?view=roslyn-dotnet-4.7.0) - Statement removal API
- [Microsoft Learn - Get Started with Semantic Analysis](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/get-started/semantic-analysis) - SemanticModel usage patterns
- [Microsoft Learn - Get Started with Syntax Analysis](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/get-started/syntax-analysis) - Syntax tree traversal and identifier extraction
- Existing Motiv.CodeFix codebase - ConvertToSpecCodeFix.cs (GetVariablesInExpression pattern)
- Existing Motiv.CodeFix codebase - StringExtensions.cs (Capitalize/ToCamelCase implementations)

### Secondary (MEDIUM confidence)
- [GitHub - dotnet/roslyn - FAQ.cs samples](https://github.com/dotnet/roslyn-sdk/blob/main/samples/CSharp/APISamples/FAQ.cs) - Common Roslyn API usage patterns
- [DEV Community - Roslyn CodeFix for updating code (July 2025)](https://dev.to/mikhailnefedov/roslyn-codefix-for-updating-code-33a4) - Recent CodeFix implementation patterns
- [GitHub - Roslyn SemanticModel.cs source](https://github.com/dotnet/roslyn/blob/main/src/Compilers/Core/Portable/Compilation/SemanticModel.cs) - LookupSymbols implementation details
- [Iditect - Sort and remove unused using statements](https://www.iditect.com/faq/csharp/sort-and-remove-unused-using-statements-roslyn-scriptcode.html) - Using directive cleanup patterns

### Tertiary (LOW confidence)
- [NuGet - CaseConverter package](https://www.nuget.org/packages/CaseConverter/1.0.11) - Alternative for complex case conversion (NOT recommended for this phase - existing Capitalize() is sufficient per user constraints)

## Metadata

**Confidence breakdown:**
- Name derivation algorithm: **MEDIUM** - Custom logic with clear user constraints, but some implementation discretion
- Roslyn APIs (LookupSymbols, RemoveNode): **HIGH** - Official Microsoft documentation with examples
- Identifier extraction: **HIGH** - Existing pattern in codebase, well-documented Roslyn API
- PascalCase conversion: **HIGH** - Existing implementation in codebase matches user requirements
- Debug.WriteLine removal: **HIGH** - Straightforward syntax tree manipulation
- Using directive cleanup: **HIGH** - Standard pattern with semantic model verification

**Research date:** 2026-02-07
**Valid until:** Stable - Roslyn APIs are mature (netstandard2.0 target), CodeFix patterns are established. Name derivation algorithm is custom but based on stable Roslyn primitives.
